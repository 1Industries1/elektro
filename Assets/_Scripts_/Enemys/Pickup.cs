// Scripts/Inventory/Pickup.cs
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(SphereCollider))] // für Interact-Prompts als Trigger
[DisallowMultipleComponent]
public class Pickup : NetworkBehaviour
{
    public enum Mode { Magnet, Interact }

    // Nur für Energy relevant
    public enum EnergyCollectMode { Battery, Inventory, Both }

    [Header("Item")]
    public ResourceType type = ResourceType.Energy;
    public int amount = 1;

    [Header("Energy Handling")]
    [Tooltip("Nur für ResourceType.Energy relevant")]
    public EnergyCollectMode energyHandling = EnergyCollectMode.Battery;

    [Header("Modus")]
    public Mode mode = Mode.Magnet;

    [Header("Magnet-Settings")]
    public float attractRadius = 10f;
    public float collectDistance = 1.0f; // wird auch im Interact-Mode zum Server-Check genutzt
    public float baseSpeed = 10f;
    public AnimationCurve speedByDistance =
        AnimationCurve.EaseInOut(0f, 1f, 1f, 3f);
    public float retargetHz = 6f;

    [Header("Interact-Settings")]
    public string playerTag = "Player";
    public KeyCode interactKey = KeyCode.E;
    public GameObject promptUI;
    public float promptYOffset = 1.2f;

    [Header("FX")]
    public AudioClip collectSfx;
    [Range(0f, 1f)] public float collectSfxVolume = 1f;

    [Header("Debug")]
    public bool debugLog;

    // intern
    private Transform _target;
    private float _retargetTimer;
    private Rigidbody _rb;
    private SphereCollider _trigger;

    // nur für lokale Prompts (Client-seitig)
    private bool _playerInRangeClient;
    private Transform _nearbyPlayerClient;

    public override void OnNetworkSpawn()
    {
        _rb = GetComponent<Rigidbody>();
        _trigger = GetComponent<SphereCollider>();
        if (_trigger) _trigger.isTrigger = true;
        if (promptUI) promptUI.SetActive(false);
    }

