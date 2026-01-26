using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LightningController : NetworkBehaviour
{
    [Header("Weapon Data")]
    public WeaponDefinition weaponDef;        // z.B. "ArcEmitter.asset"
    private PlayerWeapons _playerWeapons;
    [SerializeField] private OverclockRuntime overclocks;

    [Header("Targeting")]
    public float retargetInterval = 0.15f;
    public LayerMask enemyLayer;
    public LayerMask lineOfSightMask;

    [Range(0f, 1f)] public float minDotToStrike = 0.5f; // muss nicht so streng sein wie Cannon

    [Header("Fire Rate / Jitter")]
    [Range(0f, 0.5f)] public float fireRateJitterPct = 0.08f;
    private float nextFireTime = 0f;
    private float nextRetargetTime = 0f;

    [Header("VFX / Audio")]
    public GameObject lightningVfxPrefab;     // Blitz-Effekt
    public AudioSource audioSource;
    public AudioClip strikeSfx;

    // Runtime / Owner
    private WeaponRuntime _runtime;
    private PlayerUpgrades _upgrades;
    private PlayerHealth _health;
    private PlayerMovement _ownerMovement;

    private Transform currentTarget;

    // Server: Anti-Exploit Cooldown-Tracking
    private readonly Dictionary<ulong, float> _lastStrikeTime = new();

    // Optional, falls du Rotation auch syncen willst wie bei Cannon
    public NetworkVariable<Quaternion> NetworkRotation = new(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn()
    {
        _health = GetComponentInParent<PlayerHealth>();
        _ownerMovement = GetComponentInParent<PlayerMovement>();
        _upgrades = GetComponentInParent<PlayerUpgrades>();
        if (!overclocks) overclocks = GetComponentInParent<OverclockRuntime>();

        _playerWeapons = GetComponentInParent<PlayerWeapons>();
        if (_playerWeapons != null)
        {
            _playerWeapons.RuntimesRebuilt += OnWeaponsRebuilt;
        }

        BuildLocalRuntime();
    }

    public override void OnNetworkDespawn()
    {
        if (_playerWeapons != null)
            _playerWeapons.RuntimesRebuilt -= OnWeaponsRebuilt;
    }

    private void OnWeaponsRebuilt()
    {
        BuildLocalRuntime();
    }

    private void BuildLocalRuntime()
    {
        _runtime = null;

        if (_playerWeapons != null)
        {
            Debug.Log($"[Lightning] Rebuild, level={_playerWeapons?.lightningLevel.Value}, rt={_playerWeapons?.LightningRuntime}");

            if (_playerWeapons.lightningLevel.Value > 0 && _playerWeapons.LightningRuntime != null)
            {
                _runtime = new WeaponRuntime(_playerWeapons.lightningDef, _playerWeapons.lightningLevel.Value);
                if (_upgrades) _upgrades.ApplyTo(_runtime);
            }
        }
    }


    private float GetTargetRange()
    {
        // 1) Runtime → beste Quelle
        if (_runtime != null && _runtime.rangeMeters > 0f)
            return _runtime.rangeMeters;

        // 2) Direktes WeaponDef (falls du Lightning im Editor testest ohne PlayerWeapons)
        if (weaponDef != null && weaponDef.rangeMeters > 0f)
            return weaponDef.rangeMeters;

        // 3) Fallback über PlayerWeapons-Def (falls gesetzt)
        if (_playerWeapons != null &&
            _playerWeapons.lightningDef != null &&
            _playerWeapons.lightningDef.rangeMeters > 0f)
            return _playerWeapons.lightningDef.rangeMeters;

        // 4) Hardcoded Default, falls alles schiefgeht
        return 12f;
    }



    private void Update()
    {
        if (!IsOwner)
        {
            // Nur für Optik, falls du Rotation syncen willst
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                NetworkRotation.Value,
                Time.deltaTime * 15f
            );
            return;
        }

        if (_runtime == null) { currentTarget = null; return; }

        AutoAimAndStrike();
    }

    // ===================== AUTO AIM + STRIKE =====================

    private void AutoAimAndStrike()
    {
        // 1) Ziel aktualisieren
        if (Time.time >= nextRetargetTime || currentTarget == null)
        {
            nextRetargetTime = Time.time + retargetInterval;
            currentTarget = AcquireTarget();
        }

        // 2) Ausrichten (optional)
        if (currentTarget != null)
        {
            Vector3 dir = (currentTarget.position - transform.position);
            if (dir.sqrMagnitude > 0.0001f)
            {
                var targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = targetRot;
                NetworkRotation.Value = targetRot;
            }
        }

        if (currentTarget == null) return;

        // 3) Feuer-Logik
        Vector3 fwd = transform.forward;
        Vector3 toTarget = (currentTarget.position - transform.position).normalized;
        float dot = Vector3.Dot(fwd, toTarget);

        bool cdReady = Time.time >= nextFireTime;
        bool okDot = dot >= minDotToStrike;
        bool los = HasLineOfSight(currentTarget);

        Debug.Log($"[Lightning] target={currentTarget.name}, dot={dot}, cdReady={cdReady}, los={los}");

        if (!cdReady || !okDot || !los) return;

        float baseCd = _runtime.GetCooldownSeconds();
        if (overclocks) baseCd = overclocks.GetEffectiveFireRateSeconds(baseCd);
        float cd = NextCooldown(baseCd, fireRateJitterPct);

        nextFireTime = Time.time + cd;

        // Ziel-NetworkId für den Server herausfinden
        NetworkObject targetNo = currentTarget.GetComponentInParent<NetworkObject>();
        if (targetNo == null)
        {
            Debug.LogWarning($"[Lightning] currentTarget {currentTarget.name} hat kein NetworkObject im Parent!");
            return;
        }

        RequestStrikeServerRpc(targetNo.NetworkObjectId, cd);
    }

    private Transform AcquireTarget()
    {
        float range = GetTargetRange();
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            range,
            enemyLayer,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
            return null;

        // Alle gültigen Kandidaten sammeln
        List<Transform> candidates = new List<Transform>();

        foreach (var h in hits)
        {
            // Root mit NetworkObject bevorzugen (wie vorher)
            var no = h.GetComponentInParent<NetworkObject>();
            Transform t = no != null ? no.transform : h.transform;

            // Optional: doppelte verhindern
            if (candidates.Contains(t))
                continue;

            if (!HasLineOfSight(t))
                continue;

            Vector3 to = t.position - transform.position;
            float dist = to.magnitude;
            if (dist <= 0.1f)
                continue;

            candidates.Add(t);
        }

        if (candidates.Count == 0)
            return null;

        // Zufälligen Gegner aus der Liste wählen
        int idx = Random.Range(0, candidates.Count);
        return candidates[idx];
    }



    private bool HasLineOfSight(Transform t)
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Vector3 target = t.position + Vector3.up * 0.8f;
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.01f) return true;

        return !Physics.Raycast(origin, dir.normalized, dist, lineOfSightMask, QueryTriggerInteraction.Ignore);
    }

    private float NextCooldown(float baseSecondsPerShot, float jitterPct)
    {
        if (jitterPct <= 0f) return Mathf.Max(0.01f, baseSecondsPerShot);
        float r = Random.Range(-jitterPct, jitterPct);
        return Mathf.Max(0.01f, baseSecondsPerShot * (1f + r));
    }

    // ===================== SERVER RPC: Blitz einschlagen lassen =====================

    [ServerRpc]
    private void RequestStrikeServerRpc(ulong targetNetworkId, float clientCooldown)
    {
        if (_runtime == null) return;
        if (_health != null && _health.IsDead()) return;

        Debug.Log($"[Lightning] ServerRpc received for target {targetNetworkId}");

        // Ziel auflösen
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkId, out var targetNo))
            return;

        Transform targetTransform = targetNo.transform;

        // Distanz & LoS check serverseitig
        Vector3 to = targetTransform.position - transform.position;
        float dist = to.magnitude;
        float maxRange = GetTargetRange();
        if (dist > maxRange + 0.5f) return;

        if (!HasLineOfSight(targetTransform)) return;

        // Cooldown-Validierung wie bei Cannon
        float now = Time.time;
        _lastStrikeTime.TryGetValue(OwnerClientId, out float last);

        float baseCd = Mathf.Max(0.01f, _runtime.GetCooldownSeconds());
        if (overclocks) baseCd = overclocks.GetEffectiveFireRateSeconds(baseCd);
        float minCd = Mathf.Max(0.01f, baseCd * (1f - fireRateJitterPct));
        float maxCd = Mathf.Max(0.01f, baseCd * (1f + fireRateJitterPct));

        if (clientCooldown < minCd || clientCooldown > maxCd) return;
        if (now - last < clientCooldown) return;

        _lastStrikeTime[OwnerClientId] = now;

        // === Schaden berechnen ===
        float dmgScale =
            (_upgrades ? _upgrades.GetDamageMultiplier() : 1f) *
            (overclocks ? overclocks.GetDamageMult() : 1f);

        float baseDmg = _runtime.ComputeDamageNonPierced(applyCrit: true);
        float finalDmg = baseDmg * dmgScale;

        // Trefferposition merken (egal ob der Enemy gleich despawned)
        Vector3 hitPoint = targetTransform.position + Vector3.up * 0.8f;

        // Enemy-Interface / Health aufrufen
        var enemy = targetTransform.GetComponent<IEnemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(finalDmg, OwnerClientId, hitPoint);
        }
        else
        {
            // Fallback, wenn du EnemyHealth nutzt:
            // var eh = targetTransform.GetComponent<EnemyHealth>();
            // if (eh != null) eh.Server_TakeDamage(finalDmg);
        }


        // VFX & SFX für alle
        SpawnLightningVfxClientRpc(hitPoint);
        PlayStrikeSoundClientRpc();
    }

    // ===================== CLIENT RPCs =====================

    [ClientRpc]
    private void SpawnLightningVfxClientRpc(Vector3 targetPos)
    {
        Debug.Log($"[Lightning] SpawnLightningVfxClientRpc at {targetPos}");

        if (lightningVfxPrefab == null)
        {
            Debug.LogWarning("[Lightning] lightningVfxPrefab is NULL!");
            return;
        }

        Object.Instantiate(lightningVfxPrefab, targetPos, Quaternion.identity);
    }

    [ClientRpc]
    private void PlayStrikeSoundClientRpc()
    {
        if (audioSource == null || strikeSfx == null) return;

        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(strikeSfx, 1f);
    }
}
