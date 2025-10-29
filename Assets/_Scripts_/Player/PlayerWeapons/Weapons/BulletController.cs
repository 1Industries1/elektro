using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BulletController : NetworkBehaviour
{
    [Header("Base")]
    public float speed = 20f;
    public float lifetime = 5f;

    [Header("Ricochet (non-enemy surfaces)")]
    [Range(0f, 1f)] public float bounciness = 0.7f;
    public int maxBounces = 3;
    public LayerMask bounceLayers = ~0;
    public float minSpeedAfterBounce = 4f;
    public float separationEpsilon = 0.01f;

    // Pierce / Damage
    private int maxPierces = 0;
    private int piercesDone = 0;
    private bool piercedOnce = false;

    private float damageNonPierced;   // vor erstem Durchdringen
    private float damageAfterPierced; // nach >=1x Durchdringen

    private Rigidbody rb;
    private Collider myCol;
    private float spawnTime;
    private ulong shooterClientId;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myCol = GetComponent<Collider>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.None; // Server sim only
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) spawnTime = Time.time;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (Time.time - spawnTime > lifetime)
            NetworkObject.Despawn();
    }

    // Neue Init, pierce-aware
    public void Init(Vector3 direction, float newSpeed, float dmgNonPierced, float dmgAfterPierced, int pierceCount, ulong ownerId)
    {
        if (!IsServer) return;

        speed = newSpeed;
        damageNonPierced = dmgNonPierced;
        damageAfterPierced = dmgAfterPierced;
        maxPierces = Mathf.Max(0, pierceCount);
        piercesDone = 0;
        piercedOnce = false;
        shooterClientId = ownerId;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false;

        rb.linearVelocity = direction.normalized * speed;
        transform.forward = rb.linearVelocity.normalized;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // Enemy getroffen
        if (collision.gameObject.CompareTag("Enemy"))
        {
            float useDmg = piercedOnce ? damageAfterPierced : damageNonPierced;
            var enemy = collision.gameObject.GetComponent<IEnemy>();
            if (enemy != null)
            {
                var hitPoint = collision.GetContact(0).point;
                enemy.TakeDamage(useDmg, shooterClientId, hitPoint);
                HitmarkerClientRpc(shooterClientId);
            }

            if (piercesDone < maxPierces)
            {
                // Durchdringen: Projektil fliegt weiter, Kollision mit diesem Collider ignorieren
                piercesDone++;
                piercedOnce = true;
                Physics.IgnoreCollision(myCol, collision.collider, true);
                rb.position += transform.forward * 0.05f;
                return;
            }

            NetworkObject.Despawn();
            return;
        }

        // Kein Enemy → Bounce-Logik (Wände etc.)
        if (((1 << collision.gameObject.layer) & bounceLayers) == 0)
        {
            NetworkObject.Despawn();
            return;
        }

        var contact = collision.GetContact(0);
        Vector3 inVel = rb.linearVelocity;
        if (inVel.sqrMagnitude <= 0.0001f)
        {
            NetworkObject.Despawn();
            return;
        }

        Vector3 reflected = Vector3.Reflect(inVel, contact.normal);
        reflected *= bounciness;

        if (reflected.magnitude < minSpeedAfterBounce || (maxBounces >= 0 && --maxBounces < 0))
        {
            NetworkObject.Despawn();
            return;
        }

        rb.linearVelocity = reflected;
        rb.position += contact.normal * separationEpsilon;
        transform.forward = rb.linearVelocity.normalized;

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
        // TODO: VFX/SFX
    }
}