    private void Update()
    {
        // Prompt schweben lassen (lokal)
        if (mode == Mode.Interact && promptUI)
        {
            promptUI.transform.position = transform.position + Vector3.up * promptYOffset;
        }

        // Interact: Client sendet bei Tastendruck eine Anfrage an den Server
        if (mode == Mode.Interact && _playerInRangeClient && Input.GetKeyDown(interactKey))
        {
            TryCollect_RequestToServer();
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (mode != Mode.Magnet) return;

        // Ziel regelmäßig neu suchen
        _retargetTimer -= Time.fixedDeltaTime;
        if (_retargetTimer <= 0f)
        {
            _retargetTimer = 1f / Mathf.Max(0.01f, retargetHz);
            _target = FindClosestPlayerWithin(attractRadius);
        }
        if (_target == null) return;

        Vector3 toPlayer = _target.position - transform.position;
        float dist = toPlayer.magnitude;

        // Sammeln?
        if (dist <= collectDistance)
        {
            Server_CollectFor(_target);
            return;
        }

        // Bewegung
        float t = Mathf.Clamp01(dist / attractRadius);
        float speedMult = speedByDistance.Evaluate(1f - t);
        float v = baseSpeed * Mathf.Max(0.2f, speedMult);
        Vector3 dir = dist > 0.0001f ? (toPlayer / dist) : Vector3.zero;
        Vector3 nextPos = transform.position + dir * v * Time.fixedDeltaTime;

        if (_rb != null && !_rb.isKinematic) _rb.MovePosition(nextPos);
        else transform.position = nextPos;

        if (debugLog)
            Debug.DrawLine(transform.position, _target.position, Color.cyan, 0.05f);
    }

    // ======= Interact: lokale Trigger (nur für Prompt) =======

    private void OnTriggerEnter(Collider other)
    {
        if (mode != Mode.Interact) return;
        if (!other.CompareTag(playerTag)) return;

        _playerInRangeClient = true;
        _nearbyPlayerClient = other.transform;
        if (promptUI) promptUI.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (mode != Mode.Interact) return;
        if (!other.CompareTag(playerTag)) return;

        _playerInRangeClient = false;
        _nearbyPlayerClient = null;
        if (promptUI) promptUI.SetActive(false);
    }

    private void TryCollect_RequestToServer()
    {
        TryCollectServerRpc(NetworkObjRefFromTransform(_nearbyPlayerClient));
    }

    private NetworkObjectReference NetworkObjRefFromTransform(Transform t)
    {
        if (t == null) return default;
        var no = t.GetComponentInParent<NetworkObject>();
        return no != null ? new NetworkObjectReference(no) : default;
    }

    // Client bittet Server um Einsammeln (Server validiert die Distanz)
    [ServerRpc(RequireOwnership = false)]
    private void TryCollectServerRpc(NetworkObjectReference playerRef, ServerRpcParams rpcParams = default)
    {
        if (!playerRef.TryGet(out var playerNo)) return;

        // Sicherheitscheck Nähe
        float dist = Vector3.Distance(playerNo.transform.position, transform.position);
        if (dist > collectDistance * 1.1f) return;

        Server_CollectFor(playerNo.transform);
    }

    // ======= Server: tatsächliches Einsammeln + FX =======

    private void Server_CollectFor(Transform player)
    {
        if (!IsServer || player == null) return;

        // Komponenten am Player
        var inv = player.GetComponent<PlayerInventory>();
        var xp  = player.GetComponent<PlayerXP>();

        // Verteillogik
        if (type == ResourceType.Energy)
        {
            // Energy == XP (VS-Style). Ignoriere Inventar, gib XP.
            if (xp != null)
            {
                xp.Server_AddXP(amount);
            }
            else
            {
                // Fallback, falls PlayerXP fehlt (damit man nichts "verliert"):
                if (inv != null) inv.Server_Add(ResourceType.Energy, amount);
                Debug.LogWarning("[Pickup] PlayerXP fehlt am Player – fallback auf Inventory.Add(Energy).");
            }
        }
        else
        {
            // Nicht-Energy: wie gehabt ins Inventar
            if (inv != null) inv.Server_Add(type, amount);
        }

        // FX nur dem Owner dieses Players schicken
        ulong ownerId = 0;
        var playerNo = player.GetComponentInParent<NetworkObject>();
        if (playerNo != null) ownerId = playerNo.OwnerClientId;

        var toOwner = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { ownerId } }
        };
        PlayPickupFxClientRpc(transform.position, toOwner);

        // Despawn
        var no = GetComponent<NetworkObject>();
        if (no && no.IsSpawned) no.Despawn(true);
        else Destroy(gameObject);
    }

    [ClientRpc]
    private void PlayPickupFxClientRpc(Vector3 worldPos, ClientRpcParams clientRpcParams = default)
    {
        if (collectSfx != null)
            AudioSource.PlayClipAtPoint(collectSfx, worldPos, collectSfxVolume);
    }

    // ======= Helpers =======
    private Transform FindClosestPlayerWithin(float radius)
    {
        float best = float.MaxValue;
        Transform bestT = null;
        var players = GameObject.FindObjectsOfType<PlayerInventory>();
        foreach (var p in players)
        {
            float d2 = (p.transform.position - transform.position).sqrMagnitude;
            float mult = 1f;
            var up = p.GetComponent<PlayerUpgrades>();
            if (up != null) mult = Mathf.Max(0.1f, up.GetMagnetRangeMult());
            float effR = radius * mult;
            if (d2 < effR * effR && d2 < best)
            {
                best = d2;
                bestT = p.transform;
            }
        }
        return bestT;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attractRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, collectDistance);
    }
#endif
}
