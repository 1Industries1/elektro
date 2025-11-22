using System;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
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

    // ----------------- Movement & Targeting -----------------
    private Rigidbody rb;
    private EnemyController enemyController; // optional – wenn vorhanden, benutzen wir ihn

    [Header("Movement")]
    [SerializeField] private float detectionRange = 40f;
    [SerializeField] private float triggerRange = 7f;       // Startet Charge
    [SerializeField] private float minMoveSpeed = 4f;
    [SerializeField] private float maxMoveSpeed = 8f;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Hills / Terrain")]
    [SerializeField] private float terrainStickYOffset = 0.05f;
    [SerializeField] private float maxTerrainFallDistance = 5f;
    [SerializeField] private float downhillCheckDistance = 1.5f;
    [SerializeField] private float downhillBoostMultiplier = 1.6f;
    [SerializeField] private float downhillThreshold = 0.4f; // mind. so viel Höhenunterschied = bergab

    [Header("Audio")]
    [SerializeField] private AudioClip explosionClip;
    [SerializeField][Range(0f, 1f)] private float explosionVolume = 1f;

    private float baseMoveSpeed;
    private Transform target;

    // ----------------- Explosion -----------------
    private enum State { Chasing, Charging, Exploded, Dead }
    private State state = State.Chasing;

    [Header("Explosion")]
    [SerializeField] private float chargeDuration = 1.5f;
    [SerializeField] private float explosionRadius = 6f;
    [SerializeField] private float explosionDamage = 30f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private GameObject explosionVfxPrefab;
    [SerializeField]
    private AnimationCurve chargeGlowCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1); // 0..1 -> für Emission-Intensität

    private float chargeStartTime;

    // Visuelle Teile (optional, im Inspector zuweisen)
    [Header("Visuals")]
    [SerializeField] private Renderer bodyRenderer; // Material mit Emission
    [SerializeField] private string emissionColorProperty = "_EmissionColor";
    [SerializeField] private Color idleEmissionColor = Color.black;
    [SerializeField] private Color chargeEmissionColor = Color.yellow;

    // Andere Komponenten aus deinem Projekt
    private EnemyDamageNumbers dmgNums;
    private EnemyEffects enemyEffects;
    private EnemyDropper enemyDropper;

    private bool isDead = false;

    // --------------------------------------------------------

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        enemyController = GetComponent<EnemyController>();
        dmgNums = GetComponent<EnemyDamageNumbers>();
        enemyEffects = GetComponent<EnemyEffects>();
        enemyDropper = GetComponent<EnemyDropper>();
    }

    public override void OnNetworkSpawn()
    {
        rb.isKinematic = !IsServer;

        if (IsServer)
        {
            baseMoveSpeed = UnityEngine.Random.Range(minMoveSpeed, maxMoveSpeed);
            health.Value = baseHealth;
        }

        health.OnValueChanged += OnHealthChanged;
    }

    void OnDestroy()
    {
        health.OnValueChanged -= OnHealthChanged;
    }

    void FixedUpdate()
    {
        if (!IsServer || isDead || state == State.Exploded || state == State.Dead)
            return;

        // Target aktualisieren – wenn EnemyController dran ist, den benutzen
        UpdateTarget();

        switch (state)
        {
            case State.Chasing:
                TickChasing();
                break;
            case State.Charging:
                TickCharging();
                break;
        }

        StickToTerrain();
    }

    // ----------------- State: Chasing -----------------
    private void TickChasing()
    {
        if (target == null) return;

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > detectionRange) return;

        RotateTowardsTarget(target);

        if (distance <= triggerRange)
        {
            StartCharge();
        }
        else
        {
            MoveTowardsTarget(target);
        }
    }

    // ----------------- State: Charging -----------------
    private void StartCharge()
    {
        state = State.Charging;
        chargeStartTime = Time.time;
        StartChargeClientRpc();
    }

    private void TickCharging()
    {
        if (Time.time - chargeStartTime >= chargeDuration)
        {
            Explode();
        }
        else
        {
            // Während des Charge leicht zum Ziel drehen, aber nicht mehr groß laufen
            if (target != null)
                RotateTowardsTarget(target);
        }
    }

    // ----------------- Bewegung / Terrain -----------------
    private void UpdateTarget()
    {
        if (enemyController != null && IsServer)
        {
            enemyController.UpdateTarget();
            target = enemyController.Target;
            return;
        }

        // Fallback: selbst suchen
        float minDist = Mathf.Infinity;
        Transform closest = null;

        if (NetworkManager.Singleton == null) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            float dist = Vector3.Distance(transform.position, playerObj.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = playerObj.transform;
            }
        }

        target = closest;
    }

    private void RotateTowardsTarget(Transform t)
    {
        Vector3 direction = (t.position - transform.position).normalized;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion wantedRot = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Lerp(transform.rotation, wantedRot,
            Time.fixedDeltaTime * rotationSpeed);
    }

    private void MoveTowardsTarget(Transform t)
    {
        Vector3 direction = (t.position - transform.position).normalized;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        float speed = GetEffectiveSpeed(direction);
        rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
    }

    private float GetEffectiveSpeed(Vector3 moveDir)
    {
        float speed = baseMoveSpeed;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            Vector3 pos = rb.position;
            float currentY = terrain.SampleHeight(pos) + terrain.transform.position.y;
            float aheadY = terrain.SampleHeight(pos + moveDir * downhillCheckDistance) + terrain.transform.position.y;

            float delta = currentY - aheadY; // > 0 => es geht bergab
            if (delta > downhillThreshold)
            {
                speed *= downhillBoostMultiplier;
            }
        }

        return speed;
    }

    private void StickToTerrain()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        Vector3 pos = rb.position;
        float terrainY = terrain.SampleHeight(pos) + terrain.transform.position.y;

        if (pos.y < terrainY - maxTerrainFallDistance)
        {
            pos.y = terrainY + terrainStickYOffset;
            rb.position = pos;

            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;
        }
    }

    // ----------------- Explosion & Damage -----------------
    private void Explode()
    {
        if (!IsServer || state == State.Exploded || state == State.Dead)
            return;

        state = State.Exploded;

        Vector3 pos = transform.position;

        // Schaden an Spielern
        Collider[] hits = Physics.OverlapSphere(pos, explosionRadius, playerLayer);
        foreach (var hit in hits)
        {
            var hp = hit.GetComponent<PlayerHealth>();
            if (hp != null)
            {
                hp.Server_TakeDamage(explosionDamage, ulong.MaxValue);
            }
        }

        // VFX + Sound auf allen Clients
        SpawnExplosionVfxClientRpc(pos);
        PlayExplosionSoundClientRpc(pos);

        Die();
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

        // erzeugt automatisch ein temporäres GameObject mit AudioSource
        AudioSource.PlayClipAtPoint(explosionClip, pos, explosionVolume);
    }


    [ClientRpc]
    private void StartChargeClientRpc()
    {
        // einfaches visuelles Feedback: Emission hochdrehen / skalieren
        if (bodyRenderer == null) return;

        var mat = bodyRenderer.material;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor(emissionColorProperty, chargeEmissionColor);
    }

    private void ResetEmission()
    {
        if (bodyRenderer == null) return;
        var mat = bodyRenderer.material;
        if (!mat.HasProperty(emissionColorProperty)) return;

        mat.SetColor(emissionColorProperty, idleEmissionColor);
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
        if (IsServer)
            health.Value = newHealth;
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (newValue <= 0 && !isDead)
        {
            // Wird er beim Tod immer explodieren?
            // Ja: wenn er noch nicht explodiert hat, Explosion auslösen.
            if (state != State.Exploded && state != State.Dead)
            {
                Explode();
            }
            else
            {
                Die();
            }
        }
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        state = State.Dead;
        ResetEmission();

        OnEnemyDied?.Invoke(this);

        enemyEffects?.PlayDeathEffectClientRpc(transform.position);
        enemyDropper?.HandleDeath();

        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn(true);
        }
    }
}
