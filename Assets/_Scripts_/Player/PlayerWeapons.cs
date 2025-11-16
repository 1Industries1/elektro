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
    public WeaponDefinition grenadeDef;
    public WeaponDefinition lightningDef;
    public WeaponDefinition orbitalDef;

    [Header("Refs")]
    [SerializeField] private PlayerUpgrades upgrades;

    public NetworkVariable<int> cannonLevel = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> blasterLevel = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> grenadeLevel = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> lightningLevel= new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> orbitalLevel  = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public event Action RuntimesRebuilt;

    public WeaponRuntime CannonRuntime { get; private set; }
    public WeaponRuntime BlasterRuntime { get; private set; }
    public WeaponRuntime GrenadeRuntime { get; private set; }
    public WeaponRuntime LightningRuntime { get; private set; }
    public WeaponRuntime OrbitalRuntime  { get; private set; }

    void Rebuild()
    {
        CannonRuntime = (cannonDef != null && cannonLevel.Value > 0) ? new WeaponRuntime(cannonDef, cannonLevel.Value) : null;
        BlasterRuntime = (blasterDef != null && blasterLevel.Value > 0) ? new WeaponRuntime(blasterDef, blasterLevel.Value) : null;
        GrenadeRuntime = (grenadeDef != null && grenadeLevel.Value > 0) ? new WeaponRuntime(grenadeDef, grenadeLevel.Value) : null;
        LightningRuntime= (lightningDef!= null && lightningLevel.Value> 0) ? new WeaponRuntime(lightningDef, lightningLevel.Value): null;
        OrbitalRuntime  = (orbitalDef  != null && orbitalLevel.Value  > 0) ? new WeaponRuntime(orbitalDef,  orbitalLevel.Value)  : null;

        if (!upgrades)
        {
            upgrades = GetComponent<PlayerUpgrades>()
                       ?? GetComponentInParent<PlayerUpgrades>()
                       ?? GetComponentInChildren<PlayerUpgrades>(true);
        }

        // Masteries auf alle Runtimes anwenden
        if (upgrades != null)
        {
            if (CannonRuntime  != null) upgrades.ApplyTo(CannonRuntime);
            if (BlasterRuntime != null) upgrades.ApplyTo(BlasterRuntime);
            if (GrenadeRuntime != null) upgrades.ApplyTo(GrenadeRuntime);
            if (LightningRuntime != null) upgrades.ApplyTo(LightningRuntime);
            if (OrbitalRuntime   != null) upgrades.ApplyTo(OrbitalRuntime);
        }

        RuntimesRebuilt?.Invoke();
    }

    // von außen aufrufbar (PlayerUpgrades, wenn Masteries sich ändern)
    public void ForceRebuild() => Rebuild();

    // ---- Eigene Handler, damit -OnValueChanged korrekt abgemeldet werden kann
    private void OnCannonLevelChanged(int _, int __) => Rebuild();
    private void OnBlasterLevelChanged(int _, int __) => Rebuild();
    private void OnGrenadeLevelChanged(int _, int __) => Rebuild();
    private void OnLightningLevelChanged(int _, int __) => Rebuild();
    private void OnOrbitalLevelChanged(int _, int __)   => Rebuild();

    public override void OnNetworkSpawn()
    {
        cannonLevel.OnValueChanged  += OnCannonLevelChanged;
        blasterLevel.OnValueChanged += OnBlasterLevelChanged;
        grenadeLevel.OnValueChanged += OnGrenadeLevelChanged;
        lightningLevel.OnValueChanged += OnLightningLevelChanged;
        orbitalLevel.OnValueChanged   += OnOrbitalLevelChanged;

        if (IsServer)
        {
            /////////////// Mit welchen Waffen gestartet wird ///////////////
            cannonLevel.Value  = 0;
            blasterLevel.Value = 0;
            grenadeLevel.Value = 1;
            lightningLevel.Value = 0;
            orbitalLevel.Value   = 0;
        }

        Rebuild();
    }


    public override void OnNetworkDespawn()
    {
        cannonLevel.OnValueChanged -= OnCannonLevelChanged;
        blasterLevel.OnValueChanged -= OnBlasterLevelChanged;
        grenadeLevel.OnValueChanged -= OnGrenadeLevelChanged;
        lightningLevel.OnValueChanged -= OnLightningLevelChanged;
        orbitalLevel.OnValueChanged   -= OnOrbitalLevelChanged;
    }

    int MaxLevel(WeaponDefinition def) => 1 + (def?.steps?.Length ?? 0);

    List<(WeaponDefinition def, string slot, int level)> GetUpgradeableList()
    {
        var list = new List<(WeaponDefinition, string, int)>();
        if (cannonDef != null && cannonLevel.Value > 0 && cannonLevel.Value < MaxLevel(cannonDef)) list.Add((cannonDef, "cannon", cannonLevel.Value));
        if (blasterDef != null && blasterLevel.Value > 0 && blasterLevel.Value < MaxLevel(blasterDef)) list.Add((blasterDef, "blaster", blasterLevel.Value));
        if (grenadeDef != null && grenadeLevel.Value > 0 && grenadeLevel.Value < MaxLevel(grenadeDef)) list.Add((grenadeDef, "grenade", grenadeLevel.Value));
        if (lightningDef != null && lightningLevel.Value > 0 && lightningLevel.Value < MaxLevel(lightningDef)) list.Add((lightningDef, "lightning", lightningLevel.Value));
        if (orbitalDef  != null && orbitalLevel.Value  > 0 && orbitalLevel.Value  < MaxLevel(orbitalDef))   list.Add((orbitalDef,  "orbital",  orbitalLevel.Value));

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
        else if (choice.slot == "blaster")
        {
            blasterLevel.Value = Mathf.Min(blasterLevel.Value + 1, MaxLevel(blasterDef));
            upgraded = blasterDef;
        }
        else if (choice.slot == "grenade")
        {
            grenadeLevel.Value = Mathf.Min(grenadeLevel.Value + 1, MaxLevel(grenadeDef));
            upgraded = grenadeDef;
        }
        else if (choice.slot == "lightning")
        {
            lightningLevel.Value = Mathf.Min(lightningLevel.Value + 1, MaxLevel(lightningDef));
            upgraded = lightningDef;
        }
        else if (choice.slot == "orbital")
        {
            orbitalLevel.Value = Mathf.Min(orbitalLevel.Value + 1, MaxLevel(orbitalDef));
            upgraded = orbitalDef;
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
            if (cannonLevel.Value == 0) { cannonLevel.Value = 1; did = true; }                 // unlock
            else if (cannonLevel.Value < max) { cannonLevel.Value++; did = true; }             // normal level up
        }
        else if (blasterDef != null && blasterDef.id == weaponId)
        {
            int max = MaxLevel(blasterDef);
            if (blasterLevel.Value == 0) { blasterLevel.Value = 1; did = true; }
            else if (blasterLevel.Value < max) { blasterLevel.Value++; did = true; }
        }
        else if (grenadeDef != null && grenadeDef.id == weaponId)
        {
            int max = MaxLevel(grenadeDef);
            if (grenadeLevel.Value == 0) { grenadeLevel.Value = 1; did = true; }
            else if (grenadeLevel.Value < max) { grenadeLevel.Value++; did = true; }
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
            if (orbitalLevel.Value == 0)           { orbitalLevel.Value = 1; did = true; }
            else if (orbitalLevel.Value < max)     { orbitalLevel.Value++;    did = true; }
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
        if (!IsOwner) return;

        WeaponDefinition def = null;
        if (cannonDef != null && cannonDef.id == weaponId) def = cannonDef;
        else if (blasterDef != null && blasterDef.id == weaponId) def = blasterDef;
        else if (grenadeDef != null && grenadeDef.id == weaponId) def = grenadeDef;
        else if (lightningDef != null && lightningDef.id == weaponId) def = lightningDef;
        else if (orbitalDef  != null && orbitalDef.id  == weaponId) def = orbitalDef; 

        if (def != null && def.uiIcon != null)
            UIChestManager.NotifyItemReceived(def.uiIcon);
    }
}
