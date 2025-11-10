using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class EnemySpawner : NetworkBehaviour
{
    // ================== Zielauswahl ==================
    public enum AttackTargetMode { NearestPlayer, RoundRobin, StaticTransform }

    [Header("Zielauswahl")]
    public AttackTargetMode targetMode = AttackTargetMode.NearestPlayer;
    [Tooltip("Nur genutzt, wenn targetMode = StaticTransform.")]
    public Transform staticTarget;

    // ================== Prefabs & Spawn-Ring ==================
    [Header("Gegner-Prefabs – Baseline Horde")]
    public List<GameObject> enemyPrefabs = new();

    [Header("Spawn-Ring um das Ziel (Baseline)")]
    public float spawnRingMin = 18f;
    public float spawnRingMax = 25f;

    // ================== Burst-Packs ==================
    [Header("Burst-Packs – Kurzreichweiten-Gegner")]
    public List<GameObject> burstEnemyPrefabs = new();
    public Vector2Int burstCountRange = new Vector2Int(4, 8);
    [Tooltip("Zusätzlicher Anstieg der Burst-Anzahl pro Welle (Min/Max je Welle).")]
    public Vector2Int burstCountIncreasePerWave = new Vector2Int(1, 1);
    public Vector2 burstCooldownRangeSeconds = new Vector2(6f, 12f);
    public float burstRingMin = 8f;
    public float burstRingMax = 14f;
    public float burstClusterRadius = 3f;

    // ================== Elite-Injektion ==================
    [Header("Elite-Gegner – Injektion")] 
    public List<GameObject> elitePrefabs = new();
    public int eliteStartWave = 3;
    [Range(0f, 1f)] public float eliteChancePerWave = 0.35f;
    public Vector2Int eliteCountRange = new Vector2Int(1, 2);
    public Vector2 eliteInjectionDelaySeconds = new Vector2(8f, 18f);
    public float eliteHealthMultiplier = 2.0f;
    public float eliteRingMin = 0f;
    public float eliteRingMax = 0f;

    // ================== Boden-Snap ==================
    [Header("Boden-Snap (Raycast nach unten)")]
    public bool useGroundSnap = true;
    public LayerMask groundLayerMask = ~0;
    public float groundSnapUpOffset = 2f;
    public float groundSnapDownDistance = 10f;
    public float groundSnapYOffset = 0.05f;

    // ================== Horde Loop & Rate ==================
    [Header("Horde-Loop Timing")]
    public float spawnTickSeconds = 0.75f;
    public float initialDelaySeconds = 2f;

    [Header("Rate-basiertes Spawning (Baseline)")]
    public float spawnsPerMinuteStart = 40f;
    public float spawnsPerMinutePerMinute = 10f;
    public int maxBurstPerTick = 8;

    [Header("HP-Skalierung über Zeit")]
    public float enemyHealthPerMinuteMultiplier = 0.15f;

    [Header("Maximal gleichzeitig aktive Gegner (Cap)")]
    public int concurrentEnemiesCap = 800;

    // ================== Waves ==================
    [Header("Wellenphasen")]
    public float waveActiveSeconds = 60f;
    public float waveBreakSeconds = 20f;
    public bool waitForClearBeforeBreak = false;

    // ================== Netzwerk-Status (für UI) ==================
    [Header("Netzwerk-Status (readonly)")]
    public readonly NetworkVariable<int> CurrentWave =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<bool> IsWaveActive =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<int> EnemiesAlive =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<double> NextHordeTickServerTime =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<double> CurrentPhaseEndsServerTime =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ================== Runtime ==================
    private double _serverStartTime;
    private int _roundRobinIdx = 0;
    private readonly List<Transform> _tempPlayers = new();
    private float _spawnBudget = 0f;
    private bool _inBreakPhase = false;
    private Coroutine _burstRoutine;
    private Coroutine _eliteRoutine;

    // ------------------ Small utilities ------------------
    private double ServerTime => NetworkManager.LocalTime.Time;
    private float MinutesSinceStart => Mathf.Max(0f, (float)((ServerTime - _serverStartTime) / 60.0));

    private struct Ring
    {
        public float min, max;
        public Ring(float min, float max) { this.min = Mathf.Max(0.01f, min); this.max = Mathf.Max(this.min + 0.01f, max); }
        public Vector3 RandomPoint(Vector3 center)
        {
            float r = Random.Range(min, max);
            float a = Random.Range(0f, Mathf.PI * 2f);
            return center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }
    }

    private void RestartRoutine(ref Coroutine handle, IEnumerator routine)
    {
        if (handle != null) StopCoroutine(handle);
        handle = StartCoroutine(routine);
    }
    private void StopRoutine(ref Coroutine handle)
    {
        if (handle != null) { StopCoroutine(handle); handle = null; }
    }

    private Vector3 SnapIfNeeded(Vector3 p)
    {
        if (!useGroundSnap) return p;
        Vector3 origin = p + Vector3.up * groundSnapUpOffset;
        float dist = groundSnapUpOffset + groundSnapDownDistance;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, dist, groundLayerMask, QueryTriggerInteraction.Ignore))
            return new Vector3(p.x, hit.point.y + groundSnapYOffset, p.z);
        return p;
    }

    private GameObject PickWaveIndexed(List<GameObject> list)
        => (list == null || list.Count == 0) ? null : list[Mathf.Clamp(CurrentWave.Value - 1, 0, list.Count - 1)];

    // ================== Lifecycle ==================
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        CurrentWave.Value = 0;
        IsWaveActive.Value = false;
        EnemiesAlive.Value = 0;

        if ((enemyPrefabs == null || enemyPrefabs.Count == 0) &&
            (burstEnemyPrefabs == null || burstEnemyPrefabs.Count == 0) &&
            (elitePrefabs == null || elitePrefabs.Count == 0))
        {
            Debug.LogWarning("[EnemySpawner] Keine Prefabs zugewiesen – Spawner bleibt inaktiv.");
            return;
        }

        _serverStartTime = ServerTime;
        NextHordeTickServerTime.Value = ServerTime + Mathf.Max(0.01f, initialDelaySeconds);

        StartCoroutine(WaveLoop());
        StartCoroutine(HordeLoop());
    }

    // ================== Wave-Phasensteuerung ==================
    private IEnumerator WaveLoop()
    {
        if (initialDelaySeconds > 0f) yield return new WaitForSeconds(initialDelaySeconds);

        while (true)
        {
            // Aktiv
            _inBreakPhase = false;
            IsWaveActive.Value = true;
            CurrentWave.Value = Mathf.Max(1, CurrentWave.Value + 1);

            double activeEnd = ServerTime + Mathf.Max(1f, waveActiveSeconds);
            CurrentPhaseEndsServerTime.Value = activeEnd;

            RestartRoutine(ref _burstRoutine, BurstLoop());
            RestartRoutine(ref _eliteRoutine, EliteLoopForCurrentWave());

            while (ServerTime < activeEnd) yield return null;

            if (waitForClearBeforeBreak)
            {
                float timeout = 10f, t = 0f;
                while (EnemiesAlive.Value > 0 && t < timeout) { t += Time.deltaTime; yield return null; }
            }

            // Pause
            StopRoutine(ref _burstRoutine);
            StopRoutine(ref _eliteRoutine);

            _inBreakPhase = true;
            IsWaveActive.Value = false;
            _spawnBudget = 0f;

            double breakEnd = ServerTime + Mathf.Max(1f, waveBreakSeconds);
            CurrentPhaseEndsServerTime.Value = breakEnd;
            while (ServerTime < breakEnd) yield return null;
        }
    }

    // ================== Burst-Packs ==================
    private IEnumerator BurstLoop()
    {
        var burstRing = new Ring(
            Mathf.Approximately(burstRingMin, 0f) ? spawnRingMin : burstRingMin,
            Mathf.Approximately(burstRingMax, 0f) ? spawnRingMax : burstRingMax);

        while (IsWaveActive.Value && !_inBreakPhase)
        {
            float cd = Random.Range(burstCooldownRangeSeconds.x, burstCooldownRangeSeconds.y);
            yield return new WaitForSeconds(Mathf.Max(0.2f, cd));
            if (!IsServer || _inBreakPhase || !IsWaveActive.Value) yield break;

            Transform target = ResolveAttackTarget();
            if (!target) continue;

            // Burst-Menge wächst pro Welle
            int waveIdx = Mathf.Max(0, CurrentWave.Value - 1);
            int minCount = Mathf.Max(1, burstCountRange.x + burstCountIncreasePerWave.x * waveIdx);
            int maxCount = Mathf.Max(minCount, burstCountRange.y + burstCountIncreasePerWave.y * waveIdx);
            int count = Random.Range(minCount, maxCount + 1);

            // Cluster-Spawns
            Vector3 center = burstRing.RandomPoint(target.position);
            SpawnGroup(PickWaveIndexed(burstEnemyPrefabs), count, target, center, burstRing, clusterRadius: burstClusterRadius, isElite: false, eliteMul: 1f);
        }
    }

    // ================== Elite – Steuerung ==================

    private GameObject PickRandom(List<GameObject> list)
    {
        if (list == null || list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    private IEnumerator EliteLoopForCurrentWave()
    {
        if (CurrentWave.Value >= eliteStartWave && Random.value <= eliteChancePerWave)
        {
            float delay = Random.Range(eliteInjectionDelaySeconds.x, eliteInjectionDelaySeconds.y);
            yield return new WaitForSeconds(Mathf.Max(0.1f, delay));
            if (!IsServer || _inBreakPhase || !IsWaveActive.Value) yield break;

            var ring = new Ring(eliteRingMin > 0f ? eliteRingMin : spawnRingMin,
                                eliteRingMax > 0f ? eliteRingMax : spawnRingMax);

            Transform target = ResolveAttackTarget();
            if (target)
            {
                int count = Mathf.Max(1, Random.Range(eliteCountRange.x, eliteCountRange.y + 1));

                // HIER: statt PickWaveIndexed → zufälliger Elite
                var elitePrefab = PickRandom(elitePrefabs);

                SpawnGroup(elitePrefab, count, target, ring.RandomPoint(target.position), ring,
                        clusterRadius: 0f, isElite: true, eliteMul: eliteHealthMultiplier);
            }
        }
    }


    // Öffentliche API
    public void InjectEliteNow(int count = 1, Transform preferredTarget = null)
    {
        if (!IsServer || elitePrefabs == null || elitePrefabs.Count == 0) return;

        Transform target = preferredTarget ? preferredTarget : ResolveAttackTarget();
        if (!target) return;

        var ring = new Ring(eliteRingMin > 0f ? eliteRingMin : spawnRingMin,
                            eliteRingMax > 0f ? eliteRingMax : spawnRingMax);

        var elitePrefab = PickRandom(elitePrefabs);

        SpawnGroup(elitePrefab, Mathf.Max(1, count), target, ring.RandomPoint(target.position), ring,
                clusterRadius: 0f, isElite: true, eliteMul: eliteHealthMultiplier);
    }


    // ================== Horde Loop (Baseline) ==================
    private IEnumerator HordeLoop()
    {
        if (initialDelaySeconds > 0f) yield return new WaitForSeconds(initialDelaySeconds);

        var wait = new WaitForSeconds(Mathf.Max(0.05f, spawnTickSeconds));
        var baseRing = new Ring(spawnRingMin, spawnRingMax);

        while (true)
        {
            NextHordeTickServerTime.Value = ServerTime + spawnTickSeconds;

            if (_inBreakPhase || EnemiesAlive.Value >= concurrentEnemiesCap)
            {
                yield return wait; 
                continue;
            }

            Transform target = ResolveAttackTarget();
            if (!target)
            {
                yield return wait; 
                continue;
            }

            // Rate-budget
            float minutes = MinutesSinceStart;
            float targetSpawnsPerMinute = Mathf.Max(0f, spawnsPerMinuteStart + spawnsPerMinutePerMinute * minutes);
            _spawnBudget = Mathf.Min(_spawnBudget + targetSpawnsPerMinute * (spawnTickSeconds / 60f), Mathf.Max(1, maxBurstPerTick));

            int toSpawn = Mathf.Min(Mathf.FloorToInt(_spawnBudget), Mathf.Max(0, concurrentEnemiesCap - EnemiesAlive.Value));
            if (toSpawn > 0)
            {
                var prefab = PickWaveIndexed(enemyPrefabs);
                // Pro Gegner das Ziel ggf. neu auflösen für weniger Clumping
                for (int i = 0; i < toSpawn; i++)
                {
                    Transform thisTarget = (targetMode == AttackTargetMode.NearestPlayer)
                        ? ResolveNearestPlayer(target.position) ?? target
                        : target;

                    Vector3 pos = baseRing.RandomPoint(thisTarget.position);
                    pos = SnapIfNeeded(pos);
                    SpawnEnemy(prefab, pos, thisTarget, isElite: false, eliteMul: 1f);
                }
                _spawnBudget -= toSpawn;
            }

            yield return wait;
        }
    }

    // ================== Zielauflösung ==================
    private Transform ResolveAttackTarget()
    {
        return targetMode switch
        {
            AttackTargetMode.StaticTransform => staticTarget ? staticTarget : ResolveAnyAlivePlayer(),
            AttackTargetMode.RoundRobin     => ResolveRoundRobinPlayer() ?? ResolveAnyAlivePlayer(),
            _                               => ResolveNearestPlayer(transform.position) ?? ResolveAnyAlivePlayer(),
        };
    }

    private Transform ResolveAnyAlivePlayer()
    {
        foreach (var kv in NetworkManager.Singleton.ConnectedClients)
        {
            var po = kv.Value?.PlayerObject;
            if (!po) continue;
            var health = po.GetComponentInChildren<PlayerHealth>();
            if (health && health.IsDead()) continue;
            return po.transform;
        }
        return staticTarget;
    }

    private Transform ResolveNearestPlayer(Vector3 reference)
    {
        Transform best = null; float bestSqr = float.MaxValue;
        foreach (var kv in NetworkManager.Singleton.ConnectedClients)
        {
            var po = kv.Value?.PlayerObject; if (!po) continue;
            var health = po.GetComponentInChildren<PlayerHealth>(); if (health && health.IsDead()) continue;
            var t = po.transform; float d2 = (t.position - reference).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; best = t; }
        }
        return best;
    }

    private Transform ResolveRoundRobinPlayer()
    {
        _tempPlayers.Clear();
        foreach (var kv in NetworkManager.Singleton.ConnectedClients)
        {
            var po = kv.Value?.PlayerObject; if (!po) continue;
            var health = po.GetComponentInChildren<PlayerHealth>(); if (health && health.IsDead()) continue;
            _tempPlayers.Add(po.transform);
        }
        if (_tempPlayers.Count == 0) return null;
        if (_roundRobinIdx >= _tempPlayers.Count) _roundRobinIdx = 0;
        var t = _tempPlayers[_roundRobinIdx];
        _roundRobinIdx = (_roundRobinIdx + 1) % _tempPlayers.Count;
        return t;
    }

    // ================== Einheitliche Spawn-Utilities ==================
    private void SpawnGroup(GameObject prefab, int count, Transform target, Vector3 center, Ring ring,
                            float clusterRadius, bool isElite, float eliteMul)
    {
        if (!prefab) return;

        for (int i = 0; i < count; i++)
        {
            if (EnemiesAlive.Value >= concurrentEnemiesCap) break;

            Vector3 pos = (clusterRadius > 0f)
                ? SnapIfNeeded(center + new Vector3(Random.insideUnitSphere.x, 0f, Random.insideUnitSphere.z) * clusterRadius)
                : SnapIfNeeded(ring.RandomPoint(target.position));

            SpawnEnemy(prefab, pos, target, isElite, eliteMul);
        }
    }

    private void SpawnEnemy(GameObject prefab, Vector3 pos, Transform target, bool isElite, float eliteMul)
    {
        if (!IsServer || prefab == null) return;

        GameObject enemy = Instantiate(prefab, pos, Quaternion.identity);
        var netObj = enemy.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned) netObj.Spawn();

        enemy.GetComponent<EnemyController>()?.SetPrimaryOverride(target);
        enemy.GetComponent<EnemySkirmisher>()?.ConfigureForBaseAttack();

        var e = enemy.GetComponent<IEnemy>();
        if (e != null)
        {
            float baseHp = e.GetBaseHealth();
            float hpMultiplier = (1f + enemyHealthPerMinuteMultiplier * MinutesSinceStart) * Mathf.Max(1f, eliteMul);
            e.SetHealth(baseHp * hpMultiplier);
            e.OnEnemyDied += OnEnemyKilled_Horde;
        }

        EnemiesAlive.Value = EnemiesAlive.Value + 1;
    }

    private void OnEnemyKilled_Horde(IEnemy enemy)
    {
        enemy.OnEnemyDied -= OnEnemyKilled_Horde;

        ulong killerId = enemy.LastHitByClientId;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(killerId, out var client))
        {
            var stats = client.PlayerObject?.GetComponentInChildren<PlayerStats>();
            if (stats) stats.AddKill();
        }

        EnemiesAlive.Value = Mathf.Max(0, EnemiesAlive.Value - 1);
    }

    // ================== Gizmos ==================
    private void OnDrawGizmosSelected()
    {
        if (targetMode != AttackTargetMode.StaticTransform || !staticTarget) return;

        void DrawRing(Color c, float r) { Gizmos.color = c; Gizmos.DrawWireSphere(staticTarget.position, r); }
        DrawRing(new Color(0f, 0.6f, 1f, 0.4f), spawnRingMin);
        DrawRing(new Color(0f, 0.6f, 1f, 0.4f), spawnRingMax);

        DrawRing(new Color(1f, 0.6f, 0f, 0.35f), burstRingMin);
        DrawRing(new Color(1f, 0.6f, 0f, 0.35f), burstRingMax);

        if (eliteRingMin > 0f && eliteRingMax > 0f)
        {
            DrawRing(new Color(0.8f, 0f, 0.8f, 0.35f), eliteRingMin);
            DrawRing(new Color(0.8f, 0f, 0.8f, 0.35f), eliteRingMax);
        }
    }
}
