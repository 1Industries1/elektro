using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerWeapons : NetworkBehaviour
{
    [Header("Assign weapon defs (ScriptableObjects)")]
    public WeaponDefinition cannonDef;
    public WeaponDefinition blasterDef;
    public WeaponDefinition grenadeDef;
    public WeaponDefinition lightningDef;
    public WeaponDefinition orbitalDef;
    public WeaponDefinition blackHoleDef;

    [Header("Refs")]
    [SerializeField] private PlayerUpgrades upgrades;

    public NetworkVariable<int> cannonLevel   = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> blasterLevel  = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> grenadeLevel  = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> lightningLevel= new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> orbitalLevel  = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> blackHoleLevel = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public event Action RuntimesRebuilt;

    public WeaponRuntime CannonRuntime    { get; private set; }
    public WeaponRuntime BlasterRuntime   { get; private set; }
    public WeaponRuntime GrenadeRuntime   { get; private set; }
    public WeaponRuntime LightningRuntime { get; private set; }
    public WeaponRuntime OrbitalRuntime   { get; private set; }
    public WeaponRuntime BlackHoleRuntime { get; private set; }

    private enum WeaponSlot
    {
        Cannon,
        Blaster,
        Grenade,
        Lightning,
        Orbital,
        BlackHole
    }

    private void EnsureUpgradesRef()
    {
        if (upgrades) return;

        upgrades = GetComponent<PlayerUpgrades>()
                ?? GetComponentInParent<PlayerUpgrades>()
                ?? GetComponentInChildren<PlayerUpgrades>(true);
    }

    private void Rebuild()
    {
        CannonRuntime    = (cannonDef    != null && cannonLevel.Value    > 0) ? new WeaponRuntime(cannonDef,    cannonLevel.Value)    : null;
        BlasterRuntime   = (blasterDef   != null && blasterLevel.Value   > 0) ? new WeaponRuntime(blasterDef,   blasterLevel.Value)   : null;
        GrenadeRuntime   = (grenadeDef   != null && grenadeLevel.Value   > 0) ? new WeaponRuntime(grenadeDef,   grenadeLevel.Value)   : null;
        LightningRuntime = (lightningDef != null && lightningLevel.Value > 0) ? new WeaponRuntime(lightningDef, lightningLevel.Value) : null;
        OrbitalRuntime   = (orbitalDef   != null && orbitalLevel.Value   > 0) ? new WeaponRuntime(orbitalDef,   orbitalLevel.Value)   : null;
        BlackHoleRuntime = (blackHoleDef != null && blackHoleLevel.Value > 0) ? new WeaponRuntime(blackHoleDef, blackHoleLevel.Value) : null;

        EnsureUpgradesRef();

        // Masteries auf alle Runtimes anwenden
        if (upgrades != null)
        {
            if (CannonRuntime    != null) upgrades.ApplyTo(CannonRuntime);
            if (BlasterRuntime   != null) upgrades.ApplyTo(BlasterRuntime);
            if (GrenadeRuntime   != null) upgrades.ApplyTo(GrenadeRuntime);
            if (LightningRuntime != null) upgrades.ApplyTo(LightningRuntime);
            if (OrbitalRuntime   != null) upgrades.ApplyTo(OrbitalRuntime);
            if (BlackHoleRuntime != null) upgrades.ApplyTo(BlackHoleRuntime);
        }

        RuntimesRebuilt?.Invoke();
    }

    public void ForceRebuild() => Rebuild();

    // ---- Eigene Handler, damit -OnValueChanged sauber abgemeldet werden kann ----
    private void OnCannonLevelChanged(int _, int __)    => Rebuild();
    private void OnBlasterLevelChanged(int _, int __)   => Rebuild();
    private void OnGrenadeLevelChanged(int _, int __)   => Rebuild();
    private void OnLightningLevelChanged(int _, int __) => Rebuild();
    private void OnOrbitalLevelChanged(int _, int __)   => Rebuild();
    private void OnBlackHoleLevelChanged(int _, int __) => Rebuild();

    public override void OnNetworkSpawn()
    {
        cannonLevel.OnValueChanged    += OnCannonLevelChanged;
        blasterLevel.OnValueChanged   += OnBlasterLevelChanged;
        grenadeLevel.OnValueChanged   += OnGrenadeLevelChanged;
        lightningLevel.OnValueChanged += OnLightningLevelChanged;
        orbitalLevel.OnValueChanged   += OnOrbitalLevelChanged;
        blackHoleLevel.OnValueChanged += OnBlackHoleLevelChanged;

        if (IsServer)
        {
            // Mit welcher Waffe gestartet wird
            cannonLevel.Value    = 1;
            blasterLevel.Value   = 0;
            grenadeLevel.Value   = 0;   
            lightningLevel.Value = 1;   // passiv
            orbitalLevel.Value   = 0;   // passiv
            blackHoleLevel.Value = 0;   // passiv
        }
        Rebuild();
    }

    public override void OnNetworkDespawn()
    {
        cannonLevel.OnValueChanged    -= OnCannonLevelChanged;
        blasterLevel.OnValueChanged   -= OnBlasterLevelChanged;
        grenadeLevel.OnValueChanged   -= OnGrenadeLevelChanged;
        lightningLevel.OnValueChanged -= OnLightningLevelChanged;
        orbitalLevel.OnValueChanged   -= OnOrbitalLevelChanged;
        blackHoleLevel.OnValueChanged -= OnBlackHoleLevelChanged;
    }

    private int MaxLevel(WeaponDefinition def) => 1 + (def?.steps?.Length ?? 0);

    private List<(WeaponDefinition def, WeaponSlot slot, int level)> GetUpgradeableList()
    {
        var list = new List<(WeaponDefinition, WeaponSlot, int)>();

        if (cannonDef    != null && cannonLevel.Value    > 0 && cannonLevel.Value    < MaxLevel(cannonDef))
            list.Add((cannonDef,    WeaponSlot.Cannon,   cannonLevel.Value));

        if (blasterDef   != null && blasterLevel.Value   > 0 && blasterLevel.Value   < MaxLevel(blasterDef))
            list.Add((blasterDef,   WeaponSlot.Blaster,  blasterLevel.Value));

        if (grenadeDef   != null && grenadeLevel.Value   > 0 && grenadeLevel.Value   < MaxLevel(grenadeDef))
            list.Add((grenadeDef,   WeaponSlot.Grenade,  grenadeLevel.Value));

        if (lightningDef != null && lightningLevel.Value > 0 && lightningLevel.Value < MaxLevel(lightningDef))
            list.Add((lightningDef, WeaponSlot.Lightning, lightningLevel.Value));

        if (orbitalDef   != null && orbitalLevel.Value   > 0 && orbitalLevel.Value   < MaxLevel(orbitalDef))
            list.Add((orbitalDef,   WeaponSlot.Orbital,  orbitalLevel.Value));

        if (blackHoleDef   != null && blackHoleLevel.Value   > 0 && blackHoleLevel.Value   < MaxLevel(blackHoleDef))
            list.Add((blackHoleDef,   WeaponSlot.BlackHole,  blackHoleLevel.Value));


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

        switch (choice.slot)
        {
            case WeaponSlot.Cannon:
                cannonLevel.Value = Mathf.Min(cannonLevel.Value + 1, MaxLevel(cannonDef));
                upgraded = cannonDef;
                break;
            case WeaponSlot.Blaster:
                blasterLevel.Value = Mathf.Min(blasterLevel.Value + 1, MaxLevel(blasterDef));
                upgraded = blasterDef;
                break;
            case WeaponSlot.Grenade:
                grenadeLevel.Value = Mathf.Min(grenadeLevel.Value + 1, MaxLevel(grenadeDef));
                upgraded = grenadeDef;
                break;
            case WeaponSlot.Lightning:
                lightningLevel.Value = Mathf.Min(lightningLevel.Value + 1, MaxLevel(lightningDef));
                upgraded = lightningDef;
                break;
            case WeaponSlot.Orbital:
                orbitalLevel.Value = Mathf.Min(orbitalLevel.Value + 1, MaxLevel(orbitalDef));
                upgraded = orbitalDef;
                break;
            case WeaponSlot.BlackHole:
                blackHoleLevel.Value = Mathf.Min(blackHoleLevel.Value + 1, MaxLevel(blackHoleDef));
                upgraded = blackHoleDef;
                break;
        }

        if (upgraded != null)
        {
            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            };
            OwnerNotifyUpgradeClientRpc(upgraded.id, target);
        }

        return upgraded != null;
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
            if (cannonLevel.Value == 0)       { cannonLevel.Value = 1; did = true; }
            else if (cannonLevel.Value < max) { cannonLevel.Value++;   did = true; }
        }
        else if (blasterDef != null && blasterDef.id == weaponId)
        {
            int max = MaxLevel(blasterDef);
            if (blasterLevel.Value == 0)       { blasterLevel.Value = 1; did = true; }
            else if (blasterLevel.Value < max) { blasterLevel.Value++;   did = true; }
        }
        else if (grenadeDef != null && grenadeDef.id == weaponId)
        {
            int max = MaxLevel(grenadeDef);
            if (grenadeLevel.Value == 0)       { grenadeLevel.Value = 1; did = true; }
            else if (grenadeLevel.Value < max) { grenadeLevel.Value++;   did = true; }
        }
        else if (lightningDef != null && lightningDef.id == weaponId)
        {
            int max = MaxLevel(lightningDef);
            if (lightningLevel.Value == 0)       { lightningLevel.Value = 1; did = true; }
            else if (lightningLevel.Value < max) { lightningLevel.Value++;   did = true; }
        }
        else if (orbitalDef != null && orbitalDef.id == weaponId)
        {
            int max = MaxLevel(orbitalDef);
            if (orbitalLevel.Value == 0)       { orbitalLevel.Value = 1; did = true; }
            else if (orbitalLevel.Value < max) { orbitalLevel.Value++;   did = true; }
        }
        else if (blackHoleDef != null && blackHoleDef.id == weaponId)
        {
            int max = MaxLevel(blackHoleDef);
            if (blackHoleLevel.Value == 0)        { blackHoleLevel.Value = 1; did = true; }
            else if (blackHoleLevel.Value < max)  { blackHoleLevel.Value++;   did = true; }
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
        WeaponDefinition _dummy;
        Server_TryLevelUpRandomWeapon(out _dummy);
    }

    [ClientRpc]
    private void OwnerNotifyUpgradeClientRpc(string weaponId, ClientRpcParams _ = default)
    {
        if (!IsOwner) return;

        WeaponDefinition def = null;
        if      (cannonDef    != null && cannonDef.id    == weaponId) def = cannonDef;
        else if (blasterDef   != null && blasterDef.id   == weaponId) def = blasterDef;
        else if (grenadeDef   != null && grenadeDef.id   == weaponId) def = grenadeDef;
        else if (lightningDef != null && lightningDef.id == weaponId) def = lightningDef;
        else if (orbitalDef   != null && orbitalDef.id   == weaponId) def = orbitalDef;
        else if (blackHoleDef   != null && blackHoleDef.id   == weaponId) def = blackHoleDef;

        if (def != null && def.uiIcon != null)
            UIChestManager.NotifyItemReceived(def.uiIcon);
    }
}
