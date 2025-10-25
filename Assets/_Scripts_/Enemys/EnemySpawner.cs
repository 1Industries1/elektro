using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Horde-Spawner (Vampire-Survivors-like), rate-basiert + Wellenphasen + Spezial-Bursts + Elite-Injektion
/// - Steuerung über "Spawns pro Minute" + Zunahme/Minute (Baseline-Horde während Aktiv-Phase)
/// - 20s (konfigurierbare) Pausen zwischen Wellen
/// - "Burst-Packs" für Nahkampf-/Kurzreichweiten-Gegner: mehrere auf einmal, näher am Ziel
/// - Elite-Gegner gezielt injektierbar (planbar pro Welle, per Chance oder per API)
/// - UI-kompatibel: CurrentWave, IsWaveActive, EnemiesAlive, CurrentPhaseEndsServerTime
/// - HP-Skalierung über verstrichene Zeit (+ optionaler Elite-Multiplikator)
/// </summary>
public class EnemySpawner : NetworkBehaviour
{
    // ================== Zielauswahl ==================
    public enum AttackTargetMode { NearestPlayer, RoundRobin, StaticTransform }

    [Header("Zielauswahl")]
    [Tooltip("Wie wird das Ziel für die Horde-Gegner bestimmt?")]
    public AttackTargetMode targetMode = AttackTargetMode.NearestPlayer;

    [Tooltip("Nur genutzt, wenn targetMode = StaticTransform.")]
    public Transform staticTarget;


    // ================== Prefabs & Spawn-Ring ==================
    [Header("Gegner-Prefabs – Baseline Horde")]
    [Tooltip("Liste der möglichen Gegner, die als kontinuierliche Horde gespawnt werden.")]
    public List<GameObject> enemyPrefabs = new();

    [Header("Spawn-Ring um das Ziel (Baseline)")]
    [Tooltip("Mindestabstand vom Ziel, an dem gespawnt wird (Offscreen).")]
    public float spawnRingMin = 18f;

    [Tooltip("Maximaler Abstand vom Ziel, an dem gespawnt wird.")]
    public float spawnRingMax = 25f;


    // ================== Burst-Packs (Nahkampf u. Kurzreichweite) ==================
    [Header("Burst-Packs – Kurzreichweiten-Gegner")]
    [Tooltip("Diese Gegner werden in Paketen während der Aktiv-Phase gespawnt (z. B. Nahkämpfer / können nicht schießen).")]
    public List<GameObject> burstEnemyPrefabs = new();

    [Tooltip("Anzahl pro Burst (inkl. Zufall).")]
    public Vector2Int burstCountRange = new Vector2Int(4, 8);

    [Tooltip("Cooldown zwischen Bursts (Zufall, nur während der Aktiv-Phase).")]
    public Vector2 burstCooldownRangeSeconds = new Vector2(6f, 12f);

    [Tooltip("Eigener (enger) Spawn-Ring für Burst-Packs – spawnt näher am Ziel.")]
    public float burstRingMin = 8f;
    public float burstRingMax = 14f;

    [Tooltip("Wie eng die Burst-Einheiten zusammenliegen (Radius in Metern um den Burst-Mittelpunkt).")]
    public float burstClusterRadius = 3f;


    // ================== Elite-Injektion ==================
    [Header("Elite-Gegner – Injektion")] 
    [Tooltip("Liste möglicher Elite-Prefabs (können auch verstärkte Varianten sein).")]
    public List<GameObject> elitePrefabs = new();

    [Tooltip("Ab welcher Welle können Elites erscheinen.")]
    public int eliteStartWave = 3;

    [Tooltip("Chance pro Welle, dass eine Elite-Injektion stattfindet (0..1).")]
    [Range(0f, 1f)] public float eliteChancePerWave = 0.35f;

    [Tooltip("Anzahl der Elites pro Injektion.")]
    public Vector2Int eliteCountRange = new Vector2Int(1, 2);

    [Tooltip("Verzögerung innerhalb der Aktiv-Phase bis zur Elite-Injektion (Sekunden).")]
    public Vector2 eliteInjectionDelaySeconds = new Vector2(8f, 18f);

    [Tooltip("Zusätzlicher HP-Multiplikator für Elite-Gegner (on top der Zeit-Skalierung).")]
    public float eliteHealthMultiplier = 2.0f;

