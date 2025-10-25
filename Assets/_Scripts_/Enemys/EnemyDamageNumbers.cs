using Unity.Netcode;
using UnityEngine;

public class EnemyDamageNumbers : NetworkBehaviour
{
    public void ShowForAllClients(float amount, Vector3 worldPos, bool isCrit = false)
    {
        if (IsServer) DamagePopupRelay.Instance?.ShowForAll_Server(amount, worldPos, isCrit);
    }

    public void ShowForAttackerOnly(float amount, Vector3 worldPos, ulong attackerClientId, bool isCrit = false)
    {
        if (IsServer) DamagePopupRelay.Instance?.ShowForAttackerOnly_Server(amount, worldPos, attackerClientId, isCrit);
    }
}
