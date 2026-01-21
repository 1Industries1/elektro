using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
//[RequireComponent(typeof(Collider))]
public class ExploderEnemy : NetworkBehaviour, IEnemy
{
    // ----------------- IEnemy / Health -----------------
    public event Action<IEnemy> OnEnemyDied;
    public ulong LastHitByClientId => lastHitByClientId;
    [HideInInspector] public ulong lastHitByClientId;

    [Header("Health")]
    [SerializeField] private float baseHealth = 5f;
    public float GetBaseHealth() => baseHealth;

    private NetworkVariable<float> health =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ----------------- Components -----------------
    private Rigidbody rb;
    private Collider col;
    private EnemyController enemyController; // optional
    private NavMeshAgent navAgent;           // optional: wir DISABLEN ihn automatisch, falls vorhanden
    private EnemyDamageNumbers dmgNums;
    private EnemyEffects enemyEffects;
    private EnemyDropper enemyDropper;

    // ----------------- Target -----------------
    private Transform target;

    [Header("Targeting")]
    [SerializeField] private float detectionRange = 40f;
    [SerializeField] private float triggerRange = 7f;
    [SerializeField] private float retargetInterval = 0.25f;
    private float nextRetargetTime;

    // ----------------- Movement -----------------
    [Header("Movement")]
    [SerializeField] private float minMoveSpeed = 4f;
    [SerializeField] private float maxMoveSpeed = 8f;
    [SerializeField] private float rotationSpeed = 7f;

    [Header("Ground Smoothing")]
    [SerializeField] private float groundFollowSpeed = 18f;   // höher = schneller folgen (10-25 gut)
    [SerializeField] private float maxStepUpPerTick = 0.08f;  // wie schnell er hoch darf pro FixedUpdate
    [SerializeField] private float maxStepDownPerTick = 0.25f; // wie schnell er runter darf
    [SerializeField] private float groundDeadZone = 0.005f;   // ignoriert Mikro-deltas

    [Header("Hills / Terrain")]
    [SerializeField] private float terrainStickYOffset = 0.05f;
    [SerializeField] private float maxTerrainSnapDown = 2.0f;
    [SerializeField] private float downhillCheckDistance = 1.5f;
    [SerializeField] private float downhillBoostMultiplier = 1.6f;
    [SerializeField] private float downhillThreshold = 0.4f;

    private float baseMoveSpeed;
    private float groundYVel; // für SmoothDamp

    // ----------------- Explosion / Charge -----------------
    private enum State { Chasing, Charging, Exploded, Dead }
    private State state = State.Chasing;

    [Header("Charge & Explosion")]
    [SerializeField] private float chargeWindup = 0.25f;
    [SerializeField] private float chargeDuration = 1.25f;
    [SerializeField] private float chargeSpeedMultiplier = 1.9f;
    [SerializeField] private float chargeTurnRate = 2.0f;
    [SerializeField] private float explodeDistance = 2.0f;
    [SerializeField] private float explosionRadius = 6f;
    [SerializeField] private float explosionDamage = 30f;
    [SerializeField] private LayerMask playerLayer;

    private double chargeStartServerTime;
    private double dashStartServerTime;
    private double dashEndServerTime;
    private Vector3 chargeDirection;

    private bool isDead;

    // ----------------- VFX/SFX -----------------
    [Header("Explosion VFX/SFX")]
    [SerializeField] private GameObject explosionVfxPrefab;
    [SerializeField] private AudioClip explosionClip;
    [SerializeField][Range(0f, 1f)] private float explosionVolume = 1f;

    // ----------------- Visuals (Glow) -----------------
    [Header("Visuals")]
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private string emissionColorProperty = "_EmissionColor";
    [SerializeField] private Color idleEmissionColor = Color.black;
    [SerializeField] private Color chargeEmissionColor = Color.yellow;
    [SerializeField] private AnimationCurve chargeGlowCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float chargeGlowIntensity = 7f;

    private MaterialPropertyBlock mpb;

    private bool clientCharging;
    private double clientChargeStartServerTime;
    private float clientChargeTotalDuration;