    [Tooltip("Separater Spawn-Ring für Elites. Falls 0, wird Baseline-Ring genutzt.")]
    public float eliteRingMin = 0f;
    public float eliteRingMax = 0f;


    // ================== Boden-Snap ==================
    [Header("Boden-Snap (Raycast nach unten)")]
    public bool useGroundSnap = true;
    public LayerMask groundLayerMask = ~0;
    public float groundSnapUpOffset = 2f;
    public float groundSnapDownDistance = 10f;
    public float groundSnapYOffset = 0.05f;


    // ================== Horde Loop & Rate-basierte Steuerung ==================
    [Header("Horde-Loop Timing")]
    [Tooltip("Sekunden zwischen Spawner-Ticks.")]
    public float spawnTickSeconds = 0.75f;

    [Tooltip("Verzögerung bis der Spawner nach Spielstart loslegt.")]
    public float initialDelaySeconds = 2f;

    [Header("Rate-basiertes Spawning (Baseline)")]
    [Tooltip("Ziel-Spawnrate zu Beginn (Gegner pro Minute).")]
    public float spawnsPerMinuteStart = 40f;

    [Tooltip("Lineare Zunahme der Spawnrate pro verstrichener Minute (Gegner/Minute^2).")]
    public float spawnsPerMinutePerMinute = 10f;

    [Tooltip("Maximal erlaubter Burst pro Tick (Budget-Kappung, schützt vor Spikes).")]
    public int maxBurstPerTick = 8;

    [Header("HP-Skalierung über Zeit")]
    [Tooltip("Zusätzlicher HP-Multiplikator pro Minute (0.15 = +15%/min linear).")]
    public float enemyHealthPerMinuteMultiplier = 0.15f;

    [Header("Maximal gleichzeitig aktive Gegner (Cap)")]
    [Tooltip("Harter Cap für ALLE aktiven Horde-Gegner (Performance-Schutz).")]
    public int concurrentEnemiesCap = 800;


    // ================== Waves ==================
    [Header("Wellenphasen")]
    [Tooltip("Dauer der Aktiv-Phase einer Welle in Sekunden (Spawns erlaubt).")]
    public float waveActiveSeconds = 60f;

    [Tooltip("Dauer der Pause zwischen Wellen in Sekunden (kein Spawning).")]
    public float waveBreakSeconds = 20f; // kurze Erholungsphase

    [Tooltip("Wenn true, beginnt die Pause erst, nachdem die Aktiv-Phase abgelaufen ist UND alle Gegner tot sind.")]
    public bool waitForClearBeforeBreak = false;


