using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerUpgrades : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerMovement movement;

    [Header("Limits")]
    public int maxLevel_MaxHP     = 12;
    public int maxLevel_Armor     = 10;
    public int maxLevel_Magnet    = 10;
    public int maxLevel_MoveSpeed = 12;
    public int maxLevel_Stamina   = 10;
    public int maxLevel_DropMoreXP   = 10;
    public int maxLevel_DropMoreGold = 10;
    public int maxLevel_Luck = 10;

    [Header("Effect per level")]
    public float maxHPPerLevel             = 15f;
    public float armorFlatPerLevel         = 1.5f;
    [Range(1.01f, 1.5f)] public float magnetRangeMultPerLevel = 1.15f;
    [Range(1.01f, 1.5f)] public float moveSpeedMultPerLevel = 1.08f;
    public float staminaMaxPerLevel   = 20f;
    public float staminaRegenPerLevel = 1.5f;
    public float xpDropBonusPerLevel   = 0.1f;
    public float goldDropBonusPerLevel = 0.1f;
    public float luckBonusPerLevel = 0.1f;

    [Header("Masteries (ScriptableObjects)")]
    [SerializeField] public MasteryTier[] startingMasteries;

    [Header("Mastery Pool (für Level-Up-Karten)")]
    [SerializeField] public MasteryDefinition[] masteryPool;

    // --- Networked Upgrade Level ---
    public NetworkVariable<int> MaxHPLevel     = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> ArmorLevel     = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> MagnetLevel    = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> MoveSpeedLevel = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> StaminaLevel = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> DropMoreXPLevel   = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> DropMoreGoldLevel = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> LuckLevel = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


    // Baseline-Werte
    private float _baseMaxHP;
    private float _baseArmorFlat;
    private float _baseMoveSpeed;
    private float _baseMaxStamina;
    private float _baseStaminaRegen;

    // --- Mastery-Besitz (Server + via RPC zum Owner gespiegelt) ---
    private readonly List<MasteryTier> _ownedMasteries = new();
    public IReadOnlyList<MasteryTier> OwnedMasteries => _ownedMasteries;

    private void Awake()
    {
        if (!health)   health   = GetComponent<PlayerHealth>();
        if (!movement) movement = GetComponent<PlayerMovement>();
    }

    public override void OnNetworkSpawn()
    {
        if (health)
        {
            _baseMaxHP     = health.GetMaxHP();
            _baseArmorFlat = health.armorFlat;
        }
        if (movement)
        {
            _baseMoveSpeed = movement.moveSpeed;
            _baseMaxStamina   = movement.maxStamina;
            _baseStaminaRegen = movement.staminaRegenPerSecond;
        }

        if (IsServer)
        {
            // Starting-Masteries nur auf dem Server initialisieren
            _ownedMasteries.Clear();
            if (startingMasteries != null)
            {
                foreach (var mt in startingMasteries)
                {
                    if (mt.def == null) continue;
                    _ownedMasteries.Add(new MasteryTier
                    {
                        def  = mt.def,
                        tier = Mathf.Clamp(mt.tier, 1, 3)
                    });
                }
            }

            // initialen Besitz an Owner-Client syncen
            SyncMasteriesToOwner();
        }

        ApplyAllUpgrades();

        MaxHPLevel.OnValueChanged     += (_, __) => ApplyMaxHP();
        ArmorLevel.OnValueChanged     += (_, __) => ApplyArmor();
        MoveSpeedLevel.OnValueChanged += (_, __) => ApplyMoveSpeed();
        MagnetLevel.OnValueChanged    += (_, __) => { /* UI only */ };
        StaminaLevel.OnValueChanged   += (_, __) => ApplyStamina();
        DropMoreXPLevel.OnValueChanged   += (_, __) => { /* UI only */ };
        DropMoreGoldLevel.OnValueChanged += (_, __) => { /* UI only */ };
        LuckLevel.OnValueChanged += (_, __) => { /* UI only */ };
    }

    // ==================== Public Infos ====================

    public int GetLevel(UpgradeType type) => type switch
    {
        UpgradeType.MaxHP     => MaxHPLevel.Value,
        UpgradeType.Armor     => ArmorLevel.Value,
        UpgradeType.Magnet    => MagnetLevel.Value,
        UpgradeType.MoveSpeed => MoveSpeedLevel.Value,
        UpgradeType.Stamina   => StaminaLevel.Value,
        UpgradeType.DropMoreXP  => DropMoreXPLevel.Value,
        UpgradeType.DropMoreGold => DropMoreGoldLevel.Value,
        UpgradeType.Luck => LuckLevel.Value,
        _                     => 0
    };

    public int GetMaxLevel(UpgradeType type) => type switch
    {
        UpgradeType.MaxHP     => maxLevel_MaxHP,
        UpgradeType.Armor     => maxLevel_Armor,
        UpgradeType.Magnet    => maxLevel_Magnet,
        UpgradeType.MoveSpeed => maxLevel_MoveSpeed,
        UpgradeType.Stamina   => maxLevel_Stamina,
        UpgradeType.DropMoreXP  => maxLevel_DropMoreXP,
        UpgradeType.DropMoreGold => maxLevel_DropMoreGold,
        UpgradeType.Luck => maxLevel_Luck,
        _                     => 0
    };

    // ---------- Mastery-Infos ----------
    public int GetMasteryTier(MasteryDefinition def)
    {
        if (def == null) return 0;
        for (int i = 0; i < _ownedMasteries.Count; i++)
        {
            if (_ownedMasteries[i].def == def)
                return Mathf.Clamp(_ownedMasteries[i].tier, 0, 3);
        }
        return 0;
    }

    // ---------- Aktuelle Effektivwerte (für UI-Anzeige) ----------
    public float GetCurrentValue(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.MaxHP:
                if (health) return health.GetMaxHP();
                return Mathf.Max(1f, _baseMaxHP + MaxHPLevel.Value * maxHPPerLevel);

            case UpgradeType.Armor:
                if (health) return health.armorFlat;
                return Mathf.Max(0f, _baseArmorFlat + ArmorLevel.Value * armorFlatPerLevel);

            case UpgradeType.Magnet:
                return Mathf.Pow(magnetRangeMultPerLevel, MagnetLevel.Value);

            case UpgradeType.MoveSpeed:
                if (movement) return movement.moveSpeed;
                return Mathf.Max(0.1f, _baseMoveSpeed * Mathf.Pow(moveSpeedMultPerLevel, MoveSpeedLevel.Value));

            case UpgradeType.Stamina:
                if (movement) return movement.maxStamina;
                return Mathf.Max(0.1f, _baseMaxStamina + StaminaLevel.Value * staminaMaxPerLevel);

            case UpgradeType.DropMoreXP:
                return 1f + DropMoreXPLevel.Value * xpDropBonusPerLevel;

            case UpgradeType.DropMoreGold:
                return 1f + DropMoreGoldLevel.Value * goldDropBonusPerLevel;

            case UpgradeType.Luck:
                return 1f + LuckLevel.Value * luckBonusPerLevel;

            default:
                return 0f;
        }
    }

    // Öffentliche Abfrage für andere Systeme
    public float GetDamageMultiplier() => 1f;
    public float GetMagnetRangeMult()  => Mathf.Pow(magnetRangeMultPerLevel, MagnetLevel.Value);
    public float GetXpDropMult()   => 1f + DropMoreXPLevel.Value * xpDropBonusPerLevel;
    public float GetGoldDropMult() => 1f + DropMoreGoldLevel.Value * goldDropBonusPerLevel;
    public float GetLuckMult() => 1f + LuckLevel.Value * luckBonusPerLevel;

    public string GetCurrentDisplay(UpgradeType t) => t switch
    {
        UpgradeType.MaxHP     => $"{GetCurrentValue(t):0.#} HP",
        UpgradeType.Armor     => $"{GetCurrentValue(t):0.#} armor",
        UpgradeType.Magnet    => $"{GetCurrentValue(t):0.00}×",
        UpgradeType.MoveSpeed => $"{GetCurrentValue(t):0.##} m/s",
        UpgradeType.Stamina   => $"{GetCurrentValue(t):0.#} ST",
        UpgradeType.DropMoreXP  => $"{(GetCurrentValue(t) - 1f) * 100f:0.#}% XP",
        UpgradeType.DropMoreGold => $"{(GetCurrentValue(t) - 1f) * 100f:0.#}% Gold",
        UpgradeType.Luck => $"{(GetCurrentValue(t) - 1f) * 100f:0.#}% Luck",
        _                     => GetCurrentValue(t).ToString("0.##")
    };

    public float GetCurrentValueAtLevel(UpgradeType type, int level)
    {
        level = Mathf.Max(0, level);

        switch (type)
        {
            case UpgradeType.MaxHP:
                return Mathf.Max(1f, _baseMaxHP + level * maxHPPerLevel);
            case UpgradeType.Armor:
                return Mathf.Max(0f, _baseArmorFlat + level * armorFlatPerLevel);
            case UpgradeType.Magnet:
                return Mathf.Pow(magnetRangeMultPerLevel, level);
            case UpgradeType.MoveSpeed:
                return Mathf.Max(0.1f, _baseMoveSpeed * Mathf.Pow(moveSpeedMultPerLevel, level));
            case UpgradeType.Stamina:
                return Mathf.Max(0.1f, _baseMaxStamina + level * staminaMaxPerLevel);
            case UpgradeType.DropMoreXP:
                return 1f + level * xpDropBonusPerLevel;
            case UpgradeType.DropMoreGold:
                return 1f + level * goldDropBonusPerLevel;
            case UpgradeType.Luck:
                return 1f + level * luckBonusPerLevel;
            default:
                return 0f;
        }
    }

    // ==================== Vergabe (kostenlos) ====================

    [ServerRpc(RequireOwnership = false)]
    public void GrantUpgradeServerRpc(UpgradeType type, int levels, ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (sender != OwnerClientId) return;
        PurchaseFreeLevel_Server(type, levels);
    }

    // Encoded Upgrade (Stat ODER Mastery) – nur Wrapper, Owner-Check
    [ServerRpc(RequireOwnership = false)]
    public void GrantEncodedUpgradeServerRpc(int encodedId, ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (sender != OwnerClientId) return;

        ApplyEncodedChoice_Server(encodedId);
    }

    public void PurchaseFreeLevel_Server(UpgradeType type, int levels)
    {
        if (!IsServer || levels <= 0) return;

        int cur  = GetLevel(type);
        int max  = GetMaxLevel(type);
        int give = Mathf.Clamp(levels, 0, max - cur);

        for (int i = 0; i < give; i++)
        {
            switch (type)
            {
                case UpgradeType.MaxHP:     MaxHPLevel.Value++;     break;
                case UpgradeType.Armor:     ArmorLevel.Value++;     break;
                case UpgradeType.Magnet:    MagnetLevel.Value++;    break;
                case UpgradeType.MoveSpeed: MoveSpeedLevel.Value++; break;
                case UpgradeType.Stamina:   StaminaLevel.Value++;   break;
                case UpgradeType.DropMoreXP:  DropMoreXPLevel.Value++;  break;
                case UpgradeType.DropMoreGold: DropMoreGoldLevel.Value++; break;
                case UpgradeType.Luck: LuckLevel.Value++; break;
            }
        }
    }

    /// <summary>
    /// Server-seitige Anwendung eines encodeten Upgrades (Stat ODER Mastery).
    /// Wird z.B. von PlayerXP auf dem Server aufgerufen.
    /// </summary>
    public void ApplyEncodedChoice_Server(int encodedId)
    {
        if (!IsServer || encodedId == 0) return;

        int baseId = ChoiceCodec.BaseId(encodedId);
        var rarity = ChoiceCodec.GetRarity(encodedId);
        int stacks = UpgradeRoller.StacksPerRarity.TryGetValue(rarity, out var s) ? s : 1;

        // Mastery
        if (UpgradeRoller.IsMasteryBaseId(baseId))
        {
            if (UpgradeRoller.TryResolveMasteryBaseId(this, baseId, out var mDef))
            {
                int addStacks = Mathf.Max(1, stacks);
                AddMasteryLevels(mDef, addStacks);
            }
            return;
        }

        // ===== Waffe =====
        if (UpgradeRoller.IsWeaponBaseId(baseId))
        {
            // Waffen werden NICHT über LevelUp/encoded vergeben.
            return;
        }

        // Stat-Upgrade
        var type = UpgradeRoller.Resolve(baseId);
        int add  = Mathf.Max(1, stacks);
        PurchaseFreeLevel_Server(type, add);
    }

    // ==================== Anwenden ====================
    private void ApplyAllUpgrades()
    {
        ApplyMaxHP();
        ApplyArmor();
        ApplyMoveSpeed();
        ApplyStamina();
    }

    private void ApplyMaxHP()
    {
        if (!health) return;
        if (!IsServer) return; // nur Server darf Stats setzen

        int   lvl    = MaxHPLevel.Value;
        float newMax = Mathf.Max(1f, _baseMaxHP + lvl * maxHPPerLevel);
        health.Server_SetMaxHP(newMax, keepRelativeRatio: true);
    }

    private void ApplyArmor()
    {
        if (!health) return;
        if (!IsServer) return; // Stats werden serverseitig gesetzt

        int   lvl      = ArmorLevel.Value;
        float newArmor = Mathf.Max(0f, _baseArmorFlat + lvl * armorFlatPerLevel);
        health.armorFlat = newArmor;
    }

    private void ApplyMoveSpeed()
    {
        if (!movement) return;
        int lvl = MoveSpeedLevel.Value;
        float mult = Mathf.Pow(moveSpeedMultPerLevel, lvl);
        movement.moveSpeed = Mathf.Max(0.1f, _baseMoveSpeed * mult);
    }
    
    private void ApplyStamina()
    {
        if (!movement) return;

        int lvl = StaminaLevel.Value;
        float newMaxStamina   = Mathf.Max(1f, _baseMaxStamina   + lvl * staminaMaxPerLevel);
        float newStaminaRegen = Mathf.Max(0f, _baseStaminaRegen + lvl * staminaRegenPerLevel);

        movement.maxStamina           = newMaxStamina;
        movement.staminaRegenPerSecond = newStaminaRegen;
    }

    // === WeaponRuntime-Hook ===
    public void ApplyTo(WeaponRuntime rt)
    {
        if (rt == null) return;

        // Alle aktuell besessenen Masteries anwenden
        foreach (var mt in _ownedMasteries)
        {
            if (mt.def != null)
                rt.AddMastery(mt.def, Mathf.Clamp(mt.tier, 1, 3));
        }
    }


    // ==================== Weapon-Upgrade Toasts (LevelUp / überall) ====================
    public void SignalWeaponUpgradeToast(WeaponDefinition def)
    {
        if (!IsServer || def == null) return;

        var pw = GetComponent<PlayerWeapons>() ?? GetComponentInChildren<PlayerWeapons>(true);
        if (pw == null) return;

        int newLevel = 1;

        if (def == pw.cannonDef)         newLevel = pw.cannonLevel.Value;
        else if (def == pw.blasterDef)   newLevel = pw.blasterLevel.Value;
        else if (def == pw.grenadeDef)   newLevel = pw.grenadeLevel.Value;
        else if (def == pw.lightningDef) newLevel = pw.lightningLevel.Value;
        else if (def == pw.orbitalDef)   newLevel = pw.orbitalLevel.Value;
        else if (def == pw.blackHoleDef) newLevel = pw.blackHoleLevel.Value;

        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };

        WeaponUpgradeToastClientRpc(def.id, newLevel, target);
    }


    [ClientRpc(Delivery = RpcDelivery.Reliable)]
    private void WeaponUpgradeToastClientRpc(string weaponId, int newLevel, ClientRpcParams rpcParams = default)
    {
        // Nur der eigentliche Owner zeigt UI
        if (!IsOwner) return;

        var pw = GetComponent<PlayerWeapons>() ?? GetComponentInChildren<PlayerWeapons>(true);
        var up = this;
        if (pw == null) return;

        WeaponDefinition def = null;
        if (pw.cannonDef   != null && pw.cannonDef.id   == weaponId) def = pw.cannonDef;
        else if (pw.blasterDef  != null && pw.blasterDef.id  == weaponId) def = pw.blasterDef;
        else if (pw.grenadeDef  != null && pw.grenadeDef.id  == weaponId) def = pw.grenadeDef;
        else if (pw.lightningDef!= null && pw.lightningDef.id== weaponId) def = pw.lightningDef;
        else if (pw.orbitalDef   != null && pw.orbitalDef.id   == weaponId) def = pw.orbitalDef;
        else if (pw.blackHoleDef != null && pw.blackHoleDef.id == weaponId) def = pw.blackHoleDef;

        if (def == null) return;

        string msg = WeaponStepDescriber.DescribeStepWithName(def, newLevel, up);
        CenterToastUI.Instance?.Show(msg, 4f);
    }


    // ==================== Mastery intern ====================

    private void AddMasteryLevels(MasteryDefinition def, int stacks)
    {
        if (def == null || stacks <= 0) return;

        int idx = -1;
        for (int i = 0; i < _ownedMasteries.Count; i++)
        {
            if (_ownedMasteries[i].def == def)
            {
                idx = i;
                break;
            }
        }

        MasteryTier mt;
        if (idx >= 0)
        {
            mt = _ownedMasteries[idx];
        }
        else
        {
            mt = new MasteryTier { def = def, tier = 0 };
        }

        mt.tier = Mathf.Clamp(mt.tier + stacks, 1, 3);

        if (idx >= 0) _ownedMasteries[idx] = mt;
        else _ownedMasteries.Add(mt);

        // Waffen auf dem Server neu berechnen
        var pw = GetComponent<PlayerWeapons>() ?? GetComponentInChildren<PlayerWeapons>(true);
        if (pw != null) pw.ForceRebuild();

        // neue Masteries an Owner-Client syncen
        SyncMasteriesToOwner();
    }

    private void SyncMasteriesToOwner()
    {
        if (!IsServer || !IsSpawned) return;

        // Wir packen alle Masteries in einen String:
        // "id1:tier1;id2:tier2;id3:tier3"
        var entries = new List<string>(_ownedMasteries.Count);
        foreach (var mt in _ownedMasteries)
        {
            if (mt.def == null) continue;
            int tier = Mathf.Clamp(mt.tier, 1, 3);
            entries.Add($"{mt.def.id}:{tier}");
        }

        string packed = string.Join(";", entries);

        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };

        OwnerSyncMasteriesClientRpc(packed, rpcParams);
    }

    // WICHTIG: kein string[] / int[] mehr, sondern nur ein string
    [ClientRpc]
    private void OwnerSyncMasteriesClientRpc(string packed, ClientRpcParams rpcParams = default)
    {
        // nur der lokale Owner interessiert sich
        if (!IsOwner) return;

        _ownedMasteries.Clear();

        if (string.IsNullOrEmpty(packed))
            return;

        // Format: "id1:tier1;id2:tier2;..."
        string[] entries = packed.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in entries)
        {
            var parts = entry.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) continue;

            string idStr = parts[0];
            if (!int.TryParse(parts[1], out int tier)) continue;

            var def = GetMasteryDefById(idStr);
            if (def == null) continue;

            _ownedMasteries.Add(new MasteryTier
            {
                def  = def,
                tier = Mathf.Clamp(tier, 1, 3)
            });
        }

        // lokale Waffen-Runtimes mit neuen Masteries neu berechnen
        var pw = GetComponent<PlayerWeapons>() ?? GetComponentInChildren<PlayerWeapons>(true);
        if (pw != null) pw.ForceRebuild();
    }

    private MasteryDefinition GetMasteryDefById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (startingMasteries != null)
        {
            foreach (var mt in startingMasteries)
            {
                if (mt.def != null && mt.def.id == id) return mt.def;
            }
        }

        if (masteryPool != null)
        {
            foreach (var m in masteryPool)
            {
                if (m != null && m.id == id) return m;
            }
        }

        return null;
    }
}

[Serializable]
public struct MasteryTier
{
    public MasteryDefinition def; // ScriptableObject
    [Range(1, 3)] public int tier;
}


public enum UpgradeType : int
{
    MaxHP = 3,
    Armor = 4,
    Magnet = 6,
    MoveSpeed = 7,
    Stamina = 8,
    DropMoreXP = 9,
    DropMoreGold = 10,
    Luck = 11
}

