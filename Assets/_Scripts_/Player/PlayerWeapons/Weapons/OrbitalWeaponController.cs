using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class OrbitalWeaponController : NetworkBehaviour
{
    [Header("Weapon Data")]
    [Tooltip("ScriptableObject für die Orbital-Plasma-Waffe (z.B. OrbitalPlasma.asset).")]
    public WeaponDefinition weaponDef;

    private PlayerWeapons _playerWeapons;
    private PlayerUpgrades _upgrades;
    private PlayerMovement _ownerMovement;
    private PlayerHealth _health;

    [SerializeField] private OverclockRuntime overclocks;

    [Header("Orbital Settings (Fallbacks)")]
    [Tooltip("Prefab mit OrbitOrb + NetworkObject + NetworkTransform.")]
    public NetworkObject orbPrefab;

    [Tooltip("Fallback-Orbitradius, falls rangeMeters in der WeaponDefinition <= 0 ist.")]
    public float orbitRadius = 2.5f;

    [Tooltip("Basis-Umlaufgeschwindigkeit in Grad/Sekunde.")]
    public float orbitAngularSpeedDeg = 120f;

    [Tooltip("Tick-Intervall für Schaden in Sekunden.")]
    public float damageTickInterval = 0.25f;

    // Laufzeitwerte
    private WeaponRuntime _runtime;

    // Server: hält die gespawnten Orbs
    private readonly List<NetworkObject> _spawnedOrbs = new();

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

        // Nur der Server spawnt / reorganisiert die Orbs
        if (IsServer && _runtime != null)
        {
            RebuildOrbs();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_playerWeapons != null)
            _playerWeapons.RuntimesRebuilt -= OnWeaponsRebuilt;

        if (IsServer)
            DespawnAllOrbs();
    }

    private void OnDisable()
    {
        // Falls der Controller deaktiviert wird, Orbs aufräumen (nur Server).
        if (IsServer)
            DespawnAllOrbs();
    }

    // =====================================================================
    // RUNTIME-SETUP
    // =====================================================================

    private void OnWeaponsRebuilt()
    {
        BuildLocalRuntime();

        if (IsServer)
        {
            if (_runtime != null) RebuildOrbs();
            else DespawnAllOrbs();
        }
    }

    private void BuildLocalRuntime()
    {
        _runtime = null;

        if (_playerWeapons != null)
        {
            // Annahme: PlayerWeapons hat orbitalDef / orbitalLevel / OrbitalRuntime
            if (_playerWeapons.orbitalLevel.Value > 0 && _playerWeapons.OrbitalRuntime != null)
            {
                _runtime = new WeaponRuntime(_playerWeapons.orbitalDef, _playerWeapons.orbitalLevel.Value);
                if (_upgrades) _upgrades.ApplyTo(_runtime);
            }
        }
        else if (weaponDef != null)
        {
            // Editor/Offline-Fallback (kein Netcode aktiv)
            var nm = NetworkManager.Singleton;
            bool netInactive = (nm == null || !nm.IsListening);
            if (netInactive)
            {
                _runtime = new WeaponRuntime(weaponDef, 1);
                if (_upgrades) _upgrades.ApplyTo(_runtime);
            }
        }
    }

    // =====================================================================
    // ORB-SERVER-LOGIK
    // =====================================================================

    private void DespawnAllOrbs()
    {
        foreach (var no in _spawnedOrbs)
        {
            if (no != null && no.IsSpawned)
                no.Despawn(true);
        }
        _spawnedOrbs.Clear();
    }

    private void RebuildOrbs()
    {
        DespawnAllOrbs();
        if (_runtime == null || orbPrefab == null) return;
        if (_health != null && _health.IsDead()) return;

        // --------- HIER: orbitRadius aus runtime.rangeMeters ---------
        // Wenn rangeMeters in der WeaponDefinition/Runtime gesetzt ist (>0),
        // benutzen wir den als Orbit-Radius. Sonst nehmen wir den Inspector-Wert.
        float orbitRad = _runtime.rangeMeters > 0f
            ? _runtime.rangeMeters
            : orbitRadius;

        orbitRad = Mathf.Max(0.25f, orbitRad); // kleine Safety, kein negativer Radius

        // Anzahl Orbs aus Runtime: benutze salvoCount als "OrbCount"
        int orbCount = Mathf.Max(1, _runtime.salvoCount > 0 ? _runtime.salvoCount : 2);

        // Basisschaden pro Tick aus Runtime
        float dmgScale =
            (_upgrades ? _upgrades.GetDamageMultiplier() : 1f) *
            (overclocks ? overclocks.GetDamageMult() : 1f);

        float baseDmg = _runtime.ComputeDamageNonPierced(applyCrit: true) * dmgScale;

        // leicht reduziert, weil es kontinuierlicher Tick-Schaden ist
        float damagePerTick = baseDmg * 0.4f;

        // Optional: Orbit-Geschwindigkeit aus projectileSpeed ableiten
        float angSpeed = orbitAngularSpeedDeg;
        if (_runtime.projectileSpeed > 0f)
            angSpeed = _runtime.projectileSpeed * 20f; // Tuning nach Geschmack

        for (int i = 0; i < orbCount; i++)
        {
            float angleDeg = (360f / orbCount) * i;

            NetworkObject orbNo = Instantiate(orbPrefab, transform.position, Quaternion.identity);
            orbNo.Spawn(true);
            _spawnedOrbs.Add(orbNo);

            var orb = orbNo.GetComponent<OrbitOrb>();
            if (orb != null)
            {
                orb.ServerInit(
                    ownerCenter: transform,       // Spieler-Transform
                    radius: orbitRad,
                    startAngleDeg: angleDeg,
                    angularSpeedDeg: angSpeed,
                    baseDamagePerTick: damagePerTick,
                    tickInterval: damageTickInterval,
                    ownerClientId: OwnerClientId,
                    orbIndex: i,
                    orbCount: orbCount
                );
            }
        }

    }
    
}
