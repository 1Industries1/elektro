// WebGlob.cs
using UnityEngine;
using Unity.Netcode;

[DisallowMultipleComponent]
public class WebGlob : NetworkBehaviour
{
    [SerializeField] private float speed = 18f;
    [SerializeField] private float lifeTime = 2.8f;
    [SerializeField] private GameObject webPatchPrefab; // has NetworkObject
    [SerializeField] private LayerMask groundMask = ~0;

    private float t0;

    void OnEnable(){ t0 = Time.time; }

    void FixedUpdate()
    {
        if (!IsServer) return;

        transform.position += transform.forward * speed * Time.fixedDeltaTime;

        if (Time.time - t0 > lifeTime)
            DespawnSelf();
    }

    void OnCollisionEnter(Collision c)
    {
        if (!IsServer) return;
        SpawnPatch(c.GetContact(0).point);
        DespawnSelf();
    }

    void OnTriggerEnter(Collider col)
    {
        if (!IsServer) return;
        // Optional: nur Boden/Level?
        SpawnPatch(transform.position);
        DespawnSelf();
    }

    private void SpawnPatch(Vector3 pos)
    {
        if (webPatchPrefab == null) return;

        // Bodenprojektion
        if (Physics.Raycast(pos + Vector3.up * 6f, Vector3.down, out var hit, 12f, groundMask, QueryTriggerInteraction.Ignore))
            pos = hit.point;

        var patch = Instantiate(webPatchPrefab, pos, Quaternion.identity);
        if (patch.TryGetComponent<NetworkObject>(out var no) && !no.IsSpawned) no.Spawn();
    }

    private void DespawnSelf()
    {
        if (TryGetComponent<NetworkObject>(out var no)) no.Despawn(true);
        else Destroy(gameObject);
    }
}
