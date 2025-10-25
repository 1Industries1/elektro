using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Klassen für Loot-Logik des Gegners (Auswirkung auf Drop-Wahrscheinlichkeit).
/// </summary>
public enum EnemyClass { Normal, Elite, Boss }

/// <summary>
/// Reine Utility-Klasse (kein MonoBehaviour, nicht in die Szene ziehen).
/// Würfelt serverseitig, ob und welcher Overclock dropt.
/// </summary>
public static class OverclockLoot
{
    // --- Basischancen je Kill ---
    // Normal: 1% Dropchance
    public const float NORMAL_ROLL = 0.02f;
    // Elite:  40% Dropchance
    public const float ELITE_ROLL  = 0.40f;

    // --- Typverteilungen bei erfolgreichem Roll ---
    // Normal: 80% Instant, 20% Tactical
    private static readonly (OverclockKind kind, float w)[] NormalDist = {
        (OverclockKind.Instant, 0.8f),
        (OverclockKind.Tactical, 0.2f)
    };

    // Elite: 60% Instant, 40% Tactical
    private static readonly (OverclockKind kind, float w)[] EliteDist = {
        (OverclockKind.Instant, 0.6f),
        (OverclockKind.Tactical, 0.4f)
    };

    /// <summary>
    /// Haupt-Wurf:
    /// - Normal/Elite: erst Dropchance, dann Verteilung auf Kind
    /// - Boss: garantiert „Loot“, 50% Ultimate, 50% Legendary Catalyst (kein Overclock)
    /// Rückgabe: true = es gibt einen Drop (Overclock oder Catalyst)
    /// </summary>
    public static bool TryRoll(EnemyClass cls, System.Random rng, out OverclockKind kind, out bool bossCatalyst)
    {
        kind = OverclockKind.Instant;
        bossCatalyst = false;

        switch (cls)
        {
            case EnemyClass.Normal:
                // Kein Drop: direkt abbrechen
                if (rng.NextDouble() > NORMAL_ROLL) return false;
                // Drop ja: verteilten Overclock wählen
                kind = Weighted(NormalDist, rng);
                return true;

            case EnemyClass.Elite:
                if (rng.NextDouble() > ELITE_ROLL) return false;
                kind = Weighted(EliteDist, rng);
                return true;

            case EnemyClass.Boss:
                // Garantiert Loot:
                //  - 50% Ultimate-Overclock
                //  - 50% Legendary Catalyst (kein Overclock-Pickup)
                if (rng.NextDouble() < 0.5)
                {
                    kind = OverclockKind.Ultimate;
                    bossCatalyst = false;
                }
                else
                {
                    bossCatalyst = true;
                }
                return true;
        }

        // Unbekannter EnemyClass — kein Drop
        return false;
    }

    /// <summary>
    /// Wählt aus einer gewichteten Liste (kind, weight) anhand rng ein Element aus.
    /// </summary>
    private static OverclockKind Weighted((OverclockKind kind, float w)[] arr, System.Random rng)
    {
        // Summe der Gewichte
        double sum = 0;
        foreach (var a in arr) sum += a.w;
        // Sicherheitsnetz: leere/0-Summe => Standard-Fallback
        if (sum <= 0.0) return arr.Length > 0 ? arr[0].kind : OverclockKind.Instant;

        // Zufallswert in [0, sum)
        double r = rng.NextDouble() * sum;
        double acc = 0;

        foreach (var a in arr)
        {
            acc += a.w;
            if (r <= acc) return a.kind;
        }

        // Numerischer Fallback (sollte nie passieren)
        return arr[^1].kind;
    }
}
