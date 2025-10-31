// EnemyHeavyBulletController.cs
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyHeavyBulletController : NetworkBehaviour
{
    [Header("Flight")]
    [SerializeField] private float speed = 120f;
    public float Speed => speed;
    [SerializeField] private float lifetime = 6f;

    [Header("Damage")]
    [SerializeField] private float directDamage = 12f;
    [SerializeField] private float splashRadius = 3.8f;
    public float SplashRadius => splashRadius;
    [SerializeField] private float splashDamage = 7f;

    [Header("FX")]
    [SerializeField] private GameObject impactFx;

    [Header("DoT Zone (E)")]
    [SerializeField] private GameObject hazardPrefab;   // Prefab mit BurnHazardZone + NetworkObject
    [SerializeField] private float hazardDuration = 4.0f;
    [SerializeField] private float hazardDps = 5.0f;
    [SerializeField] private float hazardTickRate = 0.5f; // alle X Sekunden ticken
    [SerializeField] private float hazardSlowPercent = 0f; // optional

    private Rigidbody rb;
    private float spawnTime;
    private bool exploded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
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
        {
            NetworkObject.Despawn();
        }
    }

    /// <summary>
    /// Initialisiert Flugparameter (Server-only).
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
        if (!IsServer || exploded) return;
        exploded = true;

        // Direkttreffer auf Player?
        if (collision.gameObject.CompareTag("Player"))
        {
            var hp = collision.gameObject.GetComponent<PlayerHealth>();
            if (hp != null)
            {
                hp.Server_TakeDamage(directDamage, OwnerClientId);
            }
        }

        // Explodiere beim ersten soliden Aufschlag
        Explode();
    }

    private void Explode()
    {
        // AoE-Splash
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

        // DoT-Zone (E)
        if (hazardPrefab != null)
        {
            var go = Instantiate(hazardPrefab, transform.position, Quaternion.identity);
            if (go.TryGetComponent<NetworkObject>(out var no))
            {
                // Setup vor Spawn
                var hz = go.GetComponent<BurnHazardZone>();
                if (hz != null)
                {
                    hz.Init(splashRadius, hazardDuration, hazardDps, hazardTickRate, hazardSlowPercent, OwnerClientId);
                }
                if (!no.IsSpawned) no.Spawn();
            }
            else
            {
                Destroy(go);
                Debug.LogError("[EnemyHeavyBulletController] Hazard prefab needs a NetworkObject!");
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