    // ----------------- Debug -----------------
    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    private float nextDebugTime;
    private Vector3 lastPos;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        enemyController = GetComponent<EnemyController>();
        navAgent = GetComponent<NavMeshAgent>(); // falls vorhanden -> später deaktivieren

        dmgNums = GetComponent<EnemyDamageNumbers>();
        enemyEffects = GetComponent<EnemyEffects>();
        enemyDropper = GetComponent<EnemyDropper>();

        mpb = new MaterialPropertyBlock();
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[ExploderEnemy] IsServer={IsServer} IsHost={IsHost} Owner={NetworkObject.OwnerClientId} ServerId={NetworkManager.ServerClientId}");

        if (IsServer)
        {
            // Enemy MUSS server-owned sein, sonst snappt NetworkTransform ihn zurück
            if (NetworkObject.OwnerClientId != NetworkManager.ServerClientId)
                NetworkObject.ChangeOwnership(NetworkManager.ServerClientId);
        }

        // Wichtig: NavMeshAgent kann Position überschreiben/konflikte verursachen -> AUS
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        // Server bewegt deterministisch => kinematic überall
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (IsServer)
        {
            baseMoveSpeed = UnityEngine.Random.Range(minMoveSpeed, maxMoveSpeed);
            health.Value = baseHealth;

            lastPos = rb.position;
        }

