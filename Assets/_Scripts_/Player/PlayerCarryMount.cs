using UnityEngine;
using Unity.Netcode;

public class PlayerCarryMount : NetworkBehaviour
{
    [Header("Mount")]
    public Transform carrySocket;        // Punkt am Player, der am DockClamp liegen soll
    public Collider[] toDisable;
    public MonoBehaviour[] inputScripts;

    private Rigidbody rb;
    public bool IsCarried { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Parente den Player unter den DockClamp (parentRef) und richte ihn so aus, 
    /// dass carrySocket exakt auf DockClamp liegt (kein Nachziehen).
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void BeginCarryServerRpc(NetworkObjectReference parentRef)
    {
        if (IsCarried) { Debug.LogWarning("[CarryMount] BeginCarry: already carried."); return; }
        if (!parentRef.TryGet(out NetworkObject parentObj) || parentObj == null)
        {
            Debug.LogError("[CarryMount] BeginCarry: invalid parentRef!"); return;
        }

        // --- 0) Physik/Input vor dem Snap entschärfen
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        foreach (var s in inputScripts) if (s) s.enabled = false;
        foreach (var c in toDisable)    if (c) c.enabled = false;

        // --- 1) Matrizentrick: Root so setzen, dass carrySocket == DockClamp in WORLD SPACE
        if (carrySocket == null) { Debug.LogWarning("[CarryMount] Missing carrySocket!"); }
        var clamp = parentObj.transform; // erwartet: skalenfreier Anchor (1,1,1)

        // Root->Socket (lokale Relation des Sockets)
        Matrix4x4 M_rootToSocket = transform.worldToLocalMatrix * carrySocket.localToWorldMatrix;
        Matrix4x4 T_clamp       = clamp.localToWorldMatrix;
        Matrix4x4 T_rootTarget  = T_clamp * M_rootToSocket.inverse; // so dass root * (root->socket) = clamp

        // TR aus Matrix ziehen (ohne lossyScale-Müll)
        Vector3 pos = T_rootTarget.GetColumn(3);
        Vector3 fwd = T_rootTarget.GetColumn(2);
        Vector3 up  = T_rootTarget.GetColumn(1);
        Quaternion rot = Quaternion.LookRotation(fwd, up);

        // --- 2) Parent setzen, ABER Weltpose beibehalten
        bool ok = NetworkObject.TrySetParent(clamp, true /*worldPositionStays*/);
        Debug.Log($"[CarryMount] TrySetParent(worldStays)= {ok} | Parent={parentObj.name}");

        // --- 3) Weltpose final setzen (numerische Sicherheit)
        transform.SetPositionAndRotation(pos, rot);
        transform.localScale = Vector3.one; // Sicherheit: niemals Skelett per Parent-Scale verzerren

        IsCarried = true;
        Debug.Log("[CarryMount] Snap OK (world-space).");
    }

    [ServerRpc(RequireOwnership = false)]
    public void EndCarryServerRpc()
    {
        if (!IsCarried) { Debug.LogWarning("[CarryMount] EndCarry: not carried."); return; }

        // Parent lösen, Weltpose behalten
        NetworkObject.TryRemoveParent(true /*worldPositionStays*/);

        if (rb != null) rb.isKinematic = false;
        foreach (var s in inputScripts) if (s) s.enabled = true;
        foreach (var c in toDisable)    if (c) c.enabled = true;

        IsCarried = false;
        Debug.Log("[CarryMount] Detach OK.");
    }
}
