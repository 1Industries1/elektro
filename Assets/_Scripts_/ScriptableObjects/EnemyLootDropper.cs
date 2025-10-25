using Unity.Netcode;
using UnityEngine;

public class EnemyLootDropper : NetworkBehaviour
{
    public EnemyClass enemyClass;

    // Pools
    public OverclockDef[] poolInstant;
    public OverclockDef[] poolTactical;
    public OverclockDef[] poolUltimate;

    // Prefabs pro Kind
    public NetworkObject pickupInstantPrefab;
    public NetworkObject pickupTacticalPrefab;
    public NetworkObject pickupUltimatePrefab;

    public NetworkObject catalystPrefab; // für Boss 50%

    private static System.Random _rng = new System.Random();

    public void OnKilled(Vector3 pos)
    {
        if (!IsServer) return;

        if (OverclockLoot.TryRoll(enemyClass, _rng, out var kind, out bool catalyst))
        {
            if (enemyClass == EnemyClass.Boss && catalyst)
            {
                if (catalystPrefab) InstantiateAndSpawn(catalystPrefab, pos);
                return;
            }

            var def = PickFromKind(kind);
            if (def == null) return;

            var prefab = PrefabFor(kind);
            if (prefab == null) return;

            var no = InstantiateAndSpawn(prefab, pos + Vector3.up * 0.3f);
            var pick = no.GetComponent<OverclockPickup>();
            pick.SetDefServer(def, forceInstant: kind == OverclockKind.Instant);
        }
    }

    private OverclockDef PickFromKind(OverclockKind kind)
    {
        var pool = kind switch {
            OverclockKind.Instant  => poolInstant,
            OverclockKind.Tactical => poolTactical,
            OverclockKind.Ultimate => poolUltimate,
            _ => null
        };
        if (pool == null || pool.Length == 0) return null;
        return pool[_rng.Next(0, pool.Length)];
    }

    private NetworkObject PrefabFor(OverclockKind kind) => kind switch {
        OverclockKind.Instant  => pickupInstantPrefab,
        OverclockKind.Tactical => pickupTacticalPrefab,
        OverclockKind.Ultimate => pickupUltimatePrefab,
        _ => null
    };

    private NetworkObject InstantiateAndSpawn(NetworkObject prefab, Vector3 pos)
    {
        var no = Instantiate(prefab, pos, Quaternion.identity);
        no.Spawn(true);
        return no;
    }
}
