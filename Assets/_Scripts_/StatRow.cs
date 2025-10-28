// StatRow.cs

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class StatRow : MonoBehaviour
{
    [Header("Refs")]
    public TextMeshProUGUI label;
    public TextMeshProUGUI value;
    public TextMeshProUGUI nextValue; // optional, grün
    public Slider progress;           // optional

    [Header("Style")]
    public Color nextColor = new Color(0.4f, 1f, 0.4f); // #66FF66
    public float progressAnimTime = 0.25f;
    public float bumpScale = 1.06f;
    public float bumpTime  = 0.12f;

    string _lastValueText;
    float  _lastProgress = -1f;
    Coroutine _progressCR;
    Coroutine _bumpCR;

    public void Set(string labelText, string valueText, string nextTextOrNull, float progress01 = -1f)
    {
        if (label) label.text = labelText;

        if (value)
        {
            // Highlight, wenn sich der Werttext ändert (z.B. nach Upgrade)
            bool changed = _lastValueText != null && _lastValueText != valueText;
            value.text = valueText;
            if (changed) PlayBump();
            _lastValueText = valueText;
        }

        if (nextValue)
        {
            bool showNext = !string.IsNullOrEmpty(nextTextOrNull);
            nextValue.gameObject.SetActive(showNext);
            if (showNext)
            {
                nextValue.text = nextTextOrNull;
                nextValue.color = nextColor;
            }
        }

        if (progress)
        {
            bool showProg = progress01 >= 0f;
            progress.gameObject.SetActive(showProg);
            if (showProg)
            {
                float target = Mathf.Clamp01(progress01);
                if (_progressCR != null) StopCoroutine(_progressCR);

                // sanft zum Zielwert animieren
                if (_lastProgress < 0f) { progress.value = target; }
                else { _progressCR = StartCoroutine(AnimProgress(progress.value, target)); }

                _lastProgress = target;
            }
        }
    }

    IEnumerator AnimProgress(float a, float b)
    {
        float t = 0f;
        while (t < progressAnimTime)
        {
            t += Time.unscaledDeltaTime;
            progress.value = Mathf.Lerp(a, b, t / Mathf.Max(0.0001f, progressAnimTime));
            yield return null;
        }
        progress.value = b;
    }

    void PlayBump()
    {
        if (_bumpCR != null) StopCoroutine(_bumpCR);
        _bumpCR = StartCoroutine(BumpCR());
    }
    IEnumerator BumpCR()
    {
        Vector3 baseScale = transform.localScale;
        Vector3 big = baseScale * bumpScale;

        float half = bumpTime * 0.5f;
        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(baseScale, big, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(big, baseScale, t / half);
            yield return null;
        }
        transform.localScale = baseScale;
    }
}
