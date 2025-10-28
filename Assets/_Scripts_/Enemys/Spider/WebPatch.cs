// WebPatch.cs (nur relevante Ausschnitte)
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class WebPatch : NetworkBehaviour
{
    [SerializeField] private float radius = 3f;
    [SerializeField] private float duration = 4.0f;      // etwas länger, da Streifen
    [SerializeField] private float slowMultiplier = 0.65f;
    [SerializeField] private float refreshStep = 0.2f;   // alle 0.2s wird Slow erneuert
    [SerializeField] private GameObject vfxRoot;
    [SerializeField] DecalProjector decal;

    private float endTime;
    private readonly Dictionary<ulong, float> nextRefresh = new();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            endTime = Time.time + duration;
            AlignToGround();
        }
        UpdateVfxScaleClientRpc(radius);
    }

    void FixedUpdate()
    {
        if (!IsServer) return;
        if (Time.time >= endTime)
        {
            if (TryGetComponent<NetworkObject>(out var no)) no.Despawn(true);
            else Destroy(gameObject);
        }
    }

    // <<< NEU: kontinuierliches Verlängern
    void OnTriggerStay(Collider other)
    {
        if (!IsServer) return;

        if (other.TryGetComponent<NetworkObject>(out var no) && no.IsPlayerObject)
        {
            ulong id = no.OwnerClientId;
            if (!nextRefresh.TryGetValue(id, out float t) || Time.time >= t)
            {
                if (other.TryGetComponent<PlayerSlowReceiver>(out var recv))
                    recv.ApplyOrRefreshSlow(slowMultiplier, refreshStep + 0.05f);

                nextRefresh[id] = Time.time + refreshStep;
            }
        }
    }

    private void AlignToGround()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            transform.position = hit.point;
    }

    [ClientRpc]
    void UpdateVfxScaleClientRpc(float r)
    {
        if (decal != null)
            decal.size = new Vector3(r * 2f, 3.5f, r * 2f);
    }
}
