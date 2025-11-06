using System;
using System.Collections.Generic;
using UnityEngine;



public static class UpgradeRoller
{
    // ======== Basis-IDs (stabil) für Stats ========
    public const int MaxHP     = 4;
    public const int Armor     = 5;
    public const int Magnet    = 7;
    public const int MoveSpeed = 8;

    // Alle passiven Stat-Upgrades
    private static readonly int[] StatPool = { MaxHP, Armor, Magnet, MoveSpeed };

    // ======== Mastery-ID-Bereich ========
    // Wir mappen masteryPool[i] → baseId = MASTERIES_OFFSET + i
    private const int MASTERIES_OFFSET = 1000;

    public static bool IsMasteryBaseId(int baseId) => baseId >= MASTERIES_OFFSET;
    public static bool IsMasteryChoice(int choiceId) => IsMasteryBaseId(ChoiceCodec.BaseId(choiceId));

    /// <summary>
    /// BaseId → MasteryDefinition aus dem PlayerUpgrades.masteryPool auflösen.
    /// </summary>
    public static bool TryResolveMasteryBaseId(PlayerUpgrades up, int baseId, out MasteryDefinition def)
    {
        def = null;
        if (up == null || up.masteryPool == null) return false;
        int idx = baseId - MASTERIES_OFFSET;
        if (idx < 0 || idx >= up.masteryPool.Length) return false;

        def = up.masteryPool[idx];
        return def != null;
    }

    // ======== Rarity-Konfig ========

    // Stacks, die eine Rarity gewährt (für "stapelnde" Upgrades/Masteries)
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

    public struct ChoiceViewModel
    {
        public string upgradeKey;    // z.B. "upgrade.maxHP" oder "mastery.pierce"
        public string upgradeName;   // finaler Anzeigename
        public Rarity rarity;
        public int    stacks;        // 1..n
        public string badgeText;     // "Common", "Rare", "Epic", "LEGENDARY"
        public Color  rarityColor;
        public Sprite rarityIcon;
    }

    // Farbcodes für Rarities
    public static readonly Dictionary<Rarity, Color> RarityColors = new()
    {
        { Rarity.Common,    Hex("#000000ff") },
        { Rarity.Rare,      Hex("#2c80eeff") },
        { Rarity.Epic,      Hex("#952BFFFF") },
        { Rarity.Legendary, Hex("#FCC653FF") },
    };

    public static readonly Dictionary<Rarity, string> RarityBadge = new()
    {
        { Rarity.Common,    "Common"     },
        { Rarity.Rare,      "Rare"       },
        { Rarity.Epic,      "Epic"       },
        { Rarity.Legendary, "LEGENDARY"  },
    };

    private static readonly Dictionary<Rarity, Sprite> _rarityIcons = new();

    public static void RegisterRarityIcon(Rarity rarity, Sprite icon)
    {
        if (icon == null) { _rarityIcons.Remove(rarity); return; }
        _rarityIcons[rarity] = icon;
    }

    // ======== 3 gültige Optionen rollen (Stats + Masteries) ========

