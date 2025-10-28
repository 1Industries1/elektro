using System.Collections;
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
        int delta = newValue - cur;

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





//    ////// Chest //////
//    // Checks a list of slots to see if there are any slots left
//    int GetSlotsLeft(List<Slot> slots)
//    {
//        int count = 0;
//        foreach (Slot s in slots)
//        {
//            if (s.IsEmpty()) count++;
//        }
//        return count;
//    }
//
//    // Generic variants of GetSlotsLeft(), hich is easier to use
//    public int GetSlotsLeft<T>() where T : Item { return GetSlotsLeft(new List<Slot>(GetSlots<T>())); }
//    public int GetSlotsLeftFor<T>() where T : ItemData { return GetSlotsLeft(new List<Slot>(GetSlotsFor<T>())); }
//
//
//    public T[] GetAvailable<T>() where T : ItemData
//    {
//        if (typeof(T) == typeof(PassiveData))
//        {
//            return availablePassives.ToArray() as T[];
//        }
//
//        else if (typeof(T) == typeof(WeaponData))
//        {
//            return availableWeapons.ToArray() as T[];
//        }
//
//        else if (typeof(T) == typeof(ItemData))
//        {
//            List<ItemData> list = new List<ItemData>(availablePassives);
//            list.AddRange(availableWeapons);
//            return list.ToArray() as T[];
//        }
//
//        Debug.LogWarning("Generic type provided to GetAvailable() call does not have a coded behaviour.");
//        return null;
//
//    }
//
//    // Get all available items (weapons or passives) that we still do not have yet
//    public T[] GetUnowned<T>() where T : ItemData
//    {
//        // Get all available items.
//        var available = GetAvailable<T>();
//
//        if (available == null || available.Length == 0)
//            return new T[0]; // Return empty array if null or empty
//
//        List<T> list = new List<T>(available);
//
//        // Check all of our slots, and remove all items in the list that are found in the slots. 
//        var slots = GetSlotsFor<T>();
//        if (slots != null)
//        {
//            foreach (Slot s in slots)
//            {
//                if (s?.item?.data != null && list.Contains(s.item.data as T))
//                    list.Remove(s.item.data as T);
//            }
//            return list.ToArray();
//        }
//    }
//
//
//    public T[] GetEvolvables<T>() where T : Item
//    {
//        // Check all the slots, and find all the items in the slot that 
//        // are capable of evolving.
//        List<T> result = new List<T>();
//        foreach (Slot s in GetSlots<T>())
//            if (s.item is T t && t.CanEvolve(0).Length > 0) result.Add(t);
//        return result.ToArray();
//    }
//
//    public T[] GetUpgradables<T>() where T : Item
//    {
//        // Check all the slots, and find all the items in the slot that
//        // are still capable of levelling up.
//        List<T> result = new List<T>();
//        foreach (Slot s in GetSlots<T>())
//            if (s.item is T t && t.CanLevelUp()) result.Add(t);
//        return result.ToArray();
//    }
//
//
//}