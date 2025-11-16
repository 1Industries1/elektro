using Unity.Netcode;
using UnityEngine;

public enum StepType
{
    AddDamagePct,
    AddFireRatePct,
    AddPierce,
    AddProjSizePct,
    AddCritChancePct,
    AddCritMult,
    TwinBarrel,           // +25% effektive DPS
    ImpactExplosionAug,   // Blaster: AOE am letzten Pierce (2.5m, 50% dmg)
    AddSalvoCount,
    AddRangePct,          // skaliert rangeMeters von PlasmaOrb
    AddProjSpeedPct 
}

[System.Serializable]
public class WeaponStep {
    public string name;
    public StepType type;
    public float value;   // Prozent als 0.20f
    public int intValue;  // z.B. +2 Pierce
}

[CreateAssetMenu(menuName="Game/Weapons/WeaponDefinition")]
public class WeaponDefinition : ScriptableObject {
    [Header("Meta")]
    public string id;
    public string displayName;
    public WeaponTag tags;

    [Header("UI")]
    public Sprite uiIcon; // Icon, das im Kisten-UI angezeigt wird

    [Header("Prefabs/Physik")]
    public NetworkObject bulletPrefab;
    public float projectileSpeed = 30f;
    public float rangeMeters = 20f;
    public float projectileSize = 1f;
    public float knockback = 0f;

    [Header("Baseline L1")]
    public float baseDamage = 10f;
    public float shotsPerSecond = 1f;
    public int basePierce = 0;
    public int baseSalvoCount = 1;

    [Header("Crit")]
    public float baseCritChance = 0f; // 0..1
    public float baseCritMult = 1.5f;

    [Header("Stufen (Level-1 hat 0 Steps aktiv)")]
    public WeaponStep[] steps;
}
