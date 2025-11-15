using System;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyController))]
public class WaspDroneEnemy : NetworkBehaviour, IEnemy
{
    // ---------- IEnemy / Health ----------
    public event Action<IEnemy> OnEnemyDied;

    [SerializeField] private float baseHealth = 5f;
    [SerializeField] private EnemyDamageNumbers dmgNums;

    private NetworkVariable<float> health =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [HideInInspector] public ulong lastHitByClientId;
    public ulong LastHitByClientId => lastHitByClientId;
    public float GetBaseHealth() => baseHealth;

    // ---------- References ----------
    private Rigidbody rb;
    private EnemyController enemyController;
    private bool isDead;
    private Transform currentTarget;

    [Header("Visuals")]
    [SerializeField] private WaspLaserVisuals laserVisuals;  // LineRenderer-Visuals (optional, aber empfohlen)

    // ---------- States ----------
    private enum WaspState
    {
        Patrol,
        Buzz,
        Windup,   // zielt / lädt Laser
        Sweep     // Laser bewegt sich über Winkel
    }

    private WaspState state = WaspState.Patrol;

    // ---------- Patrol ----------
    [Header("Patrol")]
    [SerializeField] private float patrolRadius = 5f;
    [SerializeField] private float patrolSpeed = 4f;
    [SerializeField] private float patrolHeight = 3f;

    private Vector3 patrolCenter;
    private float patrolAngle; // für Kreisbewegung

    // ---------- Detection & Buzz ----------
    [Header("Detection & Buzz")]
    [SerializeField] private float detectionRange = 35f;
    [SerializeField] private float buzzPreferredDistance = 6f;
    [SerializeField] private float buzzDistanceTolerance = 2f;
    [SerializeField] private float buzzSpeed = 9f;
    [SerializeField, Range(0f, 1f)] private float orbitWeight = 0.4f;
    [SerializeField] private float verticalWobbleAmplitude = 1.0f;
    [SerializeField] private float verticalWobbleSpeed = 2.5f;
    [SerializeField] private float hoverHeightRelativeToTarget = 1.5f;
    [SerializeField] private float heightLerpSpeed = 6f;

    private int orbitDirection = 1; // +1 oder -1 (links/rechts)

    // ---------- Sweep-Laser ----------
    [Header("Sweep Laser")]
    [SerializeField] private float sweepCooldownMin = 3.5f;
    [SerializeField] private float sweepCooldownMax = 6f;
    [SerializeField] private float sweepWindupTime = 1.0f;
    [SerializeField] private float sweepDuration = 1.2f;
    [SerializeField] private float sweepTotalAngle = 90f; // Grad, z.B. 90 = 45° links nach 45° rechts
    [SerializeField] private float laserLength = 40f;
    [SerializeField] private float sweepHitRadius = 0.75f; // „Breite“ des Lasers
    [SerializeField] private float sweepDamagePerSecond = 40f; // DPS (Server_TakeDamage + iFrames balancieren)

    private float nextSweepTime;
    private float windupTimer;
    private float sweepTimer;

    private Vector3 sweepBaseDir;   // Basisrichtung (zum Spieler bei Start)
    private float sweepCurrentAngle;
    private float sweepAngleSign;   // +1 oder -1, Richtung des Sweeps

    // ---------- Look / Drone Behaviour ----------
    [Header("Look / Drone Behaviour")]
    [SerializeField] private float rotationSpeed = 10f;      // wie schnell Richtung gewechselt wird
    [SerializeField] private float idleYawRotateSpeed = 45f; // Grad/s beim Patrol
    [SerializeField] private float tiltAmplitude = 8f;       // Grad hoch/runter
    [SerializeField] private float tiltSpeed = 2.5f;         // wie schnell sie „nickt“
    [SerializeField, Range(0f, 1f)] private float lookAtTargetWeight = 0.6f;

    private Vector3 lastLookDir = Vector3.forward;