        health.OnValueChanged += OnHealthChanged;
        ResetEmissionClient();
    }

    private void OnDestroy()
    {
        health.OnValueChanged -= OnHealthChanged;
    }

    private void FixedUpdate()
    {
        if (!IsServer || isDead || state == State.Exploded || state == State.Dead)
            return;

        // Target throttled
        if (Time.time >= nextRetargetTime)
        {
            nextRetargetTime = Time.time + retargetInterval;
            UpdateTarget();
        }

        // Debug heartbeat (1x/s)
        if (debugLogs && Time.time >= nextDebugTime)
        {
            nextDebugTime = Time.time + 1f;

            float moved = Vector3.Distance(rb.position, lastPos);
            lastPos = rb.position;

            string tName = target != null ? target.name : "NULL";
            float dist = target != null ? Vector3.Distance(transform.position, target.position) : -1f;

            Debug.Log($"[ExploderEnemy] state={state} target={tName} dist={dist:F2} movedLastSec={moved:F3} baseSpeed={baseMoveSpeed:F2}");
        }

        if (target == null) return;

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > detectionRange) return;

        switch (state)
        {
            case State.Chasing:
                TickChasing(distance);
                break;
            case State.Charging:
                TickCharging(distance);
                break;
        }

        StickToTerrain();
    }

    // ----------------- Chasing -----------------
    private void TickChasing(float dist)
    {
        if (dist <= triggerRange)
        {
            StartChargeServer();
            return;
        }

        Vector3 dir = (target.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        dir.Normalize();

        RotateTowardsDirection(dir, rotationSpeed);

        float speed = GetEffectiveSpeed(dir);

        // WICHTIG: Bei kinematic RB ist rb.position setzen extrem robust.
        Vector3 next = rb.position + dir * speed * Time.fixedDeltaTime;
        rb.position = next;
    }

    // ----------------- Charging -----------------
    private void StartChargeServer()
    {
        if (state != State.Chasing) return;
        state = State.Charging;

        double now = NetworkManager.Singleton.ServerTime.Time;
        chargeStartServerTime = now;
        dashStartServerTime = now + chargeWindup;
        dashEndServerTime = dashStartServerTime + chargeDuration;

        Vector3 snap = (target != null) ? (target.position - transform.position) : transform.forward;
        snap.y = 0f;
        if (snap.sqrMagnitude < 0.001f) snap = transform.forward;
        chargeDirection = snap.normalized;

        StartChargeClientRpc(chargeStartServerTime, (float)(chargeWindup + chargeDuration));
    }

    private void TickCharging(float distToTarget)
    {
        double now = NetworkManager.Singleton.ServerTime.Time;

        if (distToTarget <= explodeDistance)
        {
            ExplodeServer();
            return;
        }

        if (now >= dashEndServerTime)
        {
            ExplodeServer();
            return;
        }

        // Windup: nur drehen/telegraph
        if (now < dashStartServerTime)
        {
            if (target != null)
            {
                Vector3 look = target.position - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f)
                    RotateTowardsDirection(look.normalized, rotationSpeed);
            }
            return;
        }

        // Dash: leichtes homing
        if (target != null)
        {
            Vector3 desired = target.position - transform.position;
            desired.y = 0f;
            if (desired.sqrMagnitude > 0.001f)
            {
                desired.Normalize();
                chargeDirection = Vector3.Slerp(chargeDirection, desired, Time.fixedDeltaTime * chargeTurnRate);
                chargeDirection.y = 0f;
                if (chargeDirection.sqrMagnitude > 0.001f) chargeDirection.Normalize();
            }
        }

        RotateTowardsDirection(chargeDirection, rotationSpeed);

        float dashSpeed = baseMoveSpeed * chargeSpeedMultiplier;

        Vector3 next = rb.position + chargeDirection * dashSpeed * Time.fixedDeltaTime;
        rb.position = next;
    }

    // ----------------- Terrain -----------------
    private float GetEffectiveSpeed(Vector3 moveDir)
    {
        float speed = baseMoveSpeed;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            Vector3 pos = rb.position;
            float currentY = terrain.SampleHeight(pos) + terrain.transform.position.y;
            float aheadY = terrain.SampleHeight(pos + moveDir * downhillCheckDistance) + terrain.transform.position.y;

            float delta = currentY - aheadY; // >0 = bergab
            if (delta > downhillThreshold)
                speed *= downhillBoostMultiplier;
        }

        return speed;
    }

    private void StickToTerrain()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        Vector3 pos = rb.position;

        float terrainY = terrain.SampleHeight(pos) + terrain.transform.position.y;
        float bottomOffset = GetBottomOffsetWorld(); // aus deiner aktuellen Fix-Version
        float wantedY = terrainY + bottomOffset + terrainStickYOffset;

        float delta = wantedY - pos.y;
        if (Mathf.Abs(delta) < groundDeadZone) return; // Mikroflackern ignorieren

        // Sanft folgen: SmoothDamp ist stabil gegen kleine Noise-Samples
        float smoothTime = 1f / Mathf.Max(groundFollowSpeed, 0.001f);
        float newY = Mathf.SmoothDamp(pos.y, wantedY, ref groundYVel, smoothTime, Mathf.Infinity, Time.fixedDeltaTime);

        // Step-Limits pro Tick (verhindert “pumpen”)
        float step = newY - pos.y;
        if (step > 0f) step = Mathf.Min(step, maxStepUpPerTick);
        else step = Mathf.Max(step, -maxStepDownPerTick);

        pos.y += step;
        rb.position = pos;
    }

    private float GetBottomOffsetWorld()
    {
        var cap = GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            float centerWorldY = transform.TransformPoint(cap.center).y;
            float bottomWorldY = centerWorldY - (cap.height * 0.5f) + cap.radius;
            return bottomWorldY - transform.position.y;
        }

        // Fallback (weniger stabil)
        return (col.bounds.min.y - transform.position.y);
    }


    // ----------------- Explosion -----------------
    private void ExplodeServer()
    {
        if (!IsServer || state == State.Exploded || state == State.Dead || isDead)
            return;

        state = State.Exploded;

        Vector3 pos = transform.position;

        Collider[] hits = Physics.OverlapSphere(pos, explosionRadius, playerLayer, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            var hp = hit.GetComponent<PlayerHealth>();
            if (hp != null)
                hp.Server_TakeDamage(explosionDamage, ulong.MaxValue);
        }

        SpawnExplosionVfxClientRpc(pos);
        PlayExplosionSoundClientRpc(pos);

        DieServer();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || isDead) return;
        if (state != State.Charging) return;

        if (((1 << collision.gameObject.layer) & playerLayer) != 0)
            ExplodeServer();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || isDead) return;
        if (state != State.Charging) return;

        if (((1 << other.gameObject.layer) & playerLayer) != 0)
            ExplodeServer();
    }

    [ClientRpc]
    private void SpawnExplosionVfxClientRpc(Vector3 pos)
    {
        if (explosionVfxPrefab != null)
            Instantiate(explosionVfxPrefab, pos, Quaternion.identity);
    }

    [ClientRpc]
    private void PlayExplosionSoundClientRpc(Vector3 pos)
    {
        if (explosionClip == null) return;
        AudioSource.PlayClipAtPoint(explosionClip, pos, explosionVolume);
    }

    // ----------------- Targeting -----------------
    private void UpdateTarget()
    {
        if (enemyController != null)
        {
            enemyController.UpdateTarget();
            target = enemyController.Target;
            return;
        }

        if (NetworkManager.Singleton == null) return;

        float minDist = Mathf.Infinity;
        Transform closest = null;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            float d = Vector3.Distance(transform.position, playerObj.transform.position);
            if (d < minDist)
            {
                minDist = d;
                closest = playerObj.transform;
            }
        }

        target = closest;
    }

    // ----------------- Rotation -----------------
    private void RotateTowardsDirection(Vector3 dir, float lerpSpeed)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion wanted = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Lerp(transform.rotation, wanted, Time.fixedDeltaTime * lerpSpeed);
    }

    // ----------------- Visuals (Glow) -----------------
    [ClientRpc]
    private void StartChargeClientRpc(double startServerTime, float totalDuration)
    {
        clientCharging = true;
        clientChargeStartServerTime = startServerTime;
        clientChargeTotalDuration = totalDuration;
    }

    private void Update()
    {
        if (!clientCharging || bodyRenderer == null) return;

        double st = (NetworkManager.Singleton != null)
            ? NetworkManager.Singleton.ServerTime.Time
            : Time.time;

        float t = Mathf.InverseLerp(
            (float)clientChargeStartServerTime,
            (float)(clientChargeStartServerTime + clientChargeTotalDuration),
            (float)st
        );
        t = Mathf.Clamp01(t);

        float glow = chargeGlowCurve.Evaluate(t);
        Color c = Color.Lerp(idleEmissionColor, chargeEmissionColor, glow) * (1f + glow * chargeGlowIntensity);

        bodyRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(emissionColorProperty, c);
        bodyRenderer.SetPropertyBlock(mpb);

        if (t >= 1f) clientCharging = false;
    }

    private void ResetEmissionClient()
    {
        if (bodyRenderer == null) return;

        bodyRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(emissionColorProperty, idleEmissionColor);
        bodyRenderer.SetPropertyBlock(mpb);

        clientCharging = false;
    }

    [ClientRpc]
    private void ResetEmissionClientRpc()
    {
        ResetEmissionClient();
    }

    // ----------------- Damage & Death -----------------
    public void TakeDamage(float amount, ulong attackerId, Vector3 hitPoint)
    {
        if (!IsServer || isDead) return;

        lastHitByClientId = attackerId;
        health.Value -= amount;

        if (dmgNums != null)
            dmgNums.ShowForAttackerOnly(amount, hitPoint, attackerId, isCrit: false);
        else
            DamagePopupRelay.Instance?.ShowForAttackerOnly_Server(amount, hitPoint, attackerId, isCrit: false);

        enemyEffects?.PlayHitEffectClientRpc(hitPoint);
    }

    public void SetHealth(float newHealth)
    {
        if (IsServer) health.Value = newHealth;
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (!IsServer || isDead) return;

        if (newValue <= 0f)
        {
            if (state != State.Exploded && state != State.Dead)
                ExplodeServer();
            else
                DieServer();
        }
    }

    private void DieServer()
    {
        if (isDead) return;

        isDead = true;
        state = State.Dead;

        ResetEmissionClient();
        ResetEmissionClientRpc();

        OnEnemyDied?.Invoke(this);

        enemyEffects?.PlayDeathEffectClientRpc(transform.position);
        enemyDropper?.HandleDeath();

        if (IsServer)
            GetComponent<NetworkObject>().Despawn(true);
    }
}
