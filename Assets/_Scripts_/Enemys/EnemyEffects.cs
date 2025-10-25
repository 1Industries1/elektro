using UnityEngine;
using Unity.Netcode;

public class EnemyEffects : NetworkBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject deathEffectPrefab;

    public void PlayHitEffect(Vector3 position)
    {
        if (hitEffectPrefab != null)
            Instantiate(hitEffectPrefab, position, Quaternion.identity);
    }

    public void PlayDeathEffect(Vector3 position)
    {
        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, position, Quaternion.identity);
    }

    // RPCs, damit alle Clients die Effekte sehen
    [ClientRpc]
    public void PlayHitEffectClientRpc(Vector3 position)
    {
        PlayHitEffect(position);
    }

    [ClientRpc]
    public void PlayDeathEffectClientRpc(Vector3 position)
    {
        PlayDeathEffect(position);
    }
}
