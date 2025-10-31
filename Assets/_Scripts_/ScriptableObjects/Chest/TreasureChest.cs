using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(SphereCollider))]
[DisallowMultipleComponent]
public class TreasureChest : NetworkBehaviour
{
    [Header("Kosten")]
    public int goldCost = 30; // Preis in Gold

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private string openTrigger = "Open";

    [Header("Interact")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [Tooltip("Optionaler lokaler Prompt (z. B. \"[E] Öffnen (30)\")")]
    [SerializeField] private GameObject promptUI;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private float promptYOffset = 1.2f;

    [Header("Reuse")]
    [Tooltip("Kann die Truhe nur einmal geöffnet werden?")]
    [SerializeField] private bool singleUse = true;
    [Tooltip("Zeit nach Öffnen, bevor erneut interagiert werden darf (falls singleUse = false).")]
    [SerializeField] private float reopenCooldown = 2f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openClip;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    // Öffnungs-Status (Server-Quelle der Wahrheit)
    public NetworkVariable<bool> Opened = new(false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // nur Server: Cooldown für Reopen (wenn singleUse = false)
    private float _serverNextOpenTime = 0f;

    // --- Profil-Lock (Server)
    private int _lockedProfileIndex = -1;
    private TreasureChestDropProfile _lockedProfile = null;

    [Header("Slow Motion (Chest)")]
    public bool chestSlowMo = true;
    [Range(0.05f, 1f)] public float chestSlowScale = 0.05f;
    public float chestSlowFadeIn = 0.35f;
    public float chestSlowFadeOut = 1.2f;
    public float chestSlowDelay = 2f;

    private int _chestSlowHandle = 0;
    private Coroutine _slowDelayCo;

    [Header("Events")]
    public UnityEvent OnOpened;
    public UnityEvent<float> OnProgress; // für Kompatibilität

    // intern (Client)
    private bool _inRangeLocal;
    private Transform _nearbyPlayerClient; // für UI-Referenz (lokal)
    private int openTriggerHash;

    // --- Server: Wer steht im Trigger? (OwnerClientIds)
    private readonly HashSet<ulong> _serverClientsInTrigger = new();

    // ==========================
    // --- Drop-Logik (bestehend)
    // ==========================
    [System.Flags]
    public enum DropType
    {
        NewPassive = 1, NewWeapon = 2, UpgradePassive = 4,
        UpgradeWeapon = 8, Evolution = 16
    }

    public DropType possibleDrops = (DropType)~0;

    public enum DropCountType { sequential, random }
    public DropCountType dropCountType = DropCountType.sequential;

    public TreasureChestDropProfile[] dropProfiles;

    public static int totalPickups = 0;
    private int currentDropProfileIndex = 0;

    // Für Preview-Lock (serverseitig vorgeplante Upgrades)
    private readonly List<string> _lockedRewardWeaponIds = new();

    // ===== Helpers =====
    private void Log(string msg)
    {
        if (!debugLogs) return;
        string id = IsSpawned ? NetworkObjectId.ToString() : "unspawned";
        Debug.Log($"[Chest#{id}] {msg}");
    }

    private void Warn(string msg)
    {
        if (!debugLogs) return;
        string id = IsSpawned ? NetworkObjectId.ToString() : "unspawned";
        Debug.LogWarning($"[Chest#{id}] {msg}");
    }

    private void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();

        var col = GetComponent<SphereCollider>();
        if (col)
        {
            col.isTrigger = true;
        }

        openTriggerHash = Animator.StringToHash(openTrigger);

        if (audioSource == null && openClip != null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (promptUI) promptUI.SetActive(false);
        if (promptText) promptText.text = $"[E] {goldCost} Gold";

        Log("Awake: setup done.");
    }

    private void OnValidate()
    {
        var col = GetComponent<SphereCollider>();
        if (col) col.isTrigger = true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Log($"OnNetworkSpawn: IsServer={IsServer}, IsClient={IsClient}, IsSpawned={IsSpawned}, Opened={Opened.Value}");
        TryPushPriceToLocalHud();
    }

    private void Update()
    {
        // Prompt schwebt über der Kiste
        if (promptUI)
            promptUI.transform.position = transform.position + Vector3.up * promptYOffset;

        // Nur lokale Eingabe scannen
        if (!_inRangeLocal || IsBlockedClientView()) return;

        if (Input.GetKeyDown(interactKey))
        {
            Log("Client: E pressed → sending open request RPC.");
            SendOpenRequestToServer();
        }
    }

    // ===== Trigger-Handling =====
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // --- Client: Prompt zeigen (nur für lokalen Player)
        var noClient = other.GetComponentInParent<NetworkObject>();
        if (noClient && NetworkManager.Singleton.LocalClientId == noClient.OwnerClientId && !IsBlockedClientView())
        {
            _inRangeLocal = true;
            _nearbyPlayerClient = other.transform;

            if (promptUI) promptUI.SetActive(true);
            if (promptText) promptText.text = $"[E] {goldCost} Gold";
            Log($"Client: Local player entered trigger (player NO #{noClient.NetworkObjectId}).");
        }

        // --- Server: Mitgliedschaft tracken
        if (IsServer && noClient != null)
        {
            _serverClientsInTrigger.Add(noClient.OwnerClientId);
            Log($"Server: Enter by client {noClient.OwnerClientId}. InTrigger={_serverClientsInTrigger.Count}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        var noClient = other.GetComponentInParent<NetworkObject>();
        if (noClient && NetworkManager.Singleton.LocalClientId == noClient.OwnerClientId)
        {
            _inRangeLocal = false;
            _nearbyPlayerClient = null;
            if (promptUI) promptUI.SetActive(false);
            Log($"Client: Local player exited trigger (player NO #{noClient.NetworkObjectId}).");
        }

        if (IsServer && noClient != null)
        {
            _serverClientsInTrigger.Remove(noClient.OwnerClientId);
            Log($"Server: Exit by client {noClient.OwnerClientId}. InTrigger={_serverClientsInTrigger.Count}");
        }
    }

    // Clientseitige Block-Sicht (Opened kommt vom Server)
    private bool IsBlockedClientView()
    {
        if (singleUse && Opened.Value)
        {
            Log("Client: Chest is blocked (already opened).");
            return true;
        }
        return false;
    }

    private void SendOpenRequestToServer()
    {
        NetworkObjectReference playerRef = default;

        // bevorzugt: aus Trigger
        if (_nearbyPlayerClient != null)
        {
            var no = _nearbyPlayerClient.GetComponentInParent<NetworkObject>();
            if (no)
            {
                playerRef = no;
                Log($"Client: Using trigger playerRef NO#{no.NetworkObjectId}.");
            }
        }

        // Fallback: Local Player
        if (!playerRef.TryGet(out _))
        {
            var local = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (local)
            {
                playerRef = local;
                Log($"Client: Trigger ref missing → using Local Player NO#{local.NetworkObjectId}.");
            }
            else
            {
                Warn("Client: No Local PlayerObject found! Sending default ref.");
            }
        }

        Log("Client: Calling RequestOpenServerRpc...");
        RequestOpenServerRpc(playerRef);
    }

    // ===== ServerRpc: Öffnen anfordern (Trigger-Mitgliedschaft als Bedingung) =====
    [ServerRpc(RequireOwnership = false)]
    private void RequestOpenServerRpc(NetworkObjectReference playerRef, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) { Warn("ServerRpc called but not on server → abort."); return; }
        ulong sender = rpcParams.Receive.SenderClientId;
        Log($"ServerRpc: Open request from client {sender}.");

        if (singleUse && Opened.Value) { Log("Server: Already opened → abort."); return; }
        if (!singleUse && Time.time < _serverNextOpenTime)
        {
            Log($"Server: Reopen cooldown {(_serverNextOpenTime - Time.time):F2}s → abort.");
            return;
        }

        // (A) Muss im Trigger sein
        if (!_serverClientsInTrigger.Contains(sender))
        {
            Log($"Server: Client {sender} not in trigger → abort.");
            return;
        }

        // (B) PlayerObject robust bestimmen (ref → Fallback Sender)
        NetworkObject playerNo = null;
        if (!playerRef.TryGet(out playerNo) || playerNo == null)
        {
            playerNo = NetworkManager.Singleton?.SpawnManager?.GetPlayerNetworkObject(sender);
            Log("Server: playerRef invalid → using sender PlayerObject.");
        }
        if (playerNo == null) { Warn("Server: No PlayerObject → abort."); return; }

        // (C) Inventory & Gold prüfen
        var inv = playerNo.GetComponent<PlayerInventory>() ?? playerNo.GetComponentInChildren<PlayerInventory>(true);
        if (inv == null) { Warn("Server: PlayerInventory missing → abort."); return; }

        int curGold = inv.GetAmount(ResourceType.Gold);
        Log($"Server: Player gold {curGold} / price {goldCost}.");
        if (!inv.Server_Has(ResourceType.Gold, goldCost))
        {
            var toOwner = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } } };
            NotEnoughGoldClientRpc(goldCost, toOwner);
            Log("Server: Not enough gold → notified owner.");
            return;
        }

