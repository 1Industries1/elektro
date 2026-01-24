using UnityEngine;

public enum AbilitySlotKind { Ability } // optional, falls du später mehr willst

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

    // Effekt-Container: wir re-use OverclockRuntime!
    public OverclockDef overclockEffect;   // enthält z.B. Override 0.07 + duration
}
