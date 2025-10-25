using System;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerUpgrades : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private CannonController cannon;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private GrenadeLauncherController grenadeLauncher;

    [Header("Limits")]
    public int maxLevel_FireRate       = 10;
    public int maxLevel_AltFireRate    = 10;
    public int maxLevel_TargetRange    = 12;
    public int maxLevel_MaxHP          = 12;
    public int maxLevel_Damage  = 15;
    public int maxLevel_GrenadeSalvo = 8;

    [Header("Effect per level")]
    [Tooltip("FireRate sinkt multiplikativ pro Stufe (kleiner = schneller)")]
    [Range(0.5f, 0.99f)] public float fireRateMultPerLevel    = 0.85f; // 15% schneller/Level
    [Range(0.5f, 0.99f)] public float altFireRateMultPerLevel = 0.85f; // 15% schneller/Level
    public float targetRangePerLevel   = 10f;                 // +10 m/Level
    public float maxHPPerLevel         = 15f;                  // +15 HP/Level
    [Range(1.01f, 1.5f)] public float damageMultPerLevel = 1.15f; // +15% Damage
    public const int GrenadePerLevel = 1;


    // --- Networked Upgrade Level ---
    public NetworkVariable<int> FireRateLevel       = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> AltFireRateLevel    = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> RangeLevel          = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> MaxHPLevel          = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> DamageLevel         = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> GrenadeSalvoLevel   = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Baseline-Werte (aus Komponenten gelesen)
    private float _baseFireRate;
    private float _baseAltFireRate;
    private float _baseTargetRange;
    private float _baseMaxHP;
    private int _baseGrenadeSalvo;

    private void Awake()
    {
        if (!cannon) cannon = GetComponentInChildren<CannonController>(true);
        if (!health) health = GetComponent<PlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        if (cannon != null)
        {
            _baseFireRate    = cannon.fireRate;
            _baseAltFireRate = cannon.altFireRate;
            _baseTargetRange = cannon.targetRange;
        }
        if (health != null)
        {
            _baseMaxHP = health.GetMaxHP();
        }
        if (grenadeLauncher != null)
        {
            _baseGrenadeSalvo = grenadeLauncher.salvoCount;
        }
        ApplyAllUpgrades();

        FireRateLevel.OnValueChanged      += (_, __) => ApplyFireRate();
        AltFireRateLevel.OnValueChanged   += (_, __) => ApplyAltFireRate();
        RangeLevel.OnValueChanged         += (_, __) => ApplyRange();
        MaxHPLevel.OnValueChanged         += (_, __) => ApplyMaxHP();
        DamageLevel.OnValueChanged        += (_, __) => { /* UI hook */ };
        GrenadeSalvoLevel.OnValueChanged  += (_, __) => ApplyGrenadeSalvo();
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
        _ => 0
    };

    // ---------- Aktuelle Effektivwerte (für UI-Anzeige) ----------
    public float GetCurrentValue(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.FireRate:
                if (cannon) return cannon.fireRate;
                return Mathf.Max(0.01f, _baseFireRate * Mathf.Pow(fireRateMultPerLevel, FireRateLevel.Value));

            case UpgradeType.AltFireRate:
                if (cannon) return cannon.altFireRate;
                return Mathf.Max(0.1f, _baseAltFireRate * Mathf.Pow(altFireRateMultPerLevel, AltFireRateLevel.Value));

            case UpgradeType.TargetRange:
                if (cannon) return cannon.targetRange;
                return Mathf.Max(0f, _baseTargetRange + RangeLevel.Value * targetRangePerLevel);

            case UpgradeType.MaxHP:
                if (health) return health.GetMaxHP();
                return Mathf.Max(1f, _baseMaxHP + MaxHPLevel.Value * maxHPPerLevel); // Hier wird MAX HP Addiert???

            case UpgradeType.Damage:
                return Mathf.Pow(damageMultPerLevel, DamageLevel.Value);

            case UpgradeType.GrenadeSalvo:
                {
                    int baseVal = grenadeLauncher ? grenadeLauncher.salvoCount : Mathf.Max(1, _baseGrenadeSalvo);
                    baseVal = (_baseGrenadeSalvo > 0 ? _baseGrenadeSalvo : baseVal);
                    return Mathf.Max(1, baseVal + GrenadePerLevel * GrenadeSalvoLevel.Value);
                }

            default:
                return 0f;
        }
    }

    // Öffentliche Abfrage für GL
    public int GetGrenadeSalvoBonus() => GrenadePerLevel * GrenadeSalvoLevel.Value;


    private void ApplyGrenadeSalvo()
    {
        if (!grenadeLauncher) return;
        int baseVal = (_baseGrenadeSalvo > 0) ? _baseGrenadeSalvo : grenadeLauncher.salvoCount;
        grenadeLauncher.salvoCount = Mathf.Max(1, baseVal + GrenadePerLevel * GrenadeSalvoLevel.Value);
    }




    // === Damage-Ranges (mit aktuellem Upgrade-Level) ===
    public Vector2 GetPrimaryDamageRangeCurrent()
    {
        Vector2 baseRange = cannon ? cannon.primaryDamageRange : new Vector2(20f, 40f);
        float mult = GetDamageMultiplier();
        return new Vector2(baseRange.x * mult, baseRange.y * mult);
    }
    public Vector2 GetAltDamageRangeCurrent()
    {
        float baseDmg = cannon ? cannon.altBaseDamage : 2f;
        float mult = GetDamageMultiplier();
        return new Vector2(baseDmg * 1f * mult, baseDmg * 3f * mult);
    }
    public Vector2 GetPrimaryDamageRangeAtLevel(int level)
    {
        Vector2 baseRange = cannon ? cannon.primaryDamageRange : new Vector2(20f, 40f);
        float mult = Mathf.Pow(damageMultPerLevel, Mathf.Max(0, level));
        return new Vector2(baseRange.x * mult, baseRange.y * mult);
    }
    public Vector2 GetAltDamageRangeAtLevel(int level)
    {
        float baseDmg = cannon ? cannon.altBaseDamage : 2f;
        float mult = Mathf.Pow(damageMultPerLevel, Mathf.Max(0, level));
        return new Vector2(baseDmg * 1f * mult, baseDmg * 3f * mult);
    }

    // Einheitlicher Multiplikator
    public float GetDamageMultiplier() => Mathf.Pow(damageMultPerLevel, DamageLevel.Value);

    public string GetCurrentDisplay(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.FireRate:
            case UpgradeType.AltFireRate: return $"{GetCurrentValue(type):0.00}s";
            case UpgradeType.TargetRange: return $"{GetCurrentValue(type):0.#} m";
            case UpgradeType.MaxHP:       return $"{GetCurrentValue(type):0.#} HP";

            case UpgradeType.Damage:
            {
                Vector2 p = GetPrimaryDamageRangeCurrent();
                Vector2 a = GetAltDamageRangeCurrent();
                // kompakte Doppelanzeige
                return $"P: {p.x:0.#}–{p.y:0.#} | A: {a.x:0.#}–{a.y:0.#}";
            }
            case UpgradeType.GrenadeSalvo:
                return $"{(int)GetCurrentValue(type)}×";

            default:
                return GetCurrentValue(type).ToString("0.##");
        }
    }

    // ==================== Vergabe (kostenlos) ====================

    /// <summary>
    /// Gratis-Upgrade vom Client anfragen (Owner-gesichert).
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void GrantUpgradeServerRpc(UpgradeType type, int levels, ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (sender != OwnerClientId) return;
        PurchaseFreeLevel_Server(type, levels);
    }

    /// <summary>
    /// Erhöht ein Level ohne Kostenprüfung. Nur Server.
    /// </summary>
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
            }
        }
    }

    // ==================== Anwenden ====================
    private void ApplyAllUpgrades()
    {
        ApplyFireRate();
        ApplyAltFireRate();
        ApplyRange();
        ApplyMaxHP();
        // Damage hat keine direkte Komponente zu setzen; Multiplier werden von der Waffe abgefragt
        ApplyGrenadeSalvo();
    }

    private void ApplyFireRate()
    {
        if (!cannon) return;
        int lvl = FireRateLevel.Value;
        float mult = Mathf.Pow(fireRateMultPerLevel, lvl);
        cannon.fireRate = Mathf.Max(0.01f, _baseFireRate * mult);
    }

    private void ApplyAltFireRate()
    {
        if (!cannon) return;
        int lvl = AltFireRateLevel.Value;
        float mult = Mathf.Pow(altFireRateMultPerLevel, lvl);
        cannon.altFireRate = Mathf.Max(0.1f, _baseAltFireRate * mult);
    }

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

        // API am PlayerHealth nutzen (setzt netzwerktauglich, hält Ratio wenn gewünscht)
        health.Server_SetMaxHP(newMax, keepRelativeRatio: true);
    }

}

public enum UpgradeType : int
{
    FireRate = 0,
    AltFireRate = 1,
    TargetRange = 2,
    MaxHP = 3,
    Damage = 4,
    GrenadeSalvo = 5
}
