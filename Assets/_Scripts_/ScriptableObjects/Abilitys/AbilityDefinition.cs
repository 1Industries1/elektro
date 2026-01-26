using UnityEngine;

public enum AbilityEffectKind { Ability, Shield }

[CreateAssetMenu(menuName = "Game/Abilities/AbilityDefinition")]
public class AbilityDefinition : ScriptableObject
{
    [Header("Meta")]
    public string id;                 // "RateCap007"
    public string displayName;
    public Sprite uiIcon;
    [TextArea] public string description;
    public int unlockCost = 0;

    [Header("Gameplay")]
    public float cooldownSeconds = 20f;

    [Header("Effect Kind")]
    public AbilityEffectKind effectKind = AbilityEffectKind.Ability;

    [Header("Overclock")]
    public OverclockDef overclockEffect;   // enthält z.B. Override 0.07 + duration

    [Header("Shield")]
    public GameObject shieldVfxPrefab;   // EarthShield Prefab
    public float shieldDuration = 5f;    // wie lange aktiv
}