    public static int[] Roll3Valid(PlayerUpgrades up)
    {
        if (up == null)
            up = UnityEngine.Object.FindObjectOfType<PlayerUpgrades>();

        var statCandidates    = new List<int>(StatPool.Length);
        var masteryCandidates = new List<int>();

        // --- Stats: nur wenn noch nicht max ---
        foreach (var id in StatPool)
        {
            var t = Resolve(id);
            if (up.GetLevel(t) < up.GetMaxLevel(t))
                statCandidates.Add(id);
        }

        // Wenn alle Stats capped → trotzdem alle erlauben (damit nie leere Liste)
        if (statCandidates.Count == 0)
            statCandidates.AddRange(StatPool);

        // --- Masteries: aus masteryPool, nur < Tier 3 ---
        if (up.masteryPool != null)
        {
            for (int i = 0; i < up.masteryPool.Length; i++)
            {
                var def = up.masteryPool[i];
                if (def == null) continue;

                int curTier = up.GetMasteryTier(def);
                const int maxTier = 3;
                if (curTier >= maxTier) continue;

                int baseId = MASTERIES_OFFSET + i;
                masteryCandidates.Add(baseId);
            }
        }

        // Kombinierter Pool: Stats + Masteries
        var allCandidates = new List<int>();
        allCandidates.AddRange(statCandidates);
        allCandidates.AddRange(masteryCandidates);

        // Fallback: sollte eigentlich nie leer sein
        if (allCandidates.Count == 0)
            allCandidates.AddRange(StatPool);

        // Fisher–Yates-Shuffle
        var r   = new System.Random();
        var bag = new List<int>(allCandidates);
        for (int i = bag.Count - 1; i > 0; i--)
        {
            int j = r.Next(i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }

        // bis zu drei einzigartige Basis-IDs
        var picks = new List<int>(3);
        foreach (var id in bag)
        {
            if (!picks.Contains(id))
                picks.Add(id);
            if (picks.Count == 3) break;
        }
        while (picks.Count < 3)
            picks.Add(allCandidates[r.Next(allCandidates.Count)]);

        int EncodeClamped(int baseId)
        {
            // ----- Mastery -----
            if (IsMasteryBaseId(baseId))
            {
                if (!TryResolveMasteryBaseId(up, baseId, out var mDef))
                    return ChoiceCodec.Encode(MaxHP, Rarity.Common); // Fallback

                int curTier = up.GetMasteryTier(mDef);
                const int maxTier = 3;
                if (curTier >= maxTier)
                    return ChoiceCodec.Encode(MaxHP, Rarity.Common);

                var rarity = WeightedRarity(r);
                int stacks = StacksPerRarity.TryGetValue(rarity, out var s) ? s : 1;
                stacks = Mathf.Min(stacks, maxTier - curTier);
                if (stacks <= 0) { rarity = Rarity.Common; stacks = 1; }

                rarity = RarityForStacks(stacks);
                return ChoiceCodec.Encode(baseId, rarity);
            }

            // ----- Stat -----
            var type = Resolve(baseId);
            int cur  = up.GetLevel(type);
            int max  = up.GetMaxLevel(type);

            if (cur >= max)
                return ChoiceCodec.Encode(baseId, Rarity.Common);

            var rarityStat = WeightedRarity(r);
            int stacksStat = StacksPerRarity.TryGetValue(rarityStat, out var sStat) ? sStat : 1;
            stacksStat = Mathf.Min(stacksStat, max - cur);
            if (stacksStat <= 0) { rarityStat = Rarity.Common; stacksStat = 1; }

            rarityStat = RarityForStacks(stacksStat);
            return ChoiceCodec.Encode(baseId, rarityStat);
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
        MaxHP     => UpgradeType.MaxHP,
        Armor     => UpgradeType.Armor,
        Magnet    => UpgradeType.Magnet,
        MoveSpeed => UpgradeType.MoveSpeed,
        _         => UpgradeType.MaxHP
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

    public static ChoiceViewModel GetChoiceViewModel(int choiceId, PlayerUpgrades up, Func<string, string> localize = null)
    {
        int baseId = ChoiceCodec.BaseId(choiceId);
        var rarity = ChoiceCodec.GetRarity(choiceId);
        int stacks = StacksForChoice(choiceId);

        // ---- Mastery-Karte ----
        if (IsMasteryBaseId(baseId) && up != null && TryResolveMasteryBaseId(up, baseId, out var mDef))
        {
            string key  = $"mastery.{mDef.id}";
            string name = !string.IsNullOrEmpty(mDef.displayName) ? mDef.displayName : "Mastery";
            if (localize != null)
            {
                var loc = localize(key);
                if (!string.IsNullOrEmpty(loc)) name = loc;
            }

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

        // ---- Stat-Karte ----
        string statKey  = UpgradeKey(baseId);
        string statName = UpgradeNameFallback(baseId);
        if (localize != null)
        {
            var loc = localize(statKey);
            if (!string.IsNullOrEmpty(loc)) statName = loc;
        }

        return new ChoiceViewModel
        {
            upgradeKey   = statKey,
            upgradeName  = statName,
            rarity       = rarity,
            stacks       = stacks,
            badgeText    = RarityBadge[rarity],
            rarityColor  = RarityColors[rarity],
            rarityIcon   = _rarityIcons.TryGetValue(rarity, out var spr2) ? spr2 : null
        };
    }

    public static string UpgradeKey(int baseId) => baseId switch
    {
        MaxHP     => "upgrade.maxHP",
        Armor     => "upgrade.armor",
        Magnet    => "upgrade.magnet",
        MoveSpeed => "upgrade.moveSpeed",
        _         => "upgrade.generic"
    };

    public static string UpgradeNameFallback(int baseId) => baseId switch
    {
        MaxHP     => "Max HP",
        Armor     => "Armor",
        Magnet    => "Magnet",
        MoveSpeed => "Move Speed",
        _         => "Upgrade"
    };

    // Wird noch von Logs / Debug verwendet – Stats + sehr generischer Fallback für Masteries
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
        string hex = r switch
        {
            Rarity.Common    => "#ffffffff",
            Rarity.Rare      => "#0071aaff",
            Rarity.Epic      => "#952bffff",
            Rarity.Legendary => "#fcc653ff",
            _                => "#ffffffff"
        };
        return $"<color={hex}>";
    }

    // ======== intern ========

    private static Rarity WeightedRarity(System.Random r)
    {
        float x   = (float)r.NextDouble();
        float acc = 0f;

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
