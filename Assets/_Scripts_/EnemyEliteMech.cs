using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections;
using Unity.Netcode.Components;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(NetworkAnimator))] // synct Animator-Parameter/Trigger
[DisallowMultipleComponent]
public class EnemyEliteMech : NetworkBehaviour, IEnemy
{
    // --- References ---
    private Rigidbody rb;
    private EnemyController controller;
    private PullReceiver pull;
    private Animator anim;
    private NetworkAnimator netAnim;

    private bool isDead;
    private Transform target;
    private float lastFireTime;
    private Coroutine fireCo;
    private ulong lastHitByClientId;

    public event Action<IEnemy> OnEnemyDied;
    public ulong LastHitByClientId => lastHitByClientId;

    // ---------- Tuning ----------
    [Header("Perception")]
    [SerializeField] private float detectionRange = 55f;
    [SerializeField] private float loseTargetRange = 70f;
    [SerializeField] private LayerMask losMask; // Hindernisse

    [Header("Movement (heavy)")]
    [SerializeField] private float moveSpeed = 2.1f;      // langsam
    [SerializeField] private float accel = 4f;            // träger Anlauf/Abbremsen
    [SerializeField] private float rotationSpeed = 2.4f;  // träge Drehung
    [SerializeField] private float stopDistance = 22f;    // bleibt stehen, wenn ≤
    [SerializeField] private float resumeDistance = 26f;  // läuft wieder los, wenn >
    [SerializeField] private float externalPullMaxSpeed = 8f;

    [Header("Cannon / Aiming")]
    [SerializeField] private Transform cannonPivot;     // optionaler Pitch-Joint (Kind)
    [SerializeField] private Transform muzzle;          // Spawnpunkt
    [SerializeField] private Vector2 pitchLimits = new Vector2(-8f, 30f);
    [SerializeField] private float pitchSpeed = 6f;
    [SerializeField] private float targetHeightOffset = 1.1f;

    [Header("Heavy Shot")]
    [SerializeField] private GameObject heavyBulletPrefab;
    [SerializeField] private float fireRange = 40f;
    [SerializeField] private float chargeTime = 1.15f;      // Auflade-Pose
    [SerializeField] private float fireCooldown = 4.0f;     // Zeit zwischen Schüssen
    [SerializeField] private GameObject chargeFx;           // optional: leuchten/partikel
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip chargeSfx;
    [SerializeField] private AudioClip fireSfx;

    [Header("Health")]
    [SerializeField] private float baseHealth = 45f;
    [SerializeField] private float deathDespawnDelay = 2.5f; // Länge deines Die-Clips
    public event Action<float, float> OnHealthChanged01; // (old01, new01)
    public float Health01 => Mathf.Clamp01(baseHealth <= 0 ? 0f : health.Value / baseHealth);

    [Header("Damage Numbers / FX")]
    [SerializeField] private EnemyDamageNumbers dmgNums;
    [SerializeField] private EnemyEffects effects;

    // Animator parameter names
    private static readonly int AnimSpeed = Animator.StringToHash("Speed"); // float
    private static readonly int AnimShoot = Animator.StringToHash("Shoot"); // trigger
    private static readonly int AnimAiming = Animator.StringToHash("Aiming"); // bool
    private static readonly int AnimDie    = Animator.StringToHash("Die"); // trigger

    // networked health (server-authoritativ)
    private readonly NetworkVariable<float> health =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // movement cache
    private Vector3 vel; // "wunsch"-geschwindigkeit im planar
    private enum State { Idle, Move, Aim, Firing, Cooldown, Dead }
    private State state = State.Idle;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        controller = GetComponent<EnemyController>();
        pull = GetComponent<PullReceiver>();
        anim = GetComponentInChildren<Animator>();
        netAnim = GetComponent<NetworkAnimator>();

