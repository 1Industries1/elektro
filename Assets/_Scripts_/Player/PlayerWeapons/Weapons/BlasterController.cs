using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BlasterController : NetworkBehaviour
{
    [Header("Refs")]
    [Tooltip("Spawn-Point für Blasterprojektile. Falls leer, wird transform.position/rotation genutzt.")]
    public Transform bulletSpawnPoint;

    [Header("Weapon Data")]
    public WeaponDefinition weaponDef; // Blaster.asset
    private PlayerWeapons _playerWeapons;

    [Header("(legacy)")]
    public NetworkObject altBulletPrefab; // Fallback, wenn weaponDef.bulletPrefab leer ist
    public float fireRate = 1.5f; // legacy; Runtime übernimmt
    [Range(0f, 0.5f)] public float altFireRateJitterPct = 0.05f;

    [Header("Accuracy / Charge")]
    public float altInaccuracyAngle = 1f;
    public float chargeTime = 3f;

    [Header("Auto-Use")]
    public bool autoAltFire = true;
    public string eliteTag = "Elite";
    public float clusterRadius = 4f;
    public int clusterCountThreshold = 3;

    [Header("Auto Aim / Targeting")]
    public bool useAutoAim = true;
    public float retargetInterval = 0.15f;
    public bool useAimLead = true;
    public float altBulletSpeedHint = 25f; // m/s
    public LayerMask enemyLayer;
    public LayerMask lineOfSightMask = ~0;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip altFireSFX;
    public AudioClip chargeLoopSFX;

    // --- intern ---
    private float _nextAltFireTime;
    private bool _isCharging;
    private float _chargeStartTime;
    private AudioSource _chargeAudioSource;

    private PlayerMovement _ownerMovement;
    private PlayerUpgrades _upgrades;

    // Targeting Cache
    private Transform _currentTarget;
    private float _nextRetargetTime;

    // Server: Anti-Exploit Cooldown-Tracking
    private readonly Dictionary<ulong, float> _lastAltFire = new();

    // Runtime
    private WeaponRuntime _runtime;

    private void Start()
    {
        if (chargeLoopSFX != null)
        {
            _chargeAudioSource = gameObject.AddComponent<AudioSource>();
            _chargeAudioSource.clip = chargeLoopSFX;
            _chargeAudioSource.loop = true;
            _chargeAudioSource.playOnAwake = false;
            _chargeAudioSource.volume = 0.75f;
        }
    }

    public override void OnNetworkSpawn()
    {
        _ownerMovement = GetComponentInParent<PlayerMovement>();
        _upgrades = GetComponentInParent<PlayerUpgrades>();

        // PlayerWeapons / Runtime
        _playerWeapons = GetComponentInParent<PlayerWeapons>();
        if (_playerWeapons != null)
            _playerWeapons.RuntimesRebuilt += OnWeaponsRebuilt;

        BuildLocalRuntime();
        ApplySpeedHint();
    }

    public override void OnNetworkDespawn()
    {
        if (_playerWeapons != null)
            _playerWeapons.RuntimesRebuilt -= OnWeaponsRebuilt;
    }

    private void OnWeaponsRebuilt()
    {
        BuildLocalRuntime();
        ApplySpeedHint();
    }

    private void BuildLocalRuntime()
    {
        _runtime = null; // harte Null

        if (_playerWeapons != null)
        {
            if (_playerWeapons.blasterLevel.Value > 0 && _playerWeapons.BlasterRuntime != null)
            {
                _runtime = new WeaponRuntime(_playerWeapons.blasterDef, _playerWeapons.blasterLevel.Value);
                if (_upgrades) _upgrades.ApplyTo(_runtime);
            }
        }
        else if (weaponDef != null)
        {
            var nm = NetworkManager.Singleton;
            bool netInactive = (nm == null || !nm.IsListening);
            if (netInactive)
            {
                _runtime = new WeaponRuntime(weaponDef, 1);
                if (_upgrades) _upgrades.ApplyTo(_runtime);
            }
        }
    }

    private void ApplySpeedHint()
    {
        if (_runtime != null)
            altBulletSpeedHint = _runtime.projectileSpeed > 0 ? _runtime.projectileSpeed : altBulletSpeedHint;
        else if (weaponDef != null)
            altBulletSpeedHint = weaponDef.projectileSpeed > 0 ? weaponDef.projectileSpeed : altBulletSpeedHint;
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (_runtime == null) { _isCharging = false; return; }
        if (!autoAltFire) return;
        if (Time.time < _nextAltFireTime) return;

        // Auto Aim / Target aktualisieren
        if (useAutoAim)
        {
            if (Time.time >= _nextRetargetTime || _currentTarget == null)
            {
                _nextRetargetTime = Time.time + retargetInterval;
                _currentTarget = AcquireTarget();
            }
        }

        Transform tgt = _currentTarget;
        bool shouldAlt = false;

        if (tgt != null && !string.IsNullOrEmpty(eliteTag) && tgt.CompareTag(eliteTag))
            shouldAlt = true;

        if (!shouldAlt && tgt != null)
        {
            int c = CountEnemiesInSphere(tgt.position, clusterRadius);
            if (c >= clusterCountThreshold) shouldAlt = true;
        }

        if (shouldAlt)
            StartCoroutine(AutoChargeAndRelease());
    }

    // ===== Auto-Charge/Release =====

    private IEnumerator AutoChargeAndRelease()
    {
        if (_isCharging || _runtime == null) yield break;

        _isCharging = true;
        _chargeStartTime = Time.time;

        if (_chargeAudioSource && !_chargeAudioSource.isPlaying)
            _chargeAudioSource.Play();

        float desiredHold = Mathf.Clamp(chargeTime * 0.6f, 0.4f, 1.2f);
        float endTime = Time.time + desiredHold;

        while (Time.time < endTime && IsOwner)
        {
            Transform tgt = _currentTarget;
            if (tgt == null) break;

            Vector3 aimPos = PredictAimPoint(tgt, useAimLead ? altBulletSpeedHint : 0f);
            TryAimTowards(aimPos);
            yield return null;
        }

        float heldTime = Time.time - _chargeStartTime;

        float baseCd = _runtime != null ? _runtime.GetCooldownSeconds() : fireRate;
        float altCd = NextCooldown(baseCd, altFireRateJitterPct);

        Vector3 pos = bulletSpawnPoint ? bulletSpawnPoint.position : transform.position;
        Quaternion rot = transform.rotation;

        RequestChargedShotServerRpc(pos, rot, heldTime, altCd);

        _nextAltFireTime = Time.time + altCd;
        _isCharging = false;

        if (_chargeAudioSource && _chargeAudioSource.isPlaying)
            _chargeAudioSource.Stop();
    }

    private float NextCooldown(float baseRate, float jitterPct)
    {
        if (jitterPct <= 0f) return Mathf.Max(0.01f, baseRate);
        float r = Random.Range(-jitterPct, jitterPct);
        return Mathf.Max(0.01f, baseRate * (1f + r));
    }

    // ===== Target/Aim helpers =====

    private float GetTargetRange()
    {
        if (_runtime != null && _runtime.rangeMeters > 0f)
            return _runtime.rangeMeters;

        if (weaponDef != null && weaponDef.rangeMeters > 0f)
            return weaponDef.rangeMeters;
            
        return 12f;
    }


    private Transform AcquireTarget()
    {
        float range = GetTargetRange();
        Collider[] hits = Physics.OverlapSphere(transform.position, range, enemyLayer, QueryTriggerInteraction.Ignore);
        Transform best = null;
        float bestScore = float.PositiveInfinity;

        Vector3 origin = bulletSpawnPoint ? bulletSpawnPoint.position : transform.position;
        Vector3 forward = transform.forward;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform t = hits[i].transform;
            if (!HasLineOfSight(t)) continue;

            Vector3 to = (t.position - origin);
            float dist = to.magnitude;
            if (dist <= 0.1f) continue;

            float dot = Vector3.Dot(forward, to.normalized);
            float score = dist * Mathf.Lerp(2f, 1f, Mathf.InverseLerp(0.5f, 1f, dot));
            if (score < bestScore)
            {
                bestScore = score;
                best = t;
            }
        }
        return best;
    }

    private bool HasLineOfSight(Transform t)
    {
        Vector3 origin = bulletSpawnPoint ? bulletSpawnPoint.position : transform.position + Vector3.up * 1.2f;
        Vector3 target = t.position + Vector3.up * 0.8f;
        Vector3 dir = (target - origin);
        float dist = dir.magnitude;
        if (dist <= 0.01f) return true;

        return !Physics.Raycast(origin, dir.normalized, dist, lineOfSightMask, QueryTriggerInteraction.Ignore);
    }

    private bool TryAimTowards(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        if (dir.sqrMagnitude <= 0.0001f) return false;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        return true;
    }

    private Vector3 PredictAimPoint(Transform target, float projectileSpeed)
    {
        Vector3 targetPos = target.position + Vector3.up * 0.6f;
        if (!useAimLead || projectileSpeed <= 0.1f) return targetPos;

        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (!rb) return targetPos;

        Vector3 v = rb.linearVelocity;
        Vector3 p = bulletSpawnPoint ? bulletSpawnPoint.position : transform.position;

        Vector3 toTarget = targetPos - p;
        float distance = toTarget.magnitude;
        float t = distance / Mathf.Max(0.1f, projectileSpeed);

        return targetPos + v * t;
    }

    private int CountEnemiesInSphere(Vector3 center, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, enemyLayer, QueryTriggerInteraction.Ignore);
        return hits?.Length ?? 0;
    }

    // ===== Server RPCs =====

    [ServerRpc]
    private void RequestChargedShotServerRpc(Vector3 position, Quaternion rotation, float heldTime, float clientCooldown)
    {
        if (_runtime == null) return;

        float now = Time.time;
        _lastAltFire.TryGetValue(OwnerClientId, out float last);

        float baseCd = Mathf.Max(0.01f, _runtime != null ? _runtime.GetCooldownSeconds() : fireRate);
        float minCd = Mathf.Max(0.01f, baseCd * (1f - altFireRateJitterPct));
        float maxCd = Mathf.Max(0.01f, baseCd * (1f + altFireRateJitterPct));

        if (clientCooldown < minCd || clientCooldown > maxCd) return;
        if (now - last < clientCooldown) return;

        float chargePercent = Mathf.Clamp01(heldTime / chargeTime);

        _lastAltFire[OwnerClientId] = now;
        FireChargedShot(position, rotation, chargePercent);
    }

    // ===== Server-only spawn =====

    private float GetRollDamageMultiplier()
    {
        if (_ownerMovement == null) _ownerMovement = GetComponentInParent<PlayerMovement>();
        if (_ownerMovement == null) return 1f;
        return _ownerMovement.ServerRollHeld ? 0.5f : 1.5f;
    }

    private void FireChargedShot(Vector3 position, Quaternion rotation, float chargePercent)
    {
        if (_runtime == null) return;

        Vector3 baseFwd = rotation * Vector3.forward;
        Vector3 upAxis = rotation * Vector3.up;
        Vector3 rightAxis = rotation * Vector3.right;

        float yaw = Random.Range(-altInaccuracyAngle, altInaccuracyAngle);
        float pitch = Random.Range(-altInaccuracyAngle, altInaccuracyAngle);

        Vector3 deviatedDir = Quaternion.AngleAxis(yaw, upAxis) * (Quaternion.AngleAxis(pitch, rightAxis) * baseFwd);
        Quaternion fireRotation = Quaternion.LookRotation(deviatedDir.normalized, upAxis);

        NetworkObject prefab = weaponDef && weaponDef.bulletPrefab ? weaponDef.bulletPrefab : altBulletPrefab;
        NetworkObject bulletNetObj = Instantiate(prefab, position, fireRotation);
        bulletNetObj.Spawn(true);

        BlasterBulletController bullet = bulletNetObj.GetComponent<BlasterBulletController>();

        // Damage-Pfade aus Runtime
        float dmgScale = GetRollDamageMultiplier() * (_upgrades ? _upgrades.GetDamageMultiplier() : 1f);
        float baseNonP = _runtime.ComputeDamageNonPierced(applyCrit: true);
        float basePier = _runtime.ComputeDamagePierced(applyCrit: true);

        // Charge skaliert Schaden/Speed
        float chargeScaleDmg = Mathf.Lerp(1f, 3f, chargePercent);
        float chargeScaleSpd = Mathf.Lerp(1f, 3.5f, chargePercent);

        float damageNonPierced = baseNonP * dmgScale * chargeScaleDmg;
        float damageAfterPierced = basePier * dmgScale * chargeScaleDmg;

        float speed = _runtime.projectileSpeed * chargeScaleSpd;

        bullet.InitBlaster(
            direction: fireRotation * Vector3.forward,
            speed: speed,
            damageNonPierced: damageNonPierced,
            damageAfterPierced: damageAfterPierced,
            maxPierces: Mathf.Max(0, _runtime.pierce),
            ownerId: OwnerClientId,
            hasImpactExplosionAug: _runtime.hasImpactExplosionAug,
            finalExplosionRadius: 2.5f,
            finalExplosionDamageFactor: 0.5f,
            baseExplosionRadius: 4f // dein Standard
        );

        PlayAltFireSoundClientRpc(Random.Range(0.7f, 1.2f));

        var clientParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        DoCameraShakeClientRpc(1.2f, 0.25f, clientParams);
    }

    // ===== Client RPCs (Audio/FX) =====
    [ClientRpc]
    private void PlayAltFireSoundClientRpc(float pitch)
    {
        if (!audioSource || !altFireSFX) return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(altFireSFX, 1f);
    }

    [ClientRpc]
    private void DoCameraShakeClientRpc(float intensity, float duration, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        var cam = Camera.main;
        if (!cam) return;
        var shake = cam.GetComponent<CameraShake>();
        if (shake) shake.Shake(intensity, duration);
    }
}