    // ================== Netzwerk-Status (für UI) ==================
    [Header("Netzwerk-Status (readonly)")]
    public readonly NetworkVariable<int> CurrentWave =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<bool> IsWaveActive =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<int> EnemiesAlive =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Tooltip("Nächster geplanter Spawner-Tick (Serverzeit) – für Client-UI/Countdowns.")]
    public readonly NetworkVariable<double> NextHordeTickServerTime =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Tooltip("Zeitpunkt (Serverzeit), zu dem die aktuelle Phase (Aktiv oder Pause) endet – für UI.")]
    public readonly NetworkVariable<double> CurrentPhaseEndsServerTime =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


    // ================== Runtime ==================
    private double _serverStartTime;           // Server-Startzeit des Spawners
    private int _roundRobinIdx = 0;            // für RoundRobin-Targeting
    private readonly List<Transform> _tempPlayers = new();

    // Rate-Accumulator (Budget), wird pro Tick befüllt:
    private float _spawnBudget = 0f;

    // Wellenstatus
    private bool _inBreakPhase = false;

    // Coroutines
    private Coroutine _burstRoutine;
    private Coroutine _eliteRoutine;


    // ================== Lifecycle ==================
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // UI-Init
        CurrentWave.Value = 0;    // wird beim ersten Wellenstart auf 1 gesetzt
        IsWaveActive.Value = false;
        EnemiesAlive.Value = 0;

        if ((enemyPrefabs == null || enemyPrefabs.Count == 0) &&
            (burstEnemyPrefabs == null || burstEnemyPrefabs.Count == 0) &&
            (elitePrefabs == null || elitePrefabs.Count == 0))
        {
            Debug.LogWarning("[EnemySpawner] Keine Prefabs zugewiesen – Spawner bleibt inaktiv.");
            return;
        }

        _serverStartTime = NetworkManager.LocalTime.Time;

        // Countdown für Clients initial bereitstellen:
        NextHordeTickServerTime.Value = NetworkManager.LocalTime.Time + Mathf.Max(0.01f, initialDelaySeconds);

        // Start
        StartCoroutine(WaveLoop());
        StartCoroutine(HordeLoop());
    }


    // ================== Wave-Phasensteuerung ==================
    private IEnumerator WaveLoop()
    {
        if (initialDelaySeconds > 0f)
            yield return new WaitForSeconds(initialDelaySeconds);

        while (true)
        {
            // ---- Aktiv-Phase ----
            _inBreakPhase = false;
            IsWaveActive.Value = true;
            CurrentWave.Value = Mathf.Max(1, CurrentWave.Value + 1);

            double activeEnd = NetworkManager.LocalTime.Time + Mathf.Max(1f, waveActiveSeconds);
            CurrentPhaseEndsServerTime.Value = activeEnd;

            // Nebenläufe (nur während Aktiv-Phase)
            StartBurstRoutine();
            StartEliteRoutine();

            // Warten bis Aktiv-Phase vorbei
            while (NetworkManager.LocalTime.Time < activeEnd)
                yield return null;

            if (waitForClearBeforeBreak)
            {
                float timeout = 10f; // Schutz gegen festhängende Gegner
                float t = 0f;
                while (EnemiesAlive.Value > 0 && t < timeout)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
            }

            // ---- Pause ----
            StopBurstRoutine();
            StopEliteRoutine();

            _inBreakPhase = true;
            IsWaveActive.Value = false;
            _spawnBudget = 0f; // Budget leeren, damit die nächste Welle "von null" startet

            double breakEnd = NetworkManager.LocalTime.Time + Mathf.Max(1f, waveBreakSeconds);
            CurrentPhaseEndsServerTime.Value = breakEnd;

            while (NetworkManager.LocalTime.Time < breakEnd)
                yield return null;
        }
    }


    // ================== Burst-Packs – Steuerung ==================
    private void StartBurstRoutine()
    {
        if (_burstRoutine != null) StopCoroutine(_burstRoutine);
        if (burstEnemyPrefabs == null || burstEnemyPrefabs.Count == 0) return;
        _burstRoutine = StartCoroutine(BurstLoop());
    }

    private void StopBurstRoutine()
    {
        if (_burstRoutine != null)
        {
            StopCoroutine(_burstRoutine);
            _burstRoutine = null;
        }
    }

    private IEnumerator BurstLoop()
    {
        while (IsWaveActive.Value && !_inBreakPhase)
        {
            float cd = Random.Range(burstCooldownRangeSeconds.x, burstCooldownRangeSeconds.y);
            yield return new WaitForSeconds(Mathf.Max(0.2f, cd));

            if (!IsServer || _inBreakPhase || !IsWaveActive.Value) yield break;

            // Ziel bestimmen
            Transform target = ResolveAttackTarget();
            if (target == null) continue;

            // Anzahl
            int count = Random.Range(burstCountRange.x, burstCountRange.y + 1);
            count = Mathf.Max(1, count);

            // Mittelpunkt nahe Ziel
            Vector3 center = GetPointInCustomRing(target.position, Mathf.Approximately(burstRingMin, 0f) ? spawnRingMin : burstRingMin,
                                                                       Mathf.Approximately(burstRingMax, 0f) ? spawnRingMax : burstRingMax);

            for (int i = 0; i < count; i++)
            {
                if (EnemiesAlive.Value >= concurrentEnemiesCap) break;

                Vector3 offset = Random.insideUnitSphere * burstClusterRadius; offset.y = 0f;
                Vector3 pos = MaybeSnapToGround(center + offset);

                var prefab = burstEnemyPrefabs[Random.Range(0, burstEnemyPrefabs.Count)];
                float minutes = Mathf.Max(0f, (float)((NetworkManager.LocalTime.Time - _serverStartTime) / 60.0));
                SpawnEnemyAtPosition(prefab, pos, target, minutes, isElite:false, eliteHpMul:1f);
            }
        }
    }


    // ================== Elite – Steuerung ==================
    private void StartEliteRoutine()
    {
        if (_eliteRoutine != null) StopCoroutine(_eliteRoutine);
        if (elitePrefabs == null || elitePrefabs.Count == 0) return;
        _eliteRoutine = StartCoroutine(EliteLoopForCurrentWave());
    }

    private void StopEliteRoutine()
    {
        if (_eliteRoutine != null)
        {
            StopCoroutine(_eliteRoutine);
            _eliteRoutine = null;
        }
    }

    private IEnumerator EliteLoopForCurrentWave()
    {
        // einfache Regel: ab eliteStartWave, einmalige Chance pro Aktiv-Phase
        if (CurrentWave.Value >= eliteStartWave && Random.value <= eliteChancePerWave)
        {
            float delay = Random.Range(eliteInjectionDelaySeconds.x, eliteInjectionDelaySeconds.y);
            yield return new WaitForSeconds(Mathf.Max(0.1f, delay));

            if (!IsServer || _inBreakPhase || !IsWaveActive.Value) yield break;

            Transform target = ResolveAttackTarget();
            if (target != null)
            {
                int count = Random.Range(eliteCountRange.x, eliteCountRange.y + 1);
                count = Mathf.Max(1, count);

                for (int i = 0; i < count; i++)
                {
                    if (EnemiesAlive.Value >= concurrentEnemiesCap) break;

                    Vector3 pos = GetPointInCustomRing(target.position,
                        eliteRingMin > 0f ? eliteRingMin : spawnRingMin,
                        eliteRingMax > 0f ? eliteRingMax : spawnRingMax);
                    pos = MaybeSnapToGround(pos);

                    var prefab = elitePrefabs[Random.Range(0, elitePrefabs.Count)];
                    float minutes = Mathf.Max(0f, (float)((NetworkManager.LocalTime.Time - _serverStartTime) / 60.0));
                    SpawnEnemyAtPosition(prefab, pos, target, minutes, isElite:true, eliteHpMul:eliteHealthMultiplier);
                }
            }
        }

        yield break; // pro Welle nur einmal prüfen
    }

    // Öffentliche API: Elite manuell injizieren (z. B. von einem Director)
    public void InjectEliteNow(int count = 1, Transform preferredTarget = null)
    {
        if (!IsServer || elitePrefabs == null || elitePrefabs.Count == 0) return;
        Transform target = preferredTarget ? preferredTarget : ResolveAttackTarget();
        if (!target) return;

        for (int i = 0; i < count; i++)
        {
            if (EnemiesAlive.Value >= concurrentEnemiesCap) break;
            Vector3 pos = GetPointInCustomRing(target.position,
                eliteRingMin > 0f ? eliteRingMin : spawnRingMin,
                eliteRingMax > 0f ? eliteRingMax : spawnRingMax);
            pos = MaybeSnapToGround(pos);

            var prefab = elitePrefabs[Random.Range(0, elitePrefabs.Count)];
            float minutes = Mathf.Max(0f, (float)((NetworkManager.LocalTime.Time - _serverStartTime) / 60.0));
            SpawnEnemyAtPosition(prefab, pos, target, minutes, isElite:true, eliteHpMul:eliteHealthMultiplier);
        }
    }


    // ================== Horde Loop (Baseline) ==================
    private IEnumerator HordeLoop()
    {
        if (initialDelaySeconds > 0f)
            yield return new WaitForSeconds(initialDelaySeconds);

        var wait = new WaitForSeconds(Mathf.Max(0.05f, spawnTickSeconds));

        while (true)
        {
            // Während der Pause wird nicht gespawnt, aber die NextHordeTickServerTime weiterhin angeboten
            if (_inBreakPhase)
            {
                NextHordeTickServerTime.Value = NetworkManager.LocalTime.Time + spawnTickSeconds;
                yield return wait;
                continue;
            }

            // Cap einhalten
            if (EnemiesAlive.Value >= concurrentEnemiesCap)
            {
                NextHordeTickServerTime.Value = NetworkManager.LocalTime.Time + spawnTickSeconds;
                yield return wait;
                continue;
            }

            // Ziel bestimmen (einmal pro Tick reicht; optional pro Gegner)
            Transform target = ResolveAttackTarget();
            if (target == null)
            {
                // Kein Ziel verfügbar -> pausieren (technische Pause)
                NextHordeTickServerTime.Value = NetworkManager.LocalTime.Time + spawnTickSeconds;
                yield return wait;
                continue;
            }

            // --- Rate-basiert ---
            // vergangene Zeit in Minuten
            float minutes = Mathf.Max(0f, (float)((NetworkManager.LocalTime.Time - _serverStartTime) / 60.0));

            // gewünschte Spawns/Minute (linear steigend)
            float targetSpawnsPerMinute = Mathf.Max(0f, spawnsPerMinuteStart + spawnsPerMinutePerMinute * minutes);

            // pro Tick Budget auffüllen (nur während Aktiv-Phase)
            _spawnBudget += targetSpawnsPerMinute * (spawnTickSeconds / 60f);

            // harte Kappung gegen Ausreißer/Bursts
            _spawnBudget = Mathf.Min(_spawnBudget, Mathf.Max(1, maxBurstPerTick));

            // ganzzahlige Spawns in diesem Tick
            int toSpawn = Mathf.FloorToInt(_spawnBudget);
            if (toSpawn <= 0)
            {
                NextHordeTickServerTime.Value = NetworkManager.LocalTime.Time + spawnTickSeconds;
                yield return wait;
                continue;
            }

            // Cap respektieren
            int capLeft = Mathf.Max(0, concurrentEnemiesCap - EnemiesAlive.Value);
            toSpawn = Mathf.Min(toSpawn, capLeft);
            if (toSpawn <= 0)
            {
                NextHordeTickServerTime.Value = NetworkManager.LocalTime.Time + spawnTickSeconds;
                yield return wait;
                continue;
            }

            // Spawnen
            for (int i = 0; i < toSpawn; i++)
            {
                // Optional: bei NearestPlayer pro Gegner neu auflösen (kleineres Clumping)
                Transform thisTarget =
                    (targetMode == AttackTargetMode.NearestPlayer) ? ResolveNearestPlayer(target.position) ?? target : target;

                Vector3 pos = GetPointInRingAround(thisTarget.position);
                pos = MaybeSnapToGround(pos);

                var prefab = enemyPrefabs != null && enemyPrefabs.Count > 0
                    ? enemyPrefabs[Random.Range(0, enemyPrefabs.Count)]
                    : null;

                if (prefab != null)
                {
                    SpawnEnemyAtPosition(prefab, pos, thisTarget, minutes, isElite:false, eliteHpMul:1f);
                }
            }

            // Budget reduzieren um die tatsächlich gespawnten
            _spawnBudget -= toSpawn;

            // nächste Tickzeit publizieren
            NextHordeTickServerTime.Value = NetworkManager.LocalTime.Time + spawnTickSeconds;

            yield return wait;
        }
    }


    // ================== Zielauflösung ==================
    private Transform ResolveAttackTarget()
    {
        switch (targetMode)
        {
            case AttackTargetMode.StaticTransform:
                return staticTarget ? staticTarget : ResolveAnyAlivePlayer();

            case AttackTargetMode.RoundRobin:
                return ResolveRoundRobinPlayer() ?? ResolveAnyAlivePlayer();

            case AttackTargetMode.NearestPlayer:
            default:
                return ResolveNearestPlayer(transform.position) ?? ResolveAnyAlivePlayer();
        }
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
        return staticTarget; // Fallback (kann null sein)
    }

    private Transform ResolveNearestPlayer(Vector3 reference)
    {
        Transform best = null;
        float bestSqr = float.MaxValue;

        foreach (var kv in NetworkManager.Singleton.ConnectedClients)
        {
            var po = kv.Value?.PlayerObject;
            if (!po) continue;

            var health = po.GetComponentInChildren<PlayerHealth>();
            if (health && health.IsDead()) continue;

            var t = po.transform;
            float d2 = (t.position - reference).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; best = t; }
        }
        return best;
    }

    private Transform ResolveRoundRobinPlayer()
    {
        _tempPlayers.Clear();
        foreach (var kv in NetworkManager.Singleton.ConnectedClients)
        {
            var po = kv.Value?.PlayerObject;
            if (!po) continue;

            var health = po.GetComponentInChildren<PlayerHealth>();
            if (health && health.IsDead()) continue;

            _tempPlayers.Add(po.transform);
        }

        if (_tempPlayers.Count == 0) return null;

        if (_roundRobinIdx >= _tempPlayers.Count) _roundRobinIdx = 0;
        var t = _tempPlayers[_roundRobinIdx];
        _roundRobinIdx = (_roundRobinIdx + 1) % _tempPlayers.Count;
        return t;
    }


    // ================== Spawn-Utilities ==================
    private Vector3 GetPointInRingAround(Vector3 center)
    {
        return GetPointInCustomRing(center, spawnRingMin, spawnRingMax);
    }

    private Vector3 GetPointInCustomRing(Vector3 center, float rMinIn, float rMaxIn)
    {
        float rMin = Mathf.Max(0.01f, rMinIn);
        float rMax = Mathf.Max(rMin + 0.01f, rMaxIn);
        float r = Random.Range(rMin, rMax);
        float a = Random.Range(0f, Mathf.PI * 2f);
        return center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
    }

    private Vector3 MaybeSnapToGround(Vector3 p)
    {
        if (!useGroundSnap) return p;

        Vector3 origin = p + Vector3.up * groundSnapUpOffset;
        float dist = groundSnapUpOffset + groundSnapDownDistance;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, dist, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            return new Vector3(p.x, hit.point.y + groundSnapYOffset, p.z);
        }
        return p;
    }


    // ================== Spawn + Setup Gegner ==================
    private void SpawnEnemyAtPosition(GameObject enemyPrefab, Vector3 pos, Transform target, float minutesSinceStart, bool isElite, float eliteHpMul)
    {
        if (!IsServer || enemyPrefab == null) return;

        GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity);
        var netObj = enemy.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned) netObj.Spawn();

        // Ziel setzen -> Angriffsmodus (kein Roam)
        enemy.GetComponent<EnemyController>()?.SetPrimaryOverride(target);
        enemy.GetComponent<EnemySkirmisher>()?.ConfigureForBaseAttack();

        // HP-Skalierung: linear pro Minute (+ Elite-Multiplikator)
        var e = enemy.GetComponent<IEnemy>();
        if (e != null)
        {
            float baseHp = e.GetBaseHealth();
            float hpMultiplier = (1f + enemyHealthPerMinuteMultiplier * minutesSinceStart) * Mathf.Max(1f, eliteHpMul);
            e.SetHealth(baseHp * hpMultiplier);

            // Optional: Elite-Flag, falls Interface vorhanden
            //try { if (isElite) e.MarkElite(); } catch { /* ignorieren, falls nicht implementiert */ }

            e.OnEnemyDied += OnEnemyKilled_Horde;
        }

        // Lebende Gegner hochzählen (für Cap/Debug/UI)
        EnemiesAlive.Value = EnemiesAlive.Value + 1;
    }

    private void OnEnemyKilled_Horde(IEnemy enemy)
    {
        enemy.OnEnemyDied -= OnEnemyKilled_Horde;

        // Optional: Killer-Credit
        ulong killerId = enemy.LastHitByClientId;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(killerId, out var client))
        {
            var stats = client.PlayerObject?.GetComponentInChildren<PlayerStats>();
            if (stats) stats.AddKill();
        }

        // Runterzählen
        EnemiesAlive.Value = Mathf.Max(0, EnemiesAlive.Value - 1);
    }


    // ================== Gizmos ==================
    private void OnDrawGizmosSelected()
    {
        // Visualisierung: Baseline-Ring
        if (targetMode == AttackTargetMode.StaticTransform && staticTarget)
        {
            Gizmos.color = new Color(0f, 0.6f, 1f, 0.4f);
            Gizmos.DrawWireSphere(staticTarget.position, spawnRingMin);
            Gizmos.DrawWireSphere(staticTarget.position, spawnRingMax);

            // Burst-Ring
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
            Gizmos.DrawWireSphere(staticTarget.position, burstRingMin);
            Gizmos.DrawWireSphere(staticTarget.position, burstRingMax);

            // Elite-Ring (falls gesetzt)
            if (eliteRingMin > 0f && eliteRingMax > 0f)
            {
                Gizmos.color = new Color(0.8f, 0f, 0.8f, 0.35f);
                Gizmos.DrawWireSphere(staticTarget.position, eliteRingMin);
                Gizmos.DrawWireSphere(staticTarget.position, eliteRingMax);
            }
        }
    }
}
