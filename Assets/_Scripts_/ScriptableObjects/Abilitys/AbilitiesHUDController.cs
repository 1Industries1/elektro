using Unity.Netcode;
using UnityEngine;

public class AbilitiesHUDController : MonoBehaviour
{
    public AbilityHUDSlotUI slot0;
    public AbilityHUDSlotUI slot1;

    public AbilityRegistry abilityRegistry;

    private PlayerAbilities playerAbilities;
    private string id0, id1;
    private AbilityDefinition def0, def1;

    private Coroutine _bindCo;

    private void OnEnable()
    {
        if (!abilityRegistry) abilityRegistry = MetaProgression.I.abilityRegistry;

        slot0?.SetKey("Q");
        slot1?.SetKey("Y");

        if (_bindCo == null) _bindCo = StartCoroutine(BindWhenReady());

        LoadFromMeta();
        ApplyIcons();
    }

    private void OnDisable()
    {
        if (_bindCo != null) { StopCoroutine(_bindCo); _bindCo = null; }
    }

    private System.Collections.IEnumerator BindWhenReady()
    {
        while (isActiveAndEnabled && playerAbilities == null)
        {
            TryBindLocalPlayerAbilities();
            if (playerAbilities != null) break;
            yield return null;
        }
        _bindCo = null;
    }

    private void TryBindLocalPlayerAbilities()
    {
        if (playerAbilities != null) return;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient) return;

        var local = nm.SpawnManager?.GetLocalPlayerObject();
        if (!local) return;

        playerAbilities = local.GetComponent<PlayerAbilities>()
                        ?? local.GetComponentInChildren<PlayerAbilities>(true);
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
        slot0?.SetAbility(def0);
        slot1?.SetAbility(def1);
    }

    private void Update()
    {
        // Falls Loadout sich ändern kann:
        var d = MetaProgression.I.Data;
        if (d.ability1 != id0 || d.ability2 != id1)
        {
            LoadFromMeta();
            ApplyIcons();
        }

        if (playerAbilities == null)
        {
            TryBindLocalPlayerAbilities();
            return;
        }

        playerAbilities.GetCooldownInfo(0, out float r0, out float cd0);
        playerAbilities.GetCooldownInfo(1, out float r1, out float cd1);

        slot0?.SetCooldown(r0, cd0);
        slot1?.SetCooldown(r1, cd1);
    }
}
