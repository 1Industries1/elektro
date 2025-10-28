// WebGlob.cs
using UnityEngine;
using Unity.Netcode;

[DisallowMultipleComponent]
public class WebGlob : NetworkBehaviour
{
    [Header("Flight")]
    [SerializeField] private float speed = 18f;
    [SerializeField] private float lifeTime = 2.8f;

    [Header("Patch")]
    [SerializeField] private GameObject webPatchPrefab; // hat NetworkObject
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Trail Spray")] // <<< NEU
    [SerializeField] private bool leaveTrail = true;
    [SerializeField] private float trailSpacing = 1.0f;     // Distanz zw. Patches entlang Flugbahn
    [SerializeField] private float trailStartDelay = 0.0f;  // optionaler Delay
    [SerializeField] private float raycastUp = 6f;          // Bodenprojektion ↑
    [SerializeField] private float raycastDown = 12f;       // Bodenprojektion ↓
    [SerializeField] private int maxTrailPatches = 20;      // Sicherheitslimit

    private float t0;
    private Vector3 lastDropPos;
    private int trailCount;

    void OnEnable()
    {
        t0 = Time.time;
        lastDropPos = transform.position;
        trailCount = 0;
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        // Vorwärts bewegen
        transform.position += transform.forward * speed * Time.fixedDeltaTime;

        // Unterwegs sprühen
        if (leaveTrail && Time.time - t0 >= trailStartDelay && trailCount < maxTrailPatches)
            TryDropAlongPath();

        // Lebenszeit abgelaufen?
        if (Time.time - t0 > lifeTime)
            DespawnSelf();
    }

    private void TryDropAlongPath()
    {
        if (Vector3.Distance(transform.position, lastDropPos) < trailSpacing) return;

        Vector3 pos = transform.position;
        // Bodenprojektion für hügelige Maps / Gras
        if (Physics.Raycast(pos + Vector3.up * raycastUp, Vector3.down, out var hit, raycastUp + raycastDown, groundMask, QueryTriggerInteraction.Ignore))
            pos = hit.point;

        SpawnPatch(pos);
        lastDropPos = transform.position;
        trailCount++;
    }

    private void SpawnPatch(Vector3 pos)
    {
        if (webPatchPrefab == null) return;

        var patch = Instantiate(webPatchPrefab, pos, Quaternion.identity);
        if (patch.TryGetComponent<NetworkObject>(out var no) && !no.IsSpawned) no.Spawn();
    }

    void OnCollisionEnter(Collision c)
    {
        if (!IsServer) return;
        // Beim Aufprall zusätzlich einen Patch setzen (Impact-Spot)
        SpawnPatch(c.GetContact(0).point);
        DespawnSelf();
    }

    void OnTriggerEnter(Collider col)
    {
        if (!IsServer) return;
        // Falls dein Projektil Triggers nutzt (z.B. mit Gegnerkollidern)
        SpawnPatch(transform.position);
        DespawnSelf();
    }

    private void DespawnSelf()
    {
        if (TryGetComponent<NetworkObject>(out var no)) no.Despawn(true);
        else Destroy(gameObject);
    }
}
