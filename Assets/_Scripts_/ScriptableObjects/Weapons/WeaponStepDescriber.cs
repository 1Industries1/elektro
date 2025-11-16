// WeaponStepDescriber.cs
using UnityEngine;
using System.Globalization;

public static class WeaponStepDescriber
{
    // Use invariant culture for numeric formatting (dots instead of commas)
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    /// <summary>
    /// Builds a short, readable description of what the *new* level added.
    /// Shows before→after DPS when meaningful.
    /// </summary>
    /// <param name="def">Weapon definition (must not be null when leveled).</param>
    /// <param name="newLevel">Resulting level after upgrade (>=2).</param>
    /// <param name="upgrades">Optional: player upgrades/masteries applied to runtime preview.</param>
    /// <returns>Human-readable upgrade line, or null if nothing to describe.</returns>
    public static string DescribeStep(WeaponDefinition def, int newLevel, PlayerUpgrades upgrades = null)
    {
        if (def == null || def.steps == null) return null;

        // Level 2 activates steps[0], so the step index is (newLevel - 2).
        int stepIndex = newLevel - 2;
        if (stepIndex < 0 || stepIndex >= def.steps.Length) return null;

        // Build "before" (old level) and "after" (new level) previews
        var rtOld = new WeaponRuntime(def, Mathf.Max(1, newLevel - 1));
        var rtNew = new WeaponRuntime(def, Mathf.Max(1, newLevel));

        // Apply player-wide effects (e.g., masteries)
        upgrades?.ApplyTo(rtOld);
        upgrades?.ApplyTo(rtNew);

        var s = def.steps[stepIndex];

        // Useful derived numbers
        int salvoOld = Mathf.Max(1, rtOld.salvoCount);
        int salvoNew = Mathf.Max(1, rtNew.salvoCount);

        float dmgOld = rtOld.damagePerShot;
        float dmgNew = rtNew.damagePerShot;

        float spsOld = rtOld.shotsPerSecond; // "salvos per second" for a salvo weapon
        float spsNew = rtNew.shotsPerSecond;

        float projPerSecOld = spsOld * salvoOld;
        float projPerSecNew = spsNew * salvoNew;

        float dpsOld = dmgOld * projPerSecOld;
        float dpsNew = dmgNew * projPerSecNew;

        // Choose a template and populate placeholders
        string tpl = DefaultTemplateFor(s.type);

        string result = tpl
            .Replace("{INT}", s.intValue.ToString(CI))
            .Replace("{VALUE_PCT}", Mathf.RoundToInt(s.value * 100f).ToString(CI))
            .Replace("{SALVO_OLD}", salvoOld.ToString(CI))
            .Replace("{SALVO_NEW}", salvoNew.ToString(CI))
            .Replace("{DMG_OLD}", dmgOld.ToString("0.#", CI))
            .Replace("{DMG_NEW}", dmgNew.ToString("0.#", CI))
            .Replace("{SPS_OLD}", spsOld.ToString("0.##", CI))
            .Replace("{SPS_NEW}", spsNew.ToString("0.##", CI))
            .Replace("{PROJPS_OLD}", projPerSecOld.ToString("0.##", CI))
            .Replace("{PROJPS_NEW}", projPerSecNew.ToString("0.##", CI))
            .Replace("{DPS_OLD}", dpsOld.ToString("0.#", CI))
            .Replace("{DPS_NEW}", dpsNew.ToString("0.#", CI));

        // Simple plural touches (only for the common cases we use)
        result = result
            .Replace("{SALVO_PLURAL}", Plural(salvoNew, "salvo", "salvos"))
            .Replace("{PIERCE_PLURAL}", Plural(s.intValue, "pierce", "pierces"))
            .Replace("{PROJECTILE_PLURAL}", Plural(salvoNew, "projectile", "projectiles"));

        return result;
    }

    /// <summary>
    /// Short, punchy English templates. Keep them concise (toast-style).
    /// </summary>
    private static string DefaultTemplateFor(StepType t) => t switch
    {
        StepType.AddDamagePct =>
            "+{VALUE_PCT}% damage  (DPS {DPS_OLD} → {DPS_NEW})",

        StepType.AddFireRatePct =>
            "+{VALUE_PCT}% fire rate  ({SPS_OLD} → {SPS_NEW} salvos/s, DPS {DPS_OLD} → {DPS_NEW})",

        StepType.AddPierce =>
            $"+{{INT}} {{PIERCE_PLURAL}}",

        StepType.AddProjSizePct =>
            "+{VALUE_PCT}% projectile size",

        StepType.AddCritChancePct =>
            "+{VALUE_PCT}% crit chance",

        StepType.AddCritMult =>
            "+{VALUE_PCT}% crit multiplier",

        StepType.TwinBarrel =>
            "Twin Barrel: +25% fire rate  (DPS {DPS_OLD} → {DPS_NEW})",

        StepType.ImpactExplosionAug =>
            "Impact Blast: 2.5 m AoE at last pierce (50% damage)",

        StepType.AddSalvoCount =>
            "+{INT} salvo(s)  (now {SALVO_NEW} {SALVO_PLURAL}; DPS {DPS_OLD} → {DPS_NEW})",

        StepType.AddRangePct =>
            "+{VALUE_PCT}% range",

        StepType.AddProjSpeedPct =>
            "+{VALUE_PCT}% projectile / orbit speed",

        _ => "Upgrade acquired"
    };

    public static string DescribeStepWithName(WeaponDefinition def, int newLevel, PlayerUpgrades upgrades = null)
    {
        // Body: what actually changed (falls back to a generic line)
        string body = DescribeStep(def, newLevel, upgrades) ?? "Level up!";

        // Header: weapon name + new level (TMP rich text friendly)
        string header = (def != null && !string.IsNullOrEmpty(def.displayName))
            ? $"<b>{def.displayName}</b>  <size=80%>Lv {newLevel}</size>"
            : $"<b>Weapon</b>  <size=80%>Lv {newLevel}</size>";

        return $"{header}\n{body}";
    }

    private static string Plural(int n, string singular, string plural)
        => Mathf.Abs(n) == 1 ? singular : plural;
}
