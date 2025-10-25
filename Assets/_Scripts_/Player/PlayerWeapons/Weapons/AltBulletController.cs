using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class AltBulletController : NetworkBehaviour
{
    [Header("Base")]
    public float speed = 15f;
    public float lifetime = 5f;
    public float damage;

    [Header("Per-Hit Behaviour")]
    [Tooltip("Wie oft das Projektil Gegner/Flächen durchdringen darf, bevor es final explodiert.")]
    public int baseMaxPierces = 2;
    [Tooltip("Geschwindigkeitsverlust pro Treffer (0..1).")]
    [Range(0f, 0.5f)] public float perHitSpeedLoss = 0.12f;
    [Tooltip("Kurzer Cooldown, damit derselbe Gegner nicht mehrfach in einem Frame getroffen wird.")]
    public float rehitCooldown = 0.15f;

    [Header("AoE (pro Treffer)")]
    public float explosionRadius = 4f;
    [Tooltip("Schadensabfall von Zentrum (0) bis Rand (1). Wenn null → linear.")]
    public AnimationCurve damageFalloff;

    [Header("Chain Lightning")]
    public int baseChainCount = 1;
    public float chainRange = 7f;
    [Range(0.1f, 1f)] public float chainDamageMultiplier = 0.6f;
    public GameObject chainHitVFX;

    [Header("Final Explosion")]
    public float finalExplosionRadiusMultiplier = 1.8f;
    public float finalExplosionDamageMultiplier = 1.75f;

    [Header("VFX/SFX")]
    public GameObject explosionVFX;
    public AudioClip explosionSound;

    private Rigidbody rb;
    private Collider myCol;
    private float spawnTime;
    private ulong shooterClientId;

    // Runtime state
    private int currentPierces = 0;
    private int maxPierces;
    private float chargePercentCached;
    private float scaledExplosionRadius;
    private int chainCount;

    // Rehit filtering
    private readonly Dictionary<int, float> lastHitAt = new Dictionary<int, float>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myCol = GetComponent<Collider>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.None; // Server sim
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            spawnTime = Time.time;
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (Time.time - spawnTime > lifetime)
        {
            // Finale Explosion, wenn Lebenszeit endet
            FinalExplosion();
            NetworkObject.Despawn();
        }
    }

    public void Init(Vector3 direction, float newSpeed, float newDamage, ulong ownerId, float chargePercent)
    {
        if (!IsServer) return;

        speed = newSpeed;
        damage = newDamage;
        shooterClientId = ownerId;
        chargePercentCached = Mathf.Clamp01(chargePercent);

        // Scale Eigenschaften basierend auf Ladung
        maxPierces = baseMaxPierces + Mathf.RoundToInt(Mathf.Lerp(0f, 2f, chargePercentCached));
        chainCount = baseChainCount + Mathf.RoundToInt(Mathf.Lerp(0f, 2f, chargePercentCached));
        scaledExplosionRadius = explosionRadius * Mathf.Lerp(1f, 1.5f, chargePercentCached);

        rb.linearVelocity = direction.normalized * speed;
        transform.forward = rb.linearVelocity.normalized;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        Vector3 hitPoint = collision.GetContact(0).point;

        // 1) Pro-Treffer AoE
        DoAreaDamage(hitPoint, scaledExplosionRadius, damage);

        // 2) Chain Lightning
        if (chainCount > 0)
        {
            DoChainLightning(hitPoint, chainCount, chainRange, damage * chainDamageMultiplier);
        }

        // 3) VFX für Clients
        ExplosionClientRpc(hitPoint, 1f);

        currentPierces++;

        if (currentPierces >= maxPierces)
        {
            // Finale Explosion (größer/stärker)
            FinalExplosion();
            NetworkObject.Despawn();
            return;
        }

        // 4) Durchdringen aktiv lassen:
        // Kollision mit diesem Collider ignorieren, damit wir „durchfliegen“
        Physics.IgnoreCollision(myCol, collision.collider, true);

        // Leicht nach vorne schieben, um Rekollision zu vermeiden
        rb.position += transform.forward * 0.05f;

        // Geschwindigkeit etwas reduzieren
        float speedFactor = Mathf.Clamp01(1f - perHitSpeedLoss * currentPierces);
        rb.linearVelocity = transform.forward * (speed * speedFactor);
    }

    private void DoAreaDamage(Vector3 center, float radius, float baseDmg)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius);
        float now = Time.time;

        foreach (var c in hits)
        {
            if (!c || !c.CompareTag("Enemy")) continue;

            int id = c.GetInstanceID();
            if (lastHitAt.TryGetValue(id, out float t) && now - t < rehitCooldown)
                continue;

            lastHitAt[id] = now;

            var enemy = c.GetComponent<IEnemy>();
            if (enemy == null) continue;

            // Schaden mit Abfall
            float dist = Vector3.Distance(center, c.transform.position);
            float k = Mathf.Clamp01(dist / Mathf.Max(0.0001f, radius));
            float falloff = damageFalloff != null && damageFalloff.keys != null && damageFalloff.keys.Length > 0
                ? Mathf.Clamp01(damageFalloff.Evaluate(k))
                : (1f - k); // linear

            float finalDmg = baseDmg * Mathf.Max(0.05f, falloff);

            enemy.TakeDamage(finalDmg, shooterClientId, c.transform.position);
        }
    }

    private void DoChainLightning(Vector3 origin, int chains, float range, float startDmg)
    {
        Vector3 lastPos = origin;
        float dmg = startDmg;

        // Vermeide doppelte Ketten auf denselben Gegner
        HashSet<int> visited = new HashSet<int>();

        for (int i = 0; i < chains; i++)
        {
            Collider[] around = Physics.OverlapSphere(lastPos, range);
            Transform best = null;
            float bestDist = float.MaxValue;

            foreach (var c in around)
            {
                if (!c || !c.CompareTag("Enemy")) continue;
                int id = c.GetInstanceID();
                if (visited.Contains(id)) continue;

                float d = Vector3.SqrMagnitude(c.transform.position - lastPos);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c.transform;
                }
            }

            if (best == null) break;

            visited.Add(best.GetInstanceID());
            var enemy = best.GetComponent<IEnemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(dmg, shooterClientId, best.position);
            }

            ChainHitClientRpc(best.position);
            lastPos = best.position;
            dmg *= chainDamageMultiplier;
        }
    }

    private void FinalExplosion()
    {
        Vector3 pos = transform.position;
        float finalRadius = scaledExplosionRadius * finalExplosionRadiusMultiplier;
        float finalDamage = damage * finalExplosionDamageMultiplier * Mathf.Lerp(1f, 1.5f, chargePercentCached);

        DoAreaDamage(pos, finalRadius, finalDamage);
        ExplosionClientRpc(pos, finalExplosionRadiusMultiplier); // skaliertes VFX
    }

    // ---------- Client RPCs (nur Effekte) ----------
    [ClientRpc]
    private void ExplosionClientRpc(Vector3 pos, float scale)
    {
        if (explosionVFX != null)
        {
            GameObject fx = Instantiate(explosionVFX, pos, Quaternion.identity);
            fx.transform.localScale *= Mathf.Clamp(scale, 0.5f, 3f);
            Object.Destroy(fx, 3f);
        }

        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, pos, 1f);
        }
    }

    [ClientRpc]
    private void ChainHitClientRpc(Vector3 pos)
    {
        if (chainHitVFX != null)
        {
            GameObject fx = Instantiate(chainHitVFX, pos, Quaternion.identity);
            Object.Destroy(fx, 1.5f);
        }
    }
}
