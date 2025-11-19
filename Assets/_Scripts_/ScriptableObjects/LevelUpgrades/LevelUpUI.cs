using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class LevelUpUI : MonoBehaviour
{
    public static LevelUpUI Instance { get; private set; }

    // -------------------- Inspector --------------------
    [Header("Refs")]
    public GameObject panel;
    public CanvasGroup panelGroup;
    public LevelUpChoiceItem choiceA;
    public LevelUpChoiceItem choiceB;
    public LevelUpChoiceItem choiceC;
    public Button btnReroll;
    public Button btnBanish;
    public TextMeshProUGUI title;
    public TextMeshProUGUI subtitle;

    [Header("Stats Overview")]
    public GameObject statsPanel;

    public Transform statsContentStats;    // nur Core-Stats (HP, Armor, ...)

    public StatRow statRowPrefab;

    [Header("Behavior")]
    public float fadeTime = 0.12f;
    public bool lockCursor = true;
    public KeyCode pickKeyA = KeyCode.Alpha1;
    public KeyCode pickKeyB = KeyCode.Alpha2;
    public KeyCode pickKeyC = KeyCode.Alpha3;

    [Header("Slow Motion")]
    public bool enableSlowMo = true;
    [Range(0.05f, 1f)] public float slowMoScale = 0.2f;
    public float slowMoFadeIn = 0.12f;
    public float slowMoFadeOut = 0.2f;

    [Header("LevelUp Loop SFX")]
    public AudioSource levelUpLoopSource;   // eigene AudioSource fürs Loop
    public AudioClip levelUpLoopClip;       // optional

    [Header("SFX")]
    public AudioSource sfxSource;
    public AudioClip sfxPick;

    // -------------------- Internals --------------------
    private readonly Dictionary<UpgradeType, StatRow> _rows =
        new Dictionary<UpgradeType, StatRow>();

    private int[] _choices;
    private bool _open;
    private bool _inputLocked;
    private bool _picked;

    private Behaviour _localControlToLock;
    private PlayerUpgrades _upgrades;
    private PlayerWeapons _weapons; // nur noch für UpgradeDescription
    private bool _statsSubscribed;

    // Preview
    private UpgradeType? _previewType;
    private int? _previewStacks;

    private int _slowHandle;

    private Coroutine _bindCo;

    private bool HasThreeChoices => _choices != null && _choices.Length >= 3;

    // Konfiguration für Core-Stats (Name + UpgradeType)
    private static readonly (UpgradeType type, string label)[] CoreStats =
    {
        (UpgradeType.MaxHP,       "Max HP"),
        (UpgradeType.Armor,       "Armor"),
        (UpgradeType.Magnet,      "Magnet"),
        (UpgradeType.MoveSpeed,   "Move Speed"),
        (UpgradeType.Stamina,     "Stamina"),
        (UpgradeType.DropMoreXP,  "XP Drops"),
        (UpgradeType.DropMoreGold,"Gold Drops")
    };

    // -------------------- Unity --------------------
    private void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
        if (panelGroup) panelGroup.alpha = 0f;
    }

    private void Start()
    {
        if (statsPanel) statsPanel.SetActive(true);
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        if (_bindCo == null)
            _bindCo = StartCoroutine(BindAndSubscribeWhenReady());
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        _open = false;
        UnsubscribeStats();
        _previewType = null;
        _previewStacks = null;

        if (_bindCo != null)
        {
            StopCoroutine(_bindCo);
            _bindCo = null;
        }

        if (levelUpLoopSource) levelUpLoopSource.Stop();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeStats();
        if (levelUpLoopSource) levelUpLoopSource.Stop();
    }

    private void Update()
    {
        if (!_open || _picked || !HasThreeChoices) return;

        if (Input.GetKeyDown(pickKeyA)) OnPick(_choices[0]);
        if (Input.GetKeyDown(pickKeyB)) OnPick(_choices[1]);
        if (Input.GetKeyDown(pickKeyC)) OnPick(_choices[2]);
    }

    private void OnClientConnected(ulong _)
    {
        if (_bindCo == null)
            _bindCo = StartCoroutine(BindAndSubscribeWhenReady());
    }

    private IEnumerator BindAndSubscribeWhenReady()
    {
        while (isActiveAndEnabled && _upgrades == null)
        {
            _upgrades ??= FindLocalUpgrades();
            yield return null;
        }

        if (!isActiveAndEnabled)
        {
            _bindCo = null;
            yield break;
        }

        SubscribeStats();
        RefreshStats();

        _bindCo = null;
    }

    // -------------------- Public API --------------------
    public void Show(int[] choices, bool lockInput = true)
    {
        _choices       = choices;
        _picked        = false;
        _open          = true;
        _previewType   = null;
        _previewStacks = null;

        var up = FindLocalUpgradesCached();

        if (title)    title.text    = "UPGRADES";
        if (subtitle) subtitle.text = "Choose 1 of 3 upgrades";

        if (HasThreeChoices)
        {
            Func<string, string> L = null; // optional Localization-Wrapper

            choiceA.Bind(choices[0], UpgradeDescription(choices[0]), OnPick, up, L);
            choiceB.Bind(choices[1], UpgradeDescription(choices[1]), OnPick, up, L);
            choiceC.Bind(choices[2], UpgradeDescription(choices[2]), OnPick, up, L);

            choiceA.SetPreviewHook(this);
            choiceB.SetPreviewHook(this);
            choiceC.SetPreviewHook(this);
        }
        else
        {
            Debug.LogWarning("[LevelUpUI] Show() called ohne 3 choices.");
        }

        // SlowMo
        if (enableSlowMo && SlowMoManager.Instance != null && _slowHandle == 0)
            _slowHandle = SlowMoManager.Instance.BeginHold(slowMoScale, slowMoFadeIn);

        if (btnReroll) btnReroll.gameObject.SetActive(false);
        if (btnBanish) btnBanish.gameObject.SetActive(false);

        if (panel) panel.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeCanvas(panelGroup, 0f, 1f, fadeTime));

        _upgrades ??= FindLocalUpgrades();
        SubscribeStats();
        RefreshStats();

        if (lockInput) LockLocalInput(true);

        if (levelUpLoopSource)
        {
            if (levelUpLoopClip) levelUpLoopSource.clip = levelUpLoopClip;
            if (!levelUpLoopSource.isPlaying)
                levelUpLoopSource.Play();
        }
    }

    public void Hide(bool unlockInput = true)
    {
        _open = false;

        if (levelUpLoopSource && levelUpLoopSource.isPlaying)
            levelUpLoopSource.Stop();

        StopAllCoroutines();
        StartCoroutine(FadeOutThenDisable(unlockInput));
    }

    // -------------------- Preview-API (von Choice-Items aufgerufen) --------------------
    public void ShowPreviewForChoice(int choiceId)
    {
        int baseId = ChoiceCodec.BaseId(choiceId);

        // Mastery → kein Stat-Preview, kein Waffen-Highlight
        if (UpgradeRoller.IsMasteryBaseId(baseId))
        {
            _previewType   = null;
            _previewStacks = null;
            WeaponHudUI.Instance?.HighlightWeapon(null);
        }
        // Weapon → HUD-Slot highlighten, aber keine Stat-Preview
        else if (UpgradeRoller.IsWeaponBaseId(baseId))
        {
            _previewType   = null;
            _previewStacks = null;

            var pw  = _weapons ?? FindLocalWeapons();
            var def = pw != null ? UpgradeRoller.ResolveWeaponDef(pw, baseId) : null;
            WeaponHudUI.Instance?.HighlightWeapon(def);
        }
        // Stat → Stat-Preview, kein Waffen-Highlight
        else
        {
            _previewType   = UpgradeRoller.ResolveFromChoice(choiceId);
            _previewStacks = UpgradeRoller.StacksForChoice(choiceId);
            WeaponHudUI.Instance?.HighlightWeapon(null);
        }

        RefreshStats();
    }

    public void ClearPreview()
    {
        _previewType   = null;
        _previewStacks = null;
        WeaponHudUI.Instance?.HighlightWeapon(null);
        RefreshStats();
    }


    // -------------------- Intern: Picking --------------------
    private void OnPick(int encodedId)
    {
        if (_picked) return;
        _picked = true;

        if (sfxSource && sfxPick) sfxSource.PlayOneShot(sfxPick);

        if (choiceA) choiceA.SetInteractable(false);
        if (choiceB) choiceB.SetInteractable(false);
        if (choiceC) choiceC.SetInteractable(false);

        _previewType   = null;
        _previewStacks = null;

        var xp = FindLocalPlayerXP();
        if (xp != null)
        {
            xp.ChooseUpgradeServerRpc(encodedId);
        }
        else
        {
            var up = FindLocalUpgrades();
            if (up != null) up.GrantEncodedUpgradeServerRpc(encodedId);
        }

        RefreshStats();
        Hide(true);
    }

    // -------------------- Intern: Finder --------------------
    private PlayerXP FindLocalPlayerXP()
    {
        var nm = NetworkManager.Singleton;
        if (!nm || !nm.IsClient) return null;
        var local = nm.SpawnManager?.GetLocalPlayerObject();
        return local ? local.GetComponent<PlayerXP>() : null;
    }

    private PlayerUpgrades FindLocalUpgrades()
    {
        var nm = NetworkManager.Singleton;
        if (!nm || !nm.IsClient) return null;
        var local = nm.SpawnManager?.GetLocalPlayerObject();
        if (!local) return null;
        var up = local.GetComponent<PlayerUpgrades>();
        if (!up) up = local.GetComponentInChildren<PlayerUpgrades>(true);
        return up;
    }

    private PlayerWeapons FindLocalWeapons()
    {
        var nm = NetworkManager.Singleton;
        if (!nm || !nm.IsClient) return null;
        var local = nm.SpawnManager?.GetLocalPlayerObject();
        if (!local) return null;
        var pw = local.GetComponent<PlayerWeapons>();
        if (!pw) pw = local.GetComponentInChildren<PlayerWeapons>(true);
        return pw;
    }

    private PlayerUpgrades FindLocalUpgradesCached()
    {
        if (_upgrades == null) _upgrades = FindLocalUpgrades();
        return _upgrades;
    }

    // -------------------- StatRow Helpers --------------------
    private StatRow GetOrCreateRow(UpgradeType type, string defaultLabel)
    {
        if (_rows.TryGetValue(type, out var row) && row != null) return row;
        if (!statRowPrefab || !statsContentStats) return null;

        row = Instantiate(statRowPrefab, statsContentStats);
        row.name = $"Row_{type}";
        _rows[type] = row;
        return row;
    }

    // -------------------- Stats Sidebar --------------------
    private void RefreshStatsIfVisible()
    {
        if (statsPanel && statsPanel.activeInHierarchy)
            RefreshStats();
    }

    private void OnAnyStatChanged(int _, int __) => RefreshStatsIfVisible();

    private void RefreshStats()
    {
        if (_upgrades == null) _upgrades = FindLocalUpgrades();
        if (!_upgrades || statsContentStats == null)
            return;

        bool allowPreview = _open && !_picked;

        foreach (var (type, label) in CoreStats)
        {
            int   lvl   = _upgrades.GetLevel(type);
            int   max   = _upgrades.GetMaxLevel(type);
            float curr  = _upgrades.GetCurrentValue(type);

            var row = GetOrCreateRow(type, label);
            if (!row) continue;

            bool previewThis = allowPreview && _previewType.HasValue && _previewType.Value == type && lvl < max;
            string nextStr = null;

            if (previewThis)
            {
                int stacks = Mathf.Clamp(_previewStacks ?? 1, 1, max - lvl);
                float nextVal = _upgrades.GetCurrentValueAtLevel(type, lvl + stacks);
                nextStr = $"{FormatValue(type, nextVal)}   (Lv {lvl + stacks}/{max})";
            }

            float prog = (max > 0) ? (lvl / (float)max) : -1f;
            string valueText = $"{FormatValue(type, curr)}   {(lvl >= max ? "(MAX)" : $"(Lv {lvl}/{max})")}";

            row.Set(label, valueText, nextStr, prog);
        }
    }

    // Anzeige-Format analog zu PlayerUpgrades.GetCurrentDisplay
    private string FormatValue(UpgradeType type, float v)
    {
        switch (type)
        {
            case UpgradeType.MaxHP:        return $"{v:0.#} HP";
            case UpgradeType.Armor:        return $"{v:0.#} armor";
            case UpgradeType.Magnet:       return $"{v:0.00}×";
            case UpgradeType.MoveSpeed:    return $"{v:0.##} m/s";
            case UpgradeType.Stamina:      return $"{v:0.#} ST";
            case UpgradeType.DropMoreXP:   return $"{(v - 1f) * 100f:0.#}% XP";
            case UpgradeType.DropMoreGold: return $"{(v - 1f) * 100f:0.#}% Gold";
            default:                       return v.ToString("0.##");
        }
    }

    private void SubscribeStats()
    {
        if (_statsSubscribed) return;
        if (_upgrades == null) _upgrades = FindLocalUpgrades();
        if (_upgrades == null) return;

        _upgrades.MaxHPLevel.OnValueChanged        += OnAnyStatChanged;
        _upgrades.ArmorLevel.OnValueChanged        += OnAnyStatChanged;
        _upgrades.MagnetLevel.OnValueChanged       += OnAnyStatChanged;
        _upgrades.MoveSpeedLevel.OnValueChanged    += OnAnyStatChanged;
        _upgrades.StaminaLevel.OnValueChanged      += OnAnyStatChanged;
        _upgrades.DropMoreXPLevel.OnValueChanged   += OnAnyStatChanged;
        _upgrades.DropMoreGoldLevel.OnValueChanged += OnAnyStatChanged;

        _statsSubscribed = true;
    }

    private void UnsubscribeStats()
    {
        if (!_statsSubscribed || _upgrades == null) return;

        _upgrades.MaxHPLevel.OnValueChanged        -= OnAnyStatChanged;
        _upgrades.ArmorLevel.OnValueChanged        -= OnAnyStatChanged;
        _upgrades.MagnetLevel.OnValueChanged       -= OnAnyStatChanged;
        _upgrades.MoveSpeedLevel.OnValueChanged    -= OnAnyStatChanged;
        _upgrades.StaminaLevel.OnValueChanged      -= OnAnyStatChanged;
        _upgrades.DropMoreXPLevel.OnValueChanged   -= OnAnyStatChanged;
        _upgrades.DropMoreGoldLevel.OnValueChanged -= OnAnyStatChanged;

        _statsSubscribed = false;
    }

    // -------------------- FX / Input Lock --------------------
    private IEnumerator FadeCanvas(CanvasGroup cg, float a, float b, float time)
    {
        if (!cg) yield break;
        float e = 0f;
        cg.alpha = a;
        while (e < time)
        {
            e += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(a, b, e / Mathf.Max(0.0001f, time));
            yield return null;
        }
        cg.alpha = b;
    }

    private IEnumerator FadeOutThenDisable(bool unlock)
    {
        yield return FadeCanvas(panelGroup, panelGroup ? panelGroup.alpha : 1f, 0f, fadeTime);
        if (panel) panel.SetActive(false);
        if (unlock) LockLocalInput(false);

        if (_slowHandle != 0 && SlowMoManager.Instance != null)
        {
            SlowMoManager.Instance.EndHold(_slowHandle, slowMoFadeOut);
            _slowHandle = 0;
        }

        _previewType = null;
        _previewStacks = null;
    }

    private void LockLocalInput(bool state)
    {
        if (_inputLocked == state) return;
        _inputLocked = state;

        if (_localControlToLock == null)
        {
            var nm = NetworkManager.Singleton;
            var local = nm?.SpawnManager?.GetLocalPlayerObject();
            if (local)
            {
                _localControlToLock = local.GetComponent<PlayerMovement>();
            }
        }

        if (_localControlToLock) _localControlToLock.enabled = !state;

        if (lockCursor)
        {
            Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = state;
        }
    }

    // -------------------- Texte --------------------
    private string UpgradeDescription(int encodedId)
    {
        var up    = FindLocalUpgradesCached();
        int baseId = ChoiceCodec.BaseId(encodedId);

        // Mastery
        if (UpgradeRoller.IsMasteryBaseId(baseId) && up != null &&
            UpgradeRoller.TryResolveMasteryBaseId(up, baseId, out var mDef))
        {
            var rarity = ChoiceCodec.GetRarity(encodedId);
            int stacks = UpgradeRoller.StacksPerRarity.TryGetValue(rarity, out var s) ? s : 1;
            return $"{mDef.displayName} (+{stacks} Tier)";
        }

        // Waffen-Beschreibung (für Karten-Text, obwohl Waffen nicht mehr in der Sidebar angezeigt werden)
        if (UpgradeRoller.IsWeaponBaseId(baseId))
        {
            var pw  = _weapons ?? FindLocalWeapons();
            var def = pw != null ? UpgradeRoller.ResolveWeaponDef(pw, baseId) : null;
            string name = def != null ? def.displayName : "Weapon";

            int curLevel = 0;
            int maxLevel = def != null ? 1 + (def.steps?.Length ?? 0) : 1;

            if (pw != null && def != null)
            {
                if (def == pw.cannonDef)         curLevel = pw.cannonLevel.Value;
                else if (def == pw.blasterDef)   curLevel = pw.blasterLevel.Value;
                else if (def == pw.grenadeDef)   curLevel = pw.grenadeLevel.Value;
                else if (def == pw.lightningDef) curLevel = pw.lightningLevel.Value;
                else if (def == pw.orbitalDef)   curLevel = pw.orbitalLevel.Value;
            }

            if (curLevel == 0)
                return $"NEW WEAPON: {name}";

            string body = WeaponStepDescriber.DescribeStep(def, curLevel + 1, up);
            if (!string.IsNullOrEmpty(body))
                return body;

            return $"{name}: +1 level (Lv {curLevel + 1}/{maxLevel})";
        }

        // Stat-Upgrade
        var type       = UpgradeRoller.Resolve(baseId);
        int stacksStat = UpgradeRoller.StacksForChoice(encodedId);

        return type switch
        {
            UpgradeType.MaxHP        => $"+{up.maxHPPerLevel        * stacksStat:0.#} max HP",
            UpgradeType.Armor        => $"+{up.armorFlatPerLevel    * stacksStat:0.#} armor",
            UpgradeType.Magnet       => $"+{PctFromDamageMult(up.magnetRangeMultPerLevel, stacksStat):0.#}% magnet range",
            UpgradeType.MoveSpeed    => $"+{PctFromDamageMult(up.moveSpeedMultPerLevel,   stacksStat):0.#}% move speed",
            UpgradeType.Stamina      => $"+{up.staminaMaxPerLevel   * stacksStat:0.#} max stamina\n+{up.staminaRegenPerLevel * stacksStat:0.#} stamina/s regen",
            UpgradeType.DropMoreXP   => $"+{up.xpDropBonusPerLevel   * stacksStat * 100f:0.#}% XP drops",
            UpgradeType.DropMoreGold => $"+{up.goldDropBonusPerLevel * stacksStat * 100f:0.#}% Gold drops",
            _                        => "Upgrade"
        };
    }

    // Hilfsfunktionen für Prozentwerte
    private static float PctFromTimeMult(float timeMultPerLevel, int stacks)
    {
        float factor = Mathf.Pow(1f / Mathf.Max(0.0001f, timeMultPerLevel), Mathf.Max(1, stacks));
        return (factor - 1f) * 100f;
    }

    private static float PctFromDamageMult(float dmgMultPerLevel, int stacks)
    {
        float factor = Mathf.Pow(Mathf.Max(1.0001f, dmgMultPerLevel), Mathf.Max(1, stacks));
        return (factor - 1f) * 100f;
    }
}
