using Unity.Netcode;
using UnityEngine;

public class OrbitOrb : NetworkBehaviour
{
    [Header("Orbit")]
    private Transform _center;
    private float _radius;
    private float _angleDeg;
    private float _angularSpeedDeg;

    [Header("Damage Runtime")]
    private float _baseDamagePerTick;
    private float _tickInterval = 0.25f;
    private float _nextTickTime;
    private ulong _ownerClientId;

    [Header("Damage Settings (Ring)")]
    public float damageRadius = 0.7f;
    public LayerMask enemyLayer;

    [Header("Inner Core (Player Aura)")]
    [Tooltip("Aktiviere inneren Kern-Schaden um den Spieler.")]
    public bool enableInnerCore = true;

    [Tooltip("Radius des inneren Kerns = Orbit-Radius * Faktor.")]
    public float innerCoreRadiusFactor = 0.5f;

    [Tooltip("Multiplikator für den Schaden im inneren Kern relativ zum Orb-Tick-Schaden.")]
    public float innerCoreDamageMul = 0.6f;

    private float _innerCoreRadius;
    private bool _isCoreDealer; // nur ein Orb macht den Kern-Schaden

    [Header("Ground Alignment (Terrain)")]
    [Tooltip("Abstand über Terrain-Oberfläche.")]
    public float groundOffset = 1.2f;

    [Tooltip("Mindestens so viel über dem Spieler-Basispunkt.")]
    public float minOffsetFromPlayer = 0.5f;

    private PlayerMovement _ownerMovement;

    // Wird NUR auf dem Server direkt nach Spawn aufgerufen
    public void ServerInit(
        Transform ownerCenter,
        float radius,
        float startAngleDeg,
        float angularSpeedDeg,
        float baseDamagePerTick,
        float tickInterval,
        ulong ownerClientId,
        int orbIndex,
        int orbCount)
    {
        if (!IsServer) return;

        _center = ownerCenter;
        _radius = radius;
        _angleDeg = startAngleDeg;
        _angularSpeedDeg = angularSpeedDeg;
        _baseDamagePerTick = baseDamagePerTick;
        _tickInterval = tickInterval;
        _ownerClientId = ownerClientId;

        _innerCoreRadius = Mathf.Max(0.1f, radius * innerCoreRadiusFactor);
        _isCoreDealer = (orbIndex == 0); // nur Orb #0 macht den inneren Kern

        if (_center != null)
            _ownerMovement = _center.GetComponentInParent<PlayerMovement>();
    }

    private void Update()
    {
        // Bewegung & Schaden nur auf dem Server, Clients bekommen das via NetworkTransform
        if (!IsServer) return;

        if (_center == null)
        {
            if (IsSpawned)
                NetworkObject.Despawn(true);
            return;
        }

        // 1) Orbit-Bewegung in XZ
        _angleDeg += _angularSpeedDeg * Time.deltaTime;
        float rad = _angleDeg * Mathf.Deg2Rad;

        Vector3 flatOffset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * _radius;
        Vector3 targetPos = _center.position + flatOffset;

        // 2) Terrain-Höhe bestimmen (Variante A)
        float groundY = targetPos.y;

        if (Terrain.activeTerrain != null)
        {
            float terrainHeight = Terrain.activeTerrain.SampleHeight(targetPos)
                                  + Terrain.activeTerrain.transform.position.y;

            groundY = terrainHeight;
        }

        // 3) Y-Position: immer über Terrain und nicht unterhalb des Spielers
        float playerBaseY = _center.position.y;
        float desiredY = Mathf.Max(
            groundY + groundOffset,
            playerBaseY + minOffsetFromPlayer
        );

        targetPos.y = desiredY;

        // 4) finale Position setzen
        transform.position = targetPos;

        // 5) Schaden-Ticks
        if (Time.time >= _nextTickTime)
        {
            _nextTickTime = Time.time + _tickInterval;
            DoDamageTick();      // äußerer Ring (Donut)
            DoInnerCoreDamage(); // innerer Kern (Aura um den Spieler, nur von einem Orb)
        }
    }

    private float GetRollMultiplier()
    {
        if (_ownerMovement == null) return 1f;
        // Beispiel: während Rollen etwas schwächer, sonst etwas stärker
        return _ownerMovement.ServerRollHeld ? 0.7f : 1.2f;
    }

    private void DoDamageTick()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            damageRadius,
            enemyLayer,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0) return;

        float rollMul = GetRollMultiplier();
        float finalDmg = _baseDamagePerTick * rollMul;

        foreach (var h in hits)
        {
            var enemy = h.GetComponentInParent<IEnemy>();
            if (enemy == null) continue;

            Vector3 hitPoint = h.bounds.center;
            enemy.TakeDamage(finalDmg, _ownerClientId, hitPoint);
        }
    }

    private void DoInnerCoreDamage()
    {
        // Nur ein Orb übernimmt die Kern-Aura
        if (!enableInnerCore || !_isCoreDealer || _center == null) return;

        float coreRadius = _innerCoreRadius;
        if (coreRadius <= 0f) return;

        Collider[] hits = Physics.OverlapSphere(
            _center.position,
            coreRadius,
            enemyLayer,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0) return;

        float rollMul = GetRollMultiplier();
        float finalDmg = _baseDamagePerTick * innerCoreDamageMul * rollMul;

        foreach (var h in hits)
        {
            var enemy = h.GetComponentInParent<IEnemy>();
            if (enemy == null) continue;

            Vector3 hitPoint = h.bounds.center;
            enemy.TakeDamage(finalDmg, _ownerClientId, hitPoint);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, damageRadius);

        if (_center != null && enableInnerCore)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_center.position, _innerCoreRadius);
        }
    }
}
