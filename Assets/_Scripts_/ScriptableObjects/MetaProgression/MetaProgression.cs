using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MetaProgression : MonoBehaviour
{
    public static MetaProgression I { get; private set; }

    [Header("Config")]
    public WeaponRegistry registry;
    public int startCurrency = 0;

    public MetaData Data { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, "meta.json");

    [Serializable]
    public class MetaData
    {
        public int metaCurrency;
        public List<string> unlockedWeaponIds = new();
        public string active1, active2, passive1, passive2; // Loadout
        // metaUpgrades später: Dictionary<string,int> (Unity JsonUtility kann kein Dictionary direkt)
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
    }

    public void Save()
    {
        var json = JsonUtility.ToJson(Data, true);
        File.WriteAllText(SavePath, json);
    }

    public bool IsUnlocked(string weaponId) => Data.unlockedWeaponIds.Contains(weaponId);

    public void UnlockWeapon(string weaponId)
    {
        if (!IsUnlocked(weaponId))
        {
            Data.unlockedWeaponIds.Add(weaponId);
            Save();
        }
    }

    public void SetLoadout(string a1, string a2, string p1, string p2)
    {
        Data.active1 = a1; Data.active2 = a2; Data.passive1 = p1; Data.passive2 = p2;
        Save();
    }
}
