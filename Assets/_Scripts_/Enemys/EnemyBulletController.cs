using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyBulletController : NetworkBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float damage = 0.1f;
    [SerializeField] private float lifetime = 5f;

    [Header("Visuals")]
    [SerializeField] private TrailRenderer trail;

    private Rigidbody rb;
    private float spawnTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        // Nur Server simulier/steuert Rigidbody
        rb.isKinematic = !IsServer;

        if (IsServer)
            spawnTime = Time.time; // safety, falls Init mal nicht direkt kommt
    }

    private void Update()
    {
        if (!IsServer) return;

        if (Time.time - spawnTime > lifetime)
            DespawnWithTrail();
    }

    public void Init(Vector3 direction, float newSpeed = -1f, float newDamage = -1f, float newLifetime = -1f)
    {
        if (!IsServer) return;

        if (newSpeed > 0f)  speed = newSpeed;
        if (newDamage > 0f) damage = newDamage;
        if (newLifetime > 0f) lifetime = newLifetime;

        spawnTime = Time.time;

        Vector3 dir = direction.normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

        // Rotation = Flugrichtung (damit Forward stimmt)
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        // Velocity
        rb.linearVelocity = dir * speed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // robust: trifft oft Child-Collider, daher InParent
        var hp = collision.collider.GetComponentInParent<PlayerHealth>();
        if (hp != null)
        {
            Vector3 p = transform.position;
            Vector3 n = -transform.forward;

            if (collision.contactCount > 0)
            {
                var c = collision.GetContact(0);
                p = c.point;
                n = c.normal;
            }

            hp.Server_TakeDamage(damage, OwnerClientId, p, n, true);
        }

        DespawnWithTrail();
    }

    private void DespawnWithTrail()
    {
        if (!IsServer) return;
        if (!IsSpawned) return;

        if (trail != null)
        {
            // Trail vom Bullet lösen (WICHTIG: Trail sollte ein Child sein!)
            trail.transform.SetParent(null);
            trail.emitting = false;
            Destroy(trail.gameObject, trail.time);
        }

        // Kugeln bleiben?!
        NetworkObject.Despawn(true);
    }
}
