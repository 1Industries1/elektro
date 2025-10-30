using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class TreasureChest : NetworkBehaviour
{
    // ==========================
    // --- ChestInteraction-Teil
    // ==========================
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Image holdCircle;
    [SerializeField] private string openTrigger = "Open";

    [Header("Settings")]
    [Tooltip("Sekunden, die im Trigger gewartet werden müssen.")]
    [SerializeField] private float holdDuration = 3f;
    [Tooltip("Fortschritt fällt zurück, wenn Spieler den Trigger verlässt?")]
    [SerializeField] private bool resetOnExit = true;
    [Tooltip("Kann die Truhe nur einmal geöffnet werden?")]
    [SerializeField] private bool singleUse = true;
    [Tooltip("Zeit nach Öffnen, bevor erneut interagiert werden darf (falls singleUse = false).")]
    [SerializeField] private float reopenCooldown = 2f;
    [SerializeField] private string playerTag = "Player";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;  // Quelle am selben GameObject
    [SerializeField] private AudioClip holdLoopClip;   // Sound, der während des Haltens looped
    [SerializeField] private AudioClip openClip;       // Einmaliger Sound beim Öffnen
    [SerializeField][Range(0f, 1f)] private float holdLoopBaseVolume = 0.25f;
    [SerializeField] private bool modulateHoldByProgress = true; // Lautstärke/Pitch nach Fortschritt

    private readonly List<string> _lockedRewardWeaponIds = new();


    public NetworkVariable<bool> Opened = new(false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // nur Server: Cooldown für Reopen (wenn singleUse=false)
    private float _serverNextOpenTime = 0f;

    // --- Lock für das je Öffnung gewählte Profil (Server-Quelle der Wahrheit)
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
    public UnityEvent<float> OnProgress; // 0..1

    // intern
    private bool inRange;
    private bool opened;
    private float holdTimer;
    private float cooldownUntil;
    private int openTriggerHash;

    // wer steht gerade im Trigger
    private PlayerInventory recipient;

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

    private void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();

        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;

        openTriggerHash = Animator.StringToHash(openTrigger);

        if (holdCircle)
        {
            holdCircle.type = Image.Type.Filled;
            holdCircle.fillAmount = 0f;
            holdCircle.gameObject.SetActive(false);
        }

        if (audioSource == null && (holdLoopClip != null || openClip != null))
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsBlocked() || !other.CompareTag(playerTag)) return;

        inRange = true;

        // Versuche PlayerInventory zu merken (für Drops)
        if (other.TryGetComponent(out PlayerInventory p))
            recipient = p;

        if (holdCircle) holdCircle.gameObject.SetActive(true);

        TryStartHoldLoop();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        inRange = false;

        if (resetOnExit) holdTimer = 0f;
        UpdateUI();

        if (holdCircle) holdCircle.gameObject.SetActive(false);

        // Spieler verlässt Bereich -> Empfänger vergessen
        if (other.TryGetComponent(out PlayerInventory p) && p == recipient)
            recipient = null;

        StopHoldLoop();
    }

    private void Update()
    {
        if (!inRange || IsBlocked()) return;

        // Automatischer Hold-Progress
        holdTimer += Time.deltaTime;
        UpdateUI();

        if (holdTimer >= holdDuration)
        {
            SendOpenRequestToServer();
            holdTimer = 0f; // reset clientseitig
        }
    }

    private void UpdateUI()
    {
        float t = holdDuration <= 0f ? 1f : Mathf.Clamp01(holdTimer / Mathf.Max(0.0001f, holdDuration));
        if (holdCircle) holdCircle.fillAmount = t;
        OnProgress?.Invoke(t);

        // Audio-Feedback an Fortschritt koppeln
        if (modulateHoldByProgress && audioSource != null && audioSource.clip == holdLoopClip && audioSource.isPlaying)
        {
            audioSource.volume = holdLoopBaseVolume * Mathf.Lerp(0.6f, 1f, t);
            audioSource.pitch = Mathf.Lerp(0.9f, 1.1f, t);
        }
    }


    // Clientseitige Block-Sicht (Opened kommt vom Server)
    private bool IsBlockedClientView()
    {
        if (singleUse && Opened.Value) return true;
        if (!singleUse && Time.time < cooldownUntil) return true; // clientseitige Visual-Cooldown-Anzeige
        return false;
    }

    private void SendOpenRequestToServer()
    {
        // Versuche den PlayerInventory zu referenzieren (optional, Server validiert trotzdem)
        NetworkObjectReference invRef = default;
        if (recipient != null)
        {
            var no = recipient.GetComponent<NetworkObject>();
            if (no) invRef = no;
        }

        RequestOpenServerRpc(invRef);
    }


    [ServerRpc(RequireOwnership = false)]
    private void RequestOpenServerRpc(NetworkObjectReference openerInventoryRef, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        ulong sender = rpcParams.Receive.SenderClientId;

        if (singleUse && Opened.Value) return;
        if (!singleUse && Time.time < _serverNextOpenTime) return;

        PlayerInventory inv = null;
        if (openerInventoryRef.TryGet(out var obj) && obj)
            inv = obj.GetComponent<PlayerInventory>();
        if (inv == null)
        {
            var playerObj = NetworkManager.Singleton?.SpawnManager?.GetPlayerNetworkObject(sender);
            if (playerObj) inv = playerObj.GetComponent<PlayerInventory>() ?? playerObj.GetComponentInChildren<PlayerInventory>(true);
        }
        if (inv == null) return;

        if (singleUse) Opened.Value = true;
        _serverNextOpenTime = Time.time + reopenCooldown;

        // Profil locken
        _lockedProfile = GetNextDropProfile();
        _lockedProfileIndex = currentDropProfileIndex;

        // --- NEU: Rewards für Preview planen (nur Upgrade-Weapon-Icons)
        _lockedRewardWeaponIds.Clear();
        int rewardCount = GetRewardCount();
        if (possibleDrops.HasFlag(DropType.UpgradeWeapon) && rewardCount > 0)
        {
            var weapons = inv.GetComponent<PlayerWeapons>();
            if (weapons != null)
            {
                for (int i = 0; i < rewardCount; i++)
                {
                    var id = weapons.Server_PeekRandomUpgradeableId();
                    if (!string.IsNullOrEmpty(id))
                        _lockedRewardWeaponIds.Add(id);
                }
            }
        }

        // Preview an Owner-Client schicken (IDs -> Icons lokal auflösen)
        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } }
        };
        
        foreach (var id in _lockedRewardWeaponIds)
            Owner_PreviewIconClientRpc(id, target);

        // UI starten (jetzt sind die Icons bereits gemeldet)
        float revealDelay = 0f;
        if (_lockedProfile != null)
            revealDelay = Mathf.Max(0f, _lockedProfile.animDuration + _lockedProfile.delayTime);

        ClientStartOpenSequenceClientRpc(revealDelay, _lockedProfileIndex, target);
    }



    [ClientRpc(Delivery = RpcDelivery.Reliable)]
    private void ClientStartOpenSequenceClientRpc(float revealDelaySeconds, int profileIndex, ClientRpcParams clientRpcParams = default)
    {
        if (animator && openTriggerHash != 0) animator.SetTrigger(openTriggerHash);
        StopHoldLoop();
        if (audioSource && openClip) audioSource.PlayOneShot(openClip);

        if (singleUse) opened = true;
        cooldownUntil = Time.time + reopenCooldown;

        // *** NEU: UI mit exakt diesem Profil-Index aktivieren ***
        UIChestManager.Activate(this, profileIndex);

        // WICHTIG: Sequenz wirklich starten
        UIChestManager.instance?.Begin();

        if (chestSlowMo && SlowMoManager.Instance != null && _chestSlowHandle == 0)
        {
            if (_slowDelayCo != null) StopCoroutine(_slowDelayCo);
            _slowDelayCo = StartCoroutine(StartChestSlowMoDelayed());
        }

        OnOpened?.Invoke();
    }
    
    [ClientRpc(Delivery = RpcDelivery.Reliable)]
    private void Owner_PreviewIconClientRpc(string weaponId, ClientRpcParams clientRpcParams = default)
    {
        if (string.IsNullOrEmpty(weaponId)) return;

        // Lokalen PlayerWeapons holen (Owner-Client)
        var playerObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (!playerObj) return;

        var pw = playerObj.GetComponent<PlayerWeapons>() ?? playerObj.GetComponentInChildren<PlayerWeapons>(true);
        if (pw == null) return;

        WeaponDefinition def = null;
        if (pw.cannonDef  != null && pw.cannonDef.id  == weaponId) def = pw.cannonDef;
        else if (pw.blasterDef != null && pw.blasterDef.id == weaponId) def = pw.blasterDef;

        if (def != null && def.uiIcon != null)
            UIChestManager.NotifyItemReceived(def.uiIcon);
    }


    private IEnumerator RequestGrantAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));
        // Falls du lieber per Done-Button triggerst, rufe diese Zeile stattdessen in UIChestManager.CloseUI() auf.
        ConfirmOpenRevealDoneServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ConfirmOpenRevealDoneServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        ulong sender = rpcParams.Receive.SenderClientId;

        // Inventar des Öffners ermitteln (wie im ersten RPC)
        PlayerInventory inv = null;
        var playerObj = NetworkManager.Singleton?.SpawnManager?.GetPlayerNetworkObject(sender);
        if (playerObj) inv = playerObj.GetComponent<PlayerInventory>() ?? playerObj.GetComponentInChildren<PlayerInventory>(true);
        if (inv == null) return;

        GiveRewardsServer(inv, out _);
    }

    //[ClientRpc]
    //private void ClientOpenVfxAndUiClientRpc(int rewardCountIssued, ClientRpcParams clientRpcParams = default)
    //{
    //    if (animator && openTriggerHash != 0) animator.SetTrigger(openTriggerHash);
    //    StopHoldLoop();
    //    if (audioSource && openClip) audioSource.PlayOneShot(openClip);
