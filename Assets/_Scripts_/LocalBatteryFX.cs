using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // Für URP. In HDRP: UnityEngine.Rendering.HighDefinition
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // optional für Rumble
#endif

public class LocalBatteryFX : MonoBehaviour
{
    public static LocalBatteryFX Instance { get; private set; }

    [Header("References")]
    [Tooltip("Post-Processing Volume in/nahe der Player-Kamera.")]
    [SerializeField] private Volume volume;

    [Header("Thresholds")]
    [Tooltip("Ab hier wird gewarnt (0..1).")]
    [Range(0f, 1f)] [SerializeField] private float warnThreshold = 0.35f;
    [Tooltip("Unterhalb hiervon: starker Effekt + Puls (0..1).")]
    [Range(0f, 1f)] [SerializeField] private float criticalThreshold = 0.15f;

    [Header("Vignette")]
    [SerializeField] private AnimationCurve vignetteByLow = AnimationCurve.EaseInOut(0, 0.05f, 1, 0.7f);
    [SerializeField] private float vignetteSmoothing = 12f;

    [Header("Chromatic Aberration")]
    [SerializeField] private AnimationCurve caByLow = AnimationCurve.EaseInOut(0, 0.0f, 1, 0.6f);
    [SerializeField] private float caSmoothing = 10f;

    [Header("Desaturation")]
    [SerializeField] private AnimationCurve desatByLow = AnimationCurve.EaseInOut(0, 0f, 1, 60f); // ColorAdjustments.saturation (in -100..100)
    [SerializeField] private float desatSmoothing = 8f;

    [Header("Lens Distortion (optional)")]
    [SerializeField] private AnimationCurve lensByLow = AnimationCurve.EaseInOut(0, 0f, 1, -0.2f);
    [SerializeField] private float lensSmoothing = 6f;

    [Header("Critical Pulse")]
    [Tooltip("Pulsfrequenz bei critical (Hz).")]
    [SerializeField] private float pulseHz = 2.0f;
    [Tooltip("Zusatz-Vignette im Puls (additiv).")]
    [SerializeField] private float pulseVignetteBoost = 0.15f;

    [Header("Lockout Burst")]
    [SerializeField] private float lockoutFlashTime = 0.25f;
    [SerializeField] private float lockoutExtraVignette = 0.8f;
    [SerializeField] private float lockoutExtraDesat = 80f;

#if ENABLE_INPUT_SYSTEM
    [Header("Gamepad Rumble (optional)")]
    [SerializeField] private bool gamepadRumble = true;
    [SerializeField] private float rumbleLow = 0.15f;
    [SerializeField] private float rumbleHigh = 0.35f;
#endif

    // PostFX handles
    private Vignette _vignette;
    private ChromaticAberration _ca;
    private ColorAdjustments _color;
    private LensDistortion _lens;

    // state
    private float _t;                   // 0..1: wie "niedrig" ist die Batterie (0 = voll ok, 1 = leer)
    private bool _isLocked;
    private float _lockoutUntil;

