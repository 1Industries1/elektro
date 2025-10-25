// Scripts/Inventory/PlayerInventory.cs
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[DisallowMultipleComponent]
public class PlayerInventory : NetworkBehaviour
{
    private readonly Dictionary<ResourceType, int> _amounts = new();

    // [Optional] Per-Player Referenz aufs HUD (im Prefab zuweisen)
    [SerializeField] private WorldSpaceResourceHUD _hud;

    public int GetAmount(ResourceType type)
        => _amounts.TryGetValue(type, out var v) ? v : 0;

    // ====== SERVER ONLY: Add / Set / TryConsume ======

    public void Server_Add(ResourceType type, int amount)
    {
        if (!IsServer || amount == 0) return;

        int cur = 0; _amounts.TryGetValue(type, out cur);
        int newValue = Mathf.Max(0, cur + amount);
        int delta    = newValue - cur;

        _amounts[type] = newValue;

        // Gesamtwert an Owner updaten
        UpdateUiOwnerClientRpc((int)type, newValue, OwnerClientId);

        // Nur beim Einsammeln (delta > 0) das kurze Aufblinken triggern
        if (delta > 0)
            FlashPickupOwnerClientRpc((int)type, delta, OwnerClientId);
    }

    public void Server_Set(ResourceType type, int value)
    {
        if (!IsServer) return;
        _amounts[type] = Mathf.Max(0, value);
        UpdateUiOwnerClientRpc((int)type, _amounts[type], OwnerClientId);
    }

    public bool Server_TryConsume(ResourceType type, int amount)
    {
        if (!IsServer || amount <= 0) return false;
        int cur = GetAmount(type);
        if (cur < amount) return false;
        _amounts[type] = cur - amount;
        UpdateUiOwnerClientRpc((int)type, _amounts[type], OwnerClientId);
        return true;
    }

    public bool Server_Has(ResourceType type, int amount) => GetAmount(type) >= amount;

    // ====== Client → Server Convenience (optional) ======

    [ServerRpc(RequireOwnership = false)]
    public void RequestAddServerRpc(ResourceType type, int amount, ServerRpcParams rpcParams = default)
        => Server_Add(type, amount);

    [ServerRpc(RequireOwnership = false)]
    public void RequestConsumeServerRpc(ResourceType type, int amount, ServerRpcParams rpcParams = default)
        => Server_TryConsume(type, amount);

    // ====== UI-Update nur an den Owner ======

    [ClientRpc]
    private void UpdateUiOwnerClientRpc(int type, int newValue, ulong forOwner, ClientRpcParams _ = default)
    {
        if (!(IsOwner && NetworkManager.Singleton.LocalClientId == forOwner)) return;

        // Sicherstellen, dass wir das HUD haben (Fallback-Suche am Player)
        if (_hud == null) _hud = GetComponentInChildren<WorldSpaceResourceHUD>(true);
        _hud?.SetTotal((ResourceType)type, newValue);
    }

    [ClientRpc]
    private void FlashPickupOwnerClientRpc(int type, int delta, ulong forOwner, ClientRpcParams _ = default)
    {
        if (!(IsOwner && NetworkManager.Singleton.LocalClientId == forOwner)) return;

        if (_hud == null) _hud = GetComponentInChildren<WorldSpaceResourceHUD>(true);
        _hud?.Flash((ResourceType)type, delta);
    }
}
