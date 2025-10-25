using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class GrenadeProjectile : NetworkBehaviour
{
    [Header("Core Flight")]
    public float hardMaxFlightTime = 1.0f;
    public float maxLifetime = 3.0f;
    public float gravityMultiplier = 1.05f;
    public float maxSpeed = 65f;

    [Header("Rocket Assist")]
    public bool rocketAssist = true;
    public float boostDelay = 0.08f;
    public float boostDuration = 0.22f;
    public float boostAcceleration = 120f;

    [Header("Soft Homing (last meters)")]
    public bool softHoming = true;
    public float homingStartDistance = 12f;
    public float turnRateDegPerSec = 320f;
    public float homingConeDotMin = 0.6f;
    public LayerMask losMask = ~0;

    [Header("Fusing")]
    public bool explodeOnAnyCollision = false;
    public bool explodeOnImpactWithEnemies = true;
    public float proximityRadius = 1.25f;
    public bool airburstAtApex = true;
    public float airburstMinHeight = 0.8f;
    public LayerMask damageMask = ~0;

    [Header("Explosion")]
    // HINWEIS: Schaden kommt nun vom Controller. Dieser Wert wird NICHT mehr genutzt.
    [Tooltip("Wird ignoriert – Damage wird vom GrenadeLauncherController gesetzt.")]
    public float baseDamage = 40f;
    public float radius = 4.5f;
    public float force = 700f;
    public float upwardModifier = 0.35f;
    public AnimationCurve damageFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("VFX/SFX")]
    public GameObject explosionVFXPrefab;
    public AudioSource explosionAudioSourceOverride;
    [Range(0f, 2f)] public float explosionVolumeMultiplier = 1f;

    // --- Runtime ---
    private Rigidbody _rb;
    private float _spawnTime;
    private bool _exploded;
    private bool _boosting;
    private float _boostEndTime;
    private float _lastVy;
    private ulong _shooter;
    private AudioSource _explosionAudioSrc;

    // Ziel
    private NetworkObjectReference _targetRef;
    private Transform _cachedTarget;
    public float serverAutoAcquireRange = 18f;

    // >>> NEU: vom Controller gesetzter Schaden
    private float _baseDamageFromShooter = 40f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = true;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.None;

        var col = GetComponent<Collider>();
        if (col.material == null)
        {
            var pm = new PhysicsMaterial("Grenade_PM") { bounciness = 0.1f, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
            col.material = pm;
        }

        _explosionAudioSrc = explosionAudioSourceOverride != null ? explosionAudioSourceOverride : GetComponent<AudioSource>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        _rb.isKinematic = false;
        _spawnTime = Time.time;
        _lastVy = _rb.linearVelocity.y;

        if (hardMaxFlightTime > 0f) StartCoroutine(HardFuseRoutine());
        if (maxLifetime > 0f && maxLifetime > hardMaxFlightTime) StartCoroutine(LifetimeGuard());

        if (rocketAssist)
            StartCoroutine(RocketAssistRoutine());
    }

    // >>> Signatur ERWEITERT: Schaden kommt jetzt vom Controller
    public void ServerInit(Vector3 initialVelocity, ulong shooterClientId, float baseDamageFromShooter, NetworkObjectReference maybeTarget)
    {
        if (!IsServer) return;

        _shooter = shooterClientId;
        _targetRef = maybeTarget;
        _baseDamageFromShooter = Mathf.Max(0f, baseDamageFromShooter);

        _rb.linearVelocity = initialVelocity;
        if (initialVelocity.sqrMagnitude > 0.01f)
            transform.forward = initialVelocity.normalized;
    }

    private IEnumerator HardFuseRoutine()
    {
        yield return new WaitForSeconds(hardMaxFlightTime);
        if (!_exploded) ExplodeServer(Vector3.zero);
    }

    private IEnumerator LifetimeGuard()
    {
        yield return new WaitForSeconds(maxLifetime);
        if (!_exploded) ExplodeServer(Vector3.zero);
    }

    private IEnumerator RocketAssistRoutine()
    {
        yield return new WaitForSeconds(boostDelay);
        if (_exploded) yield break;

        _boosting = true;
        _boostEndTime = Time.time + boostDuration;

        while (!_exploded && Time.time < _boostEndTime)
        {
            _rb.AddForce(transform.forward * boostAcceleration, ForceMode.Acceleration);
            float spd = _rb.linearVelocity.magnitude;
            if (spd > maxSpeed) _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;

            yield return new WaitForFixedUpdate();
        }
        _boosting = false;
    }

    private void FixedUpdate()
    {
        if (!IsServer || _exploded) return;

        if (gravityMultiplier > 1.001f)
        {
            Vector3 extraG = Physics.gravity * (gravityMultiplier - 1f);
            _rb.AddForce(extraG, ForceMode.Acceleration);
        }

        if (softHoming)
            HomingStep();

        float sp = _rb.linearVelocity.magnitude;
        if (sp > maxSpeed) _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;

        float vy = _rb.linearVelocity.y;
        if (airburstAtApex && _lastVy > 0f && vy <= 0f)
        {
            if (transform.position.y - GetGroundY(transform.position) > airburstMinHeight)
            {
                if (EnemyNearby(proximityRadius * 1.2f, out _))
                    ExplodeServer(Vector3.zero);
            }
        }
        _lastVy = vy;

        if (EnemyNearby(proximityRadius, out var hitPoint))
            ExplodeServer(hitPoint);
    }

    private void HomingStep()
    {
        Transform t = ResolveTargetOrAcquire();
        if (t == null) return;

        Vector3 to = t.position + Vector3.up * 0.6f - transform.position;
        float dist = to.magnitude;
        if (dist > homingStartDistance) return;

        Vector3 v = _rb.linearVelocity;
        if (v.sqrMagnitude < 1e-3f) return;

        Vector3 dir = v.normalized;
        Vector3 desired = to.normalized;

        if (Vector3.Dot(dir, desired) < homingConeDotMin) return;
        if (!HasLineOfSight(transform.position, t.position + Vector3.up * 0.6f)) return;

        float maxTurnRad = Mathf.Deg2Rad * turnRateDegPerSec * Time.fixedDeltaTime;
        Vector3 newDir = Vector3.RotateTowards(dir, desired, maxTurnRad, Mathf.Infinity);

        _rb.linearVelocity = newDir * v.magnitude;
        transform.forward = newDir;
    }

    private Transform ResolveTargetOrAcquire()
    {
        if (_cachedTarget == null)
        {
            if (_targetRef.TryGet(out NetworkObject no) && no != null)
            {
                _cachedTarget = no.transform;
            }
            else
            {
                var hits = Physics.OverlapSphere(transform.position, serverAutoAcquireRange, damageMask, QueryTriggerInteraction.Ignore);
                float best = float.PositiveInfinity;
                Transform bestT = null;
                foreach (var c in hits)
                {
                    var enemy = c.GetComponentInParent<IEnemy>();
                    if (enemy == null) continue;
                    float d = (c.transform.position - transform.position).sqrMagnitude;
                    if (d < best) { best = d; bestT = c.transform; }
                }
                _cachedTarget = bestT;
            }
        }
        return _cachedTarget;
    }

    private bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= 0.01f) return true;
        return !Physics.Raycast(from, dir.normalized, dist, losMask, QueryTriggerInteraction.Ignore);
    }

    private bool EnemyNearby(float radius, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        var cols = Physics.OverlapSphere(transform.position, radius, damageMask, QueryTriggerInteraction.Ignore);
        foreach (var c in cols)
        {
            var enemy = c.GetComponentInParent<IEnemy>();
            if (enemy == null) continue;

            Vector3 pt = c.ClosestPoint(transform.position);
            if (HasLineOfSight(transform.position, pt))
            {
                hitPoint = pt;
                return true;
            }
        }
        return false;
    }

    private float GetGroundY(Vector3 pos)
    {
        if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return pos.y;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || _exploded) return;

        bool hitEnemy = collision.collider.GetComponentInParent<IEnemy>() != null;

        if (explodeOnAnyCollision || (explodeOnImpactWithEnemies && hitEnemy))
        {
            ExplodeServer(collision.GetContact(0).point);
            return;
        }

        ExplodeServer(collision.GetContact(0).point);
    }

    // ====================== EXPLOSION (SERVER) ======================

    private void ExplodeServer(Vector3 contactPoint)
    {
        if (_exploded) return;
        _exploded = true;

        Vector3 center = (contactPoint == Vector3.zero) ? transform.position : contactPoint;

        var cols = Physics.OverlapSphere(center, radius, damageMask, QueryTriggerInteraction.Ignore);
        var alreadyDamaged = new HashSet<IEnemy>();

        foreach (var c in cols)
        {
            Vector3 targetPoint = c.ClosestPoint(center);
            Vector3 dir = targetPoint - center;
            float dist = dir.magnitude;

            bool blocked = false;
            if (dist > 0.001f)
            {
                var hits = Physics.RaycastAll(center, dir.normalized, dist, damageMask, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var h in hits)
                {
                    if (h.collider.isTrigger) continue;
                    if (h.collider.transform.root == c.transform.root) { blocked = false; break; }
                    blocked = true; break;
                }
            }
            if (blocked) continue;

            float nd = Mathf.Clamp01(dist / radius);
            float dmg = _baseDamageFromShooter * damageFalloff.Evaluate(nd);

            var enemy = c.GetComponentInParent<IEnemy>();
            if (enemy != null && dmg > 0.05f && alreadyDamaged.Add(enemy))
                enemy.TakeDamage(dmg, _shooter, targetPoint);

            if (c.attachedRigidbody != null)
                c.attachedRigidbody.AddExplosionForce(force, center, radius, upwardModifier, ForceMode.Impulse);
        }

        SpawnExplosionClientRpc(center);
        NetworkObject.Despawn();
    }

    [ClientRpc]
    private void SpawnExplosionClientRpc(Vector3 pos)
    {
        if (explosionVFXPrefab != null)
        {
            var go = Instantiate(explosionVFXPrefab, pos, Quaternion.identity);
            var auto = go.GetComponent<ExplosionDecalOrVFX>();
            if (auto != null) auto.PlayAndAutoDestroy();
            else Destroy(go, 5f);
        }

        if (_explosionAudioSrc != null && _explosionAudioSrc.clip != null)
            PlayOneShotAtPoint(_explosionAudioSrc.clip, pos, _explosionAudioSrc, explosionVolumeMultiplier);
    }

    private static void PlayOneShotAtPoint(AudioClip clip, Vector3 pos, AudioSource template, float volumeMul = 1f)
    {
        if (clip == null) return;
        var go = new GameObject("GrenadeExplosionAudioProxy");
        go.transform.position = pos;

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = (template ? template.volume : 1f) * Mathf.Clamp01(volumeMul);
        src.pitch = template ? template.pitch : 1f;
        src.spatialBlend = template ? template.spatialBlend : 1f;
        src.minDistance = template ? template.minDistance : 1f;
        src.maxDistance = template ? template.maxDistance : 50f;
        src.rolloffMode = template ? template.rolloffMode : AudioRolloffMode.Logarithmic;
        src.dopplerLevel = template ? template.dopplerLevel : 1f;
        src.spread = template ? template.spread : 0f;
        src.outputAudioMixerGroup = template ? template.outputAudioMixerGroup : null;
        src.reverbZoneMix = template ? template.reverbZoneMix : 1f;
        src.playOnAwake = false;
        src.loop = false;

        src.Play();
        UnityEngine.Object.Destroy(go, clip.length + 0.15f);
    }

    public override void OnNetworkDespawn()
    {
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }
    }
}
