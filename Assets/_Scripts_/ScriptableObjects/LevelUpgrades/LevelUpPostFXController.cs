// LevelUpPostFXController.cs (URP)
// Requires: using Universal Render Pipeline (URP)
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class LevelUpPostFXController : MonoBehaviour
{
    [Header("Volume Ref")]
    public Volume globalVolume;               // dein Global Volume aus der Szene
    public bool cloneProfileAtRuntime = true; // schützt das Asset vor Runtime-Änderungen

    [Header("Bloom Settings")]
    public float targetIntensity = 1.5f;      // wie stark beim Level-Up
    public float targetThreshold = 0.9f;      // optional etwas weicher
    public float fadeInTime = 0.15f;
    public float fadeOutTime = 0.18f;

    // intern
    private VolumeProfile _runtimeProfile;
    private Bloom _bloom;
    private float _baseIntensity;
    private float _baseThreshold;
    private bool _hasBloom;
    private Coroutine _anim;

    private void Awake()
    {
        if (!globalVolume) globalVolume = GetComponent<Volume>();
        if (!globalVolume) { Debug.LogWarning("[PostFX] No Volume assigned."); return; }

        // Profil ggf. klonen, damit wir das Asset nicht verändern
        if (cloneProfileAtRuntime && globalVolume.profile != null)
        {
            _runtimeProfile = Instantiate(globalVolume.profile);
            globalVolume.profile = _runtimeProfile;
        }
        else
        {
            _runtimeProfile = globalVolume.profile;
        }

        if (_runtimeProfile == null)
        {
            _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            globalVolume.profile = _runtimeProfile;
        }

        // Bloom besorgen oder anlegen
        _hasBloom = _runtimeProfile.TryGet(out _bloom);
        if (!_hasBloom)
        {
            _bloom = _runtimeProfile.Add<Bloom>(true);
            _hasBloom = true;
        }

        // Basiswerte merken
        _baseIntensity = _bloom.intensity.overrideState ? _bloom.intensity.value : 0f;
        _baseThreshold = _bloom.threshold.overrideState ? _bloom.threshold.value : 1f;

        // Sicherstellen, dass Bloom aktiv ist (nur Override-States setzen, rest bleibt)
        _bloom.intensity.overrideState = true;
        _bloom.threshold.overrideState = true;
    }

    public void SetLevelUpBloom(bool on)
    {
        if (!_hasBloom) return;
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateBloom(on));
    }

    private IEnumerator AnimateBloom(bool on)
    {
        float dur = on ? fadeInTime : fadeOutTime;
        if (dur <= 0f)
        {
            _bloom.intensity.value = on ? targetIntensity : _baseIntensity;
            _bloom.threshold.value = on ? targetThreshold : _baseThreshold;
            yield break;
        }

        float t = 0f;
        float startI = _bloom.intensity.value;
        float startT = _bloom.threshold.value;
        float endI = on ? targetIntensity : _baseIntensity;
        float endT = on ? targetThreshold : _baseThreshold;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime; // wichtig wegen Slow-Mo
            float k = Mathf.Clamp01(t / dur);
            _bloom.intensity.value = Mathf.Lerp(startI, endI, k);
            _bloom.threshold.value = Mathf.Lerp(startT, endT, k);
            yield return null;
        }

        _bloom.intensity.value = endI;
        _bloom.threshold.value = endT;
    }
}
