using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyController))]
[DisallowMultipleComponent]
public class EnemySkirmisher : NetworkBehaviour, IEnemy
{
    private Rigidbody rb;
    private EnemyController controller;
    private PullReceiver pull;
    private EnemyLootDropper lootDropper;

    private bool isDead;
    private ulong lastHitByClientId;

    public event Action<IEnemy> OnEnemyDied;
    public ulong LastHitByClientId => lastHitByClientId;

    [Header("GravityWell Pull")]
    [SerializeField] private float pullMaxSpeed = 10f;

    [Header("Perception")]
    [SerializeField] private float detectionRange = 40f;
    [SerializeField] private float loseTargetRange = 55f;
    [SerializeField] private float preferredDistance = 12f;
    [SerializeField] private LayerMask losMask;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float strafeSpeed = 5f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float distanceTolerance = 2.5f;

    [Header("Dash Attack")]
    [SerializeField] private float dashSpeed = 22f;
    [SerializeField] private float dashWindup = 0.35f;
    [SerializeField] private float dashDuration = 0.40f;
    [SerializeField] private float dashCooldown = 5.0f;
    [SerializeField] private GameObject dashTelegraphFx;

    [Header("Aiming")]
    [SerializeField] private float targetHeightOffset = 0.9f;

    [Header("Shooting (Burst)")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform bulletSpawn; // NUR POSITION (Mündung)
    [SerializeField] private float shootRange = 25f;
    [SerializeField] private int burstCount = 3;
    [SerializeField] private float burstInterval = 0.12f;
    [SerializeField] private float inaccuracyAngle = 1.8f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootSfx;

    [Header("Health")]
    [SerializeField] private float baseHealth = 5f;
    [SerializeField] private EnemyDamageNumbers dmgNums;

    private readonly NetworkVariable<float> health =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Transform target;
    private float nextDashTime = 0f;
    private float recoverTimer = 0f;
    private int strafeDir = 1;
    private float nextStrafeFlip = 0f;

    private enum State { Idle, Chase, Strafe, Burst, DashWindup, Dashing, Recover }
    private State state = State.Idle;

    private bool isBursting = false;
    private Coroutine burstCo;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        controller = GetComponent<EnemyController>();
        pull = GetComponent<PullReceiver>();
        lootDropper = GetComponent<EnemyLootDropper>();
        dmgNums = GetComponent<EnemyDamageNumbers>();

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void OnNetworkSpawn()
    {
        rb.isKinematic = !IsServer;

        if (IsServer)
        {
            health.Value = baseHealth;
            controller.UpdateTarget();
            target = controller.Target;
            state = State.Idle;
            ScheduleNextStrafeFlip();
        }

        health.OnValueChanged += OnHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        health.OnValueChanged -= OnHealthChanged;
    }

    void FixedUpdate()
    {
        if (!IsServer || isDead) return;

        controller.UpdateTarget();
        target = controller.Target;

        TickStateMachine(Time.fixedDeltaTime);
        ApplyExternalPull(Time.fixedDeltaTime);

        // Debug: Zielrichtung (Server)
        if (bulletSpawn != null && target != null)
        {
            Vector3 origin = bulletSpawn.position;
            Vector3 targetPos = target.position + Vector3.up * targetHeightOffset;
            Vector3 dir = (targetPos - origin).normalized;

            Debug.DrawRay(origin, dir * 5f, Color.cyan, 0.1f);  // Sollrichtung zum Ziel
        }
    }

    private void TickStateMachine(float dt)
    {
        if (target == null)
        {
            state = State.Idle;
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);
        bool hasLOS = HasLineOfSight(target);

        if (dist > loseTargetRange) { state = State.Idle; return; }

        RotateBodyTowards(target, dt);

        switch (state)
        {
            case State.Idle:
                if (dist <= detectionRange && hasLOS)
                    state = (dist > preferredDistance + distanceTolerance) ? State.Chase : State.Strafe;
                break;

            case State.Chase:
                MoveTowards(target, moveSpeed, dt);
                if (dist <= preferredDistance + distanceTolerance && hasLOS)
                    state = State.Strafe;
                break;

            case State.Strafe:
                MaintainDistance(target, dt);
                StrafeAround(target, dt);

                if (Time.time >= nextStrafeFlip)
                    FlipStrafeDir();

                if (!isBursting && hasLOS && dist <= shootRange && UnityEngine.Random.value < 0.2f)
                {
                    state = State.Burst;
                    burstCo = StartCoroutine(BurstRoutine());
                }
                break;

            case State.Burst:
                MaintainDistance(target, dt * 0.2f);
                break;

            case State.Dashing:
                ApplyMove(transform.forward * dashSpeed * dt, dt);
                break;

            case State.Recover:
                recoverTimer -= dt;
                if (recoverTimer <= 0f) state = State.Strafe;
                break;
        }
    }

    private void RotateBodyTowards(Transform t, float dt)
    {
        Vector3 dir = t.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * dt);
    }

    private Vector3 ConsumeExternalStep(float dt)
    {
        return pull != null ? pull.ConsumeStep(dt, pullMaxSpeed) : Vector3.zero;
    }

    private void ApplyMove(Vector3 baseStep, float dt)
    {
        Vector3 ext = ConsumeExternalStep(dt);
        rb.MovePosition(rb.position + baseStep + ext);
    }

