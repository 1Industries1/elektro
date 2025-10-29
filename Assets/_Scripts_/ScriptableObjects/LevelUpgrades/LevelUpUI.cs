using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Collections;
using UnityEngine.Audio;
using System;

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
    public Transform statsContent;
    public StatRow statRowPrefab;
    public DamageRow damageRowPrefab;

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

    private int _slowHandle = 0;

    [Header("SFX")]
    public AudioSource sfxSource;
    public AudioClip sfxPick;


    // -------------------- Internals --------------------
    private readonly System.Collections.Generic.Dictionary<UpgradeType, StatRow> _rows
    = new System.Collections.Generic.Dictionary<UpgradeType, StatRow>();

    private DamageRow _damageRow;


    private int[] _choices;
    private bool _open;
    private bool _inputLocked;
    private bool _picked;

    private Behaviour _localControlToLock;
    private PlayerUpgrades _upgrades;
    private PlayerWeapons _weapons;
    private bool _weaponsSubscribed;
    private readonly System.Collections.Generic.Dictionary<string, StatRow> _rowsCustom
        = new System.Collections.Generic.Dictionary<string, StatRow>();

    private bool _statsSubscribed;

    // Preview: Welcher UpgradeType soll +1 simuliert werden?
    private System.Nullable<UpgradeType> _previewType;

    // Merkt die Stacks der aktuell gehoverten Karte
    private int? _previewStacks;

    private bool HasThreeChoices => _choices != null && _choices.Length >= 3;

    // -------------------- Unity --------------------
    private void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
        if (panelGroup) panelGroup.alpha = 0f;

        //if (statsPanel) statsPanel.SetActive(false);
    }

    private void Start()
    {
        // Stats dauerhaft sichtbar & aktualisiert halten
        if (statsPanel) statsPanel.SetActive(true);
        FindLocalUpgradesCached();
        SubscribeStats();

        _weapons = FindLocalWeapons();
        SubscribeWeaponStats();

        RefreshStats();
    }

    private void Update()
    {
        if (!_open || _picked || !HasThreeChoices) return;

        if (Input.GetKeyDown(pickKeyA)) OnPick(_choices[0]);
        if (Input.GetKeyDown(pickKeyB)) OnPick(_choices[1]);
        if (Input.GetKeyDown(pickKeyC)) OnPick(_choices[2]);
    }

    private void OnDisable()
    {
        _open = false;
        UnsubscribeStats();
        UnsubscribeWeaponStats();
        _previewType = null;
    }

    private void SubscribeWeaponStats()
    {
        if (_weaponsSubscribed || _weapons == null) return;

        // NetworkVariables der Levels
        _weapons.cannonLevel.OnValueChanged  += OnAnyWeaponStatChanged;
        _weapons.blasterLevel.OnValueChanged += OnAnyWeaponStatChanged;

        // Runtime-Rebuild-Event (wenn Levels neu angewendet wurden)
        _weapons.RuntimesRebuilt += OnWeaponsRebuiltUI;

        _weaponsSubscribed = true;
    }

    private void UnsubscribeWeaponStats()
    {
        if (!_weaponsSubscribed || _weapons == null) return;

        _weapons.cannonLevel.OnValueChanged -= OnAnyWeaponStatChanged;
        _weapons.blasterLevel.OnValueChanged -= OnAnyWeaponStatChanged;
        _weapons.RuntimesRebuilt -= OnWeaponsRebuiltUI;

        _weaponsSubscribed = false;
    }
    
    private void RefreshStatsIfVisible()
    {
        // refresht auch außerhalb des LevelUp-Overlays,
        // aber nur wenn die Sidebar wirklich sichtbar ist
        if (statsPanel && statsPanel.activeInHierarchy)
            RefreshStats();
    }

    private void OnWeaponsRebuiltUI() => RefreshStatsIfVisible();

    private void OnAnyWeaponStatChanged(int _, int __) => RefreshStatsIfVisible();

    private void OnAnyStatChanged(int _, int __) => RefreshStatsIfVisible();


    // -------------------- Public API --------------------
    public void Show(int[] choices, bool lockInput = true)
    {
        _choices = choices;
        _picked = false;
        _open = true;
        _previewType = null;
        _previewStacks = null;
        FindLocalUpgradesCached();

        if (title) title.text = "UPGRADES";
        if (subtitle) subtitle.text = "Choose 1 of 3 upgrades";

        if (HasThreeChoices)
        {
            Func<string, string> L = null; // oder dein Localization-Wrapper

            choiceA.Bind(choices[0], UpgradeDescription(choices[0]), OnPick, L);
            choiceB.Bind(choices[1], UpgradeDescription(choices[1]), OnPick, L);
            choiceC.Bind(choices[2], UpgradeDescription(choices[2]), OnPick, L);

            choiceA.SetPreviewHook(this);
            choiceB.SetPreviewHook(this);
            choiceC.SetPreviewHook(this);
        }
        else
        {
            Debug.LogWarning("[LevelUpUI] Show() called without 3 choices.");
        }

        // SlowMo
        if (enableSlowMo && SlowMoManager.Instance != null && _slowHandle == 0)
            _slowHandle = SlowMoManager.Instance.BeginHold(slowMoScale, slowMoFadeIn);

        if (btnReroll) btnReroll.gameObject.SetActive(false);
        if (btnBanish) btnBanish.gameObject.SetActive(false);

        if (panel) panel.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeCanvas(panelGroup, 0f, 1f, fadeTime));

        RefreshStats();
        _weapons ??= FindLocalWeapons();
        SubscribeWeaponStats();
    }

    public void Hide(bool unlockInput = true)
    {
        _open = false;
        StopAllCoroutines();
        StartCoroutine(FadeOutThenDisable(unlockInput));
    }

    // -------------------- Preview-API (von Choice-Items aufgerufen) --------------------
    public void ShowPreviewForChoice(int choiceId)
    {
        _previewType = UpgradeRoller.ResolveFromChoice(choiceId);
        _previewStacks = UpgradeRoller.StacksForChoice(choiceId);
        RefreshStats();
    }

    public void ClearPreview()
    {
        _previewType = null;
        _previewStacks = null;
        RefreshStats();
    }

    private StatRow GetOrCreateRow(UpgradeType type, string defaultLabel)
    {
        if (_rows.TryGetValue(type, out var row) && row != null) return row;
        if (!statRowPrefab || !statsContent) return null;
        row = Instantiate(statRowPrefab, statsContent);
        // Optional: Name fürs Hierarchy-Aufräumen
        row.name = $"Row_{type}";
        _rows[type] = row;
        return row;
    }

    private DamageRow GetOrCreateDamageRow()
    {
        if (_damageRow && _damageRow.gameObject) return _damageRow;
        if (!damageRowPrefab || !statsContent) return null;
        _damageRow = Instantiate(damageRowPrefab, statsContent);
        _damageRow.name = "Row_Damage";
        return _damageRow;
    }


    // -------------------- Intern --------------------
    private void OnPick(int encodedId)
    {
        if (_picked) return;
        _picked = true;

        if (sfxSource && sfxPick) sfxSource.PlayOneShot(sfxPick);

        var type = UpgradeRoller.ResolveFromChoice(encodedId);
        int stacks = UpgradeRoller.StacksForChoice(encodedId);

        if (choiceA) choiceA.SetInteractable(false);
        if (choiceB) choiceB.SetInteractable(false);
        if (choiceC) choiceC.SetInteractable(false);

        // Wichtig: Preview sofort abschalten, damit keine "Next"-Werte mehr angezeigt werden.
        _previewType = null;
        _previewStacks = null;

        var xp = FindLocalPlayerXP();
        if (xp != null)
        {
            // TODO: Deine PlayerXP sollte eine Methode annehmen, die encodedId (oder type + stacks) zum Server sendet.
            xp.ChooseUpgradeServerRpc(encodedId);
            RefreshStats();
        }
        else
        {
            var up = FindLocalUpgrades();
            if (up != null) up.GrantUpgradeServerRpc(type, stacks);
            Hide(true);
        }
    }

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

    private StatRow GetOrCreateCustomRow(string key, string defaultLabel)
    {
        if (_rowsCustom.TryGetValue(key, out var row) && row) return row;
        if (!statRowPrefab || !statsContent) return null;
        row = Instantiate(statRowPrefab, statsContent);
        row.name = key;
        _rowsCustom[key] = row;
        return row;
    }

    // -------------------- Stats Sidebar --------------------
    private void RefreshStats()
    {
        if (_upgrades == null) _upgrades = FindLocalUpgrades();
        if (!_upgrades || !statsContent) return;

        bool allowPreview = _open && !_picked;

        // Helper für common pattern
        void FillBasic(UpgradeType type, string uiName, float currVal, int lvl, int max, Func<int, float> predictAtLevel, Func<float, string> fmt)
        {
            var row = GetOrCreateRow(type, uiName);
            if (!row) return;

            bool previewThis = _previewType.HasValue && _previewType.Value == type;
            string nextStr = null;

            if (previewThis && lvl < max)
            {
                int stacks = Mathf.Clamp(_previewStacks ?? 1, 1, max - lvl);
                float nextVal = _upgrades.GetCurrentValueAtLevel(type, lvl + stacks);
                nextStr = fmt(nextVal) + $"   (Lv {lvl + stacks}/{max})";
            }

            float prog = (max > 0) ? (lvl / (float)max) : -1f;

            row.Set(
                uiName,
                $"{fmt(currVal)}   {(lvl >= max ? "(MAX)" : $"(Lv {lvl}/{max})")}",
                nextStr,
                prog
            );
        }

        // --- Max HP ---
        {
            int lvl = _upgrades.GetLevel(UpgradeType.MaxHP);
            int max = _upgrades.GetMaxLevel(UpgradeType.MaxHP);
            float curr = _upgrades.GetCurrentValue(UpgradeType.MaxHP);
            FillBasic(UpgradeType.MaxHP, "Max HP", curr, lvl, max,
                      l => _upgrades.GetCurrentValueAtLevel(UpgradeType.MaxHP, l),
                      v => FormatValue(UpgradeType.MaxHP, v));
        }

        // --- Grenade Salvo ---
        {
            int lvl = _upgrades.GetLevel(UpgradeType.GrenadeSalvo);
            int max = _upgrades.GetMaxLevel(UpgradeType.GrenadeSalvo);
            float curr = _upgrades.GetCurrentValue(UpgradeType.GrenadeSalvo);
            FillBasic(UpgradeType.GrenadeSalvo, "GL Salvo", curr, lvl, max,
                      l => _upgrades.GetCurrentValueAtLevel(UpgradeType.GrenadeSalvo, l),
                      v => FormatValue(UpgradeType.GrenadeSalvo, v));
        }

        // --- Magnet ---
        {
            int lvl = _upgrades.GetLevel(UpgradeType.Magnet);
            int max = _upgrades.GetMaxLevel(UpgradeType.Magnet);
            float curr = _upgrades.GetCurrentValue(UpgradeType.Magnet);
            var row = GetOrCreateRow(UpgradeType.Magnet, "Magnet");
            if (row)
            {
                string nextStr = null;
                if (allowPreview && _previewType.HasValue && _previewType.Value == UpgradeType.Magnet && lvl < max)
                {
                    int stacks = Mathf.Clamp(_previewStacks ?? 1, 1, max - lvl);
                    float nextVal = _upgrades.GetCurrentValueAtLevel(UpgradeType.Magnet, lvl + stacks);
                    nextStr = $"{FormatValue(UpgradeType.Magnet, nextVal)}   (Lv {lvl + stacks}/{max})";
                }
                float prog = (max > 0) ? (lvl / (float)max) : -1f;
                row.Set("Magnet",
                    $"{FormatValue(UpgradeType.Magnet, curr)}   {(lvl >= max ? "(MAX)" : $"(Lv {lvl}/{max})")}",
                    nextStr, prog);
            }
        }

        // --- Move Speed ---
        {
            int lvl = _upgrades.GetLevel(UpgradeType.MoveSpeed);
            int max = _upgrades.GetMaxLevel(UpgradeType.MoveSpeed);
            float curr = _upgrades.GetCurrentValue(UpgradeType.MoveSpeed);
            var row = GetOrCreateRow(UpgradeType.MoveSpeed, "Move Speed");
            if (row)
            {
                string nextStr = null;
                if (allowPreview && _previewType.HasValue && _previewType.Value == UpgradeType.MoveSpeed && lvl < max)
                {
                    int stacks = Mathf.Clamp(_previewStacks ?? 1, 1, max - lvl);
                    float nextVal = _upgrades.GetCurrentValueAtLevel(UpgradeType.MoveSpeed, lvl + stacks);
                    nextStr = $"{FormatValue(UpgradeType.MoveSpeed, nextVal)}   (Lv {lvl + stacks}/{max})";
                }
                float prog = (max > 0) ? (lvl / (float)max) : -1f;
                row.Set("Move Speed",
                    $"{FormatValue(UpgradeType.MoveSpeed, curr)}   {(lvl >= max ? "(MAX)" : $"(Lv {lvl}/{max})")}",
                    nextStr, prog);
            }
        }

        // --- WEAPON LEVEL ROWS (Cannon / Blaster) ---
        _weapons ??= FindLocalWeapons();
        if (_weapons != null)
        {
            // Cannon
            {
                var def = _weapons.cannonDef;
                int lvl = _weapons.cannonLevel.Value;
                int max = 1 + (def?.steps?.Length ?? 0);
                var row = GetOrCreateCustomRow("Weapon_Cannon", def ? def.displayName : "Cannon");
                if (row)
                {
                    string label = def ? def.displayName : "Cannon";
                    // Optional: DPS/FireRate aus Runtime (falls vorhanden)
                    var rt = _weapons.CannonRuntime;
                    string extra = rt != null
                        ? $"DPS≈ {(rt.damagePerShot * rt.shotsPerSecond):0.#}  |  {rt.shotsPerSecond:0.##}/s"
                        : null;

                    row.Set(
                        label,
                        $"Lv {lvl}/{max}" + (string.IsNullOrEmpty(extra) ? "" : $"   ({extra})"),
                        null,
                        max > 0 ? (lvl / (float)max) : -1f
                    );
                }
            }

            // Blaster
            {
                var def = _weapons.blasterDef;
                int lvl = _weapons.blasterLevel.Value;
                int max = 1 + (def?.steps?.Length ?? 0);
                var row = GetOrCreateCustomRow("Weapon_Blaster", def ? def.displayName : "Blaster");
                if (row)
                {
                    string label = def ? def.displayName : "Blaster";
                    var rt = _weapons.BlasterRuntime;
                    string extra = rt != null
                        ? $"DPS≈ {(rt.damagePerShot * rt.shotsPerSecond):0.#}  |  {rt.shotsPerSecond:0.##}/s"
                        : null;

                    row.Set(
                        label,
                        $"Lv {lvl}/{max}" + (string.IsNullOrEmpty(extra) ? "" : $"   ({extra})"),
                        null,
                        max > 0 ? (lvl / (float)max) : -1f
                    );
                }
            }
        }

        
    }





    private float PredictNextValueWithStacks(UpgradeType type, int stacks)
    {
        // Wir rechnen vom aktuellen Effektivwert aus.
        float curr = _upgrades.GetCurrentValue(type);
        switch (type)
        {
            case UpgradeType.MaxHP: return Mathf.Max(1f, curr + stacks * _upgrades.maxHPPerLevel);
            case UpgradeType.GrenadeSalvo: return Mathf.Max(1f, curr + PlayerUpgrades.GrenadePerLevel * stacks);
            default: return curr;
        }
    }


    // Anzeige-Format analog zu PlayerUpgrades.GetCurrentDisplay
    private string FormatValue(UpgradeType type, float v)
    {
        switch (type)
        {
            case UpgradeType.MaxHP: return $"{v:0.#} HP";
            case UpgradeType.GrenadeSalvo: return $"{v:0}×";
            case UpgradeType.Magnet: return $"{v:0.00}×";
            case UpgradeType.MoveSpeed: return $"{v:0.##} m/s";
            default: return v.ToString("0.##");
        }
    }

    private void SubscribeStats()
    {
        if (_statsSubscribed) return;
        if (_upgrades == null) _upgrades = FindLocalUpgrades();
        if (_upgrades == null) return;

        _upgrades.MaxHPLevel.OnValueChanged        += OnAnyStatChanged;
        _upgrades.GrenadeSalvoLevel.OnValueChanged += OnAnyStatChanged;
        _upgrades.MagnetLevel.OnValueChanged       += OnAnyStatChanged;
        _upgrades.MoveSpeedLevel.OnValueChanged    += OnAnyStatChanged;
        _statsSubscribed = true;
    }

    private void UnsubscribeStats()
    {
        if (!_statsSubscribed || _upgrades == null) return;

        _upgrades.MaxHPLevel.OnValueChanged        -= OnAnyStatChanged;
        _upgrades.GrenadeSalvoLevel.OnValueChanged -= OnAnyStatChanged;
        _upgrades.MagnetLevel.OnValueChanged       -= OnAnyStatChanged;
        _upgrades.MoveSpeedLevel.OnValueChanged    -= OnAnyStatChanged;
        _statsSubscribed = false;
    }

    // -------------------- FX / Input Lock --------------------

    private IEnumerator FadeCanvas(CanvasGroup cg, float a, float b, float type)
    {
        if (!cg) yield break;
        float e = 0f;
        cg.alpha = a;
        while (e < type)
        {
            e += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(a, b, e / Mathf.Max(0.0001f, type));
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
                _localControlToLock = local.GetComponent<PlayerMovement>(); // dein Movement/Input-Script
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
        // Stelle sicher, dass wir _upgrades haben
        var up = FindLocalUpgradesCached();

        var type = UpgradeRoller.ResolveFromChoice(encodedId);
        int stacks = UpgradeRoller.StacksForChoice(encodedId);
        string tag = stacks switch { 1 => "(Common)", 2 => "(Rare)", 3 => "(Epic)", 4 => "(Legendary)", _ => "" };

        // Falls kein PlayerUpgrades gefunden wurde (Edge-Case, z.B. sehr frühe UI-Init),
        // liefere eine neutrale Fallback-Beschreibung.
        if (up == null)
        {
            return type switch
            {
                UpgradeType.MaxHP => $"+{15f * stacks:0.#} max HP {tag}",
                UpgradeType.GrenadeSalvo => $"+{PlayerUpgrades.GrenadePerLevel * stacks} bullets per salvo {tag}",
                _ => $"Upgrade {tag}"
            };
        }

        // Dynamisch aus den tatsächlich eingestellten Werten
        return type switch
        {
            UpgradeType.MaxHP => $"+{up.maxHPPerLevel * stacks:0.#} max HP",
            UpgradeType.GrenadeSalvo => $"+{PlayerUpgrades.GrenadePerLevel * stacks} bullets per salvo",
            UpgradeType.Magnet => $"+{PctFromDamageMult(up.magnetRangeMultPerLevel, stacks):0.#}% magnet range",
            UpgradeType.MoveSpeed => $"+{PctFromDamageMult(up.moveSpeedMultPerLevel, stacks):0.#}% move speed",
            _ => $"Upgrade"
        };
    }

    // Zeit/Schuss-Multiplikator → Feuerraten-Zuwachs (in %)
    private static float PctFromTimeMult(float timeMultPerLevel, int stacks)
    {
        // fireRate(sec/shot) *= timeMult^stacks  ==> shots/sec *= (1/timeMult)^stacks
        float factor = Mathf.Pow(1f / Mathf.Max(0.0001f, timeMultPerLevel), Mathf.Max(1, stacks));
        return (factor - 1f) * 100f;
    }

    // Damage-Multiplikator → % Zuwachs
    private static float PctFromDamageMult(float dmgMultPerLevel, int stacks)
    {
        float factor = Mathf.Pow(Mathf.Max(1.0001f, dmgMultPerLevel), Mathf.Max(1, stacks));
        return (factor - 1f) * 100f;
    }



    private PlayerUpgrades FindLocalUpgradesCached()
    {
        if (_upgrades == null) _upgrades = FindLocalUpgrades();
        return _upgrades;
    }

    private void SubscribeStatsIfPossible()
    {
        if (!_statsSubscribed)
        {
            FindLocalUpgradesCached();
            SubscribeStats();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeWeaponStats();
    }
}