        // schwerer Mech: nur Y-Rotation, keine Physik-Kippmomente
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
            lastFireTime = -999f;
        }

        health.OnValueChanged += OnHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        health.OnValueChanged -= OnHealthChanged;
    }

    private void FixedUpdate()
    {
        if (!IsServer || isDead) return;

        // Ziel regelmäßig aktualisieren
        controller.UpdateTarget();
        target = controller.Target;

        // State-Tick
        TickState(Time.fixedDeltaTime);

        // Externe Sogkräfte (GravityWell) immer additiv
        if (pull != null)
        {
            var ext = pull.ConsumeStep(Time.fixedDeltaTime, externalPullMaxSpeed);
            if (ext.sqrMagnitude > 0f) rb.MovePosition(rb.position + ext);
        }

        // Animator-Speed (Clients erhalten über NetworkAnimator die Parameter)
        if (anim != null) anim.SetFloat(AnimSpeed, new Vector3(vel.x, 0f, vel.z).magnitude);
    }

    private void TickState(float dt)
    {
        if (target == null)
        {
            vel = Vector3.zero;
            state = State.Idle;
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);
        bool hasLOS = HasLineOfSight(target);

        // Körper yawen
        RotateBodyTowards(target.position, dt);

        // Cannon pitchen
        AimCannon(target.position, dt);

        switch (state)
        {
            case State.Idle:
                if (dist <= detectionRange && hasLOS)
                    state = (dist > stopDistance) ? State.Move : State.Aim;
                vel = Vector3.zero;
                ApplyMove(dt);
                break;

            case State.Move:
                // Schwer & träge anlaufen
                MoveTowards(target.position, dt);
                if (dist <= stopDistance && hasLOS) { state = State.Aim; vel = Vector3.zero; }
                break;

            case State.Aim:
                vel = Vector3.Lerp(vel, Vector3.zero, dt * accel);
                ApplyMove(dt);

                if (anim) anim.SetBool(AnimAiming, true);

                if (hasLOS && dist <= fireRange && Time.time >= lastFireTime + fireCooldown && fireCo == null)
                {
                    Debug.Log($"[EliteMech] Firing... dist={dist:F1}");
                    fireCo = StartCoroutine(FireSequence());
                    state = State.Firing;
                }
                else if (dist > resumeDistance || !hasLOS)
                {
                    if (anim) anim.SetBool(AnimAiming, false);
                    state = State.Move;
                }
                break;

            case State.Firing:
                // Bewegung während des Feuerns minimal
                vel = Vector3.Lerp(vel, Vector3.zero, dt * accel);
                ApplyMove(dt);
                break;

            case State.Cooldown:
                // kleine Pause bevor er wieder entscheidet
                vel = Vector3.Lerp(vel, Vector3.zero, dt * accel);
                ApplyMove(dt);
                if (Time.time >= lastFireTime + 0.5f) state = State.Aim;
                break;
        }
    }

    // ---------- Movement helpers ----------
    private void MoveTowards(Vector3 worldPos, float dt)
    {
        Vector3 to = worldPos - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) { ApplyMove(dt); return; }

        Vector3 desired = to.normalized * moveSpeed;
        vel = Vector3.MoveTowards(vel, desired, accel * dt);
        ApplyMove(dt);
    }

    private void ApplyMove(float dt)
    {
        rb.MovePosition(rb.position + vel * dt);
    }

    private void RotateBodyTowards(Vector3 pos, float dt)
    {
        Vector3 dir = pos - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * dt);
    }

    private void AimCannon(Vector3 pos, float dt)
    {
        if (!cannonPivot || !muzzle) return;
        Vector3 origin = muzzle.position;
        Vector3 targetPos = pos + Vector3.up * targetHeightOffset;
        Vector3 dir = (targetPos - origin);
        if (dir.sqrMagnitude < 0.0001f) return;

        var look = Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles;
        float pitch = look.x > 180f ? look.x - 360f : look.x;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);

        var local = cannonPivot.localEulerAngles;
        float currentPitch = local.x > 180f ? local.x - 360f : local.x;
        float newPitch = Mathf.MoveTowards(currentPitch, pitch, pitchSpeed * dt);
        cannonPivot.localEulerAngles = new Vector3(newPitch, 0f, 0f);
    }

    private bool HasLineOfSight(Transform t)
    {
        if (!muzzle) return true;
        Vector3 eye = muzzle.position;
        Vector3 to = t.position + Vector3.up * targetHeightOffset - eye;
        float dist = to.magnitude;

        // RaycastAll und Self ignorieren
        var hits = Physics.RaycastAll(eye, to.normalized, dist, losMask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (!h.collider || h.collider.transform.IsChildOf(transform)) continue; // self skip
            return false; // irgendein Hindernis zwischen drin
        }
        return true;
    }

    // ---------- Fire sequence ----------
    private IEnumerator FireSequence()
    {
        lastFireTime = Time.time;

        // Aufladen
        if (chargeFx) chargeFx.SetActive(true);
        if (audioSource && chargeSfx) audioSource.PlayOneShot(chargeSfx);
        if (anim) { anim.SetBool(AnimAiming, true); netAnim.SetTrigger(AnimShoot); } // Startpose

        float tEnd = Time.time + chargeTime;
        while (Time.time < tEnd && !isDead)
        {
            // nachziehen, falls Ziel läuft
            if (target) { RotateBodyTowards(target.position, Time.fixedDeltaTime); AimCannon(target.position, Time.fixedDeltaTime); }
            yield return new WaitForFixedUpdate();
        }
        if (chargeFx) chargeFx.SetActive(false);

        // Schuss
        FireHeavyBullet();
        if (audioSource && fireSfx) audioSource.PlayOneShot(fireSfx);

        state = State.Cooldown;
        fireCo = null;
    }

    private void FireHeavyBullet()
    {
        if (!IsServer || isDead || !heavyBulletPrefab || !muzzle) return;

        Debug.Log("[EliteMech] Spawning heavy bullet");

        Quaternion rot = muzzle.rotation; // bereits geprüft/gepitcht
        GameObject go = Instantiate(heavyBulletPrefab, muzzle.position, rot);

        if (!go.TryGetComponent<NetworkObject>(out var no))
        {
            Debug.LogError("[EnemyEliteMech] Heavy bullet needs a NetworkObject!");
            Destroy(go);
            return;
        }
        if (!no.IsSpawned) no.Spawn();

        if (go.TryGetComponent<EnemyHeavyBulletController>(out var ctrl))
            ctrl.Init(muzzle.forward);
    }

    // ---------- Health / Damage ----------
    public void TakeDamage(float amount, ulong attackerId, Vector3 hitPoint)
    {
        if (!IsServer || isDead) return;

        lastHitByClientId = attackerId;
        health.Value -= amount;

        if (dmgNums) dmgNums.ShowForAttackerOnly(amount, hitPoint, attackerId, isCrit: false);
        if (effects) effects.PlayHitEffectClientRpc(hitPoint);
    }

    public float GetBaseHealth() => baseHealth;
    public void SetHealth(float newHealth) { if (IsServer) health.Value = newHealth; }

    private void OnHealthChanged(float oldV, float newV)
    {
        OnHealthChanged01?.Invoke(
            baseHealth <= 0 ? 0f : Mathf.Clamp01(oldV / baseHealth),
            baseHealth <= 0 ? 0f : Mathf.Clamp01(newV / baseHealth)
        );

        if (newV <= 0 && !isDead) Die();
    }

    public void ConfigureForBaseAttack()
    {
        if (!IsServer) return;
        detectionRange = Mathf.Max(detectionRange, 2000f);
        loseTargetRange = 2000f;
        losMask = 0; // always LOS
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        state = State.Dead;

        // Wenn alle Collider aus sind fliegt der Mech durch den Boden.
        //foreach (var c in GetComponentsInChildren<Collider>())
        //    c.enabled = false; // oder Layer wechseln

        // sofort Bewegungen stoppen
        vel = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // FX + Animation
        effects?.PlayDeathEffectClientRpc(transform.position);
        if (anim) anim.SetBool(AnimAiming, false);
        if (netAnim) netAnim.SetTrigger(AnimDie);  // synchronisiert zu Clients

        // Drops sofort oder nachher – wie du magst
        GetComponent<EnemyDropper>()?.HandleDeath();

        // nach der Animation despawnen
        StartCoroutine(DespawnAfter(deathDespawnDelay));

        // Events
        OnEnemyDied?.Invoke(this);
    }

    private IEnumerator DespawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (IsServer)
            GetComponent<NetworkObject>().Despawn(true);
    }
}

// kleine Utility, um null-safe StopCoroutine zu machen
static class CoExt { public static void Let<T>(this T obj, Action<T> f) { if (obj != null) f(obj); } }