    // smooth values
    private float _vigCur, _caCur, _satCur, _lensCur;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!volume)
            volume = GetComponentInChildren<Volume>();

        if (!volume || volume.profile == null)
        {
            Debug.LogWarning("[LocalBatteryFX] Volume/Profile fehlt. Effekte deaktiviert.");
            enabled = false;
            return;
        }

        volume.profile.TryGet(out _vignette);
        volume.profile.TryGet(out _ca);
        volume.profile.TryGet(out _color);
        volume.profile.TryGet(out _lens);

        if (_vignette == null) { _vignette = volume.profile.Add<Vignette>(true); }
        if (_ca == null) { _ca = volume.profile.Add<ChromaticAberration>(true); }
        if (_color == null) { _color = volume.profile.Add<ColorAdjustments>(true); }
        if (_lens == null) { _lens = volume.profile.Add<LensDistortion>(true); }

        // Startwerte soft
        _vigCur = _vignette.intensity.value;
        _caCur = _ca.intensity.value;
        _satCur = _color.saturation.value;
        _lensCur = _lens.intensity.value;
    }

    void OnDestroy()
    {
#if ENABLE_INPUT_SYSTEM
        if (gamepadRumble && Gamepad.current != null)
            Gamepad.current.SetMotorSpeeds(0, 0);
#endif
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Zielwerte anhand _t bestimmen (0=ok,1=leer)
        float dt = Time.deltaTime;
        float vigTarget = vignetteByLow.Evaluate(_t);
        float caTarget  = caByLow.Evaluate(_t);
        float satTarget = -Mathf.Abs(desatByLow.Evaluate(_t)); // negative Saturation = Entsättigung
        float lensTarget= lensByLow.Evaluate(_t);

        // Critical Puls oben drauf
        if (!_isLocked && _t >= Mathf.Clamp01((1f - criticalThreshold) / 1f))
        {
            float pulse = 0.5f * (1f + Mathf.Sin(2f * Mathf.PI * pulseHz * Time.time));
            vigTarget += pulseVignetteBoost * pulse;
        }

        // Lockout Burst (kurz)
        if (Time.time < _lockoutUntil)
        {
            float f = 1f - Mathf.InverseLerp(_lockoutUntil - lockoutFlashTime, _lockoutUntil, Time.time);
            vigTarget = Mathf.Max(vigTarget, lockoutExtraVignette * f);
            satTarget = Mathf.Min(satTarget, -lockoutExtraDesat * f);
        }

        // Smooth towards
        _vigCur = Mathf.Lerp(_vigCur, Mathf.Clamp01(vigTarget), 1f - Mathf.Exp(-vignetteSmoothing * dt));
        _caCur  = Mathf.Lerp(_caCur,  Mathf.Clamp01(caTarget),   1f - Mathf.Exp(-caSmoothing * dt));
        _satCur = Mathf.Lerp(_satCur, Mathf.Clamp(satTarget, -100f, 100f), 1f - Mathf.Exp(-desatSmoothing * dt));
        _lensCur= Mathf.Lerp(_lensCur, Mathf.Clamp(lensTarget, -1f, 1f), 1f - Mathf.Exp(-lensSmoothing * dt));

        // Apply
        if (_vignette) { _vignette.active = true; _vignette.intensity.Override(_vigCur); }
        if (_ca)       { _ca.active = true;       _ca.intensity.Override(_caCur); }
        if (_color)    { _color.active = true;    _color.saturation.Override(_satCur); }
        if (_lens)     { _lens.active = true;     _lens.intensity.Override(_lensCur); }

#if ENABLE_INPUT_SYSTEM
        if (gamepadRumble && Gamepad.current != null)
        {
            float rumble = Mathf.InverseLerp(warnThreshold, 1f, _t); // steigt mit „lowness“
            Gamepad.current.SetMotorSpeeds(rumble * rumbleLow, rumble * rumbleHigh);
        }
#endif
    }

    /// <summary>
    /// Aktualisiert die Effekte. charge/maxCharge im selben Frame der UI-Updates aufrufen.
    /// </summary>
    public void SetBatteryState(float charge, float maxCharge, bool locked)
    {
        maxCharge = Mathf.Max(0.0001f, maxCharge);
        float frac = Mathf.Clamp01(charge / maxCharge);

        // Wie „niedrig“ (0..1) relativ zur Warnschwelle? 0 = über Warnung, 1 = 0% Rest
        float lowness = 0f;
        if (frac < warnThreshold)
        {
            // Mappe [warnThreshold..0] -> [0..1]
            lowness = Mathf.InverseLerp(warnThreshold, 0f, frac);
        }

        _t = lowness;
        if (locked && !_isLocked)
        {
            // frisch gelockt -> Burst
            _lockoutUntil = Time.time + lockoutFlashTime;
        }
        _isLocked = locked;
    }
}
