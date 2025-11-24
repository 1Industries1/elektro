using UnityEngine;
using Unity.Netcode;

[DisallowMultipleComponent]
public class BlackHoleWeaponController : NetworkBehaviour
{
    [Header("Weapon Data (Fallback)")]
    public WeaponDefinition weaponDef;

    private PlayerWeapons _playerWeapons;
    private PlayerUpgrades _upgrades;
    private PlayerMovement _ownerMovement;
    private PlayerHealth _health;

    [SerializeField] private OverclockRuntime overclocks;

    [Header("Placement Settings")]
    [Tooltip("Layer für den Boden (Base).")]
    public LayerMask baseLayer;

    [Tooltip("Fallback-Prefab, falls in WeaponDefinition kein bulletPrefab gesetzt ist.")]
    public NetworkObject gravityWellPrefab;

    [Tooltip("Offset über dem Boden entlang der Normalen.")]
    public float surfaceOffset = 1.8f;

    [Tooltip("Wo entlang der Range gespawnt wird (0 = direkt am Spieler, 1 = maximale Range).")]
    [Range(0f, 1f)] public float forwardFraction = 0.7f;

    [Tooltip("Minimale Reichweite, falls rangeMeters in der Definition zu klein ist.")]
    public float minRange = 5f;

    [Header("Bullet Time (optional)")]
    public bool triggerBulletTime = false;
    [Range(0.01f, 1f)] public float bulletScale = 0.2f;
    public float bulletIn  = 0.25f;
    public float bulletHold= 0.75f;
    public float bulletOut = 0.35f;

    // Laufzeit
    private WeaponRuntime _runtime;
    private float _nextFireTime;

    // =====================================================================
    // LIFECYCLE
    // =====================================================================

    public override void OnNetworkSpawn()
    {
        _health        = GetComponentInParent<PlayerHealth>();
        _ownerMovement = GetComponentInParent<PlayerMovement>();
        _upgrades      = GetComponentInParent<PlayerUpgrades>();
        if (!overclocks) overclocks = GetComponentInParent<OverclockRuntime>();

        _playerWeapons = GetComponentInParent<PlayerWeapons>();
        if (_playerWeapons != null)
        {
            _playerWeapons.RuntimesRebuilt += OnWeaponsRebuilt;
        }

        BuildLocalRuntime();

        // Erste Fire-Time leicht randomisieren, damit nicht alle Spieler synchron feuern.
        if (IsServer && _runtime != null)
            _nextFireTime = Time.time + Random.Range(0.1f, _runtime.GetCooldownSeconds());
    }

    public override void OnNetworkDespawn()
    {
        if (_playerWeapons != null)
            _playerWeapons.RuntimesRebuilt -= OnWeaponsRebuilt;
    }

    private void OnDisable()
    {
        // hier nix despawnen – GravityWell kümmert sich selbst um sein Despawn per duration.
    }

    // =====================================================================
    // RUNTIME-SETUP
    // =====================================================================

    private void OnWeaponsRebuilt()
    {
        BuildLocalRuntime();

        if (IsServer && _runtime != null)
        {
            // bei Level-Up Cooldown neu „einpendeln“
            _nextFireTime = Mathf.Min(_nextFireTime, Time.time + _runtime.GetCooldownSeconds());
        }
    }

    private void BuildLocalRuntime()
    {
        _runtime = null;

        if (_playerWeapons != null)
        {
            // Annahme: du hast in PlayerWeapons bereits:
            // public WeaponDefinition blackHoleDef;
            // public NetworkVariable<int> blackHoleLevel;
            // und evtl. BlackHoleRuntime.
            if (_playerWeapons.blackHoleDef != null &&
                _playerWeapons.blackHoleLevel.Value > 0)
            {
                _runtime = new WeaponRuntime(_playerWeapons.blackHoleDef, _playerWeapons.blackHoleLevel.Value);
                if (_upgrades)   _upgrades.ApplyTo(_runtime);
                if (overclocks)  _runtime.damagePerShot *= overclocks.GetDamageMult(); // falls du das so machen willst
            }
        }
        else if (weaponDef != null)
        {
            // Editor/Offline-Fallback (kein Netcode)
            var nm = NetworkManager.Singleton;
            bool netInactive = (nm == null || !nm.IsListening);
            if (netInactive)
            {
                _runtime = new WeaponRuntime(weaponDef, 1);
                if (_upgrades)  _upgrades.ApplyTo(_runtime);
                if (overclocks) _runtime.damagePerShot *= overclocks.GetDamageMult();
            }
        }
    }

    // =====================================================================
    // SERVER: PERIODISCHES SPAWNEN
    // =====================================================================

    private void Update()
    {
        // Wie bei Orbital: nur der Server macht echte Logik
        if (!IsServer) return;
        if (_runtime == null) return;
        if (_health != null && _health.IsDead()) return;

        if (Time.time < _nextFireTime) return;

        float cd = _runtime.GetCooldownSeconds();
        _nextFireTime = Time.time + cd;

        TrySpawnBlackHole();
    }

