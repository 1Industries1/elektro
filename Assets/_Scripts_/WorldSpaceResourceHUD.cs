// Scripts/UI/WorldSpaceResourceHUD.cs
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WorldSpaceResourceHUD : MonoBehaviour
{
    [System.Serializable]
    public class ResourceEntry
    {
        public ResourceType type;
        [Header("Texts")]
        public TextMeshProUGUI totalText;   // z.B. "Energy: 35"
        public TextMeshProUGUI popupText;   // z.B. "+5" (wird eingeblendet)
        [Header("Optional")]
        public CanvasGroup popupGroup;      // für Fade; wenn null, wird alpha über color gemacht
        public string labelOverride;        // optional: eigener Label-Name statt enum.ToString()
    }

    [Header("Entries")]
    public List<ResourceEntry> entries = new();

    [Header("Flash Settings")]
    [Tooltip("Gesamtdauer des Popup-Fades in Sekunden")]
    public float flashDuration = 0.9f;

    [Tooltip("Kurve für die Alpha/Scale-Entwicklung (0..1 über die Dauer)")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Start-Skalierung beim Einblenden")]
    public float popupScaleFrom = 1.2f;

    [Tooltip("End-Skalierung beim Ausblenden")]
    public float popupScaleTo = 1.0f;

    // Laufzeit-Zustand pro Ressource
    private class Runtime
    {
        public int pendingDelta = 0;   // neue Deltas, die seit letzter Anzeige eingetroffen sind
        public int aggregateDelta = 0; // Summe über die gesamte Popup-Laufzeit
        public Coroutine routine = null;
        public float t = 0f;
    }

    private readonly Dictionary<ResourceType, ResourceEntry> _entry = new();
    private readonly Dictionary<ResourceType, Runtime> _rt = new();

    void Awake()
    {
        _entry.Clear();
        _rt.Clear();

        foreach (var e in entries)
        {
            if (e == null) continue;
            _entry[e.type] = e;
            _rt[e.type] = new Runtime();

            // Init UI
            if (e.popupText != null)
            {
                e.popupText.gameObject.SetActive(false);
                if (e.popupGroup != null) e.popupGroup.alpha = 0f;
                else e.popupText.alpha = 0f;
            }
        }
    }

    public void SetTotal(ResourceType type, int total)
    {
        if (!_entry.TryGetValue(type, out var e) || e.totalText == null) return;
        string label = string.IsNullOrEmpty(e.labelOverride) ? type.ToString() : e.labelOverride;
        e.totalText.text = $"{label}: {total}";
    }

    public void Flash(ResourceType type, int delta)
    {
        if (delta == 0) return;
        if (!_entry.TryGetValue(type, out var e) || e.popupText == null) return;

        var rt = _rt[type];
        rt.pendingDelta += delta;

        if (rt.routine == null)
            rt.routine = StartCoroutine(CoFlash(type, e, rt));
        else
            rt.t = 0f; // Reset Zeit, damit Verlängerung bei erneutem Einsammeln spürbar ist
    }

    // --- 3) CoFlash: pending -> aggregate addieren und AGGREGAT anzeigen
    private IEnumerator CoFlash(ResourceType type, ResourceEntry e, Runtime rt)
    {
        var tr = e.popupText.rectTransform;
        e.popupText.gameObject.SetActive(true);

        float dur = Mathf.Max(0.05f, flashDuration);
        rt.t = 0f;

        // WICHTIG: Aggregat beim Start leeren
        rt.aggregateDelta = 0;

        while (rt.t < dur || rt.pendingDelta != 0)
        {
            // neue Deltas addieren und den AGGREGAT-Wert anzeigen
            if (rt.pendingDelta != 0)
            {
                rt.aggregateDelta += rt.pendingDelta; // <— hier wird aufsummiert
                rt.pendingDelta = 0;

                int show = rt.aggregateDelta;
                e.popupText.text = show > 0 ? $"+{show}" : $"{show}";

                // „Re-Pop“ Effekt, damit es sich frisch anfühlt
                rt.t = Mathf.Min(rt.t, dur * 0.35f);
            }

            float u = Mathf.Clamp01(rt.t / dur);
            float k = ease != null ? ease.Evaluate(u) : u;

            if (e.popupGroup != null) e.popupGroup.alpha = 1f - k;
            else e.popupText.alpha = 1f - k;

            float s = Mathf.Lerp(popupScaleFrom, popupScaleTo, k);
            tr.localScale = Vector3.one * s;

            rt.t += Time.deltaTime;
            yield return null;
        }

        // Ende: zurücksetzen
        if (e.popupGroup != null) e.popupGroup.alpha = 0f;
        else e.popupText.alpha = 0f;

        e.popupText.gameObject.SetActive(false);
        rt.aggregateDelta = 0; // <— Aggregat leeren
        rt.routine = null;
    }

}
