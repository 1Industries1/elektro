using UnityEngine;
using Unity.Netcode;
using System;
using System.Linq;

[DisallowMultipleComponent]
public class PlayerXP : NetworkBehaviour
{
    [Header("Progression")]
    [Tooltip("Aktuelles Spielerlevel (startet bei 1).")]
    public int level = 1;

    [Tooltip("Gesammelte XP im aktuellen Level.")]
    public int xp = 0;

    [Tooltip("Startkosten für Level 1 → 2.")]
    public int baseCost = 10;

    [Tooltip("Multiplikator pro Level (z. B. 1.12 = +12 % je Level).")]
    [Range(1.05f, 1.25f)]
    public float costMult = 1.12f;

    [SerializeField, Tooltip("XP, die zum nächsten Level benötigt werden (readonly).")]
    private int _xpToNext;

    /// <summary>Öffentlicher Readonly-Zugriff (z. B. für HUD/Debug).</summary>
    public int xpToNext => _xpToNext;

    [Header("State")]
    [Tooltip("Blockt weitere Upgrade-Angebote bis eine Wahl getroffen wurde.")]
    private bool _awaitingChoice = false;

    private PlayerUpgrades _upgrades;

    // Speichert die zuletzt gerollten 3 (encodeten) Choices für diesen Spieler (Server-seitig).
    private int[] _lastChoicesEncoded = null;

    // ======================== Lifecycle ========================

    private void Awake()
    {
        _upgrades = GetComponent<PlayerUpgrades>();
        RecalcCost(); // Initialisieren
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Editor-Schutz + Live-Vorschau im Inspector
        costMult = Mathf.Clamp(costMult, 1.01f, 2.0f);
        baseCost = Mathf.Max(1, baseCost);
        level = Mathf.Max(1, level);

        RecalcCost();
    }
#endif

    /// <summary>
    /// Berechnet die Kosten für das nächste Level: baseCost * costMult^(level-1)
    /// </summary>
    private void RecalcCost()
    {
        double pow = Math.Pow(costMult, Math.Max(0, level - 1));
        _xpToNext = Mathf.Max(1, Mathf.RoundToInt((float)(baseCost * pow)));
    }

    // ======================== XP / Level ========================

    /// <summary>
    /// Server: XP hinzufügen. Kann mehrere Levelups auf einmal auslösen.
    /// </summary>
    public void Server_AddXP(int amount)
    {
        if (!IsServer || amount <= 0) return;

        xp += amount;

        // HUD (Owner)
        XpUpdateOwnerClientRpc(level, xp, xpToNext, OwnerClientId);

        // Mehrere Stufen auf einmal möglich (z. B. großer XP-Drop)
        while (xp >= xpToNext)
        {
            xp -= xpToNext;
            level++;

            RecalcCost();

            // Wenn bereits eine Auswahl offen ist, spare weitere Offers auf
            if (!_awaitingChoice)
                OfferUpgradeChoices();

            XpUpdateOwnerClientRpc(level, xp, xpToNext, OwnerClientId);
        }
    }

    // ======================== Upgrades: Angebot ========================

    /// <summary>
    /// Server: 3 Upgrade-Optionen anbieten (encodet mit Rarity).
    /// </summary>
    private void OfferUpgradeChoices()
    {
        if (!IsServer || _awaitingChoice) return;
        if (_upgrades == null) _upgrades = GetComponent<PlayerUpgrades>();
        if (_upgrades == null) return;

        // Encodete 3 Optionen bestimmen (Rarity/Stacks eingerechnet)
        int[] choices = UpgradeRoller.Roll3Valid(_upgrades);

        _awaitingChoice = true;
        _lastChoicesEncoded = choices;

        var p = new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        OfferUpgradesClientRpc(choices, p);
    }

    // ======================== Client: UI öffnen ========================

    [ClientRpc]
    private void OfferUpgradesClientRpc(int[] choices, ClientRpcParams _ = default)
    {
        // LevelUpUI zeigt bereits Label/Desc passend (Label nutzt Rarity-Farbe/★)
        LevelUpUI.Instance?.Show(choices, lockInput: true);
    }

    // ======================== Server: Wahl entgegennehmen ========================

