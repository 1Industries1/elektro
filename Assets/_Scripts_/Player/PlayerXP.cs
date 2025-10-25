using UnityEngine;
using Unity.Netcode;
using System;
using System.Linq;

[DisallowMultipleComponent]
public class PlayerXP : NetworkBehaviour
{
    [Header("Progression")]
    public int level = 1;
    public int xp = 0;
    public int xpToNext = 10;
    public int baseIncrease = 5;    // xpToNext += baseIncrease + level * perLevelScale
    public int perLevelScale = 5;

    [Header("State")]
    private bool _awaitingChoice = false;           // blockt weitere Offers bis gewählt
    private PlayerUpgrades _upgrades;

    // Speichert die zuletzt gerollten 3 (encodeten) Choices für diesen Spieler (Server-seitig).
    private int[] _lastChoicesEncoded = null;

    private void Awake()
    {
        _upgrades = GetComponent<PlayerUpgrades>();
    }

    // ===== Server: XP hinzufügen =====
    public void Server_AddXP(int amount)
    {
        if (!IsServer || amount <= 0) return;
        xp += amount;

        // HUD (Owner)
        XpUpdateOwnerClientRpc(level, xp, xpToNext, OwnerClientId);

        // Mehrere Stufen auf einmal möglich (z.B. großer Drop)
        while (xp >= xpToNext)
        {
            xp -= xpToNext;
            level++;
            xpToNext += baseIncrease + (level * perLevelScale);

            // Wenn bereits eine Auswahl offen ist, spare weitere Offers auf
            if (!_awaitingChoice)
                OfferUpgradeChoices();

            XpUpdateOwnerClientRpc(level, xp, xpToNext, OwnerClientId);
        }
    }

    // ===== Server: 3 Upgrade-Optionen anbieten (encodet mit Rarity) =====
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

    // ===== Client: UI öffnen =====
    [ClientRpc]
    private void OfferUpgradesClientRpc(int[] choices, ClientRpcParams _ = default)
    {
        // LevelUpUI zeigt bereits Label/Desc passend (Label nutzt Rarity-Farbe/★)
        LevelUpUI.Instance?.Show(choices, lockInput: true);
    }

    // ===== Server: Wahl entgegennehmen (encoded choiceId) =====
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
        if (_upgrades == null) { Debug.LogWarning("[PlayerXP] No PlayerUpgrades found"); ChoiceResultOwnerClientRpc(false, choiceId, OwnerClientId); _awaitingChoice = false; return; }

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

        int cur = _upgrades.GetLevel(type);
        int max = _upgrades.GetMaxLevel(type);
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

    // Vergibt N Level, ohne dass du zwingend deine PlayerUpgrades-Overload brauchst.
    private void ApplyLevelsSafely(PlayerUpgrades up, UpgradeType type, int levels)
    {
        // Wenn du eine Overload hast: up.PurchaseFreeLevel_Server(type, levels);
        // Hier fallback über Schleife:
        for (int i = 0; i < levels; i++)
            up.PurchaseFreeLevel_Server(type, levels);
    }

    // ===== Client: Ergebnis & UI schließen =====
    [ClientRpc]
    private void ChoiceResultOwnerClientRpc(bool ok, int choiceId, ulong forOwner, ClientRpcParams _ = default)
    {
        if (!(IsOwner && NetworkManager.Singleton.LocalClientId == forOwner)) return;
        Debug.Log($"[PlayerXP] Choice result (client): ok={ok} {UpgradeRoller.Label(choiceId)} (id={choiceId})");
        LevelUpUI.Instance?.Hide(unlockInput: true);
        // Optional: Toast/SFX hier
    }

    // ===== Client: XP HUD =====
    [ClientRpc]
    private void XpUpdateOwnerClientRpc(int lvl, int cur, int next, ulong forOwner, ClientRpcParams _ = default)
    {
        if (!(IsOwner && NetworkManager.Singleton.LocalClientId == forOwner)) return;
        PlayerHUD.Instance?.SetXP(lvl, cur, next); // optional
    }
}
