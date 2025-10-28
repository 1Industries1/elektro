using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SlowMoManager : MonoBehaviour
{
    public static SlowMoManager Instance { get; private set; }

    [Range(0.01f, 1f)] public float defaultScale = 0.2f;
    public float defaultInDuration  = 0.25f;
    public float defaultHold        = 0.75f;
    public float defaultOutDuration = 0.35f;

    [Tooltip("Ob Time.fixedDeltaTime mitskaliert wird.")]
    public bool scaleFixedDeltaTime = true;

    float _baseFixedDelta = 0.02f; // Unity-Default
    Coroutine _routine;

    // — NEU: Token-basierte Holds —
    int _nextHandle = 1;
    readonly Dictionary<int, float> _activeHolds = new Dictionary<int, float>();
    float _preHoldTimeScale = 1f;
    float _preHoldFixedDelta = 0.02f;

    public bool IsActive => _activeHolds.Count > 0 || _routine != null;
    public float CurrentScale => Time.timeScale;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _baseFixedDelta = Time.fixedDeltaTime;
        _preHoldFixedDelta = _baseFixedDelta;
    }

    // --------- Bestehendes „Fire & Forget“ BulletTime (unverändert nutzbar) ----------
    public void BulletTime(float targetScale, float inDur, float hold, float outDur)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(BulletTimeRoutine(
            Mathf.Clamp(targetScale, 0.01f, 1f),
            Mathf.Max(0f, inDur),
            Mathf.Max(0f, hold),
            Mathf.Max(0f, outDur)
        ));
    }
    public void BulletTime() => BulletTime(defaultScale, defaultInDuration, defaultHold, defaultOutDuration);

    IEnumerator BulletTimeRoutine(float target, float inDur, float hold, float outDur)
    {
        // Temporär unabhängig von Hold-Mechanik
        float prevScale = Time.timeScale;
        float prevFixed = Time.fixedDeltaTime;

        yield return LerpTimeScale(prevScale, target, inDur);
        float t = 0f;
        while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }
        yield return LerpTimeScale(Time.timeScale, prevScale, outDur);

        if (scaleFixedDeltaTime) Time.fixedDeltaTime = prevFixed;
        _routine = null;
    }

    // ---------------------- NEU: Hold-API ----------------------
    /// <summary>Startet ein „halte bis EndHold“; gibt Handle zurück.</summary>
    public int BeginHold(float targetScale, float fadeIn = 0.2f)
    {
        targetScale = Mathf.Clamp(targetScale, 0.01f, 1f);
        int handle = _nextHandle++;
        _activeHolds[handle] = targetScale;

        // Wenn erster Hold: Baseline merken und zu Ziel interpolieren
        if (_activeHolds.Count == 1)
        {
            _preHoldTimeScale  = Time.timeScale;
            _preHoldFixedDelta = Time.fixedDeltaTime;
            StopFadeRoutineIfAny();
            _routine = StartCoroutine(LerpTimeScale(Time.timeScale, GetMinActiveScale(), Mathf.Max(0f, fadeIn)));
        }
        else
        {
            // Schon im Hold: ggf. weiter runterblenden, falls neuer Scale kleiner ist
            float minTarget = GetMinActiveScale();
            if (minTarget < Time.timeScale)
            {
                StopFadeRoutineIfAny();
                _routine = StartCoroutine(LerpTimeScale(Time.timeScale, minTarget, Mathf.Max(0f, fadeIn)));
            }
        }

        return handle;
    }

    /// <summary>Beendet einen Hold. Wenn keine Holds mehr aktiv: zurückblenden.</summary>
    public void EndHold(int handle, float fadeOut = 0.25f)
    {
        if (_activeHolds.Remove(handle))
        {
            if (_activeHolds.Count == 0)
            {
                // zurück zur Baseline
                StopFadeRoutineIfAny();
                _routine = StartCoroutine(LerpTimeScale(Time.timeScale, _preHoldTimeScale, Mathf.Max(0f, fadeOut), restoreFixedOnEnd:true));
            }
            else
            {
                // ggf. wieder etwas hochblenden, aber nicht über das Minimum der verbliebenen Holds
                float minTarget = GetMinActiveScale();
                if (Time.timeScale != minTarget)
                {
                    StopFadeRoutineIfAny();
                    _routine = StartCoroutine(LerpTimeScale(Time.timeScale, minTarget, Mathf.Max(0f, fadeOut)));
                }
            }
        }
    }

    float GetMinActiveScale()
    {
        float min = 1f;
        foreach (var kv in _activeHolds) if (kv.Value < min) min = kv.Value;
        return min;
    }

    void StopFadeRoutineIfAny()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }

    IEnumerator LerpTimeScale(float from, float to, float duration, bool restoreFixedOnEnd = false)
    {
        if (Mathf.Approximately(duration, 0f))
        {
            SetScale(to);
            if (restoreFixedOnEnd && scaleFixedDeltaTime) Time.fixedDeltaTime = _preHoldFixedDelta;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float s = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            SetScale(s);
            yield return null;
        }

        SetScale(to);
        if (restoreFixedOnEnd && scaleFixedDeltaTime) Time.fixedDeltaTime = _preHoldFixedDelta;
    }

    void SetScale(float s)
    {
        Time.timeScale = s;
        if (scaleFixedDeltaTime)
        {
            // fixedDelta relativ zur Baseline (_preHoldFixedDelta während Hold, sonst _baseFixedDelta)
            float basis = (_activeHolds.Count > 0) ? _preHoldFixedDelta : _baseFixedDelta;
            Time.fixedDeltaTime = basis * s;
        }
    }
}
