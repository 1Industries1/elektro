using System;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerUpgrades : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private CannonController cannon;
    [SerializeField] private BlasterController altWeapon;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private GrenadeLauncherController grenadeLauncher;
    [SerializeField] private PlayerMovement movement;

    [Header("Limits")]
    public int maxLevel_FireRate       = 0;  // deaktiviert – RoF kommt aus WeaponRuntime
    public int maxLevel_AltFireRate    = 0;  // deaktiviert – RoF kommt aus WeaponRuntime
    public int maxLevel_TargetRange    = 12;
    public int maxLevel_MaxHP          = 12;
    public int maxLevel_Damage         = 15;
    public int maxLevel_GrenadeSalvo   = 8;
    public int maxLevel_Magnet         = 10;
    public int maxLevel_MoveSpeed      = 12;

    [Header("Effect per level")]
    [Tooltip("Legacy (nicht mehr aktiv verwendet)")]
    [Range(0.5f, 0.99f)] public float fireRateMultPerLevel    = 0.85f;
    [Range(0.5f, 0.99f)] public float altFireRateMultPerLevel = 0.85f;
    public float targetRangePerLevel   = 10f;                 // +10 m/Level
    public float maxHPPerLevel         = 15f;                 // +15 HP/Level
    [Range(1.01f, 1.5f)] public float damageMultPerLevel      = 1.15f; // +15% Damage
    public const int GrenadePerLevel   = 1;
    [Range(1.01f, 1.5f)] public float magnetRangeMultPerLevel = 1.15f; // Magnet: Reichweite ×
    [Range(1.01f, 1.5f)] public float moveSpeedMultPerLevel   = 1.08f; // Move: Speed ×

    [Header("Masteries (ScriptableObjects)")]
    [SerializeField] public MasteryTier[] startingMasteries;

    // --- Networked Upgrade Level ---
    public NetworkVariable<int> FireRateLevel       = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> AltFireRateLevel    = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> RangeLevel          = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> MaxHPLevel          = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> DamageLevel         = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> GrenadeSalvoLevel   = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> MagnetLevel         = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> MoveSpeedLevel      = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Baseline-Werte für nicht-WeaponRuntime-Stats
    private float _baseTargetRange;
    private float _baseMaxHP;
    private int _baseGrenadeSalvo;
    private float _baseMoveSpeed;

    private void Awake()
    {
        if (!cannon) cannon = GetComponentInChildren<CannonController>(true);
        if (!altWeapon) altWeapon = GetComponentInChildren<BlasterController>(true);
        if (!health) health = GetComponent<PlayerHealth>();
        if (!movement) movement = GetComponent<PlayerMovement>();
    }

    public override void OnNetworkSpawn()
    {
        if (cannon != null)
            _baseTargetRange = cannon.targetRange;

        if (health != null)
            _baseMaxHP = health.GetMaxHP();

        if (grenadeLauncher != null)
            _baseGrenadeSalvo = grenadeLauncher.salvoCount;

        if (movement != null)
            _baseMoveSpeed = movement.moveSpeed;

        ApplyAllUpgrades();

        // RoF-Events deaktiviert – WeaponRuntime regelt das
        // FireRateLevel.OnValueChanged      += (_, __) => ApplyFireRate();
        // AltFireRateLevel.OnValueChanged   += (_, __) => ApplyAltFireRate();

        RangeLevel.OnValueChanged         += (_, __) => ApplyRange();
        MaxHPLevel.OnValueChanged         += (_, __) => ApplyMaxHP();
        DamageLevel.OnValueChanged        += (_, __) => { /* UI hook */ };
        GrenadeSalvoLevel.OnValueChanged  += (_, __) => ApplyGrenadeSalvo();
        MoveSpeedLevel.OnValueChanged     += (_, __) => ApplyMoveSpeed();
        MagnetLevel.OnValueChanged        += (_, __) => { /* UI-only */ };
    }

    // ==================== Public Infos ====================

    public int GetLevel(UpgradeType type) => type switch
    {
        UpgradeType.FireRate      => FireRateLevel.Value,
        UpgradeType.AltFireRate   => AltFireRateLevel.Value,
        UpgradeType.TargetRange   => RangeLevel.Value,
        UpgradeType.MaxHP         => MaxHPLevel.Value,
        UpgradeType.Damage        => DamageLevel.Value,
        UpgradeType.GrenadeSalvo  => GrenadeSalvoLevel.Value,
        UpgradeType.Magnet        => MagnetLevel.Value,
        UpgradeType.MoveSpeed     => MoveSpeedLevel.Value,
        _ => 0
    };

    public int GetMaxLevel(UpgradeType type) => type switch
    {
        UpgradeType.FireRate      => maxLevel_FireRate,
        UpgradeType.AltFireRate   => maxLevel_AltFireRate,
        UpgradeType.TargetRange   => maxLevel_TargetRange,
        UpgradeType.MaxHP         => maxLevel_MaxHP,
        UpgradeType.Damage        => maxLevel_Damage,
        UpgradeType.GrenadeSalvo  => maxLevel_GrenadeSalvo,
        UpgradeType.Magnet        => maxLevel_Magnet,
        UpgradeType.MoveSpeed     => maxLevel_MoveSpeed,
        _ => 0
    };

    // ---------- Aktuelle Effektivwerte (für UI-Anzeige) ----------
    public float GetCurrentValue(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.FireRate:

            case UpgradeType.AltFireRate:
                // RoF wird nun in WeaponRuntime berechnet – für UI hier neutraler Wert
                return 0f;

            case UpgradeType.TargetRange:
                if (cannon) return cannon.targetRange;
                return Mathf.Max(0f, _baseTargetRange + RangeLevel.Value * targetRangePerLevel);

            case UpgradeType.MaxHP:
                if (health) return health.GetMaxHP();
                return Mathf.Max(1f, _baseMaxHP + MaxHPLevel.Value * maxHPPerLevel);

            case UpgradeType.Damage:
                return Mathf.Pow(damageMultPerLevel, DamageLevel.Value);

            case UpgradeType.GrenadeSalvo:
            {
                int baseVal = grenadeLauncher ? grenadeLauncher.salvoCount : Mathf.Max(1, _baseGrenadeSalvo);
                baseVal = (_baseGrenadeSalvo > 0 ? _baseGrenadeSalvo : baseVal);
                return Mathf.Max(1, baseVal + GrenadePerLevel * GrenadeSalvoLevel.Value);
            }

            case UpgradeType.Magnet:
                return Mathf.Pow(magnetRangeMultPerLevel, MagnetLevel.Value);
     
            case UpgradeType.MoveSpeed:
                if (movement) return movement.moveSpeed;
                return Mathf.Max(0.1f, _baseMoveSpeed * Mathf.Pow(moveSpeedMultPerLevel, MoveSpeedLevel.Value));

            default:
                return 0f;
        }
    }

    // Öffentliche Abfrage für GL
    public int GetGrenadeSalvoBonus() => GrenadePerLevel * GrenadeSalvoLevel.Value;

    // Einheitlicher Multiplikator
    public float GetDamageMultiplier() => Mathf.Pow(damageMultPerLevel, DamageLevel.Value);
    public float GetMagnetRangeMult() => Mathf.Pow(magnetRangeMultPerLevel, MagnetLevel.Value);

    private void ApplyGrenadeSalvo()
    {
        if (!grenadeLauncher) return;
        int baseVal = (_baseGrenadeSalvo > 0) ? _baseGrenadeSalvo : grenadeLauncher.salvoCount;
        grenadeLauncher.salvoCount = Mathf.Max(1, baseVal + GrenadePerLevel * GrenadeSalvoLevel.Value);
    }

    // === Legacy-Damage-Range-Getter entfernt (nutze Runtime für UI, falls gewünscht) ===

    public string GetCurrentDisplay(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.FireRate:
            case UpgradeType.AltFireRate:
                return "runtime";
            case UpgradeType.TargetRange:
                return $"{GetCurrentValue(type):0.#} m";
            case UpgradeType.MaxHP:
                return $"{GetCurrentValue(type):0.#} HP";
            case UpgradeType.Damage:
                return $"{GetCurrentValue(type):0.00}×";
            case UpgradeType.GrenadeSalvo:
                return $"{(int)GetCurrentValue(type)}×";
            case UpgradeType.Magnet:
                return $"{GetCurrentValue(type):0.00}×";
            case UpgradeType.MoveSpeed:
                return $"{GetCurrentValue(type):0.##} m/s";
            default:
                return GetCurrentValue(type).ToString("0.##");
        }
    }
    
    public float GetCurrentValueAtLevel(UpgradeType type, int level)
    {
        level = Mathf.Max(0, level);

        switch (type)
        {
            case UpgradeType.FireRate:
            case UpgradeType.AltFireRate:
                // nicht mehr skaliert – WeaponRuntime übernimmt
                return 0f;

            case UpgradeType.TargetRange:
                return Mathf.Max(0f, _baseTargetRange + level * targetRangePerLevel);
            case UpgradeType.MaxHP:
                return Mathf.Max(1f, _baseMaxHP + level * maxHPPerLevel);
            case UpgradeType.Damage:
                return Mathf.Pow(damageMultPerLevel, level);
            case UpgradeType.GrenadeSalvo:
            {
                int baseVal = (_baseGrenadeSalvo > 0)
                    ? _baseGrenadeSalvo
                    : (grenadeLauncher ? grenadeLauncher.salvoCount : 1);
                return Mathf.Max(1, baseVal + GrenadePerLevel * level);
            }
            case UpgradeType.Magnet:
                return Mathf.Pow(magnetRangeMultPerLevel, level);
            case UpgradeType.MoveSpeed:
                return Mathf.Max(0.1f, _baseMoveSpeed * Mathf.Pow(moveSpeedMultPerLevel, level));
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

    public void PurchaseFreeLevel_Server(UpgradeType type, int levels)
    {
        if (!IsServer) return;
        if (levels <= 0) return;
        
        int cur = GetLevel(type);
        int max = GetMaxLevel(type);
        int give = Mathf.Clamp(levels, 0, max - cur);
        for (int i = 0; i < give; i++)
        {
            switch (type)
            {
                case UpgradeType.FireRate:     FireRateLevel.Value++;     break;
                case UpgradeType.AltFireRate:  AltFireRateLevel.Value++;  break;
                case UpgradeType.TargetRange:  RangeLevel.Value++;        break;
                case UpgradeType.MaxHP:        MaxHPLevel.Value++;        break;
                case UpgradeType.Damage:       DamageLevel.Value++;       break;
                case UpgradeType.GrenadeSalvo: GrenadeSalvoLevel.Value++; break;
                case UpgradeType.Magnet:       MagnetLevel.Value++;       break;
                case UpgradeType.MoveSpeed:    MoveSpeedLevel.Value++;    break;
            }
        }
    }

    // ==================== Anwenden ====================
    private void ApplyAllUpgrades()
    {
        // ApplyFireRate();      // deaktiviert – WeaponRuntime steuert RoF
        // ApplyAltFireRate();   // deaktiviert – WeaponRuntime steuert RoF
        ApplyRange();
        ApplyMaxHP();
        ApplyGrenadeSalvo();
        ApplyMoveSpeed();
    }

    // Legacy-Stubs, falls irgendwo noch aufgerufen:
    private void ApplyFireRate()  { /* noop: WeaponRuntime */ }
    private void ApplyAltFireRate(){ /* noop: WeaponRuntime */ }

    private void ApplyRange()
    {
        if (!cannon) return;
        int lvl = RangeLevel.Value;
        cannon.targetRange = Mathf.Max(0f, _baseTargetRange + lvl * targetRangePerLevel);
    }

    private void ApplyMaxHP()
    {
        if (!health) return;
        if (!IsServer) return; // nur Server darf Stats setzen

        int lvl = MaxHPLevel.Value;
        float newMax = Mathf.Max(1f, _baseMaxHP + lvl * maxHPPerLevel);
        health.Server_SetMaxHP(newMax, keepRelativeRatio: true);
    }

    private void ApplyMoveSpeed()
    {
        if (!movement) return;
        int lvl = MoveSpeedLevel.Value;
        float mult = Mathf.Pow(moveSpeedMultPerLevel, lvl);
        movement.moveSpeed = Mathf.Max(0.1f, _baseMoveSpeed * mult);
    }

    // === WeaponRuntime-Hook ===
    public void ApplyTo(WeaponRuntime rt)
    {
        if (rt == null) return;
        if (startingMasteries != null)
        {
            foreach (var mt in startingMasteries)
            {
                if (mt.def != null)
                    rt.AddMastery(mt.def, Mathf.Clamp(mt.tier, 1, 3));
            }
        }
    }
}

[Serializable]
public struct MasteryTier {
    public MasteryDefinition def; // ScriptableObject
    [Range(1,3)] public int tier; // 1..3
}

public enum UpgradeType : int
{
    FireRate = 0,
    AltFireRate = 1,
    TargetRange = 2,
    MaxHP = 3,
    Damage = 4,
    GrenadeSalvo = 5,
    Magnet = 6,
    MoveSpeed = 7
}


// SCHON VORHANDEN

// MoveSpeed
// MaxHP
// Magnet
// FireRate (wird in SO gemacht)





// FEHLT

// Jump Force
// Masterys
// Lucky Find
// XP Siphon
// (Parts Salvage)





// MUSS HIER RAUS

// Damage
// AltFireRate
// TargetRange