using UnityEngine;
using Unity.Netcode;

public class EnemyEffects : NetworkBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject deathEffectPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] [Range(0f, 1f)] private float hitVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float deathVolume = 1f;

    public void PlayHitEffect(Vector3 position)
    {
        // VFX
        if (hitEffectPrefab != null)
            Instantiate(hitEffectPrefab, position, Quaternion.identity);

        // SFX – eigenes OneShot-Objekt, unabhängig vom Enemy
        if (hitSound != null)
            AudioSource.PlayClipAtPoint(hitSound, position, hitVolume);
    }

    public void PlayDeathEffect(Vector3 position)
    {
        // VFX
        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, position, Quaternion.identity);

        // SFX – eigenes OneShot-Objekt, unabhängig vom Enemy
        if (deathSound != null)
            AudioSource.PlayClipAtPoint(deathSound, position, deathVolume);
    }

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
