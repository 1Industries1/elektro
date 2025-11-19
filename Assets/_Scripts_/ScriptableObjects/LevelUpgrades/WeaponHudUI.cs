using TMPro;
using Unity.Netcode;
using UnityEngine;
using static WeaponUiUtils;

public class WeaponHudUI : MonoBehaviour
{
    public static WeaponHudUI Instance { get; private set; }

    [Header("Bindings")]
    public WeaponHudSlot[] slots;
    public TextMeshProUGUI totalDpsText;

    [Header("Behavior")]
    [Tooltip("Falls true: versucht automatisch den lokalen PlayerWeapons zu finden.")]
    public bool autoFindLocalPlayer = true;

    private PlayerWeapons _weapons;
    private bool _subscribed;
    private Coroutine _bindCo;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        // Sicherstellen, dass Instance stimmt, falls HUD neu enabled wird
        if (Instance == null)
            Instance = this;

        if (autoFindLocalPlayer)
        {
            // Coroutine, die wartet, bis der Local Player existiert
            if (_bindCo == null)
                _bindCo = StartCoroutine(BindAndRefreshWhenReady());
        }
        else
        {
            // Falls du _weapons manuell irgendwo setzt
            if (_weapons != null)
                Subscribe();

            Refresh();
        }
    }

    private void OnDisable()
    {
        if (_bindCo != null)
        {
            StopCoroutine(_bindCo);
            _bindCo = null;
        }

        Unsubscribe();
        if (Instance == this) Instance = null;
    }

    // -------------------- Coroutine: warten bis PlayerWeapons da ist --------------------
    private System.Collections.IEnumerator BindAndRefreshWhenReady()
    {
        while (isActiveAndEnabled && _weapons == null)
        {
            TryBindLocalWeapons();

            if (_weapons != null)
            {
                // Sobald wir PlayerWeapons haben: einmal sauber zeichnen
                Refresh();
                break;
            }

            // 1 Frame warten, dann nochmal probieren
            yield return null;
        }

        _bindCo = null;
    }

    // -------------------- Binding --------------------
    private void TryBindLocalWeapons()
    {
        if (_weapons != null) return;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient) return;

        var local = nm.SpawnManager?.GetLocalPlayerObject();
        if (!local) return;

        _weapons = local.GetComponent<PlayerWeapons>()
                   ?? local.GetComponentInChildren<PlayerWeapons>(true);

        if (_weapons != null)
        {
            Subscribe();
        }
    }

    private void Subscribe()
    {
        if (_weapons == null || _subscribed) return;

        _weapons.RuntimesRebuilt += OnWeaponsRebuilt;
        _weapons.cannonLevel.OnValueChanged    += OnWeaponLevelChanged;
        _weapons.blasterLevel.OnValueChanged   += OnWeaponLevelChanged;
        _weapons.grenadeLevel.OnValueChanged   += OnWeaponLevelChanged;
        _weapons.lightningLevel.OnValueChanged += OnWeaponLevelChanged;
        _weapons.orbitalLevel.OnValueChanged   += OnWeaponLevelChanged;

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (_weapons == null || !_subscribed) return;

        _weapons.RuntimesRebuilt -= OnWeaponsRebuilt;
        _weapons.cannonLevel.OnValueChanged    -= OnWeaponLevelChanged;
        _weapons.blasterLevel.OnValueChanged   -= OnWeaponLevelChanged;
        _weapons.grenadeLevel.OnValueChanged   -= OnWeaponLevelChanged;
        _weapons.lightningLevel.OnValueChanged -= OnWeaponLevelChanged;
        _weapons.orbitalLevel.OnValueChanged   -= OnWeaponLevelChanged;

        _subscribed = false;
    }

    private void OnWeaponsRebuilt()                  => Refresh();
    private void OnWeaponLevelChanged(int _, int __) => Refresh();

    // -------------------- Refresh --------------------
    public void Refresh()
    {
        if (slots == null || slots.Length == 0)
            return;

        // Falls wir noch immer keinen PlayerWeapons haben,
        // z.B. wenn Refresh manuell gerufen wurde
        if (_weapons == null)
        {
            TryBindLocalWeapons();
            if (_weapons == null)
            {
                foreach (var s in slots)
                    s?.Clear();
                if (totalDpsText) totalDpsText.text = "";
                return;
            }
        }

        float totalDps = 0f;
        var configs = WeaponSlots; // aus WeaponUiUtils
        int count = Mathf.Min(slots.Length, configs.Length);

        for (int i = 0; i < count; i++)
        {
            var slotUi = slots[i];
            if (slotUi == null) continue;

            var cfg   = configs[i];
            var def   = cfg.GetDef(_weapons);
            int level = cfg.GetLevel(_weapons);

            if (def == null || level <= 0)
            {
                slotUi.Clear();
                continue;
            }

            int maxLevel   = 1 + (def.steps?.Length ?? 0);
            var rt         = cfg.GetRuntime(_weapons);
            float dps      = cfg.ComputeDps(_weapons, rt);

            totalDps += dps;
            slotUi.Set(def, level, maxLevel, dps);
        }

        if (totalDpsText)
            totalDpsText.text = $"{totalDps:0.#} DPS";
    }

    // -------------------- Highlight API (für LevelUpUI) --------------------
    public void HighlightWeapon(WeaponDefinition defOrNull)
    {
        if (slots == null) return;

        foreach (var s in slots)
        {
            if (s == null) continue;
            bool on = defOrNull != null && s.CurrentDef == defOrNull;
            s.SetHighlighted(on);
        }
    }
}