        // (D) Abbuchen
        bool ok = inv.Server_TryConsume(ResourceType.Gold, goldCost);
        Log($"Server: Consume gold result = {ok}.");

        // (E) Öffnen status/cooldown
        if (singleUse) Opened.Value = true;
        _serverNextOpenTime = Time.time + reopenCooldown;
        Log($"Server: Opened={Opened.Value}, next reopen @ {_serverNextOpenTime:F2}.");

        // (F) Drop-Profil locken
        _lockedProfile = GetNextDropProfile();
        _lockedProfileIndex = currentDropProfileIndex;
        Log($"Server: Locked profile index = {_lockedProfileIndex}.");

        // (G) Preview-Upgrades planen (optional)
        _lockedRewardWeaponIds.Clear();
        int rewardCount = GetRewardCount();
        var weapons = playerNo.GetComponent<PlayerWeapons>();
        if (possibleDrops.HasFlag(DropType.UpgradeWeapon) && rewardCount > 0 && weapons != null)
        {
            for (int i = 0; i < rewardCount; i++)
            {
                var id = weapons.Server_PeekRandomUpgradeableId();
                if (!string.IsNullOrEmpty(id))
                {
                    _lockedRewardWeaponIds.Add(id);
                    Log($"Server: Planned weapon upgrade id='{id}'.");
                }
            }
        }

