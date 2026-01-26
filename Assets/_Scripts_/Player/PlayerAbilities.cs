using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using PixPlays.ElementalVFX;

[DisallowMultipleComponent]
public class PlayerAbilities : NetworkBehaviour
{
    [Header("Refs")]
    public AbilityRegistry abilityRegistry;
    [SerializeField] private OverclockRuntime overclock;

    [Header("ShieldEarth (VFX)")]
    [SerializeField] private EarthShield shieldEarthPrefab;     // Prefab mit EarthShield-Komponente
    [SerializeField] private Transform shieldSocket;            // optional: Child "ShieldSocket"
    [SerializeField] private float shieldEarthDuration = 5f;    // wie lange aktiv (Sekunden)

    // Server sagt: Schild aktiv bis (ServerTime)
    public NetworkVariable<double> shieldActiveUntil =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private EarthShield _shieldInstance;

    // 2 Slots: readyAt als ServerTime (double), 0 = bereit
    public NetworkVariable<double> abilityReadyAt0 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<double> abilityReadyAt1 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // wann der Cooldown gestartet hat (ServerTime)
    public NetworkVariable<double> abilityCdStartAt0 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<double> abilityCdStartAt1 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // wie lang dieser Cooldown insgesamt ist (für Fill)
    public NetworkVariable<float> abilityCdDuration0 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> abilityCdDuration1 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private string _equippedId0;
    private string _equippedId1;

    private const string SHIELD_EARTH_ID = "ShieldEarth";

    private void Awake()
    {
        if (!overclock) overclock = GetComponent<OverclockRuntime>() ?? GetComponentInChildren<OverclockRuntime>(true);
    }

    public override void OnNetworkSpawn()
    {
        if (!abilityRegistry) abilityRegistry = MetaProgression.I.abilityRegistry;

        if (!shieldSocket)
        {
            // optional: lege im Player ein Child an: "ShieldSocket"
            var t = transform.Find("ShieldSocket");
            if (t) shieldSocket = t;
        }

        shieldActiveUntil.OnValueChanged += OnShieldUntilChanged;

        if (IsServer)
        {
            abilityReadyAt0.Value = abilityReadyAt1.Value = 0;
            abilityCdStartAt0.Value = abilityCdStartAt1.Value = 0;
            abilityCdDuration0.Value = abilityCdDuration1.Value = 0;
            shieldActiveUntil.Value = 0;
        }

        if (IsOwner)
        {
            var d = MetaProgression.I.Data;
            SendAbilityLoadoutServerRpc(d.ability1, d.ability2);
        }

        // Late-Join / initial state
        RefreshShieldVfx();
    }

    public override void OnNetworkDespawn()
    {
        shieldActiveUntil.OnValueChanged -= OnShieldUntilChanged;
        CleanupShieldImmediate();
    }

    private void OnShieldUntilChanged(double _, double __) => RefreshShieldVfx();

    private void Update()
    {
        if (!IsSpawned) return;

        // Wenn Zeit vorbei -> lokal VFX stoppen (Server muss nicht extra resetten)
        if (_shieldInstance != null)
        {
            double now = NetworkManager ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
            if (shieldActiveUntil.Value <= now)
            {
                StopShieldVfx();
            }
        }
    }

    private void RefreshShieldVfx()
    {
        double now = NetworkManager ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
        bool shouldBeActive = shieldActiveUntil.Value > now;

        if (shouldBeActive)
        {
            if (_shieldInstance == null)
                StartShieldVfx();
        }
        else
        {
            if (_shieldInstance != null)
                StopShieldVfx();
        }
    }

    private void StartShieldVfx()
    {
        if (!shieldEarthPrefab) return;

        Transform target = shieldSocket ? shieldSocket : transform;

        // NICHT parenten!
        _shieldInstance = Instantiate(shieldEarthPrefab);

        // Position setzen
        _shieldInstance.transform.position = target.position;

        // Rotation festlegen (weltfest):
        // Option A (empfohlen): gleiche Rotation auf allen Clients
        _shieldInstance.transform.rotation = Quaternion.identity;

        // Follow-Komponente dranhängen (folgt nur Position)
        var follow = _shieldInstance.gameObject.AddComponent<FollowPositionOnly>();
        follow.target = target;
        follow.keepInitialRotation = true;

        _shieldInstance.PlayShield();
    }

