using System;
using System.Collections.Generic;
using UnityEngine;

public static class UpgradeRoller
{
    // ======== Basis-IDs (stabil) ========
    public const int FireRate     = 1;
    public const int AltFire      = 2;
    public const int Range        = 3;
    public const int MaxHP        = 4;
    public const int Damage       = 5;
    public const int GrenadeSalvo = 6;
    public const int Magnet       = 7;
    public const int MoveSpeed    = 8;

    private static readonly int[] pool = { FireRate, AltFire, Range, MaxHP, Damage, GrenadeSalvo, Magnet, MoveSpeed };

    // ======== Rarity-Konfig ========
    // Stacks, die eine Rarity gewährt (für "stapelnde" Upgrades)
    public static readonly Dictionary<Rarity, int> StacksPerRarity = new()
    {
        { Rarity.Common,    1 },
        { Rarity.Rare,      2 },
        { Rarity.Epic,      3 },
        { Rarity.Legendary, 4 }
    };

    // Ziehungsgewichte der Rarities (Summe ~ 1.0)
    public static readonly Dictionary<Rarity, float> Weights = new()
    {
        { Rarity.Common,    0.65f },
        { Rarity.Rare,      0.25f },
        { Rarity.Epic,      0.09f },
        { Rarity.Legendary, 0.01f }
    };

    // ======== Präsentation (Farben/Badges/Icons) ========
    // --> UI soll kein <color> in Strings mehr bekommen. Siehe ChoiceViewModel.

    public struct ChoiceViewModel
    {
        public string upgradeKey;    // z.B. "upgrade.fireRate" (Lokalisierungsschlüssel)
        public string upgradeName;   // Fallback-Anzeige, falls kein Loca-System
        public Rarity rarity;
        public int    stacks;        // 1..n
        public string badgeText;     // "", "(I)", "(II)", "(III)"
        public Color  rarityColor;   // UI kann direkt Farbe setzen
        public Sprite rarityIcon;    // optionales Emblem
    }

    // Farbcodes für Rarities (entspricht bisherigen Hexwerten)
    public static readonly Dictionary<Rarity, Color> RarityColors = new()
    {
        { Rarity.Common,    Hex("#000000ff") },
        { Rarity.Rare,      Hex("#2c80eeff") },
        { Rarity.Epic,      Hex("#952BFFFF") },
        { Rarity.Legendary, Hex("#FCC653FF") },
    };

    // Text-Badges (ersetzt "star")
    public static readonly Dictionary<Rarity, string> RarityBadge = new()
    {
        { Rarity.Common,    "Common"     },
        { Rarity.Rare,      "Rare"  },
        { Rarity.Epic,      "Epic" },
        { Rarity.Legendary, "LEGENDARY"},
    };

    // Optional: Icons pro Rarity (Designer-Asset)
    private static readonly Dictionary<Rarity, Sprite> _rarityIcons = new();

    public static void RegisterRarityIcon(Rarity rarity, Sprite icon)
    {
        if (icon == null) { _rarityIcons.Remove(rarity); return; }
        _rarityIcons[rarity] = icon;
    }

