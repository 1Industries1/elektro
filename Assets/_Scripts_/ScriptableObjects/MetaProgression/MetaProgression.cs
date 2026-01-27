using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MetaProgression : MonoBehaviour
{
    public static MetaProgression I { get; private set; }

    [Header("Config")]
    public WeaponRegistry registry;
    public AbilityRegistry abilityRegistry;
    public int startCurrency = 0;

    public MetaData Data { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, "meta.json");

    [Serializable]
    public class MetaData
    {
        public int metaCurrency;
        public int metaXP;
        public int metaLevel = 1;

        public List<string> unlockedWeaponIds = new();
        public List<string> unlockedAbilityIds = new();

        public string active1, active2, passive1, passive2; // Waffen
        public string ability1, ability2;                   // Abilities
    }

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        LoadOrCreate();
    }

    public void LoadOrCreate()
    {
        if (File.Exists(SavePath))
        {
            Data = JsonUtility.FromJson<MetaData>(File.ReadAllText(SavePath));
        }
        else
        {
            Data = new MetaData { metaCurrency = startCurrency };
            Save();
        }

        // Safety: entferne IDs die es nicht mehr gibt
        Data.unlockedWeaponIds.RemoveAll(id => registry && !registry.Has(id));
        Data.unlockedAbilityIds.RemoveAll(id => abilityRegistry && !abilityRegistry.Has(id));
        Data.metaLevel = Mathf.Max(1, Data.metaLevel);
        Data.metaXP = Mathf.Max(0, Data.metaXP);
    }

    public void Save()
    {
        var json = JsonUtility.ToJson(Data, true);
        File.WriteAllText(SavePath, json);
    }

    public bool IsUnlocked(string weaponId) => Data.unlockedWeaponIds.Contains(weaponId);
    public bool IsAbilityUnlocked(string abilityId) => Data.unlockedAbilityIds.Contains(abilityId);

    public void UnlockWeapon(string weaponId)
    {
        if (!IsUnlocked(weaponId))
        {
            Data.unlockedWeaponIds.Add(weaponId);
            Save();
        }
    }

    public void UnlockAbility(string abilityId)
    {
        if (!IsAbilityUnlocked(abilityId))
        {
            Data.unlockedAbilityIds.Add(abilityId);
            Save();
        }
    }

    public void SetLoadout(string a1, string a2, string p1, string p2, string b1, string b2)
    {
        Data.active1 = a1; Data.active2 = a2; Data.passive1 = p1; Data.passive2 = p2;
        Data.ability1 = b1; Data.ability2 = b2;
        Save();
    }

    public int GetMetaXpToNext(int baseCost, float costMult)
    {
        // gleiche Formel wie PlayerXP
        double pow = Math.Pow(costMult, Math.Max(0, Data.metaLevel - 1));
        return Mathf.Max(1, Mathf.RoundToInt((float)(baseCost * pow)));
    }

    /// <summary>
    /// Fügt Meta-XP hinzu, löst ggf. mehrere Meta-LevelUps aus und speichert.
    /// </summary>
    public bool AddMetaXP(int amount, int baseCost, float costMult)
    {
        if (amount <= 0) return false;

        Data.metaLevel = Mathf.Max(1, Data.metaLevel);
        Data.metaXP = Mathf.Max(0, Data.metaXP);

        Data.metaXP += amount;

        bool leveled = false;
        int toNext = GetMetaXpToNext(baseCost, costMult);

        while (Data.metaXP >= toNext)
        {
            Data.metaXP -= toNext;
            Data.metaLevel++;
            leveled = true;

            toNext = GetMetaXpToNext(baseCost, costMult);
        }

        Save();
        return leveled;
    }

}
