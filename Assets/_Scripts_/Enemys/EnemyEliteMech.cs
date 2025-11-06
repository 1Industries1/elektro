using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode.Components;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(NetworkAnimator))]
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
    private Coroutine stompCo;
    private ulong lastHitByClientId;

    public event Action<IEnemy> OnEnemyDied;
    public ulong LastHitByClientId => lastHitByClientId;

    // ---------- Tuning ----------
    [Header("Perception")]
    [SerializeField] private float detectionRange = 55f;
    [SerializeField] private float loseTargetRange = 70f;
    [SerializeField] private LayerMask losMask;

    [Header("Movement (heavy)")]
    [SerializeField] private float moveSpeed = 2.1f;
    [SerializeField] private float accel = 4f;
    [SerializeField] private float rotationSpeed = 2.4f;
    [SerializeField] private float stopDistance = 22f;
    [SerializeField] private float resumeDistance = 26f;
    [SerializeField] private float externalPullMaxSpeed = 8f;

    [Header("A) Strafen")]
    [SerializeField] private float strafeChangeEvery = 1.6f;
    [SerializeField, Range(0f, 1f)] private float strafeIntensity = 0.7f;
    private float nextStrafeFlip;
    private int strafeDir = 1;

    [Header("Cannon / Aiming")]
    [SerializeField] private Transform cannonPivot;      // Pitch-Joint
    [SerializeField] private Transform muzzle;           // Spawnpunkt
    [SerializeField] private Vector2 pitchLimits = new Vector2(-8f, 30f);
    [SerializeField] private float pitchSpeed = 6f;
    [SerializeField] private float targetHeightOffset = 1.1f;

    [Header("Heavy Shot")]
    [SerializeField] private GameObject heavyBulletPrefab;
    [SerializeField] private float fireRange = 40f;
    private float projectileSpeed = 30f;     // Fallback, wird beim Spawn überschrieben
    private float cachedSplashRadius = 3.8f; // Fallback, wird beim Spawn überschrieben
    [SerializeField] private float chargeTime = 1.15f;
    [SerializeField] private float fireCooldown = 4.0f;
    [SerializeField] private GameObject chargeFx;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip chargeSfx;
    [SerializeField] private AudioClip fireSfx;

    [Header("Flame Burst")]
    [SerializeField] private GameObject flameExplosionPrefab; // Feuer-Explosion-Prefab
    [SerializeField] private float flameMinRadiusAroundPlayer = 2f;
    [SerializeField] private float flameMaxRadiusAroundPlayer = 6f;
    [SerializeField] private float flameCooldown = 7f;
    [SerializeField] private float flameSpawnHeight = 5f; // für Raycast von oben
    private float lastFlameTime = -999f;

    [Header("Attack Pattern")]
    [SerializeField] private Vector2Int shotsPerCycleRange = new Vector2Int(2, 4);   // 2–4 Schüsse
    [SerializeField] private Vector2Int flamesPerCycleRange = new Vector2Int(1, 2);  // 1–2 Flame Bursts
    private int remainingShotsInPhase;
    private int remainingFlamesInPhase;
    private enum AttackPhase { Shooting, Flame }
    private AttackPhase attackPhase = AttackPhase.Shooting;

    [Header("C) Telegrafie")]
    [SerializeField] private LineRenderer aimLaser;             // einfacher Aimpointer
    [SerializeField] private GameObject splashTelegraphPrefab;  // Kreis-Decal (Prefab)
    private GameObject splashTelegraphInstance;

    [SerializeField, Range(0f, 1f)] private float telegraphLockFraction = 0.5f; // Anteil der chargeTime, ab dem der Kreis NICHT mehr folgt
    [SerializeField] private float telegraphRandomMax = 2.0f;   // max. Zufallsoffset (Meter) rund um Ziel
    [SerializeField] private float telegraphMaxLeadSecs = 0.6f; // cap für Lead-Zeit in der Anzeige
    [SerializeField] private LayerMask telegraphGroundMask = ~0; // Bodenprojektion

    private Vector3? lockedAimPoint;   // gelockter Einschlagpunkt
    private float fireSeqStartTime;

    [Header("D) Shockwave-Stomp")]
    [SerializeField] private float stompRange = 8f;
    [SerializeField] private float stompWindup = 0.7f;
    [SerializeField] private float stompDamage = 10f;
    [SerializeField] private float stompForce = 800f;
    [SerializeField] private float stompCooldown = 6f;
    [SerializeField] private GameObject stompTelegraphPrefab;   // Kreis-Decal
    [SerializeField] private GameObject stompFx;                // Staub/Impuls FX
    private float lastStompTime = -999f;

    [Header("Health")]
    [SerializeField] private float baseHealth = 45f;
    [SerializeField] private float deathDespawnDelay = 2.5f;
    public event Action<float, float> OnHealthChanged01;
    public float Health01 => Mathf.Clamp01(baseHealth <= 0 ? 0f : health.Value / baseHealth);

    [Header("Damage Numbers / FX")]
    [SerializeField] private EnemyDamageNumbers dmgNums;
    [SerializeField] private EnemyEffects effects;

    // Animator parameter names
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimShoot = Animator.StringToHash("Shoot");
    private static readonly int AnimAiming = Animator.StringToHash("Aiming");
    private static readonly int AnimDie = Animator.StringToHash("Die");

    // networked health (server-authoritativ)
    private readonly NetworkVariable<float> health =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // movement cache
    private Vector3 vel;
    private enum State { Idle, Move, Aim, Firing, Cooldown, Dead, Stomp }
    private State state = State.Idle;

    // (B) Vorhalt-Aiming Helpers
    private readonly Dictionary<ulong, Vector3> lastPos = new();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        controller = GetComponent<EnemyController>();
        pull = GetComponent<PullReceiver>();
        anim = GetComponentInChildren<Animator>();
        netAnim = GetComponent<NetworkAnimator>();

        // nur Y-Rotation
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void OnNetworkSpawn()
    {
        rb.isKinematic = !IsServer;

        if (heavyBulletPrefab != null)
        {
            var comp = heavyBulletPrefab.GetComponent<EnemyHeavyBulletController>();
            if (comp != null)
            {
                projectileSpeed = comp.Speed;
                cachedSplashRadius = comp.SplashRadius;
            }
            else Debug.LogWarning("[EnemyEliteMech] heavyBulletPrefab hat keinen EnemyHeavyBulletController – nutze Fallbacks.");
        }

        if (IsServer)
        {
            health.Value = baseHealth;
            controller.UpdateTarget();
            target = controller.Target;
            state = State.Idle;
            lastFireTime = -999f;
            lastStompTime = -999f;
            lastFlameTime = -999f;

            // Attack-Pattern initialisieren
            attackPhase = AttackPhase.Shooting;
            remainingShotsInPhase = RandomInclusive(shotsPerCycleRange.x, shotsPerCycleRange.y);
            remainingFlamesInPhase = 0;
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

        // Externe Sogkräfte (GravityWell)
        if (pull != null)
        {
            var ext = pull.ConsumeStep(Time.fixedDeltaTime, externalPullMaxSpeed);
            if (ext.sqrMagnitude > 0f) rb.MovePosition(rb.position + ext);
        }

        // Animator-Speed
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

        // Körper yawen & Kanone pitchen
        RotateBodyTowards(target.position, dt);
        AimCannon(target.position, dt);

        // (D) Stomp-Check (Anti-Rush)
        if (state != State.Stomp && state != State.Firing && Time.time >= lastStompTime + stompCooldown && dist <= stompRange * 1.05f)
        {
            if (stompCo == null) stompCo = StartCoroutine(StompSequence());
            return;
        }

        switch (state)
        {
            case State.Idle:
                if (dist <= detectionRange && hasLOS)
                    state = (dist > stopDistance) ? State.Move : State.Aim;
                vel = Vector3.zero;
                ApplyMove(dt);
                break;

            case State.Move:
                MoveTowards(target.position, dt); // (A) mit Strafen
                if (dist <= stopDistance && hasLOS) { state = State.Aim; vel = Vector3.zero; }
                break;

            case State.Aim:
                vel = Vector3.Lerp(vel, Vector3.zero, dt * accel);
                ApplyMove(dt);

                if (anim) anim.SetBool(AnimAiming, true);

                // --- Angriffsmuster: Schießen-Phase vs. Flame-Phase ---
                if (hasLOS && dist <= fireRange)
                {
                    if (attackPhase == AttackPhase.Shooting)
                    {
                        // Heavy Shots Phase
                        if (Time.time >= lastFireTime + fireCooldown && fireCo == null && remainingShotsInPhase > 0)
                        {
                            fireCo = StartCoroutine(FireSequence());
                            state = State.Firing;

                            remainingShotsInPhase--;

                            // Wenn keine Schüsse mehr übrig → zur Flame-Phase wechseln
                            if (remainingShotsInPhase <= 0)
                            {
                                attackPhase = AttackPhase.Flame;
                                remainingFlamesInPhase = RandomInclusive(flamesPerCycleRange.x, flamesPerCycleRange.y);

                                // kleine Pause vor dem ersten Flame (optional)
                                lastFlameTime = Time.time;
                            }
                        }
                    }
                    else if (attackPhase == AttackPhase.Flame)
                    {
                        // Flame-Burst-Phase
                        if (remainingFlamesInPhase > 0 && Time.time >= lastFlameTime + flameCooldown)
                        {
                            SpawnFlameBurstAroundTarget(target);
                            remainingFlamesInPhase--;
                            lastFlameTime = Time.time;

                            // Wenn alle Flame Bursts gemacht → zurück zur Shooting-Phase
                            if (remainingFlamesInPhase <= 0)
                            {
                                attackPhase = AttackPhase.Shooting;
                                remainingShotsInPhase = RandomInclusive(shotsPerCycleRange.x, shotsPerCycleRange.y);
                            }
                        }
                        // In dieser Phase keine HeavyShots – er „hält an“ und zoned mit Feuer.
                    }
                }

                if (dist > resumeDistance || !hasLOS)
                {
                    if (anim) anim.SetBool(AnimAiming, false);
                    state = State.Move;
                }
                break;

            case State.Firing:
                vel = Vector3.Lerp(vel, Vector3.zero, dt * accel);
                ApplyMove(dt);
                break;

            case State.Cooldown:
                vel = Vector3.Lerp(vel, Vector3.zero, dt * accel);
                ApplyMove(dt);
                if (Time.time >= lastFireTime + 0.5f) state = State.Aim;
                break;

            case State.Stomp:
                // Bewegung ruht im Coroutine
                break;

            case State.Dead:
                break;
        }
    }

    // ---------- Helpers ----------
    private int RandomInclusive(int min, int max)
    {
        // Random.Range(int, int) ist [min, max), also max+1 verwenden
        return UnityEngine.Random.Range(min, max + 1);
    }

    // ---------- Movement helpers ----------
    private void MoveTowards(Vector3 worldPos, float dt)
    {
        Vector3 to = worldPos - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) { ApplyMove(dt); return; }

        Vector3 fwd = to.normalized;

        // (A) Strafen
        if (Time.time >= nextStrafeFlip)
        {
            strafeDir = UnityEngine.Random.value < 0.5f ? -1 : 1;
            nextStrafeFlip = Time.time + strafeChangeEvery;
        }
        Vector3 strafe = Vector3.Cross(Vector3.up, fwd) * (strafeDir * strafeIntensity);

        Vector3 desired = (fwd + strafe).normalized * moveSpeed;
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

        var hits = Physics.RaycastAll(eye, to.normalized, dist, losMask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (!h.collider || h.collider.transform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }

    // ---------- (B) Lead Aim ----------
    private Vector3 EstimateTargetVelocity(Transform t, ulong netId, float dt)
    {
        if (dt <= 0f) return Vector3.zero;
        Vector3 pos = t.position;
        if (lastPos.TryGetValue(netId, out var prev))
        {
            var v = (pos - prev) / dt; lastPos[netId] = pos; return v;
        }
        lastPos[netId] = pos; return Vector3.zero;
    }

    private Vector3 SolveInterceptDirection(Vector3 origin, Vector3 targetPos, Vector3 targetVel, float projSpeed)
    {
        Vector3 dir = targetPos - origin;
        float a = Vector3.Dot(targetVel, targetVel) - projSpeed * projSpeed;
        float b = 2f * Vector3.Dot(dir, targetVel);
        float c = Vector3.Dot(dir, dir);
        float t;
        if (Mathf.Abs(a) < 0.0001f) t = -c / b;
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return (targetPos - origin).normalized;
            float t1 = (-b + Mathf.Sqrt(disc)) / (2f * a);
            float t2 = (-b - Mathf.Sqrt(disc)) / (2f * a);
            t = Mathf.Max(t1, t2);
        }
        if (t <= 0f) return (targetPos - origin).normalized;
        Vector3 aimPoint = targetPos + targetVel * t;
        return (aimPoint - origin).normalized;
    }

    // ---------- Fire sequence + (C) Telegrafie ----------

    // Schätzt einen Aim-Punkt mit Lead, aber capped (für Telegraphie)
    private Vector3 PredictAimPoint(Transform t, float projSpeed, float maxLeadSec)
    {
        // nutze vorhandene Velocity-Schätzung
        ulong tid = t.GetComponent<NetworkObject>()?.NetworkObjectId ?? 0UL;
        var v = EstimateTargetVelocity(t, tid, Time.fixedDeltaTime);
        Vector3 targetPos = t.position + Vector3.up * targetHeightOffset;

        // Intercept-Zeit t
        Vector3 dir = targetPos - muzzle.position;
        float a = Vector3.Dot(v, v) - projSpeed * projSpeed;
        float b = 2f * Vector3.Dot(dir, v);
        float c = Vector3.Dot(dir, dir);
        float tHit;
        if (Mathf.Abs(a) < 1e-4f) tHit = -c / Mathf.Max(1e-4f, b);
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0f) tHit = 0f;
            else
            {
                float t1 = (-b + Mathf.Sqrt(disc)) / (2f * a);
                float t2 = (-b - Mathf.Sqrt(disc)) / (2f * a);
                tHit = Mathf.Max(t1, t2, 0f);
            }
        }

        tHit = Mathf.Clamp(tHit, 0f, maxLeadSec);
        return targetPos + v * tHit;
    }

    // kleiner Zufallsoffset in Kreisform
    private Vector3 RandomPlanarOffset(float maxRadius)
    {
        if (maxRadius <= 0f) return Vector3.zero;
        var r = UnityEngine.Random.insideUnitCircle * maxRadius; // gleichverteilte Scheibe
        return new Vector3(r.x, 0f, r.y);
    }

    // auf Boden projizieren (damit der Kreis sauber liegt)
    private Vector3 ProjectToGround(Vector3 p)
    {
        var origin = p + Vector3.up * 50f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 100f, telegraphGroundMask, QueryTriggerInteraction.Ignore))
            return hit.point;
        return new Vector3(p.x, p.y + 0.05f, p.z); // fallback: leicht anheben
    }

    private IEnumerator FireSequence()
    {
        lastFireTime = Time.time;
        fireSeqStartTime = Time.time;
        lockedAimPoint = null;

        // Telegraphie an
        EnsureSplashTelegraph();
        SetAimLaser(true);

        if (chargeFx) chargeFx.SetActive(true);
        if (audioSource && chargeSfx) audioSource.PlayOneShot(chargeSfx);
        if (anim) { anim.SetBool(AnimAiming, true); netAnim.SetTrigger(AnimShoot); }

        float lockAt = chargeTime * Mathf.Clamp01(telegraphLockFraction);
        float tEnd = Time.time + chargeTime;

        while (Time.time < tEnd && !isDead)
        {
            if (target && muzzle)
            {
                // Solange NICHT gelockt: Zielpunkt „mitführen“ (Lead + Zufall), danach stabil lassen
                if (!lockedAimPoint.HasValue)
                {
                    var pred = PredictAimPoint(target, projectileSpeed, telegraphMaxLeadSecs);
                    pred += RandomPlanarOffset(telegraphRandomMax);
                    var ground = ProjectToGround(pred);
                    // zeige Kreis an dieser Stelle
                    if (splashTelegraphInstance)
                    {
                        splashTelegraphInstance.transform.position = ground;
                        splashTelegraphInstance.transform.localScale = Vector3.one * (cachedSplashRadius * 2f);
                    }
                    // Laser zeigt Richtung
                    if (aimLaser && aimLaser.enabled)
                    {
                        aimLaser.SetPosition(0, muzzle.position);
                        aimLaser.SetPosition(1, ground + Vector3.up * 0.05f);
                    }

                    // Lock ab einem Bruchteil der Ladedauer
                    if (Time.time >= fireSeqStartTime + lockAt)
                        lockedAimPoint = ground;
                }
                else
                {
                    // Nach Lock NICHT mehr folgen → nur visuell aktualisieren (falls Boden bewegt)
                    var p = ProjectToGround(lockedAimPoint.Value);
                    if (splashTelegraphInstance)
                    {
                        splashTelegraphInstance.transform.position = p;
                        splashTelegraphInstance.transform.localScale = Vector3.one * (cachedSplashRadius * 2f);
                    }
                    if (aimLaser && aimLaser.enabled)
                    {
                        aimLaser.SetPosition(0, muzzle.position);
                        aimLaser.SetPosition(1, p + Vector3.up * 0.05f);
                    }
                }

                // weiterhin Körper/Kanone nachführen (Optik)
                RotateBodyTowards(target.position, Time.fixedDeltaTime);
                AimCannon(target.position, Time.fixedDeltaTime);
            }

            yield return new WaitForFixedUpdate();
        }

        if (chargeFx) chargeFx.SetActive(false);
        SetAimLaser(false);
        HideSplashTelegraph();

        // Schuss: auf den gelockten Punkt (falls vorhanden), sonst Standard-Lead
        FireHeavyBullet();

        if (audioSource && fireSfx) audioSource.PlayOneShot(fireSfx);

        state = State.Cooldown;
        fireCo = null;
    }

    private void FireHeavyBullet()
    {
        if (!IsServer || isDead || !heavyBulletPrefab || !muzzle) return;

        Vector3 dir;
        if (lockedAimPoint.HasValue)
        {
            // auf gelockten boden-projizierten Punkt zielen
            dir = (lockedAimPoint.Value + Vector3.up * 0.05f - muzzle.position).normalized;
        }
        else if (target != null)
        {
            // Fallback: klassisches Vorhalt-Aiming
            ulong tid = target.GetComponent<NetworkObject>()?.NetworkObjectId ?? 0UL;
            var v = EstimateTargetVelocity(target, tid, Time.fixedDeltaTime);
            dir = SolveInterceptDirection(muzzle.position, target.position + Vector3.up * targetHeightOffset, v, projectileSpeed);
        }
        else
        {
            dir = muzzle.forward;
        }

        var go = Instantiate(heavyBulletPrefab, muzzle.position, Quaternion.LookRotation(dir, Vector3.up));
        if (!go.TryGetComponent<NetworkObject>(out var no))
        {
            Debug.LogError("[EnemyEliteMech] Heavy bullet needs a NetworkObject!");
            Destroy(go);
            return;
        }
        if (!no.IsSpawned) no.Spawn();

        if (go.TryGetComponent<EnemyHeavyBulletController>(out var ctrl))
            ctrl.Init(dir);
    }

    private void SpawnFlameBurstAroundTarget(Transform t)
    {
        if (!IsServer || flameExplosionPrefab == null) return;

        Debug.Log("[EnemyEliteMech] FlameBurst gespawnt");

        // zufällige Richtung + Distanz um den Spieler
        Vector2 circle = UnityEngine.Random.insideUnitCircle.normalized *
                        UnityEngine.Random.Range(flameMinRadiusAroundPlayer, flameMaxRadiusAroundPlayer);
        Vector3 candidate = t.position + new Vector3(circle.x, 0f, circle.y);

        // Von oben auf den Boden raycasten
        Vector3 origin = candidate + Vector3.up * flameSpawnHeight;
        if (Physics.Raycast(origin, Vector3.down, out var hit, flameSpawnHeight * 2f, telegraphGroundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 spawnPos = hit.point;
            Debug.Log("[EnemyEliteMech] FlameBurst Hit: " + hit.collider.name + " at " + spawnPos);

            var go = Instantiate(flameExplosionPrefab, spawnPos, Quaternion.identity);
            if (go.TryGetComponent<NetworkObject>(out var no))
            {
                if (!no.IsSpawned)
                    no.Spawn();
            }
            Debug.Log("[EnemyEliteMech] FlameBurst GO: " + go.name + " layer=" + go.layer);
        }
        else
        {
            Debug.LogWarning("[EnemyEliteMech] FlameBurst Raycast hat NICHTS getroffen. Origin=" + origin);
        }

    }

    // ---------- (C) Telegraphie helpers ----------
    private void EnsureSplashTelegraph()
    {
        if (!splashTelegraphInstance && splashTelegraphPrefab)
        {
            splashTelegraphInstance = Instantiate(splashTelegraphPrefab);
        }
        if (splashTelegraphInstance)
        {
            splashTelegraphInstance.SetActive(true);
        }
    }

    private void HideSplashTelegraph()
    {
        if (splashTelegraphInstance) splashTelegraphInstance.SetActive(false);
    }

    private void SetAimLaser(bool on)
    {
        if (!aimLaser) return;
        aimLaser.enabled = on;
        if (on && muzzle && target)
        {
            aimLaser.SetPosition(0, muzzle.position);
            aimLaser.SetPosition(1, target.position + Vector3.up * targetHeightOffset);
        }
    }

    // ---------- (D) Shockwave Stomp ----------
    private IEnumerator StompSequence()
    {
        state = State.Stomp;
        lastStompTime = Time.time;

        // Telegraphie-Kreis
        GameObject tele = null;
        if (stompTelegraphPrefab) tele = Instantiate(stompTelegraphPrefab, transform.position, Quaternion.identity);
        if (tele) tele.transform.localScale = Vector3.one * stompRange * 2f;

        // kurze Windup-Animation
        if (anim) anim.SetBool(AnimAiming, false);
        Vector3 oldVel = vel;
        vel = Vector3.zero;

        float tEnd = Time.time + stompWindup;
        while (Time.time < tEnd && !isDead)
        {
            if (tele) tele.transform.position = transform.position;
            yield return new WaitForFixedUpdate();
        }

        if (tele) Destroy(tele);

        // Impact FX
        if (stompFx) Instantiate(stompFx, transform.position, Quaternion.identity);

        // Schaden + Rückstoß
        var hits = Physics.OverlapSphere(transform.position, stompRange, ~0, QueryTriggerInteraction.Ignore);
        foreach (var c in hits)
        {
            if (c.CompareTag("Player"))
            {
                var hp = c.GetComponentInParent<PlayerHealth>();
                if (hp != null) hp.Server_TakeDamage(stompDamage, OwnerClientId);

                if (c.attachedRigidbody != null)
                {
                    c.attachedRigidbody.AddExplosionForce(stompForce, transform.position, stompRange, 0.5f, ForceMode.Impulse);
                }
            }
        }

        vel = oldVel;
        state = State.Cooldown;
        stompCo = null;
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

        // Bewegungen stoppen
        vel = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // FX + Animation
        effects?.PlayDeathEffectClientRpc(transform.position);
        if (anim) anim.SetBool(AnimAiming, false);
        if (netAnim) netAnim.SetTrigger(AnimDie);

        GetComponent<EnemyDropper>()?.HandleDeath();

        StartCoroutine(DespawnAfter(deathDespawnDelay));
        OnEnemyDied?.Invoke(this);
    }

    private IEnumerator DespawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (IsServer)
            GetComponent<NetworkObject>().Despawn(true);
    }
}
