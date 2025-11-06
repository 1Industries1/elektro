using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;


[DisallowMultipleComponent]
public class PlayerInventory : NetworkBehaviour
{
    private readonly Dictionary<ResourceType, int> _amounts = new();
    public static readonly List<PlayerInventory> ServerPlayers = new();
    public static event System.Action<ResourceType, int, int> OnLocalResourceChanged;


    // [Optional] Per-Player Referenz aufs HUD (im Prefab zuweisen)
    [SerializeField] private WorldSpaceResourceHUD _hud;


    public override void OnNetworkSpawn()
    {
        if (IsServer) ServerPlayers.Add(this);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer) ServerPlayers.Remove(this);
    }


    public int GetAmount(ResourceType type)
        => _amounts.TryGetValue(type, out var v) ? v : 0;


    // ====== SERVER ONLY: Add / Set / TryConsume ======
    public void Server_Add(ResourceType type, int amount)
    {
        if (!IsServer || amount == 0) return;

        int cur = GetAmount(type);
        int newValue = Mathf.Max(0, cur + amount);
        int delta = newValue - cur;
        _amounts[type] = newValue;

        ulong ownerId = GetComponent<NetworkObject>()?.OwnerClientId ?? OwnerClientId;
        var toOwner = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { ownerId } }
        };

        // Nur Owner updaten
        UpdateUiOwnerClientRpc((int)type, newValue, ownerId, toOwner);
        if (delta > 0) FlashPickupOwnerClientRpc((int)type, delta, ownerId, toOwner);

        ResourceUpdateOwnerClientRpc(type, delta, newValue, ownerId, toOwner);
    }


    public void Server_Set(ResourceType type, int value)
    {
        if (!IsServer) return;

        int cur = GetAmount(type);
        int newValue = Mathf.Max(0, value);
        int delta = newValue - cur;
        _amounts[type] = newValue;

        ulong ownerId = GetComponent<NetworkObject>()?.OwnerClientId ?? OwnerClientId;
        var toOwner = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { ownerId } }
        };

        UpdateUiOwnerClientRpc((int)type, newValue, ownerId, toOwner);
        if (delta != 0) ResourceUpdateOwnerClientRpc(type, delta, newValue, ownerId, toOwner);
    }

    public bool Server_TryConsume(ResourceType type, int amount)
    {
        if (!IsServer || amount <= 0) return false;
        int cur = GetAmount(type);
        if (cur < amount) return false;

        int newValue = cur - amount;
        _amounts[type] = newValue;

        ulong ownerId = GetComponent<NetworkObject>()?.OwnerClientId ?? OwnerClientId;
        var toOwner = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { ownerId } }
        };

        UpdateUiOwnerClientRpc((int)type, newValue, ownerId, toOwner);
        // negatives Delta fürs Overlay
        ResourceUpdateOwnerClientRpc(type, -amount, newValue, ownerId, toOwner);
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

    [ClientRpc]
    private void ResourceUpdateOwnerClientRpc(ResourceType type, int delta, int newTotal, ulong forOwner, ClientRpcParams _ = default)
    {
        if (!(IsOwner && NetworkManager.Singleton.LocalClientId == forOwner)) return;
        OnLocalResourceChanged?.Invoke(type, delta, newTotal);
    }

}