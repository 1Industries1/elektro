using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[DisallowMultipleComponent]
public class WaspSpawner : NetworkBehaviour
{
    [Header("Wasp Drone Prefab")]
    [Tooltip("Prefab mit NetworkObject, WaspDroneEnemy, EnemyController, WaspLaserVisuals, etc.")]
    [SerializeField] private GameObject waspPrefab;

    [Header("Spawnpunkte")]
    [Tooltip("Hier deine Spawnpunkte im Level reinziehen (Empty GameObjects, Transforms etc.).")]
    [SerializeField] private List<Transform> spawnPoints = new();

    [Header("Spawn-Einstellungen")]
    [SerializeField] private int initialCountPerPoint = 1;

    [Tooltip("Radius um den Spawnpoint, in dem gespawnt wird (0 = exakt am Punkt).")]
    [SerializeField] private float spawnRadius = 0f;

    [SerializeField] private bool spawnOnStart = true;

    [Tooltip("Optional: statisches Primärziel für alle Wasp-Drohnen (z.B. deine Base).")]
    [SerializeField] private Transform staticPrimaryTarget;

    [Header("HP-Skalierung (optional)")]
    [Tooltip("Zusätzlicher HP-Multiplikator für alle gespawnten Wasp-Drohnen.")]
    [SerializeField] private float healthMultiplier = 1f;

    [Header("Debug / Info (readonly)")]
    public readonly NetworkVariable<int> WaspsAlive =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Mapping, um bei OnEnemyDied sauber unsubscriben zu können
    private readonly HashSet<IEnemy> _trackedEnemies = new();

    // ================== Lifecycle ==================

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        WaspsAlive.Value = 0;

        if (spawnOnStart)
        {
            Server_SpawnAllAtPoints();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        // Aufräumen
        foreach (var e in _trackedEnemies)
        {
            if (e != null)
                e.OnEnemyDied -= OnWaspDied;
        }
        _trackedEnemies.Clear();
    }

    // ================== Public API ==================

    /// <summary>
    /// Spawnt einmal alle Wasp-Drohnen an allen Spawnpunkten (Server-only).
    /// Kann z.B. von Wave-Logik oder Buttons aufgerufen werden.
    /// </summary>
    [ContextMenu("Server_SpawnAllAtPoints")]
    public void Server_SpawnAllAtPoints()
    {
        if (!IsServer) return;

        if (waspPrefab == null)
        {
            Debug.LogWarning("[WaspSpawner] Kein WaspPrefab zugewiesen.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[WaspSpawner] Keine SpawnPoints zugewiesen.");
            return;
        }

        foreach (var sp in spawnPoints)
        {
            if (sp == null) continue;

            for (int i = 0; i < Mathf.Max(1, initialCountPerPoint); i++)
            {
                Vector3 pos = sp.position;

                // kleiner Kreis um den Spawnpunkt
                if (spawnRadius > 0f)
                {
                    Vector2 offset2D = Random.insideUnitCircle * spawnRadius;
                    pos += new Vector3(offset2D.x, 0f, offset2D.y);
                }

                Quaternion rot = sp.rotation;
                SpawnSingleWasp(pos, rot);
            }
        }

    }

    /// <summary>
    /// Spawnt eine einzelne Wasp an einem bestimmten Punkt (Server-only).
    /// </summary>
    public void Server_SpawnSingleAt(Transform spawnPoint)
    {
        if (!IsServer || spawnPoint == null) return;

        Vector3 pos = spawnPoint.position;
        if (spawnRadius > 0f)
        {
            Vector2 offset2D = Random.insideUnitCircle * spawnRadius;
            pos += new Vector3(offset2D.x, 0f, offset2D.y);
        }

        SpawnSingleWasp(pos, spawnPoint.rotation);
    }


    // ================== Intern: tatsächliches Spawnen ==================

    private void SpawnSingleWasp(Vector3 position, Quaternion rotation)
    {
        if (!IsServer || waspPrefab == null) return;

        GameObject go = Instantiate(waspPrefab, position, rotation);

        if (!go.TryGetComponent<NetworkObject>(out var netObj))
        {
            Debug.LogError("[WaspSpawner] WaspPrefab hat kein NetworkObject!");
            Destroy(go);
            return;
        }

        if (!netObj.IsSpawned)
            netObj.Spawn();

        // Primärziel für EnemyController setzen (optional)
        if (staticPrimaryTarget != null && go.TryGetComponent(out EnemyController controller))
        {
            controller.SetPrimaryOverride(staticPrimaryTarget);
        }

        // HP-Skalierung & Kill-Callback
        if (go.TryGetComponent(out IEnemy enemy))
        {
            float baseHp = enemy.GetBaseHealth();
            float hpMul = Mathf.Max(0.01f, healthMultiplier);
            enemy.SetHealth(baseHp * hpMul);

            enemy.OnEnemyDied += OnWaspDied;
            _trackedEnemies.Add(enemy);

            WaspsAlive.Value = WaspsAlive.Value + 1;
        }
        else
        {
            Debug.LogWarning("[WaspSpawner] WaspPrefab implementiert IEnemy nicht – keine HP-Skalierung / Alive-Tracking.");
        }
    }

    private void OnWaspDied(IEnemy enemy)
    {
        if (!IsServer || enemy == null) return;

        enemy.OnEnemyDied -= OnWaspDied;
        _trackedEnemies.Remove(enemy);

        WaspsAlive.Value = Mathf.Max(0, WaspsAlive.Value - 1);
    }

    // ================== Gizmos ==================
    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
        foreach (var sp in spawnPoints)
        {
            if (sp == null) continue;

            Gizmos.DrawSphere(sp.position, 0.3f);
            Gizmos.DrawWireSphere(sp.position, 0.6f);
        }
    }
}
