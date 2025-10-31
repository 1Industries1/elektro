using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class GrenadeLauncherController : NetworkBehaviour
{
    [Header("WeaponDef")]
    public WeaponDefinition grenadeDef;
    
    [Header("Grenade Prefab & Spawn")]
    public NetworkObject grenadePrefab;
    public Transform muzzle;

    [Header("Fire & Salvo")]
    public float fireRate = 0.9f;
    public int salvoCount = 3;
    public float salvoInterval = 0.06f;
    [Range(0f, 0.5f)] public float fireRateJitterPct = 0.06f;

    [Header("Launch Profile")]
    public float launchSpeed = 32f;
    public float maxElevationDeg = 10f;
    [Range(0f, 2f)] public float inaccuracyAngle = 0.35f;

    [Header("Targeting (Auto)")]
    public bool autoShootEnabled = true;
    public float targetRange = 32f;
    [Range(0f,1f)] public float minDotToShoot = 0.80f;
    public float retargetInterval = 0.15f;
    public LayerMask enemyLayer;
    public LayerMask lineOfSightMask = ~0;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip salvoFireSFX;

    [Header("Net Sync (optional)")]
    public float rotationLerpRate = 18f;
    public NetworkVariable<Quaternion> NetworkRotation = new NetworkVariable<Quaternion>(
        Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );


    [Header("Damage Multipliers")]
    [Tooltip("Schadensfaktor während Roll_Anim (LSHIFT gehalten). < 1 für Nerf beim Rollen.")]
    public float damageWhileRolling = 0.8f;
    [Tooltip("Schadensfaktor wenn NICHT gerollt wird. > 1 für Buff im Stand/Lauf.")]
    public float damageWhileNotRolling = 1.15f;

    private PlayerMovement _ownerMovement;   // wie bei Cannon
    private PlayerUpgrades _upgrades;        // wie bei Cannon
    private PlayerWeapons _weapons;
    private WeaponRuntime _runtime;

    private float _nextFireTime;
    private float _nextRetarget;
    private Transform _currentTarget;
    private readonly Dictionary<ulong, float> _lastSalvoFire = new();
    private float _dbgNext;

    private void Start()
    {
        if (muzzle == null) muzzle = transform;
    }

    public override void OnNetworkSpawn()
    {
        // Owner-Kontext für Multiplikatoren
        _ownerMovement = GetComponentInParent<PlayerMovement>();
        _upgrades = GetComponentInParent<PlayerUpgrades>();

        _weapons = GetComponentInParent<PlayerWeapons>();
        if (_weapons != null)
        {
            _runtime = _weapons.GrenadeRuntime;
            _weapons.RuntimesRebuilt += OnRuntimesRebuilt;
        }
    }

    private void OnDestroy()
    {
        if (_weapons != null) _weapons.RuntimesRebuilt -= OnRuntimesRebuilt;
    }
    
    private void OnRuntimesRebuilt()
    {
        _runtime = _weapons != null ? _weapons.GrenadeRuntime : null;
    }

    // === replace your cooldown source with runtime ===
    private float NextCooldownRuntime()
    {
        // runtime cooldown (with your jitter)
        float baseCd = (_runtime != null) ? _runtime.GetCooldownSeconds() : fireRate;
        return NextCooldown(baseCd, fireRateJitterPct);
    }

    private void Update()
    {
        if (!IsOwner)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, NetworkRotation.Value, Time.deltaTime * rotationLerpRate);
            return;
        }

        if (autoShootEnabled) { AutoAim(); AutoFire(); }
        else { ManualAimWithMouse(); ManualFire(); }
    }

    // ================== AIM ==================

    private void AutoAim()
    {
        if (Time.time >= _nextRetarget || _currentTarget == null)
        {
            _nextRetarget = Time.time + retargetInterval;
            _currentTarget = AcquireTarget();
        }
        if (_currentTarget == null) return;

        Vector3 dir = GetClampedAimDirection(muzzle.position, _currentTarget.position + Vector3.up * 0.6f);
        var look = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = look;
        NetworkRotation.Value = look;
    }

    // === damage: runtime + crit + roll + generic upgrade multiplier ===
    private float ComputeGrenadeDamage()
    {
        // roll crit at shot-time (per salvo), then per-grenade ±5% variance stays as you had
        float dmg = (_runtime != null)
            ? _runtime.ComputeDamageNonPierced(applyCrit:true)
            : Random.Range(30f, 55f); // fallback to old average range if runtime missing

        float rollMul = GetRollDamageMultiplier();
        float upgradeMul = _upgrades ? _upgrades.GetDamageMultiplier() : 1f;

        return dmg * rollMul * upgradeMul;
    }

    // === OPTIONAL: expose projectile size from runtime ===
    private float GetProjectileSizeMul() => (_runtime != null) ? Mathf.Max(0.05f, _runtime.projectileSize) : 1f;

    private void AutoFire()
    {
        if (_currentTarget == null) return;

        Vector3 origin = muzzle.position;
        Vector3 dir = GetClampedAimDirection(origin, _currentTarget.position + Vector3.up * 0.6f);
        bool los = HasLineOfSight(_currentTarget);
        float dot = Vector3.Dot(transform.forward, dir);
        bool cdReady = Time.time >= _nextFireTime;

        if (cdReady && los && dot >= minDotToShoot)
        {
            float cd = NextCooldownRuntime();          // CHANGED
            _nextFireTime = Time.time + cd;

            Vector3 v0 = ApplySpread(dir) * GetLaunchSpeed();

            NetworkObjectReference targetRef = default;
            var maybeNo = _currentTarget.GetComponentInParent<NetworkObject>();
            if (maybeNo != null) targetRef = maybeNo;

            float damageForSalvo = ComputeGrenadeDamage();  // CHANGED
            int countEff = GetEffectiveSalvoCount();
            float sizeMul = GetProjectileSizeMul();         // NEW

            RequestSalvoServerRpc(origin, v0, targetRef, countEff, salvoInterval, cd, damageForSalvo, sizeMul); // signature extended
        }
    }


    private void ManualAimWithMouse()
    {
        Ray ray = Camera.main ? Camera.main.ScreenPointToRay(Input.mousePosition) : new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 dir = GetClampedAimDirection(muzzle.position, hit.point);
            var look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = look;
            NetworkRotation.Value = look;
        }
    }

    private void ManualFire()
    {
        if (!Input.GetMouseButtonDown(0) || Time.time < _nextFireTime) return;

        float cd = NextCooldownRuntime();      // CHANGED
        _nextFireTime = Time.time + cd;

        Vector3 origin = muzzle.position;
        Vector3 dir;

        Ray ray = Camera.main ? Camera.main.ScreenPointToRay(Input.mousePosition) : new Ray(transform.position, transform.forward);
        Vector3 targetPos = origin + transform.forward * 25f;
        if (Physics.Raycast(ray, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            targetPos = hit.point;

        dir = GetClampedAimDirection(origin, targetPos);
        Vector3 v0 = ApplySpread(dir) * GetLaunchSpeed();

        float damageForSalvo = ComputeGrenadeDamage(); // CHANGED
        float sizeMul = GetProjectileSizeMul();        // NEW
        int countEff = GetEffectiveSalvoCount();

        RequestSalvoServerRpc(origin, v0, default, countEff, salvoInterval, cd, damageForSalvo, sizeMul); // signature extended
    }

    private int GetEffectiveSalvoCount()
    {
        if (_runtime != null) return Mathf.Max(1, _runtime.salvoCount);
        return Mathf.Max(1, salvoCount); // Fallback auf Inspector
    }

    // ================== DAMAGE (NEU) ==================

    private float GetRollDamageMultiplier()
    {
        if (_ownerMovement == null) _ownerMovement = GetComponentInParent<PlayerMovement>();
        if (_ownerMovement == null) return 1f;
        return _ownerMovement.ServerRollHeld ? damageWhileRolling : damageWhileNotRolling;
    }

    // ================== UTILS ==================

    private Vector3 GetClampedAimDirection(Vector3 origin, Vector3 target)
    {
        Vector3 to = (target - origin);
        if (to.sqrMagnitude < 1e-6f) return transform.forward;

        Vector3 horiz = Vector3.ProjectOnPlane(to, Vector3.up).normalized;
        float elev = Vector3.SignedAngle(horiz, to.normalized, Vector3.Cross(horiz, Vector3.up));
        float clampedElev = Mathf.Clamp(elev, -maxElevationDeg, maxElevationDeg);

        Quaternion clampRot = Quaternion.AngleAxis(clampedElev, Vector3.Cross(horiz, Vector3.up).normalized);
        Vector3 dir = (clampRot * horiz).normalized;
        return dir;
    }

    private Vector3 ApplySpread(Vector3 dir)
    {
        if (inaccuracyAngle <= 0f) return dir;
        Quaternion spread =
            Quaternion.AngleAxis(Random.Range(-inaccuracyAngle, inaccuracyAngle), Vector3.up) *
            Quaternion.AngleAxis(Random.Range(-inaccuracyAngle, inaccuracyAngle), Vector3.right);
        return (spread * dir).normalized;
    }

    private Transform AcquireTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, GetTargetRange(), enemyLayer, QueryTriggerInteraction.Ignore);
        Transform best = null; float bestScore = float.PositiveInfinity;

        foreach (var h in hits)
        {
            var t = h.transform;
            if (!HasLineOfSight(t)) continue;

            Vector3 to = t.position - transform.position;
            float dist = to.magnitude; if (dist <= 0.1f) continue;

            float dot = Vector3.Dot(transform.forward, to.normalized);
            float score = dist * Mathf.Lerp(2f, 1f, Mathf.InverseLerp(0.5f, 1f, dot));
            if (score < bestScore) { bestScore = score; best = t; }
        }
        return best;
    }

    private bool HasLineOfSight(Transform t)
    {
        Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.2f;
        Vector3 target = t.position + Vector3.up * 0.8f;
        Vector3 dir = target - origin; float dist = dir.magnitude;
        if (dist <= 0.01f) return true;
        return !Physics.Raycast(origin, dir.normalized, dist, lineOfSightMask, QueryTriggerInteraction.Ignore);
    }

    private float NextCooldown(float baseRate, float jitterPct)
    {
        if (jitterPct <= 0f) return Mathf.Max(0.01f, baseRate);
        float r = Random.Range(-jitterPct, jitterPct);
        return Mathf.Max(0.01f, baseRate * (1f + r));
    }

    private float GetServerBaseCooldown()
    {
        // Laufzeitwert, Fallback auf Inspector-Feld
        var baseCd = (_runtime != null) ? _runtime.GetCooldownSeconds() : fireRate;
        return Mathf.Max(0.01f, baseCd);
    }

    private float GetLaunchSpeed()
        => (_runtime != null) ? Mathf.Max(0.1f, _runtime.projectileSpeed) : launchSpeed;

    private float GetTargetRange()
        => (_runtime != null) ? Mathf.Max(0.5f, _runtime.def.rangeMeters) : targetRange;

    // ================== RPC / SPAWN ==================

    [ServerRpc]
    private void RequestSalvoServerRpc(
        Vector3 spawnPos, Vector3 initialVelocity, NetworkObjectReference targetRef,
        int count, float interval, float clientCooldown, float damageFromClient, float sizeMul)
    {
        float now = Time.time;

        if (!_lastSalvoFire.TryGetValue(OwnerClientId, out var last)) last = -9999f;

        float baseCd = GetServerBaseCooldown();                    // CHANGED
        float minCd  = Mathf.Max(0.01f, baseCd * (1f - fireRateJitterPct));
        float maxCd  = Mathf.Max(0.01f, baseCd * (1f + fireRateJitterPct));

        if (clientCooldown < minCd || clientCooldown > maxCd) return;
        if (now - last < clientCooldown) return;

        if (grenadePrefab == null)
        {
            // Fallback: Prefab aus WeaponDefinition, wenn im Controller nicht gesetzt
            if (grenadeDef != null && grenadeDef.bulletPrefab != null)
                grenadePrefab = grenadeDef.bulletPrefab;
            if (grenadePrefab == null) { Debug.LogError("[GL] grenadePrefab NULL"); return; }
        }

        float serverDamage = ComputeGrenadeDamage();               // deine sichere Serverberechnung
        int serverCount = GetEffectiveSalvoCount();

        _lastSalvoFire[OwnerClientId] = now;
        StartCoroutine(FireSalvoRoutine(spawnPos, initialVelocity, targetRef, serverCount, Mathf.Max(0f, interval), serverDamage, sizeMul));
        PlaySalvoSFXClientRpc(Random.Range(0.95f, 1.05f));
    }

    private IEnumerator FireSalvoRoutine(
        Vector3 spawnPos,
        Vector3 initialVelocity,
        NetworkObjectReference targetRef,
        int count,
        float interval,
        float baseDamageForThisSalvo,
        float sizeMul) // NEW
    {
        var targets = AcquireTargetsForSalvo(spawnPos, count);

        for (int i = 0; i < count; i++)
        {
            Vector3 v0 = initialVelocity;
            if (inaccuracyAngle > 0f)
            {
                Quaternion spread =
                    Quaternion.AngleAxis(Random.Range(-inaccuracyAngle, inaccuracyAngle), Vector3.up) *
                    Quaternion.AngleAxis(Random.Range(-inaccuracyAngle, inaccuracyAngle), Vector3.right);
                v0 = (spread * v0).normalized * v0.magnitude;
            }

            NetworkObjectReference perGrenadeTarget = default;
            if (targets.Count > 0)
                perGrenadeTarget = new NetworkObjectReference(targets[i % targets.Count]);

            var obj = Instantiate(grenadePrefab, spawnPos, Quaternion.LookRotation(v0.normalized, Vector3.up));
            if (obj == null) { Debug.LogError("[GL] Instantiate returned NULL"); yield break; }

            obj.Spawn(true);

            var gp = obj.GetComponent<GrenadeProjectile>();
            if (gp != null)
            {
                float perGrenade = baseDamageForThisSalvo * Random.Range(0.95f, 1.05f);
                gp.ServerInit(v0, OwnerClientId, perGrenade, perGrenadeTarget, sizeMul); // NEW param
            }

            if (i < count - 1 && interval > 0f) yield return new WaitForSeconds(interval);
        }
    }

    private List<NetworkObject> AcquireTargetsForSalvo(Vector3 origin, int maxTargets)
    {
        var result = new List<NetworkObject>(maxTargets);
        float searchRadius = targetRange;

        var cols = Physics.OverlapSphere(origin, searchRadius, enemyLayer, QueryTriggerInteraction.Ignore);

        var scored = new List<(NetworkObject no, float score)>();
        foreach (var c in cols)
        {
            var no = c.GetComponentInParent<NetworkObject>();
            var enemy = c.GetComponentInParent<IEnemy>();
            if (no == null || enemy == null) continue;

            Vector3 to = c.transform.position - origin;
            float dist = to.magnitude; 
            if (dist < 0.2f) continue;

            float dot = Vector3.Dot(transform.forward, to.normalized);
            if (dot < 0.4f) continue;

            float score = dist * Mathf.Lerp(2f, 1f, Mathf.InverseLerp(0.5f, 1f, dot));
            scored.Add((no, score));
        }

        scored.Sort((a, b) => a.score.CompareTo(b.score));

        var usedRoots = new HashSet<Transform>();
        foreach (var s in scored)
        {
            var root = s.no.transform.root;
            if (usedRoots.Add(root))
            {
                result.Add(s.no);
                if (result.Count >= maxTargets) break;
            }
        }

        return result;
    }

    [ClientRpc]
    private void PlaySalvoSFXClientRpc(float pitch)
    {
        if (audioSource == null || salvoFireSFX == null) return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(salvoFireSFX);
    }
}
