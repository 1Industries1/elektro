using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class AltarEventSpawner : NetworkBehaviour
{
    [Header("Activation")]
    public float activationRadius = 8f;
    public LayerMask playerLayer = ~0;     // ideally: Player
    public bool triggerOnlyOnce = true;

    [Header("Spawn Point")]
    [Tooltip("All enemies spawn from this point (e.g. a child transform on the altar).")]
    public Transform spawnPoint;

    [Header("VFX (Idle -> Off when triggered)")]
    [Tooltip("This VFX is active at start (idle). It will be stopped/disabled when the event triggers.")]
    public ParticleSystem idleVfx; // can also be VisualEffect, but ParticleSystem here

    [Tooltip("If true: disables the GameObject of idleVfx (hard off). If false: just Stop().")]
    public bool disableIdleVfxGameObject = true;

    [Header("VFX (Start Event) - Option B Networked")]
    [Tooltip("Networked VFX prefab (must have NetworkObject and be in NetworkManager prefab list).")]
    public NetworkObject startVfxNetworkPrefab;

    public float startVfxLifetime = 5f;
    public float idleVfxDisableDelaySeconds = 15f;

    [Tooltip("Spawn the start VFX at spawnPoint instead of altar transform.")]
    public bool spawnStartVfxAtSpawnPoint = true;

    [Header("Enemy Spawn")]
    public List<GameObject> enemyPrefabs = new();
    public Vector2Int spawnCountRange = new Vector2Int(1, 3);
    public float spawnInterval = 0.15f;

    [Header("Debug")]
    public bool drawGizmos = true;

    private bool _eventStarted = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogWarning("[AltarEventSpawner] No enemy prefabs assigned.", this);
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogWarning("[AltarEventSpawner] SpawnPoint is not set! Enemies will spawn at altar position.", this);
        }

        StartCoroutine(ServerWatchLoop());
    }

    private IEnumerator ServerWatchLoop()
    {
        var wait = new WaitForSeconds(0.1f);

        while (true)
        {
            if (!_eventStarted && IsAnyAlivePlayerInRange(transform.position, activationRadius))
            {
                _eventStarted = true;
                StartCoroutine(RunEvent());
                if (triggerOnlyOnce) yield break;
            }

            yield return wait;
        }
    }

    private bool IsAnyAlivePlayerInRange(Vector3 center, float radius)
    {
        var hits = Physics.OverlapSphere(center, radius, playerLayer, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            var root = h.attachedRigidbody ? h.attachedRigidbody.gameObject : h.gameObject;
            var netObj = root.GetComponentInParent<NetworkObject>();
            if (netObj == null) continue;

            var health = root.GetComponentInParent<PlayerHealth>();
            if (health != null && health.IsDead()) continue;

            return true;
        }
        return false;
    }

    private IEnumerator RunEvent()
    {
        // 0) Turn off idle vfx for everyone
        StopIdleVfxClientRpc();

        // 1) Spawn start VFX networked (Option B)
        if (startVfxNetworkPrefab != null)
        {
            Vector3 vfxPos = (spawnStartVfxAtSpawnPoint && spawnPoint != null)
                ? spawnPoint.position
                : transform.position;

            var vfx = Instantiate(startVfxNetworkPrefab, vfxPos, Quaternion.identity);
            vfx.Spawn(true);

            if (startVfxLifetime > 0f)
                StartCoroutine(DespawnAfter(vfx, startVfxLifetime));
        }

        // 2) Spawn enemies FROM spawnPoint
        int count = Random.Range(spawnCountRange.x, spawnCountRange.y + 1);
        count = Mathf.Max(1, count);

        for (int i = 0; i < count; i++)
        {
            var prefab = PickRandom(enemyPrefabs);
            if (prefab != null)
            {
                Vector3 pos = (spawnPoint != null) ? spawnPoint.position : transform.position;
                SpawnEnemy(prefab, pos);
            }

            if (spawnInterval > 0f)
                yield return new WaitForSeconds(spawnInterval);
        }
    }

    [ClientRpc]
    private void StopIdleVfxClientRpc()
    {
        if (idleVfx == null) return;

        var root = idleVfx.gameObject;
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);

        foreach (var ps in systems)
        {
            var emission = ps.emission;
            emission.enabled = false;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        StartCoroutine(DisableAfterAllParticlesDeadDelayed(root, idleVfxDisableDelaySeconds));
    }

    private IEnumerator DisableAfterAllParticlesDeadDelayed(GameObject root, float delaySeconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delaySeconds));
        yield return DisableAfterAllParticlesDead(root);
    }

    private IEnumerator DisableAfterAllParticlesDead(GameObject root)
    {
        if (root == null) yield break;

        var systems = root.GetComponentsInChildren<ParticleSystem>(true);

        while (true)
        {
            bool anyAlive = false;

            foreach (var ps in systems)
            {
                if (ps != null && ps.IsAlive(true))
                {
                    anyAlive = true;
                    break;
                }
            }

            if (!anyAlive) break;
            yield return null;
        }

        root.SetActive(false);
    }


    private IEnumerator DespawnAfter(NetworkObject no, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (no != null && no.IsSpawned)
            no.Despawn(true);
    }

    private GameObject PickRandom(List<GameObject> list)
    {
        if (list == null || list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    private void SpawnEnemy(GameObject prefab, Vector3 pos)
    {
        if (!IsServer || prefab == null) return;

        GameObject enemy = Instantiate(prefab, pos, Quaternion.identity);

        if (enemy.TryGetComponent(out NetworkObject netObj) && !netObj.IsSpawned)
            netObj.Spawn(true);

        // Optional: target nearest player (if your enemies support it)
        var target = ResolveNearestAlivePlayer(pos);
        if (target != null && enemy.TryGetComponent(out EnemyController controller))
            controller.SetPrimaryOverride(target);
    }

    private Transform ResolveNearestAlivePlayer(Vector3 reference)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return null;

        Transform best = null;
        float bestSqr = float.MaxValue;

        foreach (var kv in nm.ConnectedClients)
        {
            var po = kv.Value?.PlayerObject;
            if (!po) continue;

            var health = po.GetComponentInChildren<PlayerHealth>();
            if (health && health.IsDead()) continue;

            float d2 = (po.transform.position - reference).sqrMagnitude;
            if (d2 < bestSqr)
            {
                bestSqr = d2;
                best = po.transform;
            }
        }

        return best;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, activationRadius);

        if (spawnPoint != null)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawSphere(spawnPoint.position, 0.2f);
        }
    }
}
