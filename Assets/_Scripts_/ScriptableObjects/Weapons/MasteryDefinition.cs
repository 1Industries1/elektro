using UnityEngine;

[CreateAssetMenu(menuName="Game/Upgrades/Mastery")]
public class MasteryDefinition : ScriptableObject {
    public string id;
    public string displayName;

    [Header("Filter")]
    public WeaponTag requiredAny;       // mind. einer
    public WeaponTag requiredAll;       // alle
    public bool requirePiercedOnce;     // Pierce-Mastery-Bedingung

    [Header("Tierwerte (Index 0..2 für Tier 1..3)")]
    public float[] damagePctByTier;     // z.B. {0.08f,0.07f,0.05f}
    public float[] critChanceAdd;       // z.B. {0.03f,0.02f,0.02f}
    public float[] critMultAdd;         // z.B. {0.2f,0.2f,0.1f}

    // Du kannst hier weitere Felder für Tracking, TurnRate etc. ergänzen.
}

public static class MasteryEval {
    public static bool Matches(WeaponTag tags, MasteryDefinition m, bool piercedAtLeastOnce) {
        if (m.requiredAny != 0 && (tags & m.requiredAny) == 0) return false;
        if (m.requiredAll != 0 && (tags & m.requiredAll) != m.requiredAll) return false;
        if (m.requirePiercedOnce && !piercedAtLeastOnce) return false;
        return true;
    }
}
