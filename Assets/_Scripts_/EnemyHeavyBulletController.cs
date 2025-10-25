using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyHeavyBulletController : NetworkBehaviour
{
    [Header("Flight")]
    [SerializeField] private float speed = 28f;         // langsamer, wuchtig
    [SerializeField] private float lifetime = 6f;

    [Header("Damage")]
    [SerializeField] private float directDamage = 12f;  // Direkttreffer
    [SerializeField] private float splashRadius = 3.8f; // AoE-Radius
    [SerializeField] private float splashDamage = 7f;   // AoE-Schaden

    [Header("FX")]
    [SerializeField] private GameObject impactFx;

    private Rigidbody rb;
    private float spawnTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        // Server steuert Physik
        rb.isKinematic = !IsServer;
        if (IsServer) spawnTime = Time.time;
    }

    private void Update()
    {
        if (!IsServer) return;

        if (Time.time - spawnTime > lifetime)
            NetworkObject.Despawn();
    }

    /// <summary>
    /// Wie bei deinem leichten Projektil.
    /// </summary>
    public void Init(Vector3 direction, float newSpeed = -1f, float newDirectDamage = -1f, float newLifetime = -1f)
    {
        if (!IsServer) return;

        if (newSpeed > 0f)        speed = newSpeed;
        if (newDirectDamage > 0f) directDamage = newDirectDamage;
        if (newLifetime > 0f)     lifetime = newLifetime;

        spawnTime = Time.time;
        rb.linearVelocity = direction.normalized * speed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // Direkttreffer auf Player?
        if (collision.gameObject.CompareTag("Player"))
        {
            var hp = collision.gameObject.GetComponent<PlayerHealth>();
            if (hp != null)
            {
                hp.Server_TakeDamage(directDamage, OwnerClientId);
            }
        }

        // Explodiere immer beim ersten soliden Aufschlag
        Explode();
    }

    private void Explode()
    {
        // AoE-Splash: alle Spieler im Radius
        var hits = Physics.OverlapSphere(transform.position, splashRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var c in hits)
        {
            if (c.CompareTag("Player"))
            {
                var hp = c.GetComponentInParent<PlayerHealth>();
                if (hp != null)
                    hp.Server_TakeDamage(splashDamage, OwnerClientId);
            }
        }

        SpawnImpactClientRpc(transform.position);
        NetworkObject.Despawn();
    }

    [ClientRpc]
    private void SpawnImpactClientRpc(Vector3 pos)
    {
        if (impactFx) Instantiate(impactFx, pos, Quaternion.identity);
    }
}
