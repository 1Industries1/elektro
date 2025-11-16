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

    // Container / Header für bessere Übersicht
    public TextMeshProUGUI statsHeader;
    public TextMeshProUGUI weaponsHeader;

    public Transform statsContentStats;    // nur Core-Stats (HP, Armor, ...)
    public Transform statsContentWeapons;  // Waffen + DamageRow

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

    [Header("LevelUp Loop SFX")]
    public AudioSource levelUpLoopSource;   // eigene AudioSource fürs Loop
    public AudioClip levelUpLoopClip;       // optional, sonst Clip direkt auf Source im Inspector setzen
    
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
    }

    private void Start()
    {
        if (statsPanel) statsPanel.SetActive(true);
    }

    private Coroutine _bindCo;

    private IEnumerator BindAndSubscribeWhenReady()
    {
        // Warten bis Local Player vorhanden ist UND wir beide Komponenten finden
        while (_upgrades == null || _weapons == null)
        {
            _upgrades ??= FindLocalUpgrades();
            _weapons ??= FindLocalWeapons();
            yield return null; // nächste Frame abwarten
        }

        // Jetzt sicher abonnieren (idempotent durch Guards in den Methoden)
        SubscribeStats();
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
        UnsubscribeWeaponStats();
        _previewType = null;
        if (_bindCo != null) { StopCoroutine(_bindCo); _bindCo = null; }

        if (levelUpLoopSource) levelUpLoopSource.Stop();
    }

    private void OnClientConnected(ulong _)
    {
        // bei Szene-Wechseln/Respawns: erneut binden
        if (_bindCo == null)
            _bindCo = StartCoroutine(BindAndSubscribeWhenReady());
    }

    private void SubscribeWeaponStats()
    {
        if (_weapons == null || _weaponsSubscribed) return;

        _weapons.cannonLevel.OnValueChanged += OnAnyWeaponStatChanged;
        _weapons.blasterLevel.OnValueChanged += OnAnyWeaponStatChanged;
        _weapons.grenadeLevel.OnValueChanged += OnAnyWeaponStatChanged;
        _weapons.lightningLevel.OnValueChanged += OnAnyWeaponStatChanged;
        _weapons.orbitalLevel.OnValueChanged += OnAnyWeaponStatChanged;
        _weapons.RuntimesRebuilt += OnWeaponsRebuiltUI;

        _weaponsSubscribed = true;
    }


    private void UnsubscribeWeaponStats()
    {
        if (!_weaponsSubscribed || _weapons == null) return;

        _weapons.cannonLevel.OnValueChanged -= OnAnyWeaponStatChanged;
        _weapons.blasterLevel.OnValueChanged -= OnAnyWeaponStatChanged;
        _weapons.grenadeLevel.OnValueChanged -= OnAnyWeaponStatChanged;
        _weapons.lightningLevel.OnValueChanged -= OnAnyWeaponStatChanged;
        _weapons.orbitalLevel.OnValueChanged -= OnAnyWeaponStatChanged;

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
            Func<string, string> L = null; // dein Localization-Wrapper, falls vorhanden

            choiceA.Bind(choices[0], UpgradeDescription(choices[0]), OnPick, up, L);
            choiceB.Bind(choices[1], UpgradeDescription(choices[1]), OnPick, up, L);
            choiceC.Bind(choices[2], UpgradeDescription(choices[2]), OnPick, up, L);

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
        if (UpgradeRoller.IsMasteryChoice(choiceId))
        {
            // keine Stat-Preview für Masteries
            _previewType   = null;
            _previewStacks = null;
        }
        else
        {
            _previewType   = UpgradeRoller.ResolveFromChoice(choiceId);
            _previewStacks = UpgradeRoller.StacksForChoice(choiceId);
        }

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
        if (!statRowPrefab || !statsContentStats) return null;

        row = Instantiate(statRowPrefab, statsContentStats);
        row.name = $"Row_{type}";
        _rows[type] = row;
        return row;
    }


    private DamageRow GetOrCreateDamageRow()
    {
        if (_damageRow && _damageRow.gameObject) return _damageRow;
        if (!damageRowPrefab || !statsContentWeapons) return null;

        _damageRow = Instantiate(damageRowPrefab, statsContentWeapons);
        _damageRow.name = "Row_Damage";
        return _damageRow;
    }


    // -------------------- Intern --------------------
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
            // PlayerXP: encodedId zum Server schicken (siehe unten)
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
        if (!statRowPrefab || !statsContentWeapons) return null;

        row = Instantiate(statRowPrefab, statsContentWeapons);
        row.name = key;
        _rowsCustom[key] = row;
        return row;
    }

    // -------------------- Stats Sidebar --------------------
    private void RefreshStats()
    {
        if (_upgrades == null) _upgrades = FindLocalUpgrades();
        if (!_upgrades || (statsContentStats == null && statsContentWeapons == null))
            return;

        bool allowPreview = _open && !_picked;

        // Helper für common pattern
        void FillBasic(UpgradeType type, string uiName, float currVal, int lvl, int max, Func<int, float> predictAtLevel, Func<float, string> fmt)
        {
            var row = GetOrCreateRow(type, uiName);
            if (!row) return;

            bool previewThis = allowPreview && _previewType.HasValue && _previewType.Value == type;

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

        // --- Armor (Flat Damage Reduction) ---
        {
            int lvl = _upgrades.GetLevel(UpgradeType.Armor);
            int max = _upgrades.GetMaxLevel(UpgradeType.Armor);
            float curr = _upgrades.GetCurrentValue(UpgradeType.Armor);
            FillBasic(UpgradeType.Armor, "Armor", curr, lvl, max,
                    l => _upgrades.GetCurrentValueAtLevel(UpgradeType.Armor, l),
                    v => FormatValue(UpgradeType.Armor, v));
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

        // --- Stamina (NEU) ---
        {
            int lvl = _upgrades.GetLevel(UpgradeType.Stamina);
            int max = _upgrades.GetMaxLevel(UpgradeType.Stamina);
            float curr = _upgrades.GetCurrentValue(UpgradeType.Stamina); // max ST

            var row = GetOrCreateRow(UpgradeType.Stamina, "Stamina");
            if (row)
            {
                string nextStr = null;
                if (allowPreview && _previewType.HasValue && _previewType.Value == UpgradeType.Stamina && lvl < max)
                {
                    int stacks = Mathf.Clamp(_previewStacks ?? 1, 1, max - lvl);
                    float nextVal = _upgrades.GetCurrentValueAtLevel(UpgradeType.Stamina, lvl + stacks);
                    nextStr = $"{FormatValue(UpgradeType.Stamina, nextVal)}   (Lv {lvl + stacks}/{max})";
                }

                float prog = (max > 0) ? (lvl / (float)max) : -1f;
                row.Set("Stamina",
                    $"{FormatValue(UpgradeType.Stamina, curr)}   {(lvl >= max ? "(MAX)" : $"(Lv {lvl}/{max})")}",
                    nextStr, prog);
            }
        }

        // --- Drop More XP ---
        {
            int lvl = _upgrades.GetLevel(UpgradeType.DropMoreXP);
            int max = _upgrades.GetMaxLevel(UpgradeType.DropMoreXP);
            float curr = _upgrades.GetCurrentValue(UpgradeType.DropMoreXP);

            FillBasic(
                UpgradeType.DropMoreXP,
                "XP Drops",
                curr,
                lvl,
                max,
                l => _upgrades.GetCurrentValueAtLevel(UpgradeType.DropMoreXP, l),
                v => FormatValue(UpgradeType.DropMoreXP, v)
            );
        }

        // --- Drop More Gold ---
        {
            int lvl = _upgrades.GetLevel(UpgradeType.DropMoreGold);
            int max = _upgrades.GetMaxLevel(UpgradeType.DropMoreGold);
            float curr = _upgrades.GetCurrentValue(UpgradeType.DropMoreGold);

            FillBasic(
                UpgradeType.DropMoreGold,
                "Gold Drops",
                curr,
                lvl,
                max,
                l => _upgrades.GetCurrentValueAtLevel(UpgradeType.DropMoreGold, l),
                v => FormatValue(UpgradeType.DropMoreGold, v)
            );
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

                    if (def != null)
                        row.SetIcon(def.uiIcon);
                    else
                        row.SetIcon(null);

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

                    if (def != null)
                        row.SetIcon(def.uiIcon);
                    else
                        row.SetIcon(null);

                    row.Set(
                        label,
                        $"Lv {lvl}/{max}" + (string.IsNullOrEmpty(extra) ? "" : $"   ({extra})"),
                        null,
                        max > 0 ? (lvl / (float)max) : -1f
                    );
                }
            }
            // Grenade
            {
                var def = _weapons.grenadeDef;
                int lvl = _weapons.grenadeLevel.Value;
                int max = 1 + (def?.steps?.Length ?? 0);
                var row = GetOrCreateCustomRow("Weapon_Grenade", def ? def.displayName : "Grenade");
                if (row)
                {
                    string label = def ? def.displayName : "Grenade";
                    var rt = _weapons.GrenadeRuntime;

                    // DPS-Schätzung für Salvenwaffe:
                    // damagePerShot = Schaden pro Granate
                    // shotsPerSecond = Salven pro Sekunde
                    // salvoCount = Granaten pro Salve
                    // => proj/s = shotsPerSecond * salvoCount
                    // (wenn dein WeaponRuntime.salvoCount noch nicht existiert,
                    //  fallback auf def.baseSalvoCount oder 1)
                    int salvo = rt != null ? Mathf.Max(1, rt.salvoCount) :
                                (def != null ? Mathf.Max(1, def.baseSalvoCount) : 1);

                    string extra = rt != null
                        ? $"DPS≈ {(rt.damagePerShot * rt.shotsPerSecond * salvo):0.#}  |  {rt.shotsPerSecond:0.##} salvos/s × {salvo}"
                        : null;

                    if (def != null)
                        row.SetIcon(def.uiIcon);
                    else
                        row.SetIcon(null);

                    row.Set(
                        label,
                        $"Lv {lvl}/{max}" + (string.IsNullOrEmpty(extra) ? "" : $"   ({extra})"),
                        null,
                        max > 0 ? (lvl / (float)max) : -1f
                    );
                }
            }
            // Lightning
            {
                var def = _weapons.lightningDef;
                int lvl = _weapons.lightningLevel.Value;
                int max = 1 + (def?.steps?.Length ?? 0);
                var row = GetOrCreateCustomRow("Weapon_Lightning", def ? def.displayName : "Lightning");
                if (row)
                {
                    string label = def ? def.displayName : "Lightning";
                    var rt = _weapons.LightningRuntime;
                    string extra = rt != null
                        ? $"DPS≈ {(rt.damagePerShot * rt.shotsPerSecond):0.#}  |  {rt.shotsPerSecond:0.##}/s"
                        : null;

                    if (def != null)
                        row.SetIcon(def.uiIcon);
                    else
                        row.SetIcon(null);

                    row.Set(
                        label,
                        $"Lv {lvl}/{max}" + (string.IsNullOrEmpty(extra) ? "" : $"   ({extra})"),
                        null,
                        max > 0 ? (lvl / (float)max) : -1f
                    );
                }
            }
            // Orbital
            {
                var def = _weapons.orbitalDef;
                int lvl = _weapons.orbitalLevel.Value;
                int max = 1 + (def?.steps?.Length ?? 0);
                var row = GetOrCreateCustomRow("Weapon_Orbital", def ? def.displayName : "Orbital");
                if (row)
                {
                    string label = def ? def.displayName : "Orbital";
                    var rt = _weapons.OrbitalRuntime;

                    // Sehr grobe DPS-Schätzung: damagePerShot * shotsPerSecond * salvoCount
                    // (salvoCount = Anzahl Orbs)
                    int salvo = rt != null ? Mathf.Max(1, rt.salvoCount) :
                                (def != null ? Mathf.Max(1, def.baseSalvoCount) : 1);

                    string extra = rt != null
                        ? $"DPS≈ {(rt.damagePerShot * rt.shotsPerSecond * salvo):0.#}  |  {rt.shotsPerSecond:0.##} ticks/s × {salvo}"
                        : null;

                    if (def != null)
                        row.SetIcon(def.uiIcon);
                    else
                        row.SetIcon(null);

                    row.Set(
                        label,
                        $"Lv {lvl}/{max}" + (string.IsNullOrEmpty(extra) ? "" : $"   ({extra})"),
                        null,
                        max > 0 ? (lvl / (float)max) : -1f
                    );
                }
            }

            // --- Weapon Summary / DamageRow ---
            {
                var dmgRow = GetOrCreateDamageRow();
                if (dmgRow != null)
                {
                    float totalDps = 0f;

                    if (_weapons.CannonRuntime != null)
                    {
                        var rt = _weapons.CannonRuntime;
                        totalDps += rt.damagePerShot * rt.shotsPerSecond;
                    }

                    if (_weapons.BlasterRuntime != null)
                    {
                        var rt = _weapons.BlasterRuntime;
                        totalDps += rt.damagePerShot * rt.shotsPerSecond;
                    }

                    if (_weapons.GrenadeRuntime != null)
                    {
                        var rt = _weapons.GrenadeRuntime;
                        int salvo = Mathf.Max(1, rt.salvoCount);
                        totalDps += rt.damagePerShot * rt.shotsPerSecond * salvo;
                    }

                    if (_weapons.LightningRuntime != null)
                    {
                        var rt = _weapons.LightningRuntime;
                        totalDps += rt.damagePerShot * rt.shotsPerSecond;
                    }

                    if (_weapons.OrbitalRuntime != null)
                    {
                        var rt = _weapons.OrbitalRuntime;
                        int salvo = Mathf.Max(1, rt.salvoCount);
                        totalDps += rt.damagePerShot * rt.shotsPerSecond * salvo;
                    }

                    // Primary: Gesamtdps, Alt: kurze Auflistung der aktiven Waffen
                    string primary = $"{totalDps:0.#} DPS total";

                    string alt = "";
                    void Append(ref string s, string add)
                    {
                        if (string.IsNullOrEmpty(add)) return;
                        if (!string.IsNullOrEmpty(s)) s += "   |   ";
                        s += add;
                    }

                    if (_weapons.CannonRuntime  != null) Append(ref alt, "Cannon");
                    if (_weapons.BlasterRuntime != null) Append(ref alt, "Blaster");
                    if (_weapons.GrenadeRuntime != null) Append(ref alt, "Grenade");
                    if (_weapons.LightningRuntime != null) Append(ref alt, "Lightning");
                    if (_weapons.OrbitalRuntime != null) Append(ref alt, "Orbital");

                    dmgRow.Set("Damage", primary, null, alt, null);
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
            default: return curr;
        }
    }


    // Anzeige-Format analog zu PlayerUpgrades.GetCurrentDisplay
    private string FormatValue(UpgradeType type, float v)
    {
        switch (type)
        {
            case UpgradeType.MaxHP: return $"{v:0.#} HP";
            case UpgradeType.Armor:     return $"{v:0.#} armor";
            case UpgradeType.Magnet: return $"{v:0.00}×";
            case UpgradeType.MoveSpeed: return $"{v:0.##} m/s";
            case UpgradeType.Stamina:   return $"{v:0.#} ST";
            case UpgradeType.DropMoreXP:  return $"{(v - 1f) * 100f:0.#}% XP";
            case UpgradeType.DropMoreGold:return $"{(v - 1f) * 100f:0.#}% Gold";
            default: return v.ToString("0.##");
        }
    }

    private void SubscribeStats()
    {
        if (_statsSubscribed) return;
        if (_upgrades == null) _upgrades = FindLocalUpgrades();
        if (_upgrades == null) return;

        _upgrades.MaxHPLevel.OnValueChanged += OnAnyStatChanged;
        _upgrades.ArmorLevel.OnValueChanged     += OnAnyStatChanged;
        _upgrades.MagnetLevel.OnValueChanged += OnAnyStatChanged;
        _upgrades.MoveSpeedLevel.OnValueChanged += OnAnyStatChanged;
        _upgrades.StaminaLevel.OnValueChanged   += OnAnyStatChanged;
        _upgrades.DropMoreXPLevel.OnValueChanged   += OnAnyStatChanged;
        _upgrades.DropMoreGoldLevel.OnValueChanged += OnAnyStatChanged;
        _statsSubscribed = true;
    }

    private void UnsubscribeStats()
    {
        if (!_statsSubscribed || _upgrades == null) return;

        _upgrades.MaxHPLevel.OnValueChanged -= OnAnyStatChanged;
        _upgrades.ArmorLevel.OnValueChanged     -= OnAnyStatChanged;
        _upgrades.MagnetLevel.OnValueChanged -= OnAnyStatChanged;
        _upgrades.MoveSpeedLevel.OnValueChanged -= OnAnyStatChanged;
        _upgrades.StaminaLevel.OnValueChanged -= OnAnyStatChanged;
        _upgrades.DropMoreXPLevel.OnValueChanged -= OnAnyStatChanged;
        _upgrades.DropMoreGoldLevel.OnValueChanged -= OnAnyStatChanged;
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

        // ===== Waffen-Beschreibung =====
        if (UpgradeRoller.IsWeaponBaseId(baseId))
        {
            var pw = _weapons ?? FindLocalWeapons();
            var def = pw != null ? UpgradeRoller.ResolveWeaponDef(pw, baseId) : null;
            string name = def != null ? def.displayName : "Weapon";
            int stacks  = UpgradeRoller.StacksForChoice(encodedId);

            int curLevel = 0;
            int maxLevel = def != null ? 1 + (def.steps?.Length ?? 0) : 1;

            if (pw != null && def != null)
            {
                if (def == pw.cannonDef)    curLevel = pw.cannonLevel.Value;
                else if (def == pw.blasterDef)   curLevel = pw.blasterLevel.Value;
                else if (def == pw.grenadeDef)   curLevel = pw.grenadeLevel.Value;
                else if (def == pw.lightningDef) curLevel = pw.lightningLevel.Value;
                else if (def == pw.orbitalDef)   curLevel = pw.orbitalLevel.Value;
            }

            if (curLevel == 0)
                return $"NEW WEAPON: {name}";

            // Nutze WeaponStepDescriber für eine kurze "was bringt L+1?"-Beschreibung
            string body = WeaponStepDescriber.DescribeStep(def, curLevel + 1, up);

            if (!string.IsNullOrEmpty(body))
                return body; // z.B. "+25% damage  (DPS 120 → 150)"
            
            // Fallback, falls kein Step vorhanden
            return $"{name}: +1 level (Lv {curLevel + 1}/{maxLevel})";
        }

        // Stat-Upgrade
        var type       = UpgradeRoller.Resolve(baseId);
        int stacksStat = UpgradeRoller.StacksForChoice(encodedId);

        return type switch
        {
            UpgradeType.MaxHP     => $"+{up.maxHPPerLevel        * stacksStat:0.#} max HP",
            UpgradeType.Armor     => $"+{up.armorFlatPerLevel    * stacksStat:0.#} armor",
            UpgradeType.Magnet    => $"+{PctFromDamageMult(up.magnetRangeMultPerLevel, stacksStat):0.#}% magnet range",
            UpgradeType.MoveSpeed => $"+{PctFromDamageMult(up.moveSpeedMultPerLevel,   stacksStat):0.#}% move speed",
            UpgradeType.Stamina   => $"+{up.staminaMaxPerLevel   * stacksStat:0.#} max stamina\n+{up.staminaRegenPerLevel * stacksStat:0.#} stamina/s regen",
            UpgradeType.DropMoreXP  => $"+{up.xpDropBonusPerLevel   * stacksStat * 100f:0.#}% XP drops",
            UpgradeType.DropMoreGold=> $"+{up.goldDropBonusPerLevel * stacksStat * 100f:0.#}% Gold drops",
            _                     => "Upgrade"
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
        if (levelUpLoopSource) levelUpLoopSource.Stop();
    }
}