    private void ApplyExternalPull(float dt)
    {
        if (pull == null) return;
        Vector3 ext = pull.ConsumeStep(dt, pullMaxSpeed);
        if (ext.sqrMagnitude > 0f)
            rb.MovePosition(rb.position + ext);
    }

    private void MoveTowards(Transform t, float speed, float dt)
    {
        Vector3 dir = (t.position - transform.position); dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        ApplyMove(dir.normalized * speed * dt, dt);
    }

    private void MaintainDistance(Transform t, float dt)
    {
        float dist = Vector3.Distance(transform.position, t.position);
        float delta = dist - preferredDistance;

        if (Mathf.Abs(delta) > distanceTolerance)
        {
            float sign = Mathf.Sign(delta);
            Vector3 dir = (t.position - transform.position); dir.y = 0f;
            Vector3 step = dir.normalized * sign * moveSpeed * 0.6f * dt;
            ApplyMove(step, dt);
            return;
        }

        ApplyMove(Vector3.zero, dt);
    }

    private void StrafeAround(Transform t, float dt)
    {
        Vector3 toTarget = (t.position - transform.position); toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) { ApplyMove(Vector3.zero, dt); return; }

        Vector3 right = Vector3.Cross(Vector3.up, toTarget.normalized);
        ApplyMove(right * strafeDir * strafeSpeed * dt, dt);
    }

    private void FlipStrafeDir()
    {
        strafeDir = (UnityEngine.Random.value < 0.5f) ? -strafeDir : strafeDir;
        ScheduleNextStrafeFlip();
    }

    private void ScheduleNextStrafeFlip()
    {
        nextStrafeFlip = Time.time + UnityEngine.Random.Range(1.2f, 2.5f);
    }

    private bool HasLineOfSight(Transform t)
    {
        if (bulletSpawn == null) return true;

        Vector3 origin = bulletSpawn.position;
        Vector3 targetEye = t.position + Vector3.up * targetHeightOffset;
        Vector3 dir = targetEye - origin;
        float dist = dir.magnitude;

        if (Physics.Raycast(origin, dir.normalized, dist, losMask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    private IEnumerator BurstRoutine()
    {
        isBursting = true;
        int shots = Mathf.Max(1, burstCount);

        while (shots-- > 0 && target != null && !isDead)
        {
            FireOneShot();
            yield return new WaitForSeconds(burstInterval);
        }

        recoverTimer = UnityEngine.Random.Range(0.15f, 0.5f);
        state = State.Recover;
        isBursting = false;
        burstCo = null;
    }

    private void FireOneShot()
    {
        if (!IsServer) return;
        if (bulletPrefab == null || bulletSpawn == null) return;

        Vector3 origin = bulletSpawn.position;

        Vector3 targetPos = (target != null)
            ? target.position + Vector3.up * targetHeightOffset
            : origin + transform.forward * 10f;

        Vector3 dir = (targetPos - origin).normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

        // Inaccuracy um die Zielrichtung herum
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        float yaw = UnityEngine.Random.Range(-inaccuracyAngle, inaccuracyAngle);
        float pitch = UnityEngine.Random.Range(-inaccuracyAngle, inaccuracyAngle);
        rot = rot * Quaternion.Euler(pitch, yaw, 0f);

        GameObject bullet = Instantiate(bulletPrefab, origin, rot);

        if (!bullet.TryGetComponent<NetworkObject>(out var netObj) || netObj == null)
        {
            Debug.LogError("[EnemySkirmisher] Bullet prefab has NO NetworkObject!");
            Destroy(bullet);
            return;
        }

        if (!netObj.IsSpawned) netObj.Spawn();

        if (bullet.TryGetComponent<EnemyBulletController>(out var ctrl) && ctrl != null)
            ctrl.Init(bullet.transform.forward); // oder ctrl.Init(dir);

        if (audioSource != null && shootSfx != null)
            audioSource.PlayOneShot(shootSfx);
    }

    // ---- Damage/Health/Die bleibt wie bei dir (hier nicht nochmal komplett ausgeschrieben) ----
    public void TakeDamage(float amount, ulong attackerId, Vector3 hitPoint)
    {
        if (!IsServer || isDead) return;

        lastHitByClientId = attackerId;
        health.Value -= amount;

        if (dmgNums != null)
            dmgNums.ShowForAttackerOnly(amount, hitPoint, attackerId, isCrit: false);

        GetComponent<EnemyEffects>()?.PlayHitEffectClientRpc(hitPoint);
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (newValue <= 0 && !isDead)
            Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        GetComponent<EnemyEffects>()?.PlayDeathEffectClientRpc(transform.position);
        GetComponent<EnemyDropper>().HandleDeath();

        if (burstCo != null)
        {
            StopCoroutine(burstCo);
            burstCo = null;
            isBursting = false;
        }

        OnEnemyDied?.Invoke(this);

        if (IsServer)
        {
            lootDropper?.OnKilled(transform.position);
            GetComponent<NetworkObject>().Despawn(true);
        }
    }

    public void ConfigureForBaseAttack()
    {
        if (!IsServer) return;

        detectionRange = Mathf.Max(detectionRange, 2000f);
        loseTargetRange = 2000f;   // damit Base immer gefunden wird
        losMask = 0;               // "Nothing" -> Raycast immer frei
    }


    public float GetBaseHealth() => baseHealth;

    public void SetHealth(float newHealth)
    {
        if (IsServer) health.Value = newHealth;
    }
}