//
    //    if (singleUse) opened = true;
    //    cooldownUntil = Time.time + reopenCooldown;
//
    //    UIChestManager.Activate(this);
//
    //    if (chestSlowMo && SlowMoManager.Instance != null && _chestSlowHandle == 0)
    //    {
    //        if (_slowDelayCo != null) StopCoroutine(_slowDelayCo);
    //        _slowDelayCo = StartCoroutine(StartChestSlowMoDelayed());
    //    }
//
    //    OnOpened?.Invoke();
    //}


    private bool IsBlocked()
    {
        if (singleUse && opened) return true;
        if (!singleUse && Time.time < cooldownUntil) return true;
        return false;
    }

    private IEnumerator StartChestSlowMoDelayed()
    {
        float d = Mathf.Max(0f, chestSlowDelay);
        if (d > 0f)
            yield return new WaitForSecondsRealtime(d);

        // Falls während des Delays schon geschlossen/abgebrochen wurde:
        if (!chestSlowMo || SlowMoManager.Instance == null || _chestSlowHandle != 0)
        {
            _slowDelayCo = null;
            yield break;
        }

        _chestSlowHandle = SlowMoManager.Instance.BeginHold(chestSlowScale, chestSlowFadeIn);
        _slowDelayCo = null;
    }

    // ==========================
    // --- Drop-Helfer
    // ==========================

    private void TryStartHoldLoop()
    {
        if (audioSource == null || holdLoopClip == null) return;

        // Nur starten, wenn noch nicht läuft bzw. anderer Clip aktiv ist
        if (!audioSource.isPlaying || audioSource.clip != holdLoopClip)
        {
            audioSource.clip = holdLoopClip;
            audioSource.loop = true;
            audioSource.volume = holdLoopBaseVolume;
            audioSource.pitch = 1f;
            audioSource.Play();
        }
    }

    private void StopHoldLoop()
    {
        if (audioSource == null) return;
        if (audioSource.clip == holdLoopClip)
            audioSource.Stop();
    }


    public TreasureChestDropProfile GetCurrentDropProfile()
    {
        if (dropProfiles == null || dropProfiles.Length == 0) return null;
        currentDropProfileIndex = Mathf.Clamp(currentDropProfileIndex, 0, dropProfiles.Length - 1);
        var p = dropProfiles[currentDropProfileIndex];
        Debug.Log($"[Chest] Using CURRENT drop profile index={currentDropProfileIndex}, name='{p.profileName}', noOfItems={p.noOfItems}");
        return p;
    }

    public TreasureChestDropProfile GetNextDropProfile()
    {
        if (dropProfiles == null || dropProfiles.Length == 0)
        {
            Debug.LogWarning("Drop profiles not set.");
            return null;
        }

        switch (dropCountType)
        {
            case DropCountType.sequential:
                currentDropProfileIndex = Mathf.Clamp(totalPickups, 0, dropProfiles.Length - 1);
                return dropProfiles[currentDropProfileIndex];

            case DropCountType.random:
                float playerLuck = 1f;
                if (recipient)
                {
                    // Optional: Luck aus Stats ziehen
                    // var stats = recipient.GetComponentInChildren<PlayerStats>();
                    // if (stats != null) playerLuck = Mathf.Max(0f, stats.Actual.luck);
                }

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
                foreach (var entry in weightedProfiles) totalWeight += entry.weight;
                if (totalWeight <= 0f) return GetCurrentDropProfile();

                float r = Random.Range(0f, totalWeight);
                float cumulative = 0f;
                foreach (var entry in weightedProfiles)
                {
                    cumulative += entry.weight;
                    if (r < cumulative)
                    {
                        currentDropProfileIndex = entry.index;
                        return entry.profile;
                    }
                }
                currentDropProfileIndex = weightedProfiles[^1].index;
                return weightedProfiles[^1].profile;
        }

        return GetCurrentDropProfile();
    }

    private int GetRewardCount()
    {
        // *** NEU: Falls Lock vorhanden, das verwenden ***
        var dp = _lockedProfile != null ? _lockedProfile : GetCurrentDropProfile();
        return dp ? Mathf.Max(1, dp.noOfItems) : 1;
    }

    // Stubs, bis Items angebunden sind
    private bool TryEvolve<T>(PlayerInventory inventory) where T : class { return false; }
    private bool TryUpgrade<T>(PlayerInventory inventory) where T : class { return false; }
    private bool TryGive<T>(PlayerInventory inventory) where T : class { return false; }

    private bool TryUpgradeOneWeapon(PlayerInventory inventory)
    {
        if (inventory == null) return false;

        // PlayerWeapons am gleichen Player finden
        var weapons = inventory.GetComponent<PlayerWeapons>();
        if (weapons == null) return false;

        // Server bevorzugt direkt
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            return weapons.Server_TryLevelUpRandomWeapon(out _);
        }
        else
        {
            // Fallback: Client fordert Server an
            weapons.RequestLevelUpFromChestServerRpc();
            return true; // UI-Icon kommt via Owner-ClientRpc vom Server
        }
    }

    // Server-only
    private void GiveRewardsServer(PlayerInventory inventory, out int issuedCount)
    {
        issuedCount = 0;
        if (!IsServer || inventory == null) return;

        int rewardCount = GetRewardCount();
        var weapons = inventory.GetComponent<PlayerWeapons>();

        for (int i = 0; i < rewardCount; i++)
        {
            issuedCount++;

            // 1) Geplantes Weapon-Upgrade deterministisch abarbeiten (ohne UI-Notify)
            if (possibleDrops.HasFlag(DropType.UpgradeWeapon) && weapons != null)
            {
                if (_lockedRewardWeaponIds.Count > 0)
                {
                    string id = _lockedRewardWeaponIds[0];
                    _lockedRewardWeaponIds.RemoveAt(0);

                    if (weapons.Server_LevelUpById(id, notifyOwner: false))
                        continue; // vergeben, nächstes Reward
                }

                // Fallback: wenn nichts geplant/fehlgeschlagen -> random (bestehendes Verhalten)
                if (TryUpgradeOneWeaponServer(inventory))
                    continue;
            }

            // 2) Rest wie gehabt
            if (possibleDrops.HasFlag(DropType.Evolution)      && TryEvolve<object>(inventory)) continue;
            if (possibleDrops.HasFlag(DropType.UpgradePassive) && TryUpgrade<object>(inventory)) continue;
            if (possibleDrops.HasFlag(DropType.NewWeapon)      && TryGive<object>(inventory))   continue;
            if (possibleDrops.HasFlag(DropType.NewPassive))          TryGive<object>(inventory);
        }

        // Aufräumen
        _lockedRewardWeaponIds.Clear();
    }


    private bool TryUpgradeOneWeaponServer(PlayerInventory inventory)
    {
        if (!IsServer || inventory == null) return false;
        var weapons = inventory.GetComponent<PlayerWeapons>();
        if (!weapons) return false;

        // hebt genau EINE vorhandene, upgradefähige Waffe um +1 an (zufällig)
        return weapons.Server_TryLevelUpRandomWeapon(out _);
    }




    public void OnChestUIClose()
    {
        if (_slowDelayCo != null)
        {
            StopCoroutine(_slowDelayCo);
            _slowDelayCo = null;
        }

        if (_chestSlowHandle != 0 && SlowMoManager.Instance != null)
        {
            SlowMoManager.Instance.EndHold(_chestSlowHandle, chestSlowFadeOut);
            _chestSlowHandle = 0;
        }
    }

}
