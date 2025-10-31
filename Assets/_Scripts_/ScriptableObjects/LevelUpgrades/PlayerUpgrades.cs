using System;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerUpgrades : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerMovement movement;

    [Header("Limits")]
    public int maxLevel_MaxHP        = 12;
    public int maxLevel_Magnet       = 10;
    public int maxLevel_MoveSpeed = 12;
    

    [Header("Effect per level")]
    public float maxHPPerLevel         = 15f;
    [Range(1.01f, 1.5f)] public float magnetRangeMultPerLevel = 1.15f;
    [Range(1.01f, 1.5f)] public float moveSpeedMultPerLevel = 1.08f;


    [Header("Masteries (ScriptableObjects)")]
    [SerializeField] public MasteryTier[] startingMasteries;
    

    // --- Networked Upgrade Level ---
    public NetworkVariable<int> MaxHPLevel         = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> MagnetLevel        = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> MoveSpeedLevel     = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    

    // Baseline-Werte für nicht-WeaponRuntime-Stats
    private float _baseTargetRange;
    private float _baseMaxHP;
    private float _baseMoveSpeed;


    private void Awake()
    {
        if (!health) health = GetComponent<PlayerHealth>();
        if (!movement) movement = GetComponent<PlayerMovement>();
    }

    
    public override void OnNetworkSpawn()
    {
        if (health)         _baseMaxHP       = health.GetMaxHP();
        if (movement)       _baseMoveSpeed   = movement.moveSpeed;

        ApplyAllUpgrades();

        MaxHPLevel.OnValueChanged        += (_, __) => ApplyMaxHP();
        MoveSpeedLevel.OnValueChanged    += (_, __) => ApplyMoveSpeed();
        MagnetLevel.OnValueChanged       += (_, __) => { /* UI only */ };
    }

    // ==================== Public Infos ====================

    public int GetLevel(UpgradeType type) => type switch
    {
        UpgradeType.MaxHP         => MaxHPLevel.Value,
        UpgradeType.Magnet        => MagnetLevel.Value,
        UpgradeType.MoveSpeed     => MoveSpeedLevel.Value,
        _ => 0
    };

    public int GetMaxLevel(UpgradeType type) => type switch
    {
        UpgradeType.MaxHP         => maxLevel_MaxHP,
        UpgradeType.Magnet        => maxLevel_Magnet,
        UpgradeType.MoveSpeed     => maxLevel_MoveSpeed,
        _ => 0
    };

    // ---------- Aktuelle Effektivwerte (für UI-Anzeige) ----------
    public float GetCurrentValue(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.MaxHP:
                if (health) return health.GetMaxHP();
                return Mathf.Max(1f, _baseMaxHP + MaxHPLevel.Value * maxHPPerLevel);

            case UpgradeType.Magnet:
                return Mathf.Pow(magnetRangeMultPerLevel, MagnetLevel.Value);

            case UpgradeType.MoveSpeed:
                if (movement) return movement.moveSpeed;
                return Mathf.Max(0.1f, _baseMoveSpeed * Mathf.Pow(moveSpeedMultPerLevel, MoveSpeedLevel.Value));
            default: return 0f;
        }
    }


    // Öffentliche Abfrage
    // Die anderen Waffen haben noch kein SO daher hier noch alte Werte nehmen
    public float GetDamageMultiplier() => 1f;
    public float GetMagnetRangeMult() => Mathf.Pow(magnetRangeMultPerLevel, MagnetLevel.Value);




    public string GetCurrentDisplay(UpgradeType t) => t switch
    {
        UpgradeType.MaxHP        => $"{GetCurrentValue(t):0.#} HP",
        UpgradeType.Magnet       => $"{GetCurrentValue(t):0.00}×",
        UpgradeType.MoveSpeed    => $"{GetCurrentValue(t):0.##} m/s",
        _ => GetCurrentValue(t).ToString("0.##")
    };
    
    public float GetCurrentValueAtLevel(UpgradeType type, int level)
    {
        level = Mathf.Max(0, level);

        switch (type)
        {
            case UpgradeType.MaxHP:
                return Mathf.Max(1f, _baseMaxHP + level * maxHPPerLevel);
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
        if (!IsServer || levels <= 0) return;
        int cur = GetLevel(type), max = GetMaxLevel(type);
        int give = Mathf.Clamp(levels, 0, max - cur);
        for (int i = 0; i < give; i++)
        {
            switch (type)
            {
                case UpgradeType.MaxHP:        MaxHPLevel.Value++;        break;
                case UpgradeType.Magnet:       MagnetLevel.Value++;       break;
                case UpgradeType.MoveSpeed:    MoveSpeedLevel.Value++;    break;
            }
        }
    }

    // ==================== Anwenden ====================
    private void ApplyAllUpgrades()
    {
        ApplyMaxHP();
        ApplyMoveSpeed();
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
    MaxHP = 3,
    Magnet = 6,
    MoveSpeed = 7
}
