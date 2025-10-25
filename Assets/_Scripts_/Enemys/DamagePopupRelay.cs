using Unity.Netcode;
using UnityEngine;

public class DamagePopupRelay : NetworkBehaviour
{
    public static DamagePopupRelay Instance;

    private void Awake() { Instance = this; }

    [ClientRpc(Delivery = RpcDelivery.Reliable)]
    private void ShowDamageNumberClientRpc(float amount, Vector3 worldPos, bool isCrit,
        ClientRpcParams sendParams = default)
    {
        DamagePopupPool.Instance?.Spawn(worldPos, amount, isCrit);
    }

    // vom Server aufrufen:
    public void ShowForAttackerOnly_Server(float amount, Vector3 pos, ulong attackerClientId, bool isCrit = false)
    {
        if (!IsServer) return;
        var send = new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { attackerClientId } }
        };
        ShowDamageNumberClientRpc(amount, pos, isCrit, send);
    }

    public void ShowForAll_Server(float amount, Vector3 pos, bool isCrit = false)
    {
        if (!IsServer) return;
        ShowDamageNumberClientRpc(amount, pos, isCrit);
    }
}
