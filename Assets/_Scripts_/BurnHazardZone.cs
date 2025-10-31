// BurnHazardZone.cs  (E: DoT-Zone)
using UnityEngine;
using Unity.Netcode;
using System.Collections;

[RequireComponent(typeof(NetworkObject))]
public class BurnHazardZone : NetworkBehaviour
{
    [Header("Params")]
    [SerializeField] private float radius = 4f;
    [SerializeField] private float duration = 4f;
    [SerializeField] private float dps = 5f;
    [SerializeField] private float tickRate = 0.5f; // Sekunden
    [SerializeField, Range(0f, 1f)] private float slowPercent = 0f;

    [Header("FX")]
    [SerializeField] private GameObject loopFx; // optionaler Partikeleffekt am Ort
    [SerializeField] private GameObject tickFx; // optional pro Tick auf Spieler

    private float attackerClientId; // als float gespeichert für SerDes Einfachheit
    private float spawnTime;

    public void Init(float radius, float duration, float dps, float tickRate, float slowPercent, ulong attackerId)
    {
        this.radius = radius;
        this.duration = duration;
        this.dps = dps;
        this.tickRate = Mathf.Max(0.05f, tickRate);
        this.slowPercent = Mathf.Clamp01(slowPercent);
        this.attackerClientId = attackerId;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        spawnTime = Time.time;

        if (loopFx) SpawnLoopFxClientRpc(transform.position, radius);

        StartCoroutine(ServerTick());
    }

    private IEnumerator ServerTick()
    {
        var wait = new WaitForSeconds(tickRate);
        while (Time.time - spawnTime < duration)
        {
            // Damage-Tick
            var hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in hits)
            {
                if (!c.CompareTag("Player")) continue;

                var hp = c.GetComponentInParent<PlayerHealth>();
                if (hp != null)
                {
                    float dmg = dps * tickRate;
                    hp.Server_TakeDamage(dmg, (ulong)attackerClientId);
                    if (tickFx) SpawnTickFxClientRpc(c.transform.position + Vector3.up * 0.2f);
                }

                // Optional: Slow-Interface
                var slow = c.GetComponentInParent<IStatusReceiver>();
                if (slow != null && slowPercent > 0f)
                {
                    slow.Server_ApplySlow(slowPercent, tickRate + 0.05f); // minimal länger als tick
                }
            }

            yield return wait;
        }

        if (IsServer) GetComponent<NetworkObject>().Despawn(true);
    }

    [ClientRpc]
    private void SpawnLoopFxClientRpc(Vector3 pos, float rad)
    {
        if (loopFx)
        {
            var fx = Instantiate(loopFx, pos, Quaternion.identity);
            fx.transform.localScale = Vector3.one * rad * 2f;
            Destroy(fx, duration + 0.25f);
        }
    }

    [ClientRpc]
    private void SpawnTickFxClientRpc(Vector3 pos)
    {
        if (tickFx)
        {
            var fx = Instantiate(tickFx, pos, Quaternion.identity);
            Destroy(fx, 1.5f);
        }
    }
}

// Optionales Interface, falls du Slow-Effekte unterstützen willst.
// Entferne es, wenn du kein Slow-System hast.
public interface IStatusReceiver
{
    void Server_ApplySlow(float percent, float seconds);
}
