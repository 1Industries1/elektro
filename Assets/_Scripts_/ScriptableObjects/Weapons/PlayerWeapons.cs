using System;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class PlayerWeapons : NetworkBehaviour
{
    [Header("Assign weapon defs (ScriptableObjects)")]
    public WeaponDefinition cannonDef;
    public WeaponDefinition blasterDef;

    public NetworkVariable<int> cannonLevel = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> blasterLevel = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public event Action RuntimesRebuilt;

    public WeaponRuntime CannonRuntime { get; private set; }
    public WeaponRuntime BlasterRuntime { get; private set; }

    void Rebuild()
    {
        if (cannonDef != null) CannonRuntime = new WeaponRuntime(cannonDef, Mathf.Max(1, cannonLevel.Value));
        if (blasterDef != null) BlasterRuntime = new WeaponRuntime(blasterDef, Mathf.Max(1, blasterLevel.Value));

        RuntimesRebuilt?.Invoke();
    }

    // ---- Eigene Handler, damit -OnValueChanged korrekt abgemeldet werden kann
    private void OnCannonLevelChanged(int _, int __) => Rebuild();
    private void OnBlasterLevelChanged(int _, int __) => Rebuild();

    public override void OnNetworkSpawn()
    {
        cannonLevel.OnValueChanged += OnCannonLevelChanged;
        blasterLevel.OnValueChanged += OnBlasterLevelChanged;
        Rebuild();
    }

    public override void OnNetworkDespawn()
    {
        cannonLevel.OnValueChanged -= OnCannonLevelChanged;
        blasterLevel.OnValueChanged -= OnBlasterLevelChanged;
    }

    int MaxLevel(WeaponDefinition def) => 1 + (def?.steps?.Length ?? 0);

    List<(WeaponDefinition def, string slot, int level)> GetUpgradeableList()
    {
        var list = new List<(WeaponDefinition, string, int)>();
        if (cannonDef != null && cannonLevel.Value < MaxLevel(cannonDef)) list.Add((cannonDef, "cannon", cannonLevel.Value));
        if (blasterDef != null && blasterLevel.Value < MaxLevel(blasterDef)) list.Add((blasterDef, "blaster", blasterLevel.Value));
        return list;
    }

    public bool Server_TryLevelUpRandomWeapon(out WeaponDefinition upgraded)
    {
        upgraded = null;
        if (!IsServer) return false;

        var up = GetUpgradeableList();
        if (up.Count == 0) return false;

        int idx = UnityEngine.Random.Range(0, up.Count);
        var choice = up[idx];

        if (choice.slot == "cannon")
        {
            cannonLevel.Value = Mathf.Min(cannonLevel.Value + 1, MaxLevel(cannonDef));
            upgraded = cannonDef;
        }
        else
        {
            blasterLevel.Value = Mathf.Min(blasterLevel.Value + 1, MaxLevel(blasterDef));
            upgraded = blasterDef;
        }

        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        OwnerNotifyUpgradeClientRpc(upgraded != null ? upgraded.id : null, target);
        return true;
    }
    

    public string Server_PeekRandomUpgradeableId()
    {
        if (!IsServer) return null;
        var up = GetUpgradeableList();
        if (up.Count == 0) return null;
        int idx = UnityEngine.Random.Range(0, up.Count);
        return up[idx].def != null ? up[idx].def.id : null;
    }


    public bool Server_LevelUpById(string weaponId, bool notifyOwner = true)
    {
        if (!IsServer || string.IsNullOrEmpty(weaponId)) return false;

        bool did = false;
        if (cannonDef != null && cannonDef.id == weaponId)
        {
            int max = MaxLevel(cannonDef);
            if (cannonLevel.Value < max)
            {
                cannonLevel.Value = Mathf.Min(cannonLevel.Value + 1, max);
                did = true;
            }
        }
        else if (blasterDef != null && blasterDef.id == weaponId)
        {
            int max = MaxLevel(blasterDef);
            if (blasterLevel.Value < max)
            {
                blasterLevel.Value = Mathf.Min(blasterLevel.Value + 1, max);
                did = true;
            }
        }

        if (did && notifyOwner)
        {
            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            };
            OwnerNotifyUpgradeClientRpc(weaponId, target);
        }

        return did;
    }





    [ServerRpc(RequireOwnership = false)]
    public void RequestLevelUpFromChestServerRpc(ServerRpcParams _ = default)
    {
        WeaponDefinition _dummy;                              // <- Fix: explizite Variable
        Server_TryLevelUpRandomWeapon(out _dummy);
    }

    [ClientRpc]
    void OwnerNotifyUpgradeClientRpc(string weaponId, ClientRpcParams _ = default)
    {
        // wir sind gezielt nur beim Owner gelandet
        // (zusätzliche IsOwner-Guard ist nett, aber nicht mehr zwingend)
        if (!IsOwner) return;

        WeaponDefinition def = null;
        if (cannonDef != null && cannonDef.id == weaponId) def = cannonDef;
        else if (blasterDef != null && blasterDef.id == weaponId) def = blasterDef;

        if (def != null && def.uiIcon != null)
            UIChestManager.NotifyItemReceived(def.uiIcon);
    }
}
