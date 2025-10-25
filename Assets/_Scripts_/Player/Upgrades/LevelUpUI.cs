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
    public TextMeshProUGUI statsTitle;
    public TextMeshProUGUI statsBody;

    [Header("Behavior")]
    public float  fadeTime   = 0.12f;
    public bool   lockCursor = true;
    public KeyCode pickKeyA  = KeyCode.Alpha1;
    public KeyCode pickKeyB  = KeyCode.Alpha2;
    public KeyCode pickKeyC  = KeyCode.Alpha3;

    [Header("Slow Motion")]
    public bool  enableSlowMo      = true;
    [Range(0.05f, 1f)] public float slowMoScale = 0.2f;
    public bool scaleFixedDeltaTime = true;
    
    [Header("SFX")]
    public AudioSource sfxSource;
    public AudioClip sfxPick;
    

    // -------------------- Internals --------------------
    private float _prevTimeScale = 1f;
    private float _prevFixedDelta = 0.02f;
    private bool  _slowApplied;

    private int[] _choices;
    private bool  _open;
    private bool  _inputLocked;
    private bool  _picked;

    private Behaviour      _localControlToLock;
    private PlayerUpgrades _upgrades;

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

        _prevTimeScale  = Time.timeScale;
        _prevFixedDelta = Time.fixedDeltaTime;

        //if (statsPanel) statsPanel.SetActive(false);
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
        ApplySlowMo(false);
        UnsubscribeStats();
        //if (statsPanel) statsPanel.SetActive(false);
        _previewType = null;
    }

    // -------------------- Public API --------------------
    public void Show(int[] choices, bool lockInput = true)
    {
        _choices = choices;
        _picked  = false;
        _open    = true;
        _previewType = null;
        FindLocalUpgradesCached();

        if (title)    title.text    = "UPGRADES";
        if (subtitle) subtitle.text = "Choose 1 of 3 upgrades";

        if (HasThreeChoices)
        {
            Func<string,string> L = null; // oder dein Localization-Wrapper

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

        if (btnReroll) btnReroll.gameObject.SetActive(false);
        if (btnBanish) btnBanish.gameObject.SetActive(false);

        if (panel) panel.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeCanvas(panelGroup, 0f, 1f, fadeTime));

        RefreshStats();
        if (statsPanel) statsPanel.SetActive(true);
        SubscribeStats();

        if (lockInput) LockLocalInput(true);
        ApplySlowMo(true);
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
        _previewType   = UpgradeRoller.ResolveFromChoice(choiceId);
        _previewStacks = UpgradeRoller.StacksForChoice(choiceId);
        RefreshStats();
    }

    public void ClearPreview()
    {
        _previewType   = null;
        _previewStacks = null;
        RefreshStats();
    }

    // -------------------- Intern --------------------
    private void OnPick(int encodedId)
    {
        if (_picked) return;
        _picked = true;

        if (sfxSource && sfxPick)
            sfxSource.PlayOneShot(sfxPick);

        var type   = UpgradeRoller.ResolveFromChoice(encodedId);
        int stacks = UpgradeRoller.StacksForChoice(encodedId);

        if (choiceA) choiceA.SetInteractable(false);
        if (choiceB) choiceB.SetInteractable(false);
        if (choiceC) choiceC.SetInteractable(false);

        var xp = FindLocalPlayerXP();
        if (xp != null)
        {
            //xp.ChooseUpgradeServerRpc(id);

            // TODO: Deine PlayerXP sollte eine Methode annehmen, die encodedId (oder type + stacks) zum Server sendet.
            xp.ChooseUpgradeServerRpc(encodedId);
            RefreshStats();
        }
        else
        {
            var up = FindLocalUpgrades();
            if (up != null)
            {
                up.GrantUpgradeServerRpc(type, stacks); // neue Überladung, siehe unten
            }
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

    // -------------------- Stats Sidebar --------------------
    private void RefreshStats()
    {
        if (_upgrades == null) _upgrades = FindLocalUpgrades();
        if (statsBody == null)
            return;

        if (_upgrades == null)
        {
            statsBody.text = "— no stats —";
            return;
        }

        string Line(UpgradeType type, string name)
        {
            int lvl = _upgrades.GetLevel(type);
            int max = _upgrades.GetMaxLevel(type);
            string valStr = _upgrades.GetCurrentDisplay(type);
            bool previewThis = _previewType.HasValue && _previewType.Value == type;

            // ---- Standardpfad für alle anderen Stats ----
            if (previewThis && lvl < max)
            {
                int stacks = Mathf.Clamp(_previewStacks ?? 1, 1, max - lvl);
                string nextStr = FormatValue(type, PredictNextValueWithStacks(type, stacks));
                return $"{name,-14} {valStr,8}  →  <color=#66FF66>{nextStr,8}</color>   (Lv {lvl + stacks}/{max})";
            }
            else
            {
                bool isMax = lvl >= max;
                string extra = isMax ? "MAX" : $"Lv {lvl + 1}/{max}";
                return $"{name,-14} {valStr,8}   ({extra})";
            }
        }

        statsBody.text =
            Line(UpgradeType.FireRate, "Fire Rate") + "\n" +
            Line(UpgradeType.AltFireRate, "Alt Fire") + "\n" +
            Line(UpgradeType.TargetRange, "Target Range") + "\n" +
            Line(UpgradeType.MaxHP, "Max HP") + "\n" +
            DamageSection() + "\n" +
            Line(UpgradeType.GrenadeSalvo, "GL Salvo");
    }
    
    private float PredictNextValueWithStacks(UpgradeType type, int stacks)
    {
        // Wir rechnen vom aktuellen Effektivwert aus.
        float curr = _upgrades.GetCurrentValue(type);
        switch (type)
        {
            case UpgradeType.FireRate:    return Mathf.Max(0.01f, curr * Mathf.Pow(_upgrades.fireRateMultPerLevel, stacks));
            case UpgradeType.AltFireRate: return Mathf.Max(0.1f,  curr * Mathf.Pow(_upgrades.altFireRateMultPerLevel, stacks));
            case UpgradeType.TargetRange: return Mathf.Max(0f,    curr + stacks * _upgrades.targetRangePerLevel);
            case UpgradeType.MaxHP:       return Mathf.Max(1f,    curr + stacks * _upgrades.maxHPPerLevel);
            case UpgradeType.Damage:      return Mathf.Max(1f,    curr * Mathf.Pow(_upgrades.damageMultPerLevel, stacks));
            case UpgradeType.GrenadeSalvo:return Mathf.Max(1f,    curr + 1f * stacks);
            default:                      return curr;
        }
    }

    
    string DamageSection()
        {
            int lvl = _upgrades.GetLevel(UpgradeType.Damage);
            int max = _upgrades.GetMaxLevel(UpgradeType.Damage);
            bool previewThis = _previewType.HasValue && _previewType.Value == UpgradeType.Damage;

            Vector2 primNow = _upgrades.GetPrimaryDamageRangeCurrent();
            Vector2 altNow  = _upgrades.GetAltDamageRangeCurrent();

            string lvlStr = (lvl >= max) ? "MAX" : $"Lv {lvl+1}/{max}";
            string header = $"{"Damage",-14} ({lvlStr})";

            string primLine = $"{"Cannon",-14} {primNow.x:0.#}–{primNow.y:0.#}";
            string altLine  = $"{"Blaster",-14}  {altNow.x:0.#}–{altNow.y:0.#}";

            if (previewThis && lvl < max)
            {
                int stacks = Mathf.Clamp(_previewStacks ?? 1, 1, max - lvl);
                Vector2 primNext = _upgrades.GetPrimaryDamageRangeAtLevel(lvl + stacks);
                Vector2 altNext  = _upgrades.GetAltDamageRangeAtLevel(lvl + stacks);

                primLine += $"  →  <color=#66FF66>{primNext.x:0.#}–{primNext.y:0.#}</color>";
                altLine  += $"  →  <color=#66FF66>{altNext.x:0.#}–{altNext.y:0.#}</color>";
            }

            return header + "\n" + primLine + "\n" + altLine;
        }


    // Nächster Wert aus aktuellem Wert ableiten (ohne Baselines)
    private float PredictNextValue(UpgradeType type, float current)
    {
        switch (type)
        {
            case UpgradeType.FireRate:
                return Mathf.Max(0.01f, current * _upgrades.fireRateMultPerLevel);
            case UpgradeType.AltFireRate:
                return Mathf.Max(0.1f,  current * _upgrades.altFireRateMultPerLevel);
            case UpgradeType.TargetRange:
                return Mathf.Max(0f,    current + _upgrades.targetRangePerLevel);
            case UpgradeType.MaxHP:
                return Mathf.Max(1f,    current + _upgrades.maxHPPerLevel);
            case UpgradeType.Damage:
                return Mathf.Max(1f, current * _upgrades.damageMultPerLevel);
            case UpgradeType.GrenadeSalvo:
                return Mathf.Max(1f, current + PlayerUpgrades.GrenadePerLevel);
            default:
                return current;
        }
    }

    // Anzeige-Format analog zu PlayerUpgrades.GetCurrentDisplay
    private string FormatValue(UpgradeType type, float v)
    {
        switch (type)
        {
            case UpgradeType.FireRate:
            case UpgradeType.AltFireRate: return $"{v:0.00}s";
            case UpgradeType.TargetRange: return $"{v:0.#} m";
            case UpgradeType.MaxHP:       return $"{v:0.#} HP";
            case UpgradeType.Damage:      return $"{v:0.00}×";
            case UpgradeType.GrenadeSalvo: return $"{v:0}×";
            default:                      return v.ToString("0.##");
        }
    }

    private void SubscribeStats()
    {
        if (_statsSubscribed) return;
        if (_upgrades == null) _upgrades = FindLocalUpgrades();
        if (_upgrades == null) return;

        _upgrades.FireRateLevel     .OnValueChanged += OnAnyStatChanged;
        _upgrades.AltFireRateLevel  .OnValueChanged += OnAnyStatChanged;
        _upgrades.RangeLevel        .OnValueChanged += OnAnyStatChanged;
        _upgrades.MaxHPLevel        .OnValueChanged += OnAnyStatChanged;
        _upgrades.DamageLevel       .OnValueChanged += OnAnyStatChanged;
        _upgrades.GrenadeSalvoLevel .OnValueChanged += OnAnyStatChanged;
        _statsSubscribed = true;
    }

    private void UnsubscribeStats()
    {
        if (!_statsSubscribed || _upgrades == null) return;

        _upgrades.FireRateLevel     .OnValueChanged -= OnAnyStatChanged;
        _upgrades.AltFireRateLevel  .OnValueChanged -= OnAnyStatChanged;
        _upgrades.RangeLevel        .OnValueChanged -= OnAnyStatChanged;
        _upgrades.MaxHPLevel        .OnValueChanged -= OnAnyStatChanged;
        _upgrades.DamageLevel       .OnValueChanged -= OnAnyStatChanged;
        _upgrades.GrenadeSalvoLevel .OnValueChanged -= OnAnyStatChanged;
        _statsSubscribed = false;
    }

    private void OnAnyStatChanged(int _, int __)
    {
        if (_open) RefreshStats();
    }

    // -------------------- FX / Input Lock --------------------
    private void ApplySlowMo(bool on)
    {
        if (!enableSlowMo) return;

        if (on)
        {
            if (_slowApplied) return;
            _prevTimeScale  = Time.timeScale;
            _prevFixedDelta = Time.fixedDeltaTime;

            Time.timeScale = Mathf.Clamp(slowMoScale, 0.05f, 1f);
            if (scaleFixedDeltaTime)
                Time.fixedDeltaTime = _prevFixedDelta * Time.timeScale;

            _slowApplied = true;
        }
        else
        {
            if (!_slowApplied) return;

            Time.timeScale = _prevTimeScale;
            if (scaleFixedDeltaTime)
                Time.fixedDeltaTime = _prevFixedDelta;

            _slowApplied = false;
        }
    }

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
        ApplySlowMo(false);
        UnsubscribeStats();
        //if (statsPanel) statsPanel.SetActive(false);
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

        var type   = UpgradeRoller.ResolveFromChoice(encodedId);
        int stacks = UpgradeRoller.StacksForChoice(encodedId);
        string tag = stacks switch { 1=>"(Common)", 2=>"(Rare)", 3=>"(Epic)", 4=>"(Legendary)", _=>"" };

        // Falls kein PlayerUpgrades gefunden wurde (Edge-Case, z.B. sehr frühe UI-Init),
        // liefere eine neutrale Fallback-Beschreibung.
        if (up == null)
        {
            return type switch
            {
                UpgradeType.FireRate      => $"+{PctFromTimeMult(0.85f, stacks):0.#}% fire rate {tag}",
                UpgradeType.AltFireRate   => $"+{PctFromTimeMult(0.85f, stacks):0.#}% blaster fire rate {tag}",
                UpgradeType.TargetRange   => $"+{10f * stacks:0.#} m target range {tag}",
                UpgradeType.MaxHP         => $"+{15f * stacks:0.#} max HP {tag}",
                UpgradeType.Damage        => $"+{PctFromDamageMult(1.15f, stacks):0.#}% damage {tag}",
                UpgradeType.GrenadeSalvo  => $"+{PlayerUpgrades.GrenadePerLevel * stacks} bullets per salvo {tag}",
                _ => $"Upgrade {tag}"
            };
        }

        // Dynamisch aus den tatsächlich eingestellten Werten
        return type switch
        {
            UpgradeType.FireRate      => $"+{PctFromTimeMult(up.fireRateMultPerLevel, stacks):0.#}% fire rate",
            UpgradeType.AltFireRate   => $"+{PctFromTimeMult(up.altFireRateMultPerLevel, stacks):0.#}% blaster fire rate",
            UpgradeType.TargetRange   => $"+{up.targetRangePerLevel * stacks:0.#} m target range",
            UpgradeType.MaxHP         => $"+{up.maxHPPerLevel * stacks:0.#} max HP",
            UpgradeType.Damage        => $"+{PctFromDamageMult(up.damageMultPerLevel, stacks):0.#}% damage",
            UpgradeType.GrenadeSalvo  => $"+{PlayerUpgrades.GrenadePerLevel * stacks} bullets per salvo",
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

    private PlayerXP FindLocalPlayerXPOrNull() => FindLocalPlayerXP();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
