using Unity.Netcode;
using UnityEngine;

// Owner-seitiges Input: nutzt die clientseitigen Snapshot-IDs (clientSlotId0/1),
// damit das auch dann funktioniert, wenn die serverseitige quickslots-Liste
// lokal nicht befüllt ist.

[DisallowMultipleComponent]
public class OverclockQuickslotInput : NetworkBehaviour
{
    private OverclockRuntime rt;

    private void Start()
    {
        rt = GetComponent<OverclockRuntime>();
        if (rt == null)
            rt = GetComponentInParent<OverclockRuntime>();
    }

    private void Update()
    {
        if (!IsOwner || rt == null) return;

        if (Input.GetKeyDown(KeyCode.Q))
        {
            var id0 = rt.clientSlotId0;
            if (!string.IsNullOrEmpty(id0))
                rt.RequestActivateTacticalServerRpc(id0);
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            var id1 = rt.clientSlotId1;
            if (!string.IsNullOrEmpty(id1))
                rt.RequestActivateTacticalServerRpc(id1);
        }
    }
}
