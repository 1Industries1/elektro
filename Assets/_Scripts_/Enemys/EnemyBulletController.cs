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

    private Rigidbody rb;
    private float spawnTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        rb.isKinematic = !IsServer;
    }

    private void Update()
    {
        if (!IsServer) return;

        if (Time.time - spawnTime > lifetime)
            NetworkObject.Despawn();
    }

    /// <summary>
    /// Kann vom Spawner aufgerufen werden, um Werte zu überschreiben.
    /// Wenn ein Wert null/negativ ist, bleibt der Inspector-Wert bestehen.
    /// </summary>
    public void Init(Vector3 direction, float newSpeed = -1f, float newDamage = -1f, float newLifetime = -1f)
    {
        if (!IsServer) return;

        if (newSpeed > 0f)  speed = newSpeed;
        if (newDamage > 0f) damage = newDamage;
        if (newLifetime > 0f) lifetime = newLifetime;

        spawnTime = Time.time;
        rb.linearVelocity = direction.normalized * speed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            var hp = collision.gameObject.GetComponent<PlayerHealth>();
            if (hp != null)
            {
                hp.Server_TakeDamage(damage, OwnerClientId);
            }
        }

        NetworkObject.Despawn();
    }
}