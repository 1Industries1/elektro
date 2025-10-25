using UnityEngine;
using System.Collections;

public class SlowMoManager : MonoBehaviour
{
    public static SlowMoManager Instance { get; private set; }

    [Range(0.01f, 1f)]
    public float defaultScale = 0.2f;

    public float defaultInDuration  = 0.25f;
    public float defaultHold        = 0.75f;
    public float defaultOutDuration = 0.35f;

    [Tooltip("Ob Time.fixedDeltaTime mitskaliert wird (für 'Welt wird auch physikalisch langsamer').")]
    public bool scaleFixedDeltaTime = true;

    float _prevTimeScale  = 1f;
    float _prevFixedDelta = 0.02f; // Unity-Default
    Coroutine _routine;

    public bool IsActive { get; private set; }
    public float CurrentScale => Time.timeScale;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _prevFixedDelta = Time.fixedDeltaTime;
    }

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
        IsActive = true;
        _prevTimeScale  = Time.timeScale;
        _prevFixedDelta = Time.fixedDeltaTime;

        // Fade-in (nutze unscaled Zeit!)
        yield return LerpTimeScale(_prevTimeScale, target, inDur);

        // Halten (unscaled)
        float t = 0f;
        while (t < hold)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade-out
        yield return LerpTimeScale(Time.timeScale, _prevTimeScale, outDur);

        if (scaleFixedDeltaTime) Time.fixedDeltaTime = _prevFixedDelta;
        IsActive = false;
        _routine = null;
    }

    IEnumerator LerpTimeScale(float from, float to, float duration)
    {
        if (Mathf.Approximately(duration, 0f))
        {
            SetScale(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float s = Mathf.Lerp(from, to, k);
            SetScale(s);
            yield return null;
        }

        SetScale(to);
    }

    void SetScale(float s)
    {
        Time.timeScale = s;
        if (scaleFixedDeltaTime)
        {
            // skaliere fixedDelta gleich mit, damit die Weltphysik „langsamer tickt“
            Time.fixedDeltaTime = _prevFixedDelta * s;
        }
        // Optional: Audio pitch etc. hier anpassen
        // AudioListener.pitch = Mathf.Lerp(1f, 0.8f, 1f - s); // Beispiel
    }
}
