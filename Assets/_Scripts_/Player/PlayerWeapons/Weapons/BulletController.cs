using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(Rigidbody))]
public class BulletController : NetworkBehaviour
{
    [Header("Base")]
    public float speed = 20f;
    public float lifetime = 5f;
    public float damage; // <-- wird vom Shooter gesetzt

    [Header("Ricochet")]
    [Tooltip("Wie stark die Kugelgeschwindigkeit nach dem Abpraller erhalten bleibt (0..1).")]
    [Range(0f, 1f)] public float bounciness = 0.7f;
    [Tooltip("Maximale Anzahl Abpraller, bevor die Kugel verschwindet.")]
    public int maxBounces = 3;
    [Tooltip("Kollisions-Layer, auf denen abgeprallt werden darf (z.B. Walls).")]
    public LayerMask bounceLayers = ~0;
    [Tooltip("Mindestgeschwindigkeit nach Abprall; darunter Despawn.")]
    public float minSpeedAfterBounce = 4f;
    [Tooltip("Kleine Trennung nach Abprall, um Sofort-Rekollision zu vermeiden.")]
    public float separationEpsilon = 0.01f;

    private Rigidbody rb;
    private float spawnTime;
    private ulong shooterClientId;
    private int bounceCount;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // wichtig bei schnellen Projektilen
        rb.interpolation = RigidbodyInterpolation.None; // Server sim only
    }

    public override void OnNetworkSpawn()
    {
        rb.isKinematic = false; // Physik nur auf dem Server
    }

    private void Update()
    {
        if (!IsServer) return;

        if (Time.time - spawnTime > lifetime)
            NetworkObject.Despawn();
    }

    public void Init(Vector3 direction, float newSpeed, float newDamage, ulong ownerId)
    {
        if (!IsServer) return;

        speed = newSpeed;
        damage = newDamage;
        spawnTime = Time.time;
        shooterClientId = ownerId;
        bounceCount = 0;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false;

        rb.linearVelocity = direction.normalized * speed;
        transform.forward = rb.linearVelocity.normalized;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // 1) Enemy getroffen -> Schaden + Despawn
        if (collision.gameObject.CompareTag("Enemy"))
        {
            var enemy = collision.gameObject.GetComponent<IEnemy>();
            if (enemy != null)
            {
                var hitPoint = collision.GetContact(0).point;
                enemy.TakeDamage(damage, shooterClientId, hitPoint);
                HitmarkerClientRpc(shooterClientId);
            }
            NetworkObject.Despawn();
            return;
        }

        // 2) Darf auf diesem Layer abprallen?
        if (((1 << collision.gameObject.layer) & bounceLayers) == 0)
        {
            NetworkObject.Despawn();
            return;
        }

        // 3) Abprallen
        var contact = collision.GetContact(0);
        Vector3 inVel = rb.linearVelocity;
        if (inVel.sqrMagnitude <= 0.0001f)
        {
            NetworkObject.Despawn();
            return;
        }

        Vector3 reflected = Vector3.Reflect(inVel, contact.normal);

        // Energie dämpfen
        reflected *= bounciness;

        // Zu langsam? -> Despawn
        if (reflected.magnitude < minSpeedAfterBounce || bounceCount + 1 > maxBounces)
        {
            NetworkObject.Despawn();
            return;
        }

        // Bounce anwenden
        bounceCount++;
        rb.linearVelocity = reflected;

        // Kleine Positionsverschiebung weg von der Oberfläche, um Doppel-Kollision zu vermeiden
        rb.position += contact.normal * separationEpsilon;

        // Optional: Ausrichtung an Flugrichtung
        transform.forward = rb.linearVelocity.normalized;

        // VFX/SFX für Clients
        RicochetClientRpc(contact.point, contact.normal);
    }

    [ClientRpc]
    private void HitmarkerClientRpc(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;
        HitmarkerUIController.Instance?.ShowHitmarker();
    }

    [ClientRpc]
    private void RicochetClientRpc(Vector3 point, Vector3 normal)
    {
        // TODO: Funken/Staub & Sound abspielen (nur visuell/Audio, keine Logik)
        // z.B. ParticleSystemManager.SpawnRicochet(point, normal);
    }

    public override void OnNetworkDespawn()
    {
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
    }
}