    private void StopShieldVfx()
    {
        if (_shieldInstance == null) return;

        _shieldInstance.StopShield();
        Destroy(_shieldInstance.gameObject, 2f); // Zeit für Shrink-Animation
        _shieldInstance = null;
    }

    private void CleanupShieldImmediate()
    {
        if (_shieldInstance != null)
        {
            Destroy(_shieldInstance.gameObject);
            _shieldInstance = null;
        }
    }

    // Owner->Server: Loadout setzen
    [ServerRpc]
    private void SendAbilityLoadoutServerRpc(string id0, string id1, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        _equippedId0 = Sanitize(id0);
        _equippedId1 = Sanitize(id1);
    }

    private string Sanitize(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var def = abilityRegistry ? abilityRegistry.Get(id) : null;
        if (def == null) return null;

        if (MetaProgression.I != null && !MetaProgression.I.IsAbilityUnlocked(def.id))
            return null;

        return def.id;
    }

    public void GetCooldownInfo(int slot, out float remaining, out float total)
    {
        double now = NetworkManager ? NetworkManager.ServerTime.Time : Time.timeAsDouble;

        if (slot == 0)
        {
            total = Mathf.Max(0.01f, abilityCdDuration0.Value);
            remaining = Mathf.Max(0f, (float)(abilityReadyAt0.Value - now));
            if (remaining <= 0.0001f) total = 0f;
        }
        else
        {
            total = Mathf.Max(0.01f, abilityCdDuration1.Value);
            remaining = Mathf.Max(0f, (float)(abilityReadyAt1.Value - now));
            if (remaining <= 0.0001f) total = 0f;
        }
    }

    // Client fragt: Slot benutzen
    [ServerRpc]
    public void RequestUseAbilityServerRpc(int slot, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (slot != 0 && slot != 1) return;

        var now = NetworkManager.ServerTime.Time;

        string abilityId = (slot == 0) ? _equippedId0 : _equippedId1;
        if (string.IsNullOrEmpty(abilityId)) return;

        var def = abilityRegistry.Get(abilityId);
        if (def == null) return;

        float totalCd = Mathf.Max(0.01f, def.cooldownSeconds);

        // Cooldown check + setzen
        if (slot == 0)
        {
            if (abilityReadyAt0.Value > now) return;
            abilityCdStartAt0.Value = now;
            abilityCdDuration0.Value = totalCd;
            abilityReadyAt0.Value = now + totalCd;
        }
        else
        {
            if (abilityReadyAt1.Value > now) return;
            abilityCdStartAt1.Value = now;
            abilityCdDuration1.Value = totalCd;
            abilityReadyAt1.Value = now + totalCd;
        }

        // ---- Ability Effekt ----
        if (abilityId == SHIELD_EARTH_ID)
        {
            float dur = Mathf.Max(0.05f, shieldEarthDuration);
            shieldActiveUntil.Value = now + dur;
            return;
        }

        // Default: Overclock (wie bisher)
        if (def.overclockEffect != null && overclock != null)
        {
            overclock.ActivateInstant_Server(def.overclockEffect);
        }
    }

    // Für Shield Damage Block
    public void Server_RegisterShieldHit(Vector3 point, Vector3 normal)
    {
        if (!IsServer) return;
        ShieldHitClientRpc(point, normal);
    }

    [ClientRpc]
    private void ShieldHitClientRpc(Vector3 point, Vector3 normal)
    {
        // _shieldInstance ist deine lokal instanziierte EarthShield-Instanz am Player
        // und EarthShield hat HitShield(point, normal) (aus der angepassten EarthShield-Version).
        if (_shieldInstance != null)
            _shieldInstance.HitShield(point, normal);
    }

    // UI-Helfer (Client): remaining cooldown in seconds
    public float GetRemainingCooldown(int slot)
    {
        double now = NetworkManager ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
        double readyAt = (slot == 0) ? abilityReadyAt0.Value : abilityReadyAt1.Value;
        return Mathf.Max(0f, (float)(readyAt - now));
    }

    // Optional: für Damage-Logik (Server)
    public bool Server_IsShieldEarthActive()
    {
        if (!IsServer) return false;
        return NetworkManager.ServerTime.Time < shieldActiveUntil.Value;
    }
}
