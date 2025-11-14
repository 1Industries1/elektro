using System.Collections.Generic;
using Unity.Collections;                 // FixedStringXXBytes
using Unity.Netcode;                     // NetworkBehaviour, RPC, NetworkVariable
using UnityEngine;

// Hinweis: Dieses Script geht davon aus, dass es OverclockDef, Stat, StatMod, ModMode gibt.
// Die Quickslots werden serverseitig verwaltet und per Snapshot an Clients gespiegelt.

[DisallowMultipleComponent]
public class OverclockRuntime : NetworkBehaviour
{
    // ===== Payload für RPC (statt string[]/int[]) =====
    public struct QuickslotSnapshot : INetworkSerializable
    {
        public FixedString64Bytes id0;
        public int charges0;
        public FixedString64Bytes id1;
        public int charges1;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref id0);
            serializer.SerializeValue(ref charges0);
            serializer.SerializeValue(ref id1);
            serializer.SerializeValue(ref charges1);
        }
    }

    [System.Serializable]
    private class Active
    {
        public OverclockDef def;
        public float endTime;
        public bool isAfter; // true = After-Effect
    }

    // Aggregierte Werte (werden vom Server berechnet, an Clients gespiegelt)
    public NetworkVariable<float> MoveSpeedMult         = new(1f);
    public NetworkVariable<float> DamageMult            = new(1f);
    public NetworkVariable<float> FireRateOverrideSeconds = new(0f); // 0 = kein Override
    public NetworkVariable<float> FireRateSecondsMult   = new(1f);

    // Quickslot (taktisch) – nur Server pflegt diese Liste
    public readonly List<OverclockDef> quickslots = new();               // max 2 verschiedene
    private readonly Dictionary<string, int> quickCharges = new();       // id -> charges

    // Laufende Effekte
    private readonly List<Active> _actives = new();
    private float _recalcCooldown;

    // ===== Clientseitiger UI-Stand (Snapshot) =====
    public readonly Dictionary<string, int> clientQuickCharges = new();  // id -> charges
    public string clientSlotId0, clientSlotId1;                          // Reihenfolge für UI
    public event System.Action OnQuickslotsUpdated;
    public event System.Action<float> OnOverclockStarted; // duration in Sekunden


    // ===== Öffentliche Helpers =====
    public float GetEffectiveFireRateSeconds(float baseSeconds)
    {
        float s = baseSeconds * FireRateSecondsMult.Value;
        if (FireRateOverrideSeconds.Value > 0f)
            s = Mathf.Min(s, FireRateOverrideSeconds.Value); // "max fire rate" als Cap
        return Mathf.Max(0.01f, s);
    }

    public float GetMoveSpeedMult() => MoveSpeedMult.Value;
    public float GetDamageMult()    => DamageMult.Value;

    // ===== Server: Sofort aktivieren (z. B. bei Pickup, "Instant") =====
    public void ActivateInstant_Server(OverclockDef def)
    {
        if (!IsServer || def == null) return;
        StartOverclock(def, false);
    }

    // ===== Server: Taktisch einlagern (Pickup) =====
    public void AddTactical_Server(OverclockDef def)
    {
        if (!IsServer || def == null) return;

        if (quickslots.Count < 2 && !quickCharges.ContainsKey(def.id))
            quickslots.Add(def);

        int cur = quickCharges.TryGetValue(def.id, out var c) ? c : 0;

        // maximal 5 Charges; passe nach Bedarf an
        quickCharges[def.id] = Mathf.Min(5, cur + def.tacticalCharges);

        BroadcastQuickslots_Server();
    }

    // ===== Client → Server: Ladung zünden (Q/Y) =====
    [ServerRpc]
    public void RequestActivateTacticalServerRpc(string overclockId)
    {
        if (!IsServer || string.IsNullOrEmpty(overclockId)) return;

        var def = FindInQuickslot(overclockId);
        if (def == null) return;
        if (!quickCharges.TryGetValue(def.id, out var c) || c <= 0) return;

        quickCharges[def.id] = c - 1;
        if (quickCharges[def.id] <= 0)
        {
            quickCharges.Remove(def.id);
            quickslots.Remove(def);
        }

        BroadcastQuickslots_Server();      // UI/Snapshots updaten
        StartOverclock(def, false);        // Effekt starten
    }

    // ===== Server → Clients: Snapshot broadcasten =====
    private void BroadcastQuickslots_Server()
    {
        if (!IsServer) return;

        var snap = new QuickslotSnapshot();

        if (quickslots.Count > 0 && quickslots[0])
        {
            var def0 = quickslots[0];
            snap.id0 = def0.id;
            snap.charges0 = quickCharges.TryGetValue(def0.id, out var c0) ? c0 : 0;
        }
        if (quickslots.Count > 1 && quickslots[1])
        {
            var def1 = quickslots[1];
            snap.id1 = def1.id;
            snap.charges1 = quickCharges.TryGetValue(def1.id, out var c1) ? c1 : 0;
        }

        SyncQuickslotsClientRpc(snap);
    }

    [ClientRpc]
    private void SyncQuickslotsClientRpc(QuickslotSnapshot snap)
    {
        clientQuickCharges.Clear();

        clientSlotId0 = snap.id0.ToString();
        clientSlotId1 = snap.id1.ToString();

        if (!string.IsNullOrEmpty(clientSlotId0))
            clientQuickCharges[clientSlotId0] = snap.charges0;

        if (!string.IsNullOrEmpty(clientSlotId1))
            clientQuickCharges[clientSlotId1] = snap.charges1;

        OnQuickslotsUpdated?.Invoke();
    }

    [ClientRpc]
    private void OverclockStartedClientRpc(float duration, ClientRpcParams rpcParams = default)
    {
        // Nur der lokale Owner reagiert (zur Sicherheit)
        OnOverclockStarted?.Invoke(duration);
    }


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // Initialzustand sofort pushen (z. B. wenn schon etwas geladen ist)
            BroadcastQuickslots_Server();
        }
    }

    // ===== Core =====
    private OverclockDef FindInQuickslot(string id)
    {
        for (int i = 0; i < quickslots.Count; i++)
            if (quickslots[i] && quickslots[i].id == id) return quickslots[i];
        return null;
    }

    private void StartOverclock(OverclockDef def, bool isAfter)
    {
        float now = Time.time;

        _actives.Add(new Active
        {
            def = def,
            endTime = now + (isAfter ? def.afterEffectDuration : def.duration),
            isAfter = isAfter
        });

        RecalculateAggregates_Server();

        if (!isAfter)
        {
            var ownerId = NetworkObject.OwnerClientId;
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { ownerId } }
            };

            OverclockStartedClientRpc(def.duration, rpcParams);
        }

        if (!isAfter && def.afterEffectDuration > 0f)
            StartCoroutine(BeginAfterEffectLater(def, now + def.duration));
    }

    private System.Collections.IEnumerator BeginAfterEffectLater(OverclockDef def, float at)
    {
        while (Time.time < at) yield return null;
        StartOverclock(def, true);
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        // Garbage collect abgelaufene Effekte sparsam
        if (Time.time < _recalcCooldown) return;
        _recalcCooldown = Time.time + 0.1f;

        bool removed = false;
        for (int i = _actives.Count - 1; i >= 0; --i)
        {
            if (Time.time >= _actives[i].endTime)
            {
                _actives.RemoveAt(i);
                removed = true;
            }
        }
        if (removed) RecalculateAggregates_Server();
    }

    private void RecalculateAggregates_Server()
    {
        float moveMult = 1f, dmgMult = 1f, frMult = 1f;
        float overrideFR = 0f; // 0 = keiner

        foreach (var a in _actives)
        {
            var list = a.isAfter ? a.def.afterMods : a.def.mods;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                switch (m.stat)
                {
                    case Stat.MoveSpeed:
                        Apply(ref moveMult, ref overrideFR, ref frMult, ref dmgMult, Stat.MoveSpeed, m);
                        break;
                    case Stat.DamageMultiplier:
                        Apply(ref moveMult, ref overrideFR, ref frMult, ref dmgMult, Stat.DamageMultiplier, m);
                        break;
                    case Stat.FireRateSeconds:
                        Apply(ref moveMult, ref overrideFR, ref frMult, ref dmgMult, Stat.FireRateSeconds, m);
                        break;
                }
            }
        }

        MoveSpeedMult.Value = moveMult;
        DamageMult.Value = dmgMult;
        FireRateSecondsMult.Value = frMult;
        FireRateOverrideSeconds.Value = overrideFR;

        RecalcBroadcastClientRpc(MoveSpeedMult.Value, DamageMult.Value, FireRateSecondsMult.Value, FireRateOverrideSeconds.Value);
    }

    [ClientRpc]
    private void RecalcBroadcastClientRpc(float ms, float dm, float fr, float frOverride)
    {
        // Platz für VFX/UI Feedback, falls benötigt
    }

    private static void Apply(
        ref float moveMult, ref float frOverride, ref float frMult, ref float dmgMult,
        Stat stat, StatMod mod)
    {
        switch (stat)
        {
            case Stat.MoveSpeed:
                if (mod.mode == ModMode.Multiply) moveMult *= mod.value;
                else if (mod.mode == ModMode.Add) moveMult += mod.value;
                break;

            case Stat.DamageMultiplier:
                if (mod.mode == ModMode.Multiply) dmgMult *= mod.value;
                else if (mod.mode == ModMode.Add) dmgMult += mod.value;
                break;

            case Stat.FireRateSeconds:
                if (mod.mode == ModMode.Override)
                    frOverride = frOverride > 0f ? Mathf.Min(frOverride, mod.value) : mod.value;
                else if (mod.mode == ModMode.Multiply) frMult *= mod.value;
                else if (mod.mode == ModMode.Add) frMult += mod.value; // selten sinnvoll
                break;
        }
    }
}