    // ======== Ziehen von 3 gültigen Optionen ========
    public static int[] Roll3Valid(PlayerUpgrades up)
    {
        var valid = new List<int>(pool.Length);
        foreach (var id in pool)
        {
            var t = Resolve(id);
            if (up.GetLevel(t) < up.GetMaxLevel(t)) valid.Add(id);
        }
        if (valid.Count == 0) valid.AddRange(pool);

        var r = new System.Random();

        // Fisher–Yates auf Kopie
        var bag = new List<int>(valid);
        for (int i = bag.Count - 1; i > 0; i--)
        {
            int j = r.Next(i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }

        // bis zu drei einzigartige Basis-IDs
        var picks = new List<int>(3);
        foreach (var id in bag)
        {
            picks.Add(id);
            if (picks.Count == 3) break;
        }
        while (picks.Count < 3) picks.Add(valid[r.Next(valid.Count)]);

        int EncodeClamped(int baseId)
        {
            var type = Resolve(baseId);
            int cur  = up.GetLevel(type);
            int max  = up.GetMaxLevel(type);

            if (cur >= max)
                return ChoiceCodec.Encode(baseId, Rarity.Common);

            var rarity = WeightedRarity(r);
            int stacks = Mathf.Min(StacksPerRarity[rarity], max - cur);
            if (stacks <= 0) { rarity = Rarity.Common; stacks = 1; }
            rarity = RarityForStacks(stacks);
            return ChoiceCodec.Encode(baseId, rarity);
        }

        return new[]
        {
            EncodeClamped(picks[0]),
            EncodeClamped(picks[1]),
            EncodeClamped(picks[2])
        };
    }

    // ======== Mapping / Hilfen ========
    public static UpgradeType Resolve(int baseId) => baseId switch
    {
        FireRate     => UpgradeType.FireRate,
        AltFire      => UpgradeType.AltFireRate,
        Range        => UpgradeType.TargetRange,
        MaxHP        => UpgradeType.MaxHP,
        Damage       => UpgradeType.Damage,
        GrenadeSalvo => UpgradeType.GrenadeSalvo,
        Magnet       => UpgradeType.Magnet,
        MoveSpeed    => UpgradeType.MoveSpeed,
        _            => UpgradeType.MaxHP
    };

    public static UpgradeType ResolveFromChoice(int choiceId) => Resolve(ChoiceCodec.BaseId(choiceId));

    public static int StacksForChoice(int choiceId)
    {
        var rarity = ChoiceCodec.GetRarity(choiceId);
        return StacksPerRarity.TryGetValue(rarity, out var s) ? s : 1;
    }

    public static Rarity RarityForStacks(int stacks)
    {
        foreach (var kv in StacksPerRarity)
            if (kv.Value == stacks) return kv.Key;
        return Rarity.Common;
    }

    // ======== Präsentations-API für deine UI ========
    public static ChoiceViewModel GetChoiceViewModel(int choiceId, Func<string, string> localize = null)
    {
        int baseId   = ChoiceCodec.BaseId(choiceId);
        var rarity   = ChoiceCodec.GetRarity(choiceId);
        int stacks   = StacksForChoice(choiceId);
        string key   = UpgradeKey(baseId);
        string name  = localize != null ? localize(key) : UpgradeNameFallback(baseId);

        return new ChoiceViewModel
        {
            upgradeKey   = key,
            upgradeName  = name,
            rarity       = rarity,
            stacks       = stacks,
            badgeText    = RarityBadge[rarity],
            rarityColor  = RarityColors[rarity],
            rarityIcon   = _rarityIcons.TryGetValue(rarity, out var spr) ? spr : null
        };
    }

    public static string UpgradeKey(int baseId) => baseId switch
    {
        FireRate     => "upgrade.fireRate",
        AltFire      => "upgrade.altFireRate",
        Range        => "upgrade.targetRange",
        MaxHP        => "upgrade.maxHP",
        Damage       => "upgrade.damage",
        GrenadeSalvo => "upgrade.grenadeSalvo",
        Magnet       => "upgrade.magnet",
        MoveSpeed    => "upgrade.moveSpeed",
        _            => "upgrade.generic"
    };

    public static string UpgradeNameFallback(int baseId) => baseId switch
    {
        FireRate     => "Fire Rate",
        AltFire      => "Fire Rate Blaster",
        Range        => "Attack Range",
        MaxHP        => "Max HP",
        Damage       => "Damage",
        GrenadeSalvo => "Grenade Salvo",
        Magnet       => "Magnet",
        MoveSpeed    => "Move Speed",
        _            => "Upgrade"
    };


    // Falls noch irgendwo im Projekt gebraucht (z. B. alte UI-Pfade) -> PlayerXP verwendet das noch
    public static string Label(int choiceId)
    {
        var baseId = ChoiceCodec.BaseId(choiceId);
        var rarity = ChoiceCodec.GetRarity(choiceId);

        string baseLabel = UpgradeNameFallback(baseId);
        string badge     = RarityBadge[rarity];

        return $"{ColorTag(rarity)}{baseLabel} {badge}</color>";
    }

    public static string ColorTag(Rarity r)
    {
        // Behalte bisherige Hexfarben exakt bei.
        string hex = r switch
        {
            Rarity.Common    => "#ffffffff",
            Rarity.Rare      => "#0071aaff",
            Rarity.Epic      => "#952bffff",
            Rarity.Legendary => "#fcc653ff",
            _ => "#ffffffff"
        };
        return $"<color={hex}>";
    }

    // ======== intern ========
    private static Rarity WeightedRarity(System.Random r)
    {
        // Iteriere in definierter Reihenfolge, damit deterministisch
        float x = (float)r.NextDouble();
        float acc = 0f;

        // feste Reihenfolge
        var order = new[] { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary };
        foreach (var rr in order)
        {
            acc += Weights[rr];
            if (x <= acc) return rr;
        }
        return Rarity.Common;
    }

    private static Color Hex(string rgba)
    {
        if (ColorUtility.TryParseHtmlString(rgba, out var c)) return c;
        return Color.white;
    }
}
