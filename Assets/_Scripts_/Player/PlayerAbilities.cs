using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAbilities : NetworkBehaviour
{
    [Header("Refs")]
    public AbilityRegistry abilityRegistry;
    [SerializeField] private OverclockRuntime overclock;

    // 2 Slots: readyAt als ServerTime (double), 0 = bereit
    public NetworkVariable<double> abilityReadyAt0 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<double> abilityReadyAt1 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // NEU: wann der Cooldown gestartet hat (ServerTime)
    public NetworkVariable<double> abilityCdStartAt0 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<double> abilityCdStartAt1 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // NEU: wie lang dieser Cooldown insgesamt ist (für Fill)
    public NetworkVariable<float> abilityCdDuration0 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> abilityCdDuration1 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private string _equippedId0;
    private string _equippedId1;

    private void Awake()
    {
        if (!overclock) overclock = GetComponent<OverclockRuntime>() ?? GetComponentInChildren<OverclockRuntime>(true);
    }

    public override void OnNetworkSpawn()
    {
        if (!abilityRegistry) abilityRegistry = MetaProgression.I.abilityRegistry;

        if (IsServer)
        {
            abilityReadyAt0.Value = abilityReadyAt1.Value = 0;
            abilityCdStartAt0.Value = abilityCdStartAt1.Value = 0;
            abilityCdDuration0.Value = abilityCdDuration1.Value = 0;
        }

        if (IsOwner)
        {
            var d = MetaProgression.I.Data;
            SendAbilityLoadoutServerRpc(d.ability1, d.ability2);
        }
    }

    // Owner->Server: Loadout setzen
    [ServerRpc]
    private void SendAbilityLoadoutServerRpc(string id0, string id1, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        _equippedId0 = Sanitize(id0);
        _equippedId1 = Sanitize(id1);
    }

    private string Sanitize(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var def = abilityRegistry ? abilityRegistry.Get(id) : null;
        if (def == null) return null;

        // Unlock-Check (wenn Server MetaProgression auch hat / trusted)
        if (MetaProgression.I != null && !MetaProgression.I.IsAbilityUnlocked(def.id))
            return null;

        return def.id;
    }

    public void GetCooldownInfo(int slot, out float remaining, out float total)
    {
        double now = NetworkManager ? NetworkManager.ServerTime.Time : Time.timeAsDouble;

        if (slot == 0)
        {
            total = Mathf.Max(0.01f, abilityCdDuration0.Value);
            remaining = Mathf.Max(0f, (float)(abilityReadyAt0.Value - now));
            // Wenn nicht im Cooldown: total=0 macht UI sauber "aus"
            if (remaining <= 0.0001f) total = 0f;
        }
        else
        {
            total = Mathf.Max(0.01f, abilityCdDuration1.Value);
            remaining = Mathf.Max(0f, (float)(abilityReadyAt1.Value - now));
            if (remaining <= 0.0001f) total = 0f;
        }
    }

    // Client fragt: Slot benutzen
    [ServerRpc]
    public void RequestUseAbilityServerRpc(int slot, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (slot != 0 && slot != 1) return;

        var now = NetworkManager.ServerTime.Time;

        string abilityId = (slot == 0) ? _equippedId0 : _equippedId1;
        if (string.IsNullOrEmpty(abilityId)) return;

        var def = abilityRegistry.Get(abilityId);
        if (def == null || def.overclockEffect == null) return;

        float total = Mathf.Max(0.01f, def.cooldownSeconds);

        if (slot == 0)
        {
            if (abilityReadyAt0.Value > now) return;
            abilityCdStartAt0.Value = now;
            abilityCdDuration0.Value = total;
            abilityReadyAt0.Value = now + total;
        }
        else
        {
            if (abilityReadyAt1.Value > now) return;
            abilityCdStartAt1.Value = now;
            abilityCdDuration1.Value = total;
            abilityReadyAt1.Value = now + total;
        }

        if (overclock != null)
            overclock.ActivateInstant_Server(def.overclockEffect);
    }



    [ClientRpc]
    private void AbilityUsedClientRpc(int slot, float cooldown, ClientRpcParams _ = default)
    {
        if (!IsOwner) return;
        // optional: UI toast / sound / animation trigger
    }

    // UI-Helfer (Client): remaining cooldown in seconds
    public float GetRemainingCooldown(int slot)
    {
        double now = NetworkManager ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
        double readyAt = (slot == 0) ? abilityReadyAt0.Value : abilityReadyAt1.Value;
        return (float)Mathf.Max(0f, (float)(readyAt - now));
    }
}
