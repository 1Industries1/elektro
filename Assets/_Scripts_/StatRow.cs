using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class StatRow : MonoBehaviour
{
    [Header("Refs")]
    public TextMeshProUGUI label;
    public TextMeshProUGUI value;
    public TextMeshProUGUI nextValue;  // grün, Preview
    public Slider progress;

    [Header("Style")]
    public Color normalValueColor = Color.white;
    public Color maxValueColor    = new Color(1f, 0.84f, 0.2f); // Gold
    public Color nextColor        = new Color(0.4f, 1f, 0.4f);  // #66FF66
    public float progressAnimTime = 0.25f;
    public float bumpScale        = 1.06f;
    public float bumpTime         = 0.12f;

    [Header("Preview Highlight")]
    // Ein halbtransparentes Hintergrund-Objekt, das NUR bei Preview aktiv ist
    public GameObject previewHighlightBG;

    string    _lastValueText;
    float     _lastProgress = -1f;
    Coroutine _progressCR;
    Coroutine _bumpCR;

    /// <summary>
    /// labelText: "Max HP"
    /// valueText: "120 HP   (Lv 3/5)" oder "120 HP   (MAX)"
    /// nextTextOrNull: z.B. "140 HP   (Lv 4/5)" oder null (kein Preview)
    /// progress01: 0..1 oder <0, um den Slider auszublenden
    /// </summary>
    public void Set(string labelText, string valueText, string nextTextOrNull, float progress01 = -1f)
    {
        if (label) 
            label.text = labelText;

        // Hauptwert
        if (value)
        {
            bool changed = _lastValueText != null && _lastValueText != valueText;
            value.text   = valueText;

            // MAX optisch hervorheben
            if (valueText.Contains("(MAX)"))
                value.color = maxValueColor;
            else
                value.color = normalValueColor;

            if (changed)
                PlayBump();

            _lastValueText = valueText;
        }

        // Preview / Next-Wert
        bool hasNext = !string.IsNullOrEmpty(nextTextOrNull);
        if (nextValue)
        {
            nextValue.gameObject.SetActive(hasNext);
            if (hasNext)
            {
                // Pfeil davor, damit klar ist: das ist der Preview-Wert
                nextValue.text  = "➜ " + nextTextOrNull;
                nextValue.color = nextColor;
            }
        }

        // Zeilen-Highlight, wenn Preview aktiv
        if (previewHighlightBG)
            previewHighlightBG.SetActive(hasNext);

        // Progressbar
        if (progress)
        {
            bool showProg = progress01 >= 0f;
            progress.gameObject.SetActive(showProg);

            if (showProg)
            {
                float target = Mathf.Clamp01(progress01);

                if (_progressCR != null)
                    StopCoroutine(_progressCR);

                // Erste Initialisierung: direkt setzen, danach animieren
                if (_lastProgress < 0f)
                {
                    progress.value = target;
                }
                else
                {
                    _progressCR = StartCoroutine(AnimProgress(progress.value, target));
                }

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
            float f = t / Mathf.Max(0.0001f, progressAnimTime);
            progress.value = Mathf.Lerp(a, b, f);
            yield return null;
        }
        progress.value = b;
    }

    void PlayBump()
    {
        if (_bumpCR != null)
            StopCoroutine(_bumpCR);

        _bumpCR = StartCoroutine(BumpCR());
    }

    IEnumerator BumpCR()
    {
        Vector3 baseScale = transform.localScale;
        Vector3 big       = baseScale * bumpScale;

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
