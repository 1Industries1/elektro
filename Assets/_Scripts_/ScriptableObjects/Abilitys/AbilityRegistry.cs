using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Abilities/AbilityRegistry")]
public class AbilityRegistry : ScriptableObject
{
    public List<AbilityDefinition> abilities = new();

    public AbilityDefinition Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < abilities.Count; i++)
            if (abilities[i] != null && abilities[i].id == id) return abilities[i];
        return null;
    }

    public bool Has(string id) => Get(id) != null;
}
