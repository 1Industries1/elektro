using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class EnemyFlameExplosion : NetworkBehaviour
{
    [Header("Damage")]
    [SerializeField] private float damage = 15f;

    // Radius der Explosion
    [SerializeField] private float radius = 3f;

    // Zeit bis zur eigentlichen Explosion (Telegraphie)
    [SerializeField] private float delayBeforeDamage = 2f;

    // Wie lange das Effekt-Objekt nach dem Schaden noch existiert (für Ausglühen der VFX)
    [SerializeField] private float lifetimeAfterDamage = 1.0f;

    // Optional: nur bestimmte Layer treffen (z.B. Player)
    [SerializeField] private LayerMask playerMask = ~0;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartCoroutine(ExplosionRoutine());
        }
    }

    private System.Collections.IEnumerator ExplosionRoutine()
    {
        // 1) Warten bis zur eigentlichen Explosion
        yield return new WaitForSeconds(delayBeforeDamage);

        // 2) Einmaliger Schaden im Umkreis
        Vector3 center = transform.position;

        var hits = Physics.OverlapSphere(center, radius, playerMask, QueryTriggerInteraction.Ignore);
        foreach (var c in hits)
        {
            if (!c.CompareTag("Player")) continue;

            var hp = c.GetComponentInParent<PlayerHealth>();
            if (hp != null)
            {
                hp.Server_TakeDamage(damage, OwnerClientId);
            }
        }

        // 3) Noch kurz VFX laufen lassen, dann despawn
        yield return new WaitForSeconds(lifetimeAfterDamage);

        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, radius);
    }
#endif
}
