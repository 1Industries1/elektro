using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BlasterBulletController : NetworkBehaviour
{
    [Header("Base")]
    public float lifetime = 5f;

    [Header("Per-Hit Behaviour")]
    [Tooltip("Geschwindigkeitsverlust pro Treffer (0..1).")]
    [Range(0f, 0.5f)] public float perHitSpeedLoss = 0.12f;
    [Tooltip("Kurzer Cooldown, damit derselbe Gegner nicht mehrfach in einem Frame getroffen wird.")]
    public float rehitCooldown = 0.15f;

    [Header("AoE (pro Treffer)")]
    public float explosionRadius = 4f;
    [Tooltip("Schadensabfall von Zentrum (0) bis Rand (1). Wenn null → linear.")]
    public AnimationCurve damageFalloff;

    [Header("Chain Lightning (optional)")]
    public int baseChainCount = 0;
    public float chainRange = 7f;
    [Range(0.1f, 1f)] public float chainDamageMultiplier = 0.6f;
    public GameObject chainHitVFX;

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
    private float scaledExplosionRadius;
    private int chainCount;

    private bool hasImpactExplosionAug;
    private float finalExplosionRadius;
    private float finalExplosionDamageFactor;

    private float damageNonPierced;
    private float damageAfterPierced;
    private float speed;
    private float chargePercentCached;

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
        if (IsServer) spawnTime = Time.time;
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

    public void InitBlaster(
        Vector3 direction,
        float speed,
        float damageNonPierced,
        float damageAfterPierced,
        int maxPierces,
        ulong ownerId,
        bool hasImpactExplosionAug,
        float finalExplosionRadius,
        float finalExplosionDamageFactor,
        float baseExplosionRadius,
        float chargePercent = 0f)
    {
        if (!IsServer) return;

        this.speed = speed;
        this.damageNonPierced = damageNonPierced;
        this.damageAfterPierced = damageAfterPierced;
        this.maxPierces = Mathf.Max(0, maxPierces);
        this.shooterClientId = ownerId;
        this.hasImpactExplosionAug = hasImpactExplosionAug;
        this.finalExplosionRadius = finalExplosionRadius;
        this.finalExplosionDamageFactor = finalExplosionDamageFactor;
        this.chargePercentCached = Mathf.Clamp01(chargePercent);

        // Kettenanzahl optional skalieren (hier schlicht Basiskonstante)
        this.chainCount = baseChainCount;

        // ExplosionRadius evtl. leicht mit Charge skalieren:
        this.scaledExplosionRadius = baseExplosionRadius * Mathf.Lerp(1f, 1.5f, chargePercentCached);

        rb.linearVelocity = direction.normalized * speed;
        transform.forward = rb.linearVelocity.normalized;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        Vector3 hitPoint = collision.GetContact(0).point;

        // Pro-Treffer AoE: Schaden variiert je nach Pierce-Phase
        float useDmg = (currentPierces >= 1) ? damageAfterPierced : damageNonPierced;
        DoAreaDamage(hitPoint, scaledExplosionRadius, useDmg);

        // Chain Lightning (optional)
        if (chainCount > 0)
        {
            DoChainLightning(hitPoint, chainCount, chainRange, useDmg * chainDamageMultiplier);
        }

        // KUGEL DESPAWNED:

        //currentPierces++;
//
        //if (currentPierces > maxPierces)
        //{
        //    // Finale Explosion (nur wenn Augment aktiv)
        //    FinalExplosion();
        //    NetworkObject.Despawn();
        //    return;
        //}

        // Durchdringen aktiv lassen:
        Physics.IgnoreCollision(myCol, collision.collider, true);
        rb.position += transform.forward * 0.05f;

        // Geschwindigkeit etwas reduzieren
        float speedFactor = Mathf.Clamp01(1f - perHitSpeedLoss * currentPierces);
        rb.linearVelocity = transform.forward * (speed * speedFactor);

        ExplosionClientRpc(hitPoint, 1f);
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
        if (!hasImpactExplosionAug) return;
        Vector3 pos = transform.position;
        float finalDmg = damageAfterPierced * finalExplosionDamageFactor;
        DoAreaDamage(pos, finalExplosionRadius, finalDmg);
        ExplosionClientRpc(pos, finalExplosionRadius / Mathf.Max(0.0001f, scaledExplosionRadius)); // skaliertes VFX
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
