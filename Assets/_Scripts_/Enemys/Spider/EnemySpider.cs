using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections;
using MimicSpace;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyController))]
[DisallowMultipleComponent]
public class EnemySpider : NetworkBehaviour, IEnemy
{
    private Rigidbody rb;
    private EnemyController controller;
    private EnemyEffects effects;

    [Header("Visual/Anim")]
    [SerializeField] private Mimic mimic;
    [SerializeField] private Transform visualsRoot;
    [SerializeField] private Transform spitMuzzle;

    [Header("Perception")]
    [SerializeField] private float detectionRange = 35f;
    [SerializeField] private float loseTargetRange = 50f;
    [SerializeField] private float targetHeightOffset = 0.9f;
    [SerializeField] private LayerMask losMask;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float strafeSpeed = 3.3f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private Vector2 strafeFlipInterval = new Vector2(0.8f, 1.6f);

    [Header("Web Spit")]
    [SerializeField] private GameObject webProjectilePrefab;
    [SerializeField] private float spitRange = 18f;
    [SerializeField] private float minSpitRange = 8f;
    [SerializeField] private float spitCooldown = 3.2f;
    [SerializeField] private float spitInaccuracy = 2.0f;
    [SerializeField] private float spitWindup = 0.18f;

    [Header("Health")]
    [SerializeField] private float baseHealth = 5f;
    [SerializeField] private EnemyDamageNumbers dmgNums;

    private readonly NetworkVariable<float> health =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Transform target;
    private int strafeDir = 1;
    private float nextStrafeFlip;
    private float nextSpitTime;
    private bool isDead;
    private ulong lastHitByClientId;
    public ulong LastHitByClientId => lastHitByClientId;
    public event Action<IEnemy> OnEnemyDied;

    private enum State { Idle, Skitter, SpitWindup, Recover }
    private State state = State.Idle;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        controller = GetComponent<EnemyController>();
        effects = GetComponent<EnemyEffects>();
        dmgNums = GetComponent<EnemyDamageNumbers>();

        if (!visualsRoot) visualsRoot = transform;

        // RB-Schutz
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
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
            ScheduleStrafeFlip();
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
        TickFSM(Time.fixedDeltaTime);
    }

    private void TickFSM(float dt)
    {
        if (target == null) { state = State.Idle; return; }
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > loseTargetRange) { state = State.Idle; return; }

        RotateBodyTowards(target.position, dt);

        switch (state)
        {
            case State.Idle:
                if (dist <= detectionRange) state = State.Skitter;
                break;

            case State.Skitter:
                SkitterMove(dt, target.position);

                bool hasLOS = HasLOS(target);
                if (Time.time >= nextSpitTime && hasLOS && dist <= spitRange && dist >= minSpitRange)
                {
                    StartCoroutine(SpitRoutine());
                    state = State.SpitWindup;
                }
                break;

            case State.SpitWindup:
                // von Coroutine gesteuert
                break;

            case State.Recover:
                state = State.Skitter;
                break;
        }
    }

    private void SkitterMove(float dt, Vector3 targetPos)
    {
        Vector3 toT = targetPos - transform.position; 
        toT.y = 0f;
        Vector3 step = Vector3.zero;

        if (toT.sqrMagnitude > 0.001f)
            step += toT.normalized * moveSpeed * dt;

        Vector3 right = Vector3.Cross(Vector3.up, toT.normalized);
        step += right * strafeDir * strafeSpeed * dt;

        rb.MovePosition(rb.position + step);

        if (Time.time >= nextStrafeFlip) FlipStrafeDir();
    }

    private void RotateBodyTowards(Vector3 worldPos, float dt)
    {
        Vector3 dir = worldPos - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion trg = Quaternion.LookRotation(dir.normalized, Vector3.up);
        visualsRoot.rotation = Quaternion.Slerp(visualsRoot.rotation, trg, rotationSpeed * dt);
    }

    private bool HasLOS(Transform t)
    {
        if (!spitMuzzle) return true;
        Vector3 eye = spitMuzzle.position;
        Vector3 tgt = t.position + Vector3.up * targetHeightOffset;
        Vector3 dir = tgt - eye;
        float dist = dir.magnitude;

        if (Physics.Raycast(eye, dir.normalized, out var hit, dist, losMask, QueryTriggerInteraction.Ignore))
            return false;
        return true;
    }

    private void FlipStrafeDir()
    {
        strafeDir = (UnityEngine.Random.value < 0.5f) ? -strafeDir : strafeDir;
        ScheduleStrafeFlip();
    }

    private void ScheduleStrafeFlip()
    {
        nextStrafeFlip = Time.time + UnityEngine.Random.Range(strafeFlipInterval.x, strafeFlipInterval.y);
    }

    private IEnumerator SpitRoutine()
    {
        nextSpitTime = Time.time + spitCooldown;

        float end = Time.time + spitWindup;
        while (Time.time < end && target != null && !isDead)
        {
            RotateBodyTowards(target.position, Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
        }

        FireWeb();
        state = State.Recover;
        yield return null;
    }

    private void FireWeb()
    {
        if (!IsServer || webProjectilePrefab == null || spitMuzzle == null) return;

        Vector3 origin = spitMuzzle.position;
        Vector3 tgt = (target != null ? target.position + Vector3.up * targetHeightOffset
                                      : origin + visualsRoot.forward * 10f);
        Vector3 dir = (tgt - origin).normalized;

        Quaternion baseRot = Quaternion.LookRotation(dir, Vector3.up);
        float yaw = UnityEngine.Random.Range(-spitInaccuracy, spitInaccuracy);
        float pitch = UnityEngine.Random.Range(-spitInaccuracy, spitInaccuracy);
        Quaternion rot = baseRot * Quaternion.Euler(pitch, yaw, 0f);

        GameObject proj = Instantiate(webProjectilePrefab, origin, rot);
        if (proj.TryGetComponent<NetworkObject>(out var no) && !no.IsSpawned) no.Spawn();
    }

    public void TakeDamage(float amount, ulong attackerId, Vector3 hitPoint)
    {
        if (!IsServer || isDead) return;
        lastHitByClientId = attackerId;
        health.Value -= amount;

        // Damage Popup
        if (dmgNums != null)
        {
            // ALLE Spieler sehen DMG Popups
            //dmgNums.ShowForAllClients(amount, hitPoint, isCrit: false);

            // Nur der Angreifer sieht die Zahl
            dmgNums.ShowForAttackerOnly(amount, hitPoint, attackerId, isCrit: false);
        }

        effects?.PlayHitEffectClientRpc(hitPoint);
    }

    public float GetBaseHealth() => baseHealth;
    public void SetHealth(float newHealth) { if (IsServer) health.Value = newHealth; }

    private void OnHealthChanged(float _, float v)
    {
        if (v <= 0 && !isDead) Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        OnEnemyDied?.Invoke(this);

        if (IsServer)
        {
            // Death-VFX an Position schicken, dann despawnen
            Vector3 pos = transform.position;
            effects?.PlayDeathEffectClientRpc(pos);

            GetComponent<NetworkObject>().Despawn(true);
        }
    }
}