    // ---------- Unity Lifecycle ----------

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        enemyController = GetComponent<EnemyController>();

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        dmgNums = GetComponent<EnemyDamageNumbers>();
    }

    public override void OnNetworkSpawn()
    {
        rb.isKinematic = !IsServer;

        if (IsServer)
        {
            health.Value = baseHealth;

            patrolCenter = transform.position;
            patrolAngle = UnityEngine.Random.Range(0f, 360f);
            orbitDirection = UnityEngine.Random.value > 0.5f ? 1 : -1;
            state = WaspState.Patrol;

            ScheduleNextSweep();
        }

        health.OnValueChanged += OnHealthChanged;
    }

    private void OnDestroy()
    {
        health.OnValueChanged -= OnHealthChanged;
    }

    private void FixedUpdate()
    {
        if (!IsServer || isDead) return;

        enemyController.UpdateTarget();
        currentTarget = enemyController.Target;

        switch (state)
        {
            case WaspState.Patrol:
                TickPatrol();
                break;
            case WaspState.Buzz:
                TickBuzz();
                TryStartSweep();
                break;
            case WaspState.Windup:
                TickWindup();
                break;
            case WaspState.Sweep:
                TickSweep();
                break;
        }

        UpdateOrientation();
    }

    // ---------- State Logic ----------

    private void TickPatrol()
    {
        patrolAngle += patrolSpeed * Time.fixedDeltaTime;
        float rad = patrolAngle * Mathf.Deg2Rad;

        Vector3 targetPos = patrolCenter + new Vector3(
            Mathf.Cos(rad) * patrolRadius,
            patrolHeight,
            Mathf.Sin(rad) * patrolRadius
        );

        MoveTowardsPosition(targetPos, patrolSpeed);

        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            if (dist <= detectionRange)
            {
                state = WaspState.Buzz;
            }
        }
    }

    private void TickBuzz()
    {
        if (currentTarget == null)
        {
            state = WaspState.Patrol;
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.position);

        if (dist > detectionRange * 1.5f)
        {
            state = WaspState.Patrol;
            return;
        }

        // Höhe relativ zum Ziel + Wobble
        float desiredY = currentTarget.position.y + hoverHeightRelativeToTarget +
                         Mathf.Sin(Time.time * verticalWobbleSpeed) * verticalWobbleAmplitude;

        // Richtung zum Ziel (XZ)
        Vector3 toTarget = currentTarget.position - transform.position;
        Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
        Vector3 flatDir = flatToTarget.sqrMagnitude > 0.001f ? flatToTarget.normalized : Vector3.zero;

        // Orbit-Komponente
        Vector3 orbitDir = Vector3.Cross(Vector3.up, flatDir) * orbitDirection;

        // Wenn wir zu weit weg sind, stärker Richtung Zentrum fliegen
        float distanceFactor = Mathf.Clamp01(Mathf.Abs(dist - buzzPreferredDistance) / buzzDistanceTolerance);
        float towardWeight = Mathf.Lerp(0.7f, 1f, distanceFactor);

        Vector3 moveDir = (flatDir * towardWeight + orbitDir * orbitWeight).normalized;

        Vector3 targetPos = transform.position + moveDir;
        targetPos.y = Mathf.Lerp(transform.position.y, desiredY, Time.fixedDeltaTime * heightLerpSpeed);

        MoveTowardsPosition(targetPos, buzzSpeed);
    }

    private void TryStartSweep()
    {
        if (currentTarget == null) return;
        if (Time.time < nextSweepTime) return;

        // In den Windup-State gehen
        state = WaspState.Windup;
        windupTimer = sweepWindupTime;

        // Basisrichtung zum Spieler im Moment des Windups
        Vector3 toTarget = currentTarget.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f)
            toTarget = transform.forward;

        sweepBaseDir = toTarget.normalized;

        // Sweep-Parameter vorbereiten
        float halfAngle = sweepTotalAngle * 0.5f;
        sweepAngleSign = UnityEngine.Random.value > 0.5f ? 1f : -1f; // links->rechts oder rechts->links
        sweepCurrentAngle = -halfAngle * sweepAngleSign;

        // *** WICHTIG: Windup-Laser in der gleichen Richtung starten,
        //              in der auch der Sweep beginnt ***
        if (laserVisuals != null)
        {
            float startAngle = -halfAngle * sweepAngleSign;
            Vector3 sweepStartDir = Quaternion.AngleAxis(startAngle, Vector3.up) * sweepBaseDir;
            laserVisuals.StartWindupClientRpc(sweepStartDir, laserLength, sweepWindupTime);
        }
    }


    private void TickWindup()
    {
        // Optional, aber meist besser: während des Ziels NICHT mehr herumschweben
        // damit der Laser „stabil“ wirkt
        rb.linearVelocity = Vector3.zero;

        windupTimer -= Time.fixedDeltaTime;
        if (windupTimer <= 0f)
        {
            // In den Sweep-State wechseln
            state = WaspState.Sweep;
            sweepTimer = sweepDuration;

            if (laserVisuals != null)
            {
                laserVisuals.StartSweepClientRpc(
                    sweepBaseDir,
                    sweepTotalAngle,
                    sweepDuration,
                    sweepAngleSign,
                    laserLength
                );
            }
        }
    }


    private void TickSweep()
    {
        if (sweepTimer <= 0f)
        {
            state = WaspState.Buzz;
            ScheduleNextSweep();

            if (laserVisuals != null)
            {
                laserVisuals.EndSweepClientRpc();
            }

            return;
        }

        sweepTimer -= Time.fixedDeltaTime;

        // Fortschritt 0..1
        float t = 1f - (sweepTimer / sweepDuration);
        float halfAngle = sweepTotalAngle * 0.5f;
        float angleOffset = Mathf.Lerp(-halfAngle, halfAngle, t) * sweepAngleSign;
        sweepCurrentAngle = angleOffset;

        Vector3 beamDir = Quaternion.AngleAxis(sweepCurrentAngle, Vector3.up) * sweepBaseDir;
        beamDir.Normalize();

        // Raycast/SphereCast für Treffer
        RaycastHit hit;
        if (Physics.SphereCast(transform.position, sweepHitRadius, beamDir, out hit, laserLength))
        {
            var ph = hit.collider.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                float dmg = sweepDamagePerSecond * Time.fixedDeltaTime;
                ph.Server_TakeDamage(dmg, OwnerClientId);
            }
        }
    }

    private void ScheduleNextSweep()
    {
        nextSweepTime = Time.time + UnityEngine.Random.Range(sweepCooldownMin, sweepCooldownMax);
    }

    // ---------- Movement / Orientation ----------

    private void MoveTowardsPosition(Vector3 targetPos, float speed)
    {
        Vector3 dir = (targetPos - rb.position);
        if (dir.sqrMagnitude < 0.0001f)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        dir.Normalize();
        rb.MovePosition(rb.position + dir * speed * Time.fixedDeltaTime);
        rb.linearVelocity = dir * speed;
    }

    private void UpdateOrientation()
    {
        Quaternion baseRot = transform.rotation;
        Vector3 lookDir = lastLookDir;

        if (state == WaspState.Sweep)
        {
            // Bei Sweep in Laser-Richtung schauen
            Vector3 beamDir = Quaternion.AngleAxis(sweepCurrentAngle, Vector3.up) * sweepBaseDir;
            beamDir.y = 0f;
            if (beamDir.sqrMagnitude > 0.001f)
                lookDir = beamDir.normalized;
        }
        else
        {
            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            bool hasVel = vel.sqrMagnitude > 0.01f;
            lookDir = hasVel ? vel.normalized : transform.forward;

            if (currentTarget != null)
            {
                Vector3 toTarget = currentTarget.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Vector3 targetDir = toTarget.normalized;
                    lookDir = Vector3.Slerp(lookDir, targetDir, lookAtTargetWeight);
                }
            }
            else if (!hasVel && state == WaspState.Patrol)
            {
                float yawDelta = idleYawRotateSpeed * Time.fixedDeltaTime;
                lookDir = Quaternion.Euler(0f, yawDelta, 0f) * transform.forward;
            }
        }

        if (lookDir.sqrMagnitude > 0.001f)
        {
            lastLookDir = lookDir;
            Quaternion yawRot = Quaternion.LookRotation(lookDir, Vector3.up);
            baseRot = Quaternion.Lerp(baseRot, yawRot, rotationSpeed * Time.fixedDeltaTime);
        }

        float tilt = Mathf.Sin(Time.time * tiltSpeed) * tiltAmplitude;
        Quaternion tiltRot = Quaternion.Euler(tilt, 0f, 0f);

        transform.rotation = baseRot * tiltRot;
    }

    // ---------- Damage / Death ----------

    public void TakeDamage(float amount, ulong attackerId, Vector3 hitPoint)
    {
        if (!IsServer || isDead) return;

        lastHitByClientId = attackerId;
        health.Value -= amount;

        if (dmgNums != null)
            dmgNums.ShowForAttackerOnly(amount, hitPoint, attackerId, isCrit: false);
        else
            DamagePopupRelay.Instance?.ShowForAttackerOnly_Server(amount, hitPoint, attackerId, isCrit: false);

        GetComponent<EnemyEffects>()?.PlayHitEffectClientRpc(hitPoint);
    }

    public void SetHealth(float newHealth)
    {
        if (IsServer)
            health.Value = newHealth;
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (newValue <= 0f && !isDead)
            Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        OnEnemyDied?.Invoke(this);

        GetComponent<EnemyEffects>()?.PlayDeathEffectClientRpc(transform.position);
        GetComponent<EnemyDropper>()?.HandleDeath();

        if (laserVisuals != null)
        {
            laserVisuals.EndSweepClientRpc();
        }

        if (IsServer)
            GetComponent<NetworkObject>().Despawn(true);
    }
}