    private void TrySpawnBlackHole()
    {
        // Prefab bestimmen: bevorzugt aus WeaponDefinition, sonst Fallback-Feld
        NetworkObject prefab = null;

        if (_playerWeapons != null && 
            _playerWeapons.blackHoleDef != null && 
            _playerWeapons.blackHoleDef.bulletPrefab != null)
        {
            prefab = _playerWeapons.blackHoleDef.bulletPrefab;
        }
        else if (weaponDef != null && weaponDef.bulletPrefab != null)
        {
            prefab = weaponDef.bulletPrefab;
        }
        else if (gravityWellPrefab != null)
        {
            prefab = gravityWellPrefab;
        }

        if (prefab == null) return;

        // Reichweite aus Runtime
        float range = Mathf.Max(minRange, _runtime.rangeMeters);
        Vector3 ownerPos = transform.position;

        // --- NEU: zufällige Position im Ring um den Spieler ---
        // Radius irgendwo zwischen 30% der Range und voller Range
        float r = Random.Range(0.3f * range, range);

        // Zufällige Richtung im Kreis (XZ-Ebene)
        Vector2 circle = Random.insideUnitCircle.normalized;
        Vector3 offset = new Vector3(circle.x, 0f, circle.y) * r;

        // Grobe Zielposition um den Spieler herum
        Vector3 approx = ownerPos + Vector3.up * 1.5f + offset;

        // Von oben nach unten auf Base-Layer raycasten
        if (!Physics.Raycast(approx + Vector3.up * 20f,
                            Vector3.down,
                            out var hit,
                            50f,
                            baseLayer,
                            QueryTriggerInteraction.Ignore))
        {
            // Fallback: direkt unter dem Spieler
            if (!Physics.Raycast(ownerPos + Vector3.up * 5f,
                                Vector3.down,
                                out hit,
                                20f,
                                baseLayer,
                                QueryTriggerInteraction.Ignore))
                return;
        }

        SpawnGravityWell(prefab, hit.point, hit.normal);
    }


    private void SpawnGravityWell(NetworkObject prefab, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Spawn-Pose berechnen (wie bei deiner Fähigkeit)
        Vector3 up = hitNormal.sqrMagnitude > 0.01f ? hitNormal.normalized : Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up);
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        Quaternion rot = Quaternion.LookRotation(forward.normalized, up);

        Vector3 spawnPos = hitPoint + up * Mathf.Max(0f, surfaceOffset);

        if (Physics.Raycast(spawnPos + up * 0.2f, -up, out var poke, 0.25f, baseLayer, QueryTriggerInteraction.Ignore))
            spawnPos = poke.point + up * surfaceOffset;

        // Spawnen
        NetworkObject no = Instantiate(prefab, spawnPos, rot);
        no.Spawn(true);

        var well = no.GetComponent<GravityWell>();
        if (well != null && _runtime != null)
        {
            // --- 1) Baseline-Def für projectileSize bestimmen ---
            // bevorzugt die Def aus der Runtime
            var baseDef = _runtime.def
                    ?? weaponDef
                    ?? (_playerWeapons != null ? _playerWeapons.blackHoleDef : null);

            float baseSize = (baseDef != null && baseDef.projectileSize > 0f)
                ? baseDef.projectileSize
                : 1f;

            // --- 2) Radius über projectileSize skalieren ---
            float sizeMult = _runtime.projectileSize / Mathf.Max(0.0001f, baseSize);
            well.radius *= sizeMult;

            // --- 3) Pull-Stärke und Dauer an Level / Damage koppeln ---
            // "Damage" der Waffe benutzen wir hier als Stärke-Faktor
            float dmgBase = (baseDef != null && baseDef.baseDamage > 0f)
                ? baseDef.baseDamage
                : 1f;

            float levelStrength = _runtime.damagePerShot / dmgBase;

            // Globale Multis (Upgrades/Overclocks) optional einbeziehen
            float globalMult = 1f;
            if (_upgrades)   globalMult *= _upgrades.GetDamageMultiplier();
            if (overclocks)  globalMult *= overclocks.GetDamageMult();

            // Pull wird mit Level / Upgrades härter
            well.pullAcceleration *= levelStrength * globalMult;

            // Dauer leicht mit Level skalieren (z.B. bis +50%)
            float t = 0f;
            if (_runtime.MaxLevel > 1)
                t = Mathf.Clamp01((float)(_runtime.level - 1) / (_runtime.MaxLevel - 1));

            well.duration *= Mathf.Lerp(1f, 1.5f, t);
        }

        if (triggerBulletTime)
            TriggerBulletTimeClientRpc(bulletScale, bulletIn, bulletHold, bulletOut);
    }


    [ClientRpc]
    private void TriggerBulletTimeClientRpc(float scale, float inDur, float hold, float outDur)
    {
        if (SlowMoManager.Instance != null)
            SlowMoManager.Instance.BulletTime(scale, inDur, hold, outDur);
    }
}
