using Unity.Netcode;
using UnityEngine;

public class AbilitiesHUDController : MonoBehaviour
{
    [Header("UI Slots")]
    public AbilityHUDSlotUI slot0;
    public AbilityHUDSlotUI slot1;

    [Header("Refs")]
    public AbilityRegistry abilityRegistry;

    private PlayerAbilities playerAbilities;
    private string id0, id1;
    private AbilityDefinition def0, def1;

    private void Start()
    {
        if (!abilityRegistry) abilityRegistry = MetaProgression.I.abilityRegistry;

        if (slot0) slot0.SetKey("Q");
        if (slot1) slot1.SetKey("Y");

        ResolveLocalPlayer();
        LoadFromMeta();
        ApplyIcons();
    }

    private void ResolveLocalPlayer()
    {
        // simplest: finde irgendeinen PlayerAbilities mit IsOwner
        var all = FindObjectsOfType<PlayerAbilities>(true);
        foreach (var a in all)
        {
            if (a != null && a.IsOwner)
            {
                playerAbilities = a;
                break;
            }
        }
    }

    private void LoadFromMeta()
    {
        var d = MetaProgression.I.Data;
        id0 = d.ability1;
        id1 = d.ability2;

        def0 = (!string.IsNullOrEmpty(id0) && abilityRegistry) ? abilityRegistry.Get(id0) : null;
        def1 = (!string.IsNullOrEmpty(id1) && abilityRegistry) ? abilityRegistry.Get(id1) : null;
    }

    private void ApplyIcons()
    {
        if (slot0) slot0.SetAbility(def0);
        if (slot1) slot1.SetAbility(def1);
    }

    private void Update()
    {
        if (playerAbilities == null)
        {
            ResolveLocalPlayer();
            return;
        }

        // Remaining CD
        float r0 = playerAbilities.GetRemainingCooldown(0);
        float r1 = playerAbilities.GetRemainingCooldown(1);

        float cd0 = def0 ? Mathf.Max(0.01f, def0.cooldownSeconds) : 0f;
        float cd1 = def1 ? Mathf.Max(0.01f, def1.cooldownSeconds) : 0f;

        if (slot0) slot0.SetCooldown(r0, cd0);
        if (slot1) slot1.SetCooldown(r1, cd1);
    }
}
