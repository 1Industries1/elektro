using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Weapons/WeaponRegistry")]
public class WeaponRegistry : ScriptableObject
{
    public List<WeaponDefinition> weapons = new();

    private Dictionary<string, WeaponDefinition> _byId;

    private void OnEnable()
    {
        _byId = new Dictionary<string, WeaponDefinition>();
        foreach (var w in weapons)
        {
            if (!w || string.IsNullOrEmpty(w.id)) continue;
            _byId[w.id] = w;
        }
    }

    public WeaponDefinition Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_byId == null) OnEnable();
        _byId.TryGetValue(id, out var def);
        return def;
    }

    public bool Has(string id) => Get(id) != null;
}
