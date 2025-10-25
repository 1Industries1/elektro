using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class CannonController : NetworkBehaviour
{
    [Header("Bullet Settings")]
    public NetworkObject bulletPrefab;
    public NetworkObject altBulletPrefab;
    public Transform bulletSpawnPoint;
    public float fireRate = 0.5f;
    public float altFireRate = 1.5f;

    public float inaccuracyAngle = 5f;
    public float altInaccuracyAngle = 1f;

    [Header("Charge Shot Settings")]
    public float chargeTime = 3f;

    [Header("Damage")]
    public Vector2 primaryDamageRange = new Vector2(20f, 40f);
    public float altBaseDamage = 120f;

    [Header("Damage Multipliers")]
    [Tooltip("Schadensfaktor während Roll_Anim (LSHIFT gehalten). < 1 für Nerf beim Rollen.")]
    public float damageWhileRolling = 0.5f;
    [Tooltip("Schadensfaktor wenn NICHT gerollt wird. > 1 für Buff im Stand/Lauf.")]
    public float damageWhileNotRolling = 1.5f;


    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip fireSFX;
    public AudioClip altFireSFX;
    public AudioClip chargeLoopSFX;
    private float _lastFireSfxTime;
    [SerializeField] private float fireSfxMinInterval = 0.05f;


    [Header("Interpolation")]
    public float rotationLerpRate = 15f;


    [Header("Upgrades")]
    [SerializeField] private OverclockRuntime overclocks;


    // ==== AUTO SHOOT ====
    [Header("Auto Shoot")]
    
    public bool autoShootEnabled = true;
    public float targetRange = 30f;
    public float retargetInterval = 0.15f;
    [Range(0f, 1f)] public float minDotToShoot = 0.95f; // wie "genau" wir hinzeigen müssen
    public LayerMask enemyLayer;
    public LayerMask lineOfSightMask; // alles, was Sicht blockiert (Wände etc.)
    public string eliteTag = "Elite";


    [Header("Aim Lead (Vorhalt)")]
    public bool useAimLead = true;
    public float primaryBulletSpeedHint = 35f; // m/s
    public float altBulletSpeedHint = 25f;     // m/s


    private PlayerMovement _ownerMovement; // Cache
    private PlayerHealth _health;
    private PlayerUpgrades _upgrades;


    [Header("Alt Fire: Automatik-Logik")]
    public bool autoAltFire = true;
    public float clusterRadius = 4f;
    public int clusterCountThreshold = 3;
    

    [Header("Fire Rate Jitter")]
    [Range(0f, 0.5f)] public float fireRateJitterPct = 0.08f;   // ±8% um fireRate
    [Range(0f, 0.5f)] public float altFireRateJitterPct = 0.05f; // ±5% um altFireRate

    private float nextFireTime = 0f;
    private float nextAltFireTime = 0f;
    private float chargeStartTime;
    private bool isCharging = false;
    private AudioSource chargeAudioSource;

    // nur noch für manuell (falls autoShootEnabled=false)
    private float fireButtonHoldTime = 0f;
    private Vector3 targetPoint;

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
    private readonly Dictionary<ulong, float> _lastAltFire = new();

    private void Start()
    {
        if (chargeLoopSFX != null)
        {
            chargeAudioSource = gameObject.AddComponent<AudioSource>();
            chargeAudioSource.clip = chargeLoopSFX;
            chargeAudioSource.loop = true;
            chargeAudioSource.playOnAwake = false;
            chargeAudioSource.volume = 0.75f;
        }
    }

    public override void OnNetworkSpawn()
    {
        _health = GetComponentInParent<PlayerHealth>();
        _ownerMovement = GetComponentInParent<PlayerMovement>();
        _upgrades = GetComponentInParent<PlayerUpgrades>();
        if (!overclocks) overclocks = GetComponentInParent<OverclockRuntime>();
    }

    private void Update()
    {
        if (!IsOwner)
        {
            // Remote: nur Rotation glätten
            transform.rotation = Quaternion.Slerp(transform.rotation, NetworkRotation.Value, Time.deltaTime * rotationLerpRate);
            return;
        }

        if (autoShootEnabled)
        {
            AutoAimAndFire();
        }
        else
        {
            // Fallback: manuelles Zielen/Schießen
            AimWithMouse();
            HandleFiringManual();
        }
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
            Vector3 dir = aimPos - transform.position; // <<< geändert: dir.y NICHT nullen
            if (dir.sqrMagnitude > 0.0001f)
            {
                var targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = targetRot;
                NetworkRotation.Value = targetRot;
            }
        }

        // 3) Primärschuss automatisch abfeuern (Client setzt Cooldown mit Jitter, Server prüft)
        if (currentTarget != null)
        {
            Vector3 forward  = transform.forward;
            Vector3 toTarget = (currentTarget.position - transform.position).normalized; // <<< geändert: kein XZ-Projection
            float dot = Vector3.Dot(forward, toTarget);

            bool cdReady = Time.time >= nextFireTime;
            bool okDot   = dot >= minDotToShoot;
            bool los     = HasLineOfSight(currentTarget);

            if (cdReady && okDot && los)
            {
                fireButtonHoldTime += Time.deltaTime; // simuliert „halten“ für Streuung
                float effPrimary = overclocks ? overclocks.GetEffectiveFireRateSeconds(fireRate) : fireRate;
                float cd = NextCooldown(effPrimary, fireRateJitterPct);
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

        // 4) Alt-Fire (Charged) automatisch verwenden?
        if (autoAltFire && Time.time >= nextAltFireTime)
        {
            bool shouldAltFire = false;

            if (currentTarget != null && !string.IsNullOrEmpty(eliteTag) && currentTarget.CompareTag(eliteTag))
                shouldAltFire = true;

            if (!shouldAltFire && currentTarget != null)
            {
                int c = CountEnemiesInSphere(currentTarget.position, clusterRadius);
                if (c >= clusterCountThreshold) shouldAltFire = true;
            }

            if (shouldAltFire)
                StartCoroutine(AutoChargeAndRelease());
        }
    }

    private System.Collections.IEnumerator AutoChargeAndRelease()
    {
        if (isCharging) yield break;

        isCharging = true;
        chargeStartTime = Time.time;

        if (chargeAudioSource != null && !chargeAudioSource.isPlaying)
            chargeAudioSource.Play();

        float desiredHold = Mathf.Clamp(chargeTime * 0.6f, 0.4f, 1.2f);
        float endTime = Time.time + desiredHold;

        // Während des Aufladens Ziel nachführen
        while (Time.time < endTime && IsOwner)
        {
            if (currentTarget == null) break;
            Vector3 aimPos = PredictAimPoint(currentTarget, useAimLead ? altBulletSpeedHint : 0f);
            Vector3 dir = aimPos - transform.position; // <<< geändert: dir.y NICHT nullen
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = targetRot;
                NetworkRotation.Value = targetRot;
            }
            yield return null;
        }

        float heldTime = Time.time - chargeStartTime;
        float altCd = NextCooldown(altFireRate, altFireRateJitterPct);
        RequestChargedShotServerRpc(bulletSpawnPoint.position, bulletSpawnPoint.rotation, heldTime, altCd);

        nextAltFireTime = Time.time + altCd;
        isCharging = false;

        if (chargeAudioSource != null && chargeAudioSource.isPlaying)
            chargeAudioSource.Stop();
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

    private int CountEnemiesInSphere(Vector3 center, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, enemyLayer, QueryTriggerInteraction.Ignore);
        return hits != null ? hits.Length : 0;
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

    // ===================== MANUAL (Fallback) =====================

    private void AimWithMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // Erst auf Geometrie zielen (echtes 3D-Zielen)
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
        {
            targetPoint = hit.point;
            Vector3 direction = targetPoint - transform.position; // <<< geändert: y NICHT nullen
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = targetRotation;
                NetworkRotation.Value = targetRotation;
            }
            return;
        }

        // Fallback: Ebene (falls nichts getroffen)
        Plane fallbackPlane = new Plane(Vector3.up, new Vector3(0, 2, 0));
        if (fallbackPlane.Raycast(ray, out float distance))
        {
            targetPoint = ray.GetPoint(distance);
            Vector3 direction = targetPoint - transform.position; // ebenfalls 3D
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = targetRotation;
                NetworkRotation.Value = targetRotation;
            }
        }
    }

    private void HandleFiringManual()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // Primär
        if (Input.GetMouseButton(0))
        {
            fireButtonHoldTime += Time.deltaTime;
            if (Time.time >= nextFireTime)
            {
                float cd = NextCooldown(fireRate, fireRateJitterPct);
                nextFireTime = Time.time + cd;
                RequestFireServerRpc(bulletSpawnPoint.position, bulletSpawnPoint.rotation, fireButtonHoldTime, cd);
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            fireButtonHoldTime = 0f;
        }

        // Charged
        if (Input.GetMouseButtonDown(1) && Time.time >= nextAltFireTime)
        {
            chargeStartTime = Time.time;
            isCharging = true;
            if (chargeAudioSource != null && !chargeAudioSource.isPlaying)
                chargeAudioSource.Play();
        }
        if (Input.GetMouseButtonUp(1) && isCharging)
        {
            float heldTime = Time.time - chargeStartTime;
            float altCd = NextCooldown(altFireRate, altFireRateJitterPct);
            RequestChargedShotServerRpc(bulletSpawnPoint.position, bulletSpawnPoint.rotation, heldTime, altCd);

            nextAltFireTime = Time.time + altCd;
            isCharging = false;

            if (chargeAudioSource != null && chargeAudioSource.isPlaying)
                chargeAudioSource.Stop();
        }
    }

    // ===================== Utils =====================

    private float NextCooldown(float baseRate, float jitterPct)
    {
        if (jitterPct <= 0f) return Mathf.Max(0.01f, baseRate);
        float r = Random.Range(-jitterPct, jitterPct); // symmetrisch um 0
        return Mathf.Max(0.01f, baseRate * (1f + r));
    }

    // ===================== SERVER RPCs =====================

    [ServerRpc]
    private void RequestFireServerRpc(Vector3 position, Quaternion rotation, float holdTime, float clientCooldown)
    {
        if (_health != null && _health.IsDead()) return;

        float now = Time.time;

        _lastPrimaryFire.TryGetValue(OwnerClientId, out float last);

        // Erlaubte Cooldown-Grenzen (serverseitig)
        float baseCd = Mathf.Max(0.01f, overclocks ? overclocks.GetEffectiveFireRateSeconds(fireRate) : fireRate);
        float minCd = Mathf.Max(0.01f, baseCd * (1f - fireRateJitterPct));
        float maxCd = Mathf.Max(0.01f, baseCd * (1f + fireRateJitterPct));

        // Validierung gegen Ausreißer / Manipulation
        if (clientCooldown < minCd || clientCooldown > maxCd) return;

        // Taktprüfung
        if (now - last < clientCooldown) return;

        _lastPrimaryFire[OwnerClientId] = now;

        float dynamicInaccuracy = inaccuracyAngle + Mathf.Clamp(holdTime * 1f, 0f, 15f);
        FireCannon(position, rotation, dynamicInaccuracy);
        
    }

    [ServerRpc]
    private void RequestChargedShotServerRpc(Vector3 position, Quaternion rotation, float heldTime, float clientCooldown)
    {
        float now = Time.time;
        _lastAltFire.TryGetValue(OwnerClientId, out float last);

        float baseCd = Mathf.Max(0.01f, altFireRate);
        float minCd = Mathf.Max(0.01f, baseCd * (1f - altFireRateJitterPct));
        float maxCd = Mathf.Max(0.01f, baseCd * (1f + altFireRateJitterPct));

        if (clientCooldown < minCd || clientCooldown > maxCd) return;
        if (now - last < clientCooldown) return;

        float chargePercent = Mathf.Clamp01(heldTime / chargeTime);


        _lastAltFire[OwnerClientId] = now;
        FireChargedShot(position, rotation, chargePercent);
    }

    // ===================== SERVER-only Projektil-Spawn =====================

    private float GetRollDamageMultiplier()
    {
        // Fallback 1.0f, falls nichts gefunden
        if (_ownerMovement == null) _ownerMovement = GetComponentInParent<PlayerMovement>();
        if (_ownerMovement == null) return 1f;

        return _ownerMovement.ServerRollHeld ? damageWhileRolling : damageWhileNotRolling;
    }

    
    private void FireCannon(Vector3 position, Quaternion rotation, float inaccuracy)
    {
        // Konische Streuung um die Vorwärtsrichtung (inkl. Pitch)
        Vector3 baseFwd = rotation * Vector3.forward;
        Vector3 upAxis  = rotation * Vector3.up;
        Vector3 rightAxis = rotation * Vector3.right;

        float yaw  = Random.Range(-inaccuracy, inaccuracy);
        float pitch = Random.Range(-inaccuracy, inaccuracy);

        Vector3 deviatedDir = Quaternion.AngleAxis(yaw, upAxis) * (Quaternion.AngleAxis(pitch, rightAxis) * baseFwd);
        Quaternion fireRotation = Quaternion.LookRotation(deviatedDir.normalized, upAxis);

        NetworkObject bulletNetObj = Instantiate(bulletPrefab, position, fireRotation);
        bulletNetObj.Spawn(true);

        BulletController bullet = bulletNetObj.GetComponent<BulletController>();

        // === NEU: Schaden serverseitig berechnen (Würfel + Roll-Skalierung) ===
        float baseDamage = Random.Range(primaryDamageRange.x, primaryDamageRange.y);
        float dmgScale   = GetRollDamageMultiplier();   // 0.8 beim Rollen / 1.15 nicht Rollen
        float upgradeMult = _upgrades ? _upgrades.GetDamageMultiplier() : 1f;
        float ocDmg = overclocks ? overclocks.GetDamageMult() : 1f;
        float finalDamage = baseDamage * dmgScale * upgradeMult * ocDmg;

        bullet.Init(fireRotation * Vector3.forward, bullet.speed, finalDamage, OwnerClientId);

        PlayFireSoundClientRpc(false, Random.Range(0.8f, 1.8f));

        var clientParams = new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        DoCameraShakeClientRpc(0.1f, 0.15f, clientParams);
    }

    private void FireChargedShot(Vector3 position, Quaternion rotation, float chargePercent)
    {
        // Konische Streuung auch für Alt-Fire
        Vector3 baseFwd = rotation * Vector3.forward;
        Vector3 upAxis  = rotation * Vector3.up;
        Vector3 rightAxis = rotation * Vector3.right;

        float yaw  = Random.Range(-altInaccuracyAngle, altInaccuracyAngle);
        float pitch = Random.Range(-altInaccuracyAngle, altInaccuracyAngle);

        Vector3 deviatedDir = Quaternion.AngleAxis(yaw, upAxis) * (Quaternion.AngleAxis(pitch, rightAxis) * baseFwd);
        Quaternion fireRotation = Quaternion.LookRotation(deviatedDir.normalized, upAxis);

        NetworkObject bulletNetObj = Instantiate(altBulletPrefab, position, fireRotation);
        bulletNetObj.Spawn(true);

        AltBulletController bullet = bulletNetObj.GetComponent<AltBulletController>();

        float chargeScale = Mathf.Lerp(1f, 3f, chargePercent);
        float dmgScale = GetRollDamageMultiplier();
        float upgradeMult = _upgrades ? _upgrades.GetDamageMultiplier() : 1f;

        float scaledDamage = altBaseDamage * chargeScale * dmgScale * upgradeMult;

        // Speed-Skalierung kann so bleiben, sie basiert auf dem Prefab-Speed:
        float scaledSpeed  = bullet.speed * Mathf.Lerp(1f, 3.5f, chargePercent);

        bullet.Init(fireRotation * Vector3.forward, scaledSpeed, scaledDamage, OwnerClientId, chargePercent);

        PlayFireSoundClientRpc(true, Random.Range(0.7f, 1.2f));

        var clientParams = new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        DoCameraShakeClientRpc(1.2f, 0.25f, clientParams);
    }

    // ===================== CLIENT RPCs =====================
    [ClientRpc]
    private void PlayFireSoundClientRpc(bool isAlt, float pitch)
    {
        if (audioSource == null) return;
        AudioClip clip = isAlt ? altFireSFX : fireSFX;
        if (clip == null) return;

        // Throttle: bei sehr hoher Rate nicht jeden Schuss abspielen
        if (!isAlt) // nur Primärfeuer drosseln
        {
            float now = Time.time;
            if (now - _lastFireSfxTime < fireSfxMinInterval) return;
            _lastFireSfxTime = now;
        }

        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, 1f); // volumeScale kann >1 sein, wenn du willst
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
