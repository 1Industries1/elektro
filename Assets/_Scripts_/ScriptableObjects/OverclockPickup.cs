using Unity.Netcode;
using UnityEngine;

public class OverclockPickup : NetworkBehaviour
{
    [Header("Design-Time (optional)")]
    [SerializeField] private OverclockDef defaultDef;
    [SerializeField] private bool forceInstantPrefab;

    [Header("SFX")]
    [SerializeField] private AudioClip pickupSfx;
    [SerializeField, Range(0f, 2f)] private float sfxVolume = 2f;

    private OverclockDef _def;
    private bool _forceInstant;

    public void SetDefServer(OverclockDef def, bool forceInstant)
    {
        if (!IsServer) return;
        _def = def;
        _forceInstant = forceInstant;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (_def == null) _def = defaultDef;
            if (!_forceInstant) _forceInstant = forceInstantPrefab;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        var rt = other.GetComponentInParent<OverclockRuntime>();
        if (!rt) return;

        var def = _def != null ? _def : defaultDef;
        if (def == null) return;

        if (_forceInstant || def.kind == OverclockKind.Instant)
            rt.ActivateInstant_Server(def);
        else if (def.kind == OverclockKind.Tactical)
            rt.AddTactical_Server(def);

        // -> Nur dem Besitzer des Spielers den Sound schicken
        var ownerId = rt.NetworkObject.OwnerClientId;
        var rpcParams = new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { ownerId } }
        };
        PlayPickupSfxClientRpc(transform.position, rpcParams);

        GetComponent<NetworkObject>()?.Despawn(true);
    }

    [ClientRpc]
    private void PlayPickupSfxClientRpc(Vector3 at, ClientRpcParams rpcParams = default)
    {
        if (!pickupSfx) return;
        AudioSource.PlayClipAtPoint(pickupSfx, at, sfxVolume);
    }
}
