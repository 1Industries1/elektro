// Scripts/Inventory/Pickup.cs
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(SphereCollider))] // für Interact-Prompts als Trigger
[DisallowMultipleComponent]
public class Pickup : NetworkBehaviour
{
    public enum Mode { Magnet, Interact }

    // Nur für XP relevant
    public enum EnergyCollectMode { Battery, Inventory, Both }

    [Header("Item")]
    public ResourceType type = ResourceType.XP;
    public int amount = 1;

    [Header("XP Handling")]
    [Tooltip("Nur für ResourceType.XP relevant")]
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

    // Behalte aktuelles Target bis es deutlich raus ist (1.2 = +20 %)
    [Range(1f, 2f)] public float leaveRadiusMult = 1.2f;

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

        // Jitter, damit nicht alle Instanzen gleichzeitig retargeten
        if (IsServer)
            _retargetTimer = Random.value / Mathf.Max(0.01f, retargetHz);
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

        // 1) Aktuelles Target behalten, solange es in einem erweiterten Radius bleibt
        if (_target != null)
        {
            // Wenn Ziel deaktiviert/zerstört ist → loslassen
            if (!_target.gameObject.activeInHierarchy)
            {
                _target = null;
            }
            else
            {
                float d2 = (_target.position - transform.position).sqrMagnitude;
                float leaveR = attractRadius * leaveRadiusMult;
                if (d2 > leaveR * leaveR)
                    _target = null;
            }
        }

        // 2) Nur retargeten, wenn aktuell kein Target existiert
        if (_target == null)
        {
            _retargetTimer -= Time.fixedDeltaTime;
            if (_retargetTimer <= 0f)
            {
                _retargetTimer = 1f / Mathf.Max(0.01f, retargetHz);
                _target = FindClosestPlayerWithin(attractRadius);
            }

            // noch keins → in diesem Tick keine Bewegung
            if (_target == null) return;
        }

        // 3) Bewegung / Einsammeln mit vorhandenem Target
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
        var up  = player.GetComponent<PlayerUpgrades>();
        
        // Multiplikatoren aus Upgrades
        float xpMult   = up != null ? up.GetXpDropMult()   : 1f;
        float goldMult = up != null ? up.GetGoldDropMult() : 1f;

        // Verteillogik
        if (type == ResourceType.XP)
        {
            int finalAmount = amount;
            finalAmount = Mathf.Max(1, Mathf.RoundToInt(amount * xpMult));
            
            // XP == XP (VS-Style). Ignoriere Inventar, gib XP.
            if (xp != null)
            {
                xp.Server_AddXP(finalAmount);
            }
            else
            {
                // Fallback, falls PlayerXP fehlt (damit man nichts "verliert"):
                if (inv != null) inv.Server_Add(ResourceType.XP, finalAmount);
                Debug.LogWarning("[Pickup] PlayerXP fehlt am Player – fallback auf Inventory.Add(XP).");
            }
        }
        else
        {
            int finalAmount = amount;

            // Gold skalieren, alles andere unverändert
            if (type == ResourceType.Gold)
            {
                finalAmount = Mathf.Max(1, Mathf.RoundToInt(amount * goldMult));
            }

            if (inv != null) inv.Server_Add(type, finalAmount);
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

        // Kleine, serverseitige Liste iterieren (siehe PlayerInventory.ServerPlayers)
        var list = PlayerInventory.ServerPlayers;
        for (int i = 0; i < list.Count; i++)
        {
            var p = list[i];
            if (p == null) continue;

            float d2 = (p.transform.position - transform.position).sqrMagnitude;

            float mult = 1f;
            var up = p.GetComponent<PlayerUpgrades>();
            if (up != null) mult = Mathf.Max(0.1f, up.GetMagnetRangeMult());

            float effR = radius * mult;
            float r2 = effR * effR;
            if (d2 < r2 && d2 < best)
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
