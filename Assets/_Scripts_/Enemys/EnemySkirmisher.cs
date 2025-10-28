using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyController))]
[DisallowMultipleComponent]
public class EnemySkirmisher : NetworkBehaviour, IEnemy
{
    // --- References ---
    private Rigidbody rb;
    private EnemyController controller;
    private PullReceiver pull;

    // Overclock
    private EnemyLootDropper lootDropper;

    private bool isDead;
    private ulong lastHitByClientId;

    public event Action<IEnemy> OnEnemyDied;
    public ulong LastHitByClientId => lastHitByClientId;
    

    // Für Anzieh-Fähigkeit (GravityWell)
    [Header("GravityWell Pull")]
    [SerializeField] private float pullMaxSpeed = 10f;

    [Header("Perception")]
    [SerializeField] private float detectionRange = 40f;
    [SerializeField] private float loseTargetRange = 55f;
    [SerializeField] private float preferredDistance = 12f;
    [SerializeField] private LayerMask losMask; // Hindernisse / Level

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
    [SerializeField] private GameObject dashTelegraphFx; // optional (Partikel/Glow)

    [Header("Cannon / Aiming")]
    [SerializeField] private Transform cannonPivot; // kippt nur in Pitch (Kind vom Enemy)
    [SerializeField] private float pitchSpeed = 12f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-10f, 45f); // Grad (min,max)
    [SerializeField] private float targetHeightOffset = 0.9f; // Augenhöhe des Ziels


    [Header("Shooting (Burst)")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform bulletSpawn;
    [SerializeField] private float shootRange = 25f;
    [SerializeField] private int burstCount = 3;
    [SerializeField] private float burstInterval = 0.12f;
    [SerializeField] private float inaccuracyAngle = 1.8f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootSfx;

    [Header("Health")]
    [SerializeField] private float baseHealth = 5f;

    [SerializeField] private EnemyDamageNumbers dmgNums;

    // networked health (server-authoritativ)
    private readonly NetworkVariable<float> health =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Transform target;
    private float nextDashTime = 0f;
    private float recoverTimer = 0f;
    private int strafeDir = 1; // +1/-1
    private float nextStrafeFlip = 0f;

    private enum State { Idle, Chase, Strafe, Burst, DashWindup, Dashing, Recover }
    private State state = State.Idle;

    // Burst-Steuerung
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
        // Nur der Server bewegt / schießt, Clients folgen via NetworkTransform
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

        // Ziel updaten (nächster Spieler)
        controller.UpdateTarget();
        target = controller.Target;

        // State Machine Tick
        TickStateMachine(Time.fixedDeltaTime);

        // WICHTIG: Externen „Well“-Zug NACH dem State-Tick additiv anwenden (wirkt in allen States)
        ApplyExternalPull(Time.fixedDeltaTime);
    }

    // ---------------- STATE MACHINE ----------------

    private void TickStateMachine(float dt)
    {
        if (target == null)
        {
            state = State.Idle;
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);
        bool hasLOS = HasLineOfSight(target);

        // Lose Target, wenn zu weit weg
        if (dist > loseTargetRange) { state = State.Idle; return; }

        RotateBodyTowards(target, dt);
        AimCannonTowards(target, dt);

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

                // Burst nur starten, wenn keiner läuft
                if (!isBursting && hasLOS && dist <= shootRange && UnityEngine.Random.value < 0.2f)
                {
                    // wechsle in Burst und starte Routine genau einmal
                    state = State.Burst;
                    burstCo = StartCoroutine(BurstRoutine());
                }
                else if (Time.time >= nextDashTime && hasLOS && dist <= preferredDistance + 6f && UnityEngine.Random.value < 0.01f)
                {
                    StartCoroutine(DashRoutine());
                    state = State.DashWindup;
                }
                break;

            case State.Burst:
                // während des Bursts nur minimal bewegen
                MaintainDistance(target, dt * 0.2f);
                break;

            case State.DashWindup:
                // Windup: nur ausrichten (siehe Coroutine)
                break;

            case State.Dashing:
                // schnelles Vorwärts-Moven + externer Sog
                ApplyMove(transform.forward * dashSpeed * dt, dt);
                break;

            case State.Recover:
                recoverTimer -= dt;
                if (recoverTimer <= 0f) state = State.Strafe;
                break;
        }
    }

    // ---------------- MOVEMENT ----------------

    // Körper yaw (wie bisher, aber ohne y=0 bei dir? Doch – der Körper bleibt 2D)
    private void RotateBodyTowards(Transform t, float dt)
    {
        Vector3 dir = t.position - transform.position;
        dir.y = 0f; // nur Yaw
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * dt);
    }

    // Kanone pitch (mit Höhe)
    private void AimCannonTowards(Transform t, float dt)
    {
        if (cannonPivot == null || bulletSpawn == null) return;

        Vector3 origin = bulletSpawn.position;
        Vector3 targetPos = t.position + Vector3.up * targetHeightOffset;

        Vector3 dir = (targetPos - origin);
        if (dir.sqrMagnitude < 0.0001f) return;

        // gewünschte Rotation
        Quaternion lookRot = Quaternion.LookRotation(dir.normalized, Vector3.up);

        // nur Pitch am Pivot verändern (optional Yaw sperren, weil der Körper yawed)
        Vector3 euler = lookRot.eulerAngles;
        // Euler normalisieren auf [-180,180]
        float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        // clampen
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);

        // aktuelle lokale Rotation holen und nur X anpassen
        Vector3 local = cannonPivot.localEulerAngles;
        // in [-180,180] bringen für sauberes Slerp
        float currentPitch = local.x > 180f ? local.x - 360f : local.x;
        float newPitch = Mathf.MoveTowards(currentPitch, pitch, pitchSpeed * dt);

        cannonPivot.localEulerAngles = new Vector3(newPitch, 0f, 0f);
    }


    private Vector3 ConsumeExternalStep(float dt)
    {
        return pull != null ? pull.ConsumeStep(dt, pullMaxSpeed) : Vector3.zero;
    }

    private void ApplyMove(Vector3 baseStep, float dt)
    {
        // einmalig externen Zug addieren
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
        Vector3 step = dir.normalized * speed * dt;
        ApplyMove(step, dt);
    }

    private void MaintainDistance(Transform t, float dt)
    {
        float dist = Vector3.Distance(transform.position, t.position);
        float delta = dist - preferredDistance;

        if (Mathf.Abs(delta) > distanceTolerance)
        {
            float sign = Mathf.Sign(delta);
            Vector3 dir = (t.position - transform.position); dir.y = 0f;
            Vector3 moveDir = dir.normalized * sign;
            Vector3 step = moveDir * moveSpeed * 0.6f * dt;
            ApplyMove(step, dt);
            return;
        }

        // selbst wenn „keine Basisbewegung“, den externen Zug nicht verlieren:
        ApplyMove(Vector3.zero, dt);
    }

    private void StrafeAround(Transform t, float dt)
    {
        Vector3 toTarget = (t.position - transform.position); toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) { ApplyMove(Vector3.zero, dt); return; }

        Vector3 right = Vector3.Cross(Vector3.up, toTarget.normalized);
        Vector3 step = right * strafeDir * strafeSpeed * dt;
        ApplyMove(step, dt);
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
        if (bulletSpawn == null) return true; // Fallback
        Vector3 eye = bulletSpawn.position;
        Vector3 targetEye = t.position + Vector3.up * targetHeightOffset;
        Vector3 dir = targetEye - eye;
        float dist = dir.magnitude;

        // losMask = Hindernisse (kein Player/Enemy!)
        if (Physics.Raycast(eye, dir.normalized, out RaycastHit hit, dist, losMask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }


    // ---------------- ATTACKS ----------------

    private IEnumerator BurstRoutine()
    {
        isBursting = true;
        int shots = Mathf.Max(1, burstCount);

        // wir bleiben im Burst, egal ob State extern geändert wird – danach gehen wir in Recover
        while (shots-- > 0 && target != null && !isDead)
        {
            FireOneShot(); // server-only intern
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

        // Zielrichtung inkl. Höhe
        Vector3 origin = bulletSpawn.position;
        Vector3 targetPos = (target != null ? target.position + Vector3.up * targetHeightOffset
                                            : origin + bulletSpawn.forward * 10f);
        Vector3 dir = (targetPos - origin).normalized;

        // leichte Ungenauigkeit (Kegel um dir)
        Quaternion baseRot = Quaternion.LookRotation(dir, Vector3.up);
        float yaw = UnityEngine.Random.Range(-inaccuracyAngle, inaccuracyAngle);
        float pitch = UnityEngine.Random.Range(-inaccuracyAngle, inaccuracyAngle);
        Quaternion rot = baseRot * Quaternion.Euler(pitch, yaw, 0f);

        GameObject bullet = Instantiate(bulletPrefab, origin, rot);
        if (!bullet.TryGetComponent<NetworkObject>(out var netObj) || netObj == null)
        {
            Debug.LogError("[EnemySkirmisher] Bullet prefab has NO NetworkObject!");
            Destroy(bullet);
            return;
        }
        if (!netObj.IsSpawned) netObj.Spawn();

        if (bullet.TryGetComponent<EnemyBulletController>(out var ctrl) && ctrl != null)
            ctrl.Init(bullet.transform.forward);

        if (audioSource != null && shootSfx != null)
            audioSource.PlayOneShot(shootSfx);
    }


    private IEnumerator DashRoutine()
    {
        nextDashTime = Time.time + dashCooldown;

        // Windup / Telegraph
        PlayDashTelegraphClientRpc(true);
        state = State.DashWindup;

        float endWindup = Time.time + dashWindup;
        while (Time.time < endWindup && target != null && !isDead)
        {
            RotateBodyTowards(target, Time.fixedDeltaTime);
            AimCannonTowards(target, Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
        }

        PlayDashTelegraphClientRpc(false);

        // Dash Phase
        float dashEnd = Time.time + dashDuration;
        state = State.Dashing;

        while (Time.time < dashEnd && !isDead)
        {
            // eigentliche Bewegung in FixedUpdate (state == Dashing)
            yield return new WaitForFixedUpdate();
        }

        recoverTimer = 0.4f;
        state = State.Recover;
    }

    [ClientRpc]
    private void PlayDashTelegraphClientRpc(bool enable)
    {
        if (dashTelegraphFx == null) return;
        dashTelegraphFx.SetActive(enable);
    }

    // ---------------- HEALTH / DAMAGE ----------------

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
        // Effekt an der Trefferstelle
        GetComponent<EnemyEffects>()?.PlayHitEffectClientRpc(hitPoint);
    }
    

    public float GetBaseHealth() => baseHealth;

    public void SetHealth(float newHealth)
    {
        if (IsServer) health.Value = newHealth;
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (newValue <= 0 && !isDead)
            Die();
    }

    public void ConfigureForBaseAttack()
    {
        if (!IsServer) return;
        detectionRange = Mathf.Max(detectionRange, 2000f);
        loseTargetRange = 2000f;   // damit Base immer gefunden wird
        losMask = 0;               // "Nothing" -> Raycast immer frei
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

        // ======================
        // Overclock-Loot (SERVER)
        // ======================
        if (IsServer)
        {
            // NUR hier Loot würfeln/spawnen
            if (lootDropper != null)
            {
                lootDropper.OnKilled(transform.position);
            }

            // Danach despawnen (Server-autorität)
            GetComponent<NetworkObject>().Despawn(true);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, shootRange);
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, preferredDistance);
    }
#endif
}