        // (H) Preview-Icons und UI-Start an den Sender
        var toSender = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } } };
        foreach (var id in _lockedRewardWeaponIds) Owner_PreviewIconClientRpc(id, toSender);

        float revealDelay = 0f;
        if (_lockedProfile != null) revealDelay = Mathf.Max(0f, _lockedProfile.animDuration + _lockedProfile.delayTime);
        Log($"Server: Starting client sequence (profile={_lockedProfileIndex}, delay={revealDelay:F2}).");
        ClientStartOpenSequenceClientRpc(revealDelay, _lockedProfileIndex, toSender);
    }

    // ===== ClientRpc: Start der Open-Sequenz (VFX/UI) =====
    [ClientRpc(Delivery = RpcDelivery.Reliable)]
    private void ClientStartOpenSequenceClientRpc(float revealDelaySeconds, int profileIndex, ClientRpcParams clientRpcParams = default)
    {
        Log($"ClientRpc: StartOpenSequence (delay={revealDelaySeconds:F2}, profileIndex={profileIndex}).");

        if (animator && openTriggerHash != 0) animator.SetTrigger(openTrigger);
        if (audioSource && openClip) audioSource.PlayOneShot(openClip);

        UIChestManager.Activate(this, profileIndex);
        UIChestManager.instance?.Begin();

        if (promptUI) promptUI.SetActive(false);

        if (chestSlowMo && SlowMoManager.Instance != null && _chestSlowHandle == 0)
        {
            if (_slowDelayCo != null) StopCoroutine(_slowDelayCo);
            _slowDelayCo = StartCoroutine(StartChestSlowMoDelayed());
        }

        OnOpened?.Invoke();
    }

    [ClientRpc(Delivery = RpcDelivery.Reliable)]
    private void NotEnoughGoldClientRpc(int price, ClientRpcParams clientRpcParams = default)
    {
        Log($"ClientRpc: Not enough gold (need {price}).");
        CenterToastUI.Instance?.Show($"Not enough gold (requires {price}).", 2.5f);
    }

    [ClientRpc(Delivery = RpcDelivery.Reliable)]
    private void Owner_PreviewIconClientRpc(string weaponId, ClientRpcParams clientRpcParams = default)
    {
        Log($"ClientRpc: Preview icon for weaponId='{weaponId}'.");
        if (string.IsNullOrEmpty(weaponId)) return;

        var playerObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (!playerObj) return;

        var pw = playerObj.GetComponent<PlayerWeapons>() ?? playerObj.GetComponentInChildren<PlayerWeapons>(true);
        if (pw == null) return;

        WeaponDefinition def = null;
        if (pw.cannonDef != null && pw.cannonDef.id == weaponId) def = pw.cannonDef;
        else if (pw.blasterDef != null && pw.blasterDef.id == weaponId) def = pw.blasterDef;
        else if (pw.grenadeDef != null && pw.grenadeDef.id == weaponId) def = pw.grenadeDef;

        if (def != null && def.uiIcon != null)
            UIChestManager.NotifyItemReceived(def.uiIcon);
    }

    private IEnumerator StartChestSlowMoDelayed()
    {
        float d = Mathf.Max(0f, chestSlowDelay);
        if (d > 0f)
            yield return new WaitForSecondsRealtime(d);

        if (!chestSlowMo || SlowMoManager.Instance == null || _chestSlowHandle != 0)
        {
            Log("Client: SlowMo cancelled/skip.");
            _slowDelayCo = null;
            yield break;
        }

        _chestSlowHandle = SlowMoManager.Instance.BeginHold(chestSlowScale, chestSlowFadeIn);
        Log("Client: SlowMo begin.");
        _slowDelayCo = null;
    }

    public void OnChestUIClose()
    {
        Log("Client: Chest UI close.");

        if (_slowDelayCo != null)
        {
            StopCoroutine(_slowDelayCo);
            _slowDelayCo = null;
        }

        if (_chestSlowHandle != 0 && SlowMoManager.Instance != null)
        {
            SlowMoManager.Instance.EndHold(_chestSlowHandle, chestSlowFadeOut);
            _chestSlowHandle = 0;
            Log("Client: SlowMo end.");
        }
    }

    // ==========================
    // --- Drop-Helfer
    // ==========================

    public TreasureChestDropProfile GetCurrentDropProfile()
    {
        if (dropProfiles == null || dropProfiles.Length == 0) return null;
        currentDropProfileIndex = Mathf.Clamp(currentDropProfileIndex, 0, dropProfiles.Length - 1);
        var p = dropProfiles[currentDropProfileIndex];
        Log($"GetCurrentDropProfile → index={currentDropProfileIndex}, name='{p.profileName}', noOfItems={p.noOfItems}");
        return p;
    }

    public TreasureChestDropProfile GetNextDropProfile()
    {
        if (dropProfiles == null || dropProfiles.Length == 0)
        {
            Warn("Drop profiles not set.");
            return null;
        }

        switch (dropCountType)
        {
            case DropCountType.sequential:
                currentDropProfileIndex = Mathf.Clamp(totalPickups, 0, dropProfiles.Length - 1);
                Log($"GetNextDropProfile (sequential) → index={currentDropProfileIndex}");
                return dropProfiles[currentDropProfileIndex];

            case DropCountType.random:
                float playerLuck = 1f; // Optional
                var weightedProfiles = new List<(int index, TreasureChestDropProfile profile, float weight)>();
                for (int i = 0; i < dropProfiles.Length; i++)
                {
                    var p = dropProfiles[i];
                    if (p == null) continue;
                    float weight = Mathf.Max(0f, p.baseDropChance * (1f + p.luckScaling * (playerLuck - 1f)));
                    weightedProfiles.Add((i, p, weight));
                }
                if (weightedProfiles.Count == 0) return GetCurrentDropProfile();

                float totalWeight = 0f;
                foreach (var e in weightedProfiles) totalWeight += e.weight;

                float r = Random.Range(0f, totalWeight);
                float cumulative = 0f;
                foreach (var e in weightedProfiles)
                {
                    cumulative += e.weight;
                    if (r < cumulative)
                    {
                        currentDropProfileIndex = e.index;
                        Log($"GetNextDropProfile (random) → index={currentDropProfileIndex}");
                        return e.profile;
                    }
                }
                currentDropProfileIndex = weightedProfiles[^1].index;
                Log($"GetNextDropProfile (random, fallback) → index={currentDropProfileIndex}");
                return weightedProfiles[^1].profile;
        }

        return GetCurrentDropProfile();
    }

    private int GetRewardCount()
    {
        var dp = _lockedProfile != null ? _lockedProfile : GetCurrentDropProfile();
        int c = dp ? Mathf.Max(1, dp.noOfItems) : 1;
        Log($"GetRewardCount → {c}");
        return c;
    }

    // Server-only: Rewards erteilen (inkl. deterministische Upgrades)
    private void GiveRewardsServer(PlayerInventory inventory, out int issuedCount)
    {
        issuedCount = 0;
        if (!IsServer || inventory == null) { Warn("GiveRewardsServer: invalid state."); return; }

        int rewardCount = GetRewardCount();
        var weapons = inventory.GetComponent<PlayerWeapons>();

        for (int i = 0; i < rewardCount; i++)
        {
            issuedCount++;

            if (possibleDrops.HasFlag(DropType.UpgradeWeapon) && weapons != null)
            {
                if (_lockedRewardWeaponIds.Count > 0)
                {
                    string id = _lockedRewardWeaponIds[0];
                    _lockedRewardWeaponIds.RemoveAt(0);

                    if (weapons.Server_LevelUpById(id, notifyOwner: false))
                    {
                        Log($"Server: Upgraded planned weapon '{id}'.");
                        SignalWeaponUpgradeToastToOwner(weapons, ResolveDefById(weapons, id), inventory.OwnerClientId);
                        continue;
                    }
                    else
                    {
                        Log($"Server: Planned upgrade '{id}' failed → fallback random.");
                    }
                }

                if (weapons.Server_TryLevelUpRandomWeapon(out var upgradedDef))
                {
                    Log($"Server: Random weapon upgrade → '{upgradedDef?.id}'.");
                    SignalWeaponUpgradeToastToOwner(weapons, upgradedDef, inventory.OwnerClientId);
                    continue;
                }
            }

            // weitere Drop-Typen hier (Stubs)
            // if (possibleDrops.HasFlag(DropType.Evolution)) { ... }
            // if (possibleDrops.HasFlag(DropType.UpgradePassive)) { ... }
            // if (possibleDrops.HasFlag(DropType.NewWeapon)) { ... }
            // if (possibleDrops.HasFlag(DropType.NewPassive)) { ... }
        }

        _lockedRewardWeaponIds.Clear();
    }

    private WeaponDefinition ResolveDefById(PlayerWeapons pw, string id)
    {
        if (pw == null) return null;
        if (pw.cannonDef != null && pw.cannonDef.id == id) return pw.cannonDef;
        if (pw.blasterDef != null && pw.blasterDef.id == id) return pw.blasterDef;
        if (pw.grenadeDef != null && pw.grenadeDef.id == id) return pw.grenadeDef;
        return null;
    }

    // SERVER: nach erfolgreichem Waffen-Upgrade … Owner-Toast triggern
    private void SignalWeaponUpgradeToastToOwner(PlayerWeapons weapons, WeaponDefinition def, ulong ownerClientId)
    {
        if (weapons == null || def == null) return;

        int newLevel = 1;
        if (def == weapons.cannonDef) newLevel = weapons.cannonLevel.Value;
        else if (def == weapons.blasterDef) newLevel = weapons.blasterLevel.Value;
        else if (def == weapons.grenadeDef) newLevel = weapons.grenadeLevel.Value;

        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { ownerClientId } }
        };
        Log($"Server: Send upgrade toast for '{def.id}' L{newLevel} to owner {ownerClientId}.");
        Owner_ShowWeaponUpgradeToastClientRpc(def.id, newLevel, target);
    }

    [ClientRpc(Delivery = RpcDelivery.Reliable)]
    private void Owner_ShowWeaponUpgradeToastClientRpc(string weaponId, int newLevel, ClientRpcParams clientRpcParams = default)
    {
        var playerObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (!playerObj) return;

        var pw = playerObj.GetComponent<PlayerWeapons>() ?? playerObj.GetComponentInChildren<PlayerWeapons>(true);
        var up = playerObj.GetComponent<PlayerUpgrades>() ?? playerObj.GetComponentInChildren<PlayerUpgrades>(true);
        if (pw == null) return;

        WeaponDefinition def = null;
        if (pw.cannonDef != null && pw.cannonDef.id == weaponId) def = pw.cannonDef;
        else if (pw.blasterDef != null && pw.blasterDef.id == weaponId) def = pw.blasterDef;
        else if (pw.grenadeDef != null && pw.grenadeDef.id == weaponId) def = pw.grenadeDef;

        string msg = WeaponStepDescriber.DescribeStepWithName(def, newLevel, up);
        CenterToastUI.Instance?.Show(msg, 4f);
    }

    // Button/Callback aus UI (z. B. „Fertig“) → Rewards wirklich gutschreiben
    [ServerRpc(RequireOwnership = false)]
    public void ConfirmOpenRevealDoneServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        ulong sender = rpcParams.Receive.SenderClientId;
        Log($"ServerRpc: Confirm reveal done from {sender}.");

        var playerObj = NetworkManager.Singleton?.SpawnManager?.GetPlayerNetworkObject(sender);
        if (!playerObj) { Warn("No PlayerObject in ConfirmOpenRevealDone."); return; }

        var inv = playerObj.GetComponent<PlayerInventory>() ?? playerObj.GetComponentInChildren<PlayerInventory>(true);
        if (inv == null) { Warn("No PlayerInventory in ConfirmOpenRevealDone."); return; }

        GiveRewardsServer(inv, out var issued);
        Log($"Server: Rewards granted, count={issued}.");
    }

    // ===== kleine Helfer =====

    private void TryPushPriceToLocalHud()
    {
        // Optional: HUD des lokalen Players updaten (Preis neben Gold-Kreis)
        var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (!localPlayer) return;

        var hud = localPlayer.GetComponentInChildren<WorldSpaceResourceHUD>(true);
        if (hud != null)
        {
            hud.SetChestPrice(goldCost);
            Log($"Client: HUD price set to {goldCost}.");
        }
    }
}
