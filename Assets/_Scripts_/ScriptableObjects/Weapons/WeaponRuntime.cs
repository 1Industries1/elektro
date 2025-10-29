using UnityEngine;
using System.Collections.Generic;

public sealed class WeaponRuntime {
    public WeaponDefinition def { get; }
    public int level { get; private set; }

    public int MaxLevel => 1 + (def.steps?.Length ?? 0);
    public bool CanLevelUp() => level < MaxLevel;
    public void LevelUp() => SetLevel(level + 1);

    // Effektive Werte
    public WeaponTag tags;
    public float damagePerShot;
    public float shotsPerSecond;
    public int pierce;
    public float projectileSpeed;
    public float projectileSize;
    public float critChance;
    public float critMult;

    // Augments
    public bool hasImpactExplosionAug;

    private readonly List<(MasteryDefinition mastery, int tier)> _masteries = new();

    public WeaponRuntime(WeaponDefinition def, int level = 1) {
        this.def = def;
        SetLevel(level);
    }

    public void SetLevel(int newLevel) {
        level = Mathf.Max(1, newLevel);
        Recompute();
    }

    public void AddMastery(MasteryDefinition m, int tier) {
        _masteries.Add((m, Mathf.Clamp(tier, 1, 3)));
        Recompute();
    }

    private void Recompute() {
        // Baseline
        tags            = def.tags;
        damagePerShot   = def.baseDamage;
        shotsPerSecond  = def.shotsPerSecond;
        pierce          = def.basePierce;
        projectileSpeed = def.projectileSpeed;
        projectileSize  = def.projectileSize;
        critChance      = def.baseCritChance;
        critMult        = def.baseCritMult;
        hasImpactExplosionAug = false;

        // Steps bis Level anwenden (Level 1 = 0 Steps)
        int stepsToApply = Mathf.Min(def.steps?.Length ?? 0, Mathf.Max(0, level - 1));
        for (int i = 0; i < stepsToApply; i++) {
            var s = def.steps[i];
            switch (s.type) {
                case StepType.AddDamagePct:    damagePerShot   *= (1f + s.value); break;
                case StepType.AddFireRatePct:  shotsPerSecond  *= (1f + s.value); break;
                case StepType.AddPierce:       pierce          += s.intValue;     break;
                case StepType.AddProjSizePct:  projectileSize  *= (1f + s.value); break;
                case StepType.AddCritChancePct:critChance      += s.value;        break;
                case StepType.AddCritMult:     critMult        += s.value;        break;
                case StepType.TwinBarrel:      shotsPerSecond  *= 1.25f;          break;
                case StepType.ImpactExplosionAug:
                    hasImpactExplosionAug = true;
                    tags |= WeaponTag.Explosive; // Explosive Mastery darf greifen
                    break;
            }
        }

        // Masteries (kontextlos – d.h. ohne „piercedOnce“) erstmal statisch auf Basisstats
        foreach (var (m, tier) in _masteries) {
            if (!MasteryEval.Matches(tags, m, piercedAtLeastOnce:false)) continue;
            int idx = Mathf.Clamp(tier - 1, 0, 2);
            if (m.damagePctByTier != null && m.damagePctByTier.Length > idx) damagePerShot *= (1f + m.damagePctByTier[idx]);
            if (m.critChanceAdd   != null && m.critChanceAdd.Length   > idx) critChance    += m.critChanceAdd[idx];
            if (m.critMultAdd     != null && m.critMultAdd.Length     > idx) critMult      += m.critMultAdd[idx];
        }

        // Clamps
        shotsPerSecond = Mathf.Max(0.05f, shotsPerSecond);
        critChance     = Mathf.Clamp01(critChance);
        critMult       = Mathf.Max(1f, critMult);
        pierce         = Mathf.Max(0, pierce);
    }

    public float GetCooldownSeconds() => 1f / shotsPerSecond;

    // Für Treffer, die NACH mind. 1× Pierce stattfinden (Pierce Mastery!)
    public float ComputeDamagePierced(bool applyCrit) {
        float dmg = damagePerShot;

        // nur Masteries mit requirePiercedOnce true
        foreach (var (m, tier) in _masteries) {
            if (!m.requirePiercedOnce) continue;
            if (!MasteryEval.Matches(tags, m, piercedAtLeastOnce:true)) continue;
            int idx = Mathf.Clamp(tier - 1, 0, 2);
            if (m.damagePctByTier != null && m.damagePctByTier.Length > idx)
                dmg *= (1f + m.damagePctByTier[idx]);
        }

        if (applyCrit && Random.value < critChance) dmg *= critMult;
        return dmg;
    }

    public float ComputeDamageNonPierced(bool applyCrit) {
        float dmg = damagePerShot;
        if (applyCrit && Random.value < critChance) dmg *= critMult;
        return dmg;
    }
}