    [ServerRpc]
    public void ChooseUpgradeServerRpc(int choiceId, ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        Debug.Log($"[PlayerXP] RPC ChooseUpgrade from {sender}, choiceId={choiceId} ({UpgradeRoller.Label(choiceId)})");

        // Ownership prüfen
        if (sender != OwnerClientId) { Debug.LogWarning("[PlayerXP] Reject: not owner"); return; }

        if (!_awaitingChoice)
        {
            Debug.LogWarning("[PlayerXP] Reject: no choice pending");
            ChoiceResultOwnerClientRpc(false, choiceId, OwnerClientId);
            return;
        }

        if (_upgrades == null) _upgrades = GetComponent<PlayerUpgrades>();
        if (_upgrades == null)
        {
            Debug.LogWarning("[PlayerXP] No PlayerUpgrades found");
            ChoiceResultOwnerClientRpc(false, choiceId, OwnerClientId);
            _awaitingChoice = false;
            return;
        }

        // Muss eine der zuletzt angebotenen Karten sein
        if (_lastChoicesEncoded == null || Array.IndexOf(_lastChoicesEncoded, choiceId) < 0)
        {
            Debug.LogWarning("[PlayerXP] Reject: choice not in last roll");
            ChoiceResultOwnerClientRpc(false, choiceId, OwnerClientId);
            _awaitingChoice = false;
            _lastChoicesEncoded = null;
            return;
        }

        // Dekodieren: Typ + Stacks (aus Rarity)
        var type   = UpgradeRoller.ResolveFromChoice(choiceId);
        int stacks = UpgradeRoller.StacksForChoice(choiceId);

        int cur  = _upgrades.GetLevel(type);
        int max  = _upgrades.GetMaxLevel(type);
        int room = Mathf.Max(0, max - cur);

        if (room <= 0)
        {
            Debug.Log($"[PlayerXP] Choice capped → {type} cur={cur}/max={max} (ignored)");
            ChoiceResultOwnerClientRpc(false, choiceId, OwnerClientId);
            _awaitingChoice = false;
            _lastChoicesEncoded = null;
            return;
        }

        int give = Mathf.Clamp(stacks, 1, room);

        // Mehrere Level vergeben (Server-seitig, sicher)
        ApplyLevelsSafely(_upgrades, type, give);

        int after = _upgrades.GetLevel(type);
        Debug.Log($"[PlayerXP] UPGRADE APPLIED → {type} {cur} → {after} (+{give})");

        ChoiceResultOwnerClientRpc(true, choiceId, OwnerClientId);
        _awaitingChoice = false;
        _lastChoicesEncoded = null;

        // Falls durch XP-Overflow noch was pending ist, sofort die nächsten Choices anbieten
        if (xp >= xpToNext) OfferUpgradeChoices();
    }

    /// <summary>
    /// Vergibt N Level, ohne dass du zwingend deine PlayerUpgrades-Overload brauchst.
    /// FIX: pro Iteration 1 Level, nicht 'levels'.
    /// </summary>
    private void ApplyLevelsSafely(PlayerUpgrades up, UpgradeType type, int levels)
    {
        for (int i = 0; i < levels; i++)
            up.PurchaseFreeLevel_Server(type, 1); // 1, nicht 'levels'
    }

    // ======================== Client: Ergebnis & HUD ========================

    [ClientRpc]
    private void ChoiceResultOwnerClientRpc(bool ok, int choiceId, ulong forOwner, ClientRpcParams _ = default)
    {
        if (!(IsOwner && NetworkManager.Singleton.LocalClientId == forOwner)) return;
        Debug.Log($"[PlayerXP] Choice result (client): ok={ok} {UpgradeRoller.Label(choiceId)} (id={choiceId})");
        LevelUpUI.Instance?.Hide(unlockInput: true);
        // Optional: Toast/SFX hier
    }

    [ClientRpc]
    private void XpUpdateOwnerClientRpc(int lvl, int cur, int next, ulong forOwner, ClientRpcParams _ = default)
    {
        if (!(IsOwner && NetworkManager.Singleton.LocalClientId == forOwner)) return;
        PlayerHUD.Instance?.SetXP(lvl, cur, next); // optional
    }

    // ======================== Utility (optional) ========================

    /// <summary>
    /// Optionaler Soft-Cap: ab Lvl 30 wird's leicht teurer.
    /// Aufruf bei Bedarf z. B. im LevelUp-Loop nach level++:
    /// if (level >= 30) BumpSoftCap(0.02f);
    /// </summary>
    private void BumpSoftCap(float addMult = 0.02f)
    {
        costMult = Mathf.Max(1.0f, costMult + Mathf.Abs(addMult));
        RecalcCost();
    }
}
