// WebPatch.cs
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class WebPatch : NetworkBehaviour
{
    [SerializeField] private float radius = 3f;
    [SerializeField] private float duration = 2.5f;
    [SerializeField] private float slowMultiplier = 0.65f;
    [SerializeField] private GameObject vfxRoot;

    private float endTime;
    private readonly HashSet<ulong> slowed = new();

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

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.TryGetComponent<NetworkObject>(out var no) && no.IsPlayerObject)
        {
            if (slowed.Contains(no.OwnerClientId)) return;

            if (other.TryGetComponent<PlayerSlowReceiver>(out var recv))
            {
                recv.ApplySlow(slowMultiplier, 1.5f);
                slowed.Add(no.OwnerClientId);
            }
        }
    }

    private void AlignToGround()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            transform.position = hit.point;
    }

    [ClientRpc]
    private void UpdateVfxScaleClientRpc(float r)
    {
        if (vfxRoot != null) vfxRoot.transform.localScale = new Vector3(r*2f, 1f, r*2f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 1f, 0.35f);
        Gizmos.DrawSphere(transform.position + Vector3.up * 0.05f, radius);
    }
#endif
}
