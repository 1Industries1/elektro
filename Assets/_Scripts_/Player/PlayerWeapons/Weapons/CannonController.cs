using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class CannonController : NetworkBehaviour
{
    [Header("Weapon Data")]
    public WeaponDefinition weaponDef; // Cannon.asset
    private PlayerWeapons _playerWeapons;
    [SerializeField] private OverclockRuntime overclocks;

    [Header("Bullet Settings (fallback)")]
    public NetworkObject bulletPrefab;       // falls weaponDef.bulletPrefab leer ist
    public Transform bulletSpawnPoint;
    public float inaccuracyAngle = 1f;


    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip fireSFX;
    private float _lastFireSfxTime;
    [SerializeField] private float fireSfxMinInterval = 0.05f;

    [Header("Interpolation")]
    public float rotationLerpRate = 15f;
    public Quaternion AimRotation => transform.rotation;

    // ==== AUTO SHOOT ====
    [Header("Auto Shoot")]
    public float targetRange = 20f;
    public float retargetInterval = 0.15f;
    [Range(0f, 1f)] public float minDotToShoot = 0.95f; 
    public LayerMask enemyLayer;
    public LayerMask lineOfSightMask; 
    public string eliteTag = "Elite";

    public Transform CurrentTarget => currentTarget;
    public Vector3 AimOrigin => bulletSpawnPoint ? bulletSpawnPoint.position : transform.position;

    [Header("Aim Lead (Vorhalt)")]
    public bool useAimLead = true;
    public float primaryBulletSpeedHint = 35f; // m/s

    [Header("Fire Rate Jitter")]
    [Range(0f, 0.5f)] public float fireRateJitterPct = 0.08f;   // ±8% um cd
    private float nextFireTime = 0f;

    private float fireButtonHoldTime = 0f;

    // Auto-Target Cache
    private Transform currentTarget;
    private float nextRetargetTime = 0f;

    public NetworkVariable<Quaternion> NetworkRotation = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    // Server: Anti-Exploit Cooldown-Tracking
    private readonly Dictionary<ulong, float> _lastPrimaryFire = new();

    // Runtime / Owner
    private PlayerMovement _ownerMovement;
    private PlayerHealth _health;
    private PlayerUpgrades _upgrades;
    private WeaponRuntime _runtime;

    public override void OnNetworkSpawn()
    {
        _health = GetComponentInParent<PlayerHealth>();
        _ownerMovement = GetComponentInParent<PlayerMovement>();
        _upgrades = GetComponentInParent<PlayerUpgrades>();
        if (!overclocks) overclocks = GetComponentInParent<OverclockRuntime>();

        // PlayerWeapons suchen & anbinden
        _playerWeapons = GetComponentInParent<PlayerWeapons>();
        if (_playerWeapons != null)
        {
            _playerWeapons.RuntimesRebuilt += OnWeaponsRebuilt;
        }

        // Lokale Runtime initial aufbauen (Server + Owner + Zuschauer ok, da read-only)
        BuildLocalRuntime();

        // Hints (Range/Speed) aus Definition (oder Runtime) setzen
        ApplyRangeAndSpeedHints();
    }

    public override void OnNetworkDespawn()
    {
        if (_playerWeapons != null)
            _playerWeapons.RuntimesRebuilt -= OnWeaponsRebuilt;
    }
    
    private void OnWeaponsRebuilt()
    {
        // Level hat sich geändert -> lokale Runtime neu holen/rekonstruieren
        BuildLocalRuntime();
        ApplyRangeAndSpeedHints();
    }

    private void BuildLocalRuntime()
    {
        // Wenn PlayerWeapons existiert, nimm deren Runtime (Level-synchron)
        if (_playerWeapons != null && _playerWeapons.CannonRuntime != null)
        {
            // Eigenes Exemplar, damit wir nicht aus Versehen Referenzen teilen
            _runtime = new WeaponRuntime(_playerWeapons.cannonDef, _playerWeapons.cannonLevel.Value);
            if (_upgrades)  _upgrades.ApplyTo(_runtime);
            // Overclocks wirken bei Schussberechnung dynamisch (GetEffectiveFireRateSeconds), dmgMult additiv hier optional
        }
        else if (weaponDef != null)
        {
            // Fallback (z. B. im Editor ohne PlayerWeapons)
            _runtime = new WeaponRuntime(weaponDef, 1);
            if (_upgrades) _upgrades.ApplyTo(_runtime);
        }
    }

    private void ApplyRangeAndSpeedHints()
    {
        // Zielweite aus Definition; Geschwindigkeits-Hint aus Runtime (falls Level sie ändert)
        if (weaponDef != null)
        {
            targetRange = weaponDef.rangeMeters > 0 ? weaponDef.rangeMeters : targetRange;
        }
        if (_runtime != null)
        {
            primaryBulletSpeedHint = _runtime.projectileSpeed > 0 ? _runtime.projectileSpeed : primaryBulletSpeedHint;
        }
    }

    private void Update()
    {
        if (!IsOwner)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, NetworkRotation.Value, Time.deltaTime * rotationLerpRate);
            return;
        }
        AutoAimAndFire();
    }

    // ===================== AUTO AIM / AUTO FIRE =====================

    private void AutoAimAndFire()
    {
        // 1) Ziel aktualisieren
        if (Time.time >= nextRetargetTime || currentTarget == null)
        {
            nextRetargetTime = Time.time + retargetInterval;
            currentTarget = AcquireTarget();
        }

        // 2) Ausrichten
        if (currentTarget != null)
        {
            Vector3 aimPos = PredictAimPoint(currentTarget, useAimLead ? primaryBulletSpeedHint : 0f);
            Vector3 dir = aimPos - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                var targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = targetRot;
                NetworkRotation.Value = targetRot;
            }
        }

        // 3) Primärschuss automatisch abfeuern
        if (currentTarget != null && _runtime != null)
        {
            Vector3 forward  = transform.forward;
            Vector3 toTarget = (currentTarget.position - transform.position).normalized;
            float dot = Vector3.Dot(forward, toTarget);

            bool cdReady = Time.time >= nextFireTime;
            bool okDot   = dot >= minDotToShoot;
            bool los     = HasLineOfSight(currentTarget);

            if (cdReady && okDot && los)
            {
                fireButtonHoldTime += Time.deltaTime; // simuliert „halten“ für Streuung
                float baseCd = _runtime.GetCooldownSeconds();
                // Overclocks können Sekunden-pro-Schuss verändern:
                if (overclocks) baseCd = overclocks.GetEffectiveFireRateSeconds(baseCd);
                float cd = NextCooldown(baseCd, fireRateJitterPct);
                nextFireTime = Time.time + cd;
                RequestFireServerRpc(bulletSpawnPoint.position, bulletSpawnPoint.rotation, fireButtonHoldTime, cd);
            }
            else
            {
                fireButtonHoldTime = 0f;
            }
        }
        else
        {
            fireButtonHoldTime = 0f;
        }
    }

    private Transform AcquireTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, targetRange, enemyLayer, QueryTriggerInteraction.Ignore);
        Transform best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform t = hits[i].transform;
            if (!HasLineOfSight(t)) continue;

            Vector3 to = (t.position - transform.position);
            float dist = to.magnitude;
            if (dist <= 0.1f) continue;

            float dot = Vector3.Dot(transform.forward, to.normalized);
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
        Vector3 origin = bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position + Vector3.up * 1.2f;
        Vector3 target = t.position + Vector3.up * 0.8f;
        Vector3 dir = (target - origin);
        float dist = dir.magnitude;
        if (dist <= 0.01f) return true;

        return !Physics.Raycast(origin, dir.normalized, dist, lineOfSightMask, QueryTriggerInteraction.Ignore);
    }

    private Vector3 PredictAimPoint(Transform target, float projectileSpeed)
    {
        Vector3 targetPos = target.position + Vector3.up * 0.6f;

        if (!useAimLead || projectileSpeed <= 0.1f) return targetPos;

        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb == null) return targetPos;

        Vector3 v = rb.linearVelocity;
        Vector3 p = bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position;

        Vector3 toTarget = targetPos - p;
        float distance = toTarget.magnitude;
        float t = distance / Mathf.Max(0.1f, projectileSpeed);

        return targetPos + v * t;
    }

    public bool TryAimTowards(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        if (dir.sqrMagnitude <= 0.0001f) return false;

        var targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = targetRot;
        NetworkRotation.Value = targetRot;
        return true;
    }

    private float NextCooldown(float baseSecondsPerShot, float jitterPct)
    {
        if (jitterPct <= 0f) return Mathf.Max(0.01f, baseSecondsPerShot);
        float r = Random.Range(-jitterPct, jitterPct); // symmetrisch um 0
        return Mathf.Max(0.01f, baseSecondsPerShot * (1f + r));
    }

    // ===================== SERVER RPCs =====================

    [ServerRpc]
    private void RequestFireServerRpc(Vector3 position, Quaternion rotation, float holdTime, float clientCooldown)
    {
        if (_health != null && _health.IsDead()) return;
        if (_runtime == null) return;

        float now = Time.time;

        _lastPrimaryFire.TryGetValue(OwnerClientId, out float last);

        // Erlaubte Cooldown-Grenzen (serverseitig)
        float baseCd = Mathf.Max(0.01f, _runtime.GetCooldownSeconds());
        if (overclocks) baseCd = overclocks.GetEffectiveFireRateSeconds(baseCd);
        float minCd = Mathf.Max(0.01f, baseCd * (1f - fireRateJitterPct));
        float maxCd = Mathf.Max(0.01f, baseCd * (1f + fireRateJitterPct));

        // Validierung
        if (clientCooldown < minCd || clientCooldown > maxCd) return;
        if (now - last < clientCooldown) return;

        _lastPrimaryFire[OwnerClientId] = now;

        float dynamicInaccuracy = inaccuracyAngle + Mathf.Clamp(holdTime * 1f, 0f, 15f);
        FireCannon(position, rotation, dynamicInaccuracy);
    }

    // ===================== SERVER-only Projektil-Spawn =====================

    private float GetRollDamageMultiplier()
    {
        if (_ownerMovement == null) _ownerMovement = GetComponentInParent<PlayerMovement>();
        if (_ownerMovement == null) return 1f;
        return _ownerMovement.ServerRollHeld ? 0.5f : 1.5f; // wie in deinem alten Script
    }

    private void FireCannon(Vector3 position, Quaternion rotation, float inaccuracy)
    {
        // Streuung
        Vector3 baseFwd = rotation * Vector3.forward;
        Vector3 upAxis  = rotation * Vector3.up;
        Vector3 rightAxis = rotation * Vector3.right;

        float yaw  = Random.Range(-inaccuracy, inaccuracy);
        float pitch = Random.Range(-inaccuracy, inaccuracy);

        Vector3 deviatedDir = Quaternion.AngleAxis(yaw, upAxis) * (Quaternion.AngleAxis(pitch, rightAxis) * baseFwd);
        Quaternion fireRotation = Quaternion.LookRotation(deviatedDir.normalized, upAxis);

        // Prefab aus Definition bevorzugen
        NetworkObject prefab = weaponDef && weaponDef.bulletPrefab ? weaponDef.bulletPrefab : bulletPrefab;
        NetworkObject bulletNetObj = Instantiate(prefab, position, fireRotation);
        bulletNetObj.Spawn(true);

        var bullet = bulletNetObj.GetComponent<BulletController>();

        // Schaden serverseitig
        float dmgScale   = GetRollDamageMultiplier()
                         * (_upgrades ? _upgrades.GetDamageMultiplier() : 1f)
                         * (overclocks ? overclocks.GetDamageMult() : 1f);

        // vor erstem Pierce
        float baseDmgNP = _runtime.ComputeDamageNonPierced(applyCrit: true);
        // nach >=1x Pierce (Pierce Mastery)
        float baseDmgP  = _runtime.ComputeDamagePierced(applyCrit: true);

        float finalNonPierced = baseDmgNP * dmgScale;
        float finalPierced    = baseDmgP  * dmgScale;

        int pierceCount = Mathf.Max(0, _runtime.pierce);

        bullet.Init(
            direction: fireRotation * Vector3.forward,
            newSpeed: _runtime.projectileSpeed,
            dmgNonPierced: finalNonPierced,
            dmgAfterPierced: finalPierced,
            pierceCount: pierceCount,
            ownerId: OwnerClientId
        );

        PlayFireSoundClientRpc(Random.Range(0.8f, 1.8f));

        var clientParams = new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        DoCameraShakeClientRpc(0.1f, 0.15f, clientParams);
    }

    // ===================== CLIENT RPCs =====================
    [ClientRpc]
    private void PlayFireSoundClientRpc(float pitch)
    {
        if (audioSource == null) return;
        AudioClip clip = fireSFX;
        if (clip == null) return;

        float now = Time.time;
        if (now - _lastFireSfxTime < fireSfxMinInterval) return;
        _lastFireSfxTime = now;
        
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, 1f);
    }

    [ClientRpc]
    private void DoCameraShakeClientRpc(float intensity, float duration, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        var cam = Camera.main;
        if (cam != null)
        {
            var shake = cam.GetComponent<CameraShake>();
            if (shake != null)
                shake.Shake(intensity, duration);
        }
    }
}
