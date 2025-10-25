using System;
using System.Collections.Generic;
using UnityEngine;

public enum OverclockKind { Instant, Tactical, Ultimate }
public enum Stat { FireRateSeconds, MoveSpeed, DamageMultiplier }
public enum ModMode { Multiply, Add, Override }

[Serializable]
public struct StatMod
{
    public Stat stat;            // z.B. FireRateSeconds
    public ModMode mode;         // Multiply, Add, Override
    public float value;          // z.B. 0.8f (-20%) oder Override-Wert
}

[CreateAssetMenu(menuName="Game/Overclocks/OverclockDef")]
public class OverclockDef : ScriptableObject
{
    [Header("Meta")]
    public string id;                   // "Electro", "RateBoost", ...
    public OverclockKind kind;
    public Sprite icon;
    [TextArea] public string description;

    [Header("Timing")]
    public float duration = 15f;        // aktive Laufzeit
    public float afterEffectDuration = 0f; // 0 = keiner

    [Header("Effects")]
    public List<StatMod> mods = new();
    public List<StatMod> afterMods = new(); // z.B. Überhitzung: FireRateSeconds * 1.2

    [Header("Taktisch")]
    [Min(1)] public int tacticalCharges = 1; // 1–2
}
