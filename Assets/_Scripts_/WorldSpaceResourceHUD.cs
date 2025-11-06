// Scripts/UI/WorldSpaceResourceHUD.cs
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
        public TextMeshProUGUI totalText;   // Zahl im Kreis (z. B. XP: 123)
        public TextMeshProUGUI popupText;   // "+5 XP" Popup

        [Header("Optional")]
        public CanvasGroup popupGroup;      // für Fade
        public string labelOverride;        // z. B. "Gold" statt enum.ToString()
    }

    [Header("Entries")]
    public List<ResourceEntry> entries = new();

    [Header("Gold-Preis (UI neben Gold-Kreis)")]
    [SerializeField] private TextMeshProUGUI goldPriceText; // Preisanzeige direkt neben dem Gold-Kreis

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
        public int pendingDelta = 0;
        public int aggregateDelta = 0;
        public Coroutine routine = null;
        public float t = 0f;
    }

    private readonly Dictionary<ResourceType, ResourceEntry> _entry = new();
    private readonly Dictionary<ResourceType, Runtime> _rt = new();

    private void Awake()
    {
        _entry.Clear();
        _rt.Clear();

        foreach (var e in entries)
        {
            if (e == null) continue;
            _entry[e.type] = e;
            _rt[e.type] = new Runtime();

            if (e.popupText != null)
            {
                e.popupText.gameObject.SetActive(false);
                if (e.popupGroup != null) e.popupGroup.alpha = 0f;
                else e.popupText.alpha = 0f;
            }
        }

        // Initial: Gold-Preis verbergen, falls nicht gesetzt
        if (goldPriceText) goldPriceText.gameObject.SetActive(!string.IsNullOrEmpty(goldPriceText.text));
    }

    private void OnEnable()
    {
        // Auf XP-Events hören (nur Owner-Client feuert)
        PlayerXP.OnLocalXPGain += HandleXPGain;
    }

    private void OnDisable()
    {
        PlayerXP.OnLocalXPGain -= HandleXPGain;
    }

    // ====== Public API ======

    public void SetTotal(ResourceType type, int total)
    {
        if (!_entry.TryGetValue(type, out var e) || e.totalText == null) return;

        // Priorität: Inspector-Override > Enum-Name
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
            rt.t = 0f; // Re-Pop
    }

    /// <summary>
    /// Setzt den aktuell zu zahlenden Goldpreis (z. B. Truhenpreis) neben den Gold-Kreis.
    /// </summary>
    public void SetChestPrice(int price)
    {
        if (!goldPriceText) return;
        goldPriceText.text = price > 0 ? price.ToString() : string.Empty;
        goldPriceText.gameObject.SetActive(price > 0);
    }

    // ====== Intern ======

    private System.Collections.IEnumerator CoFlash(ResourceType type, ResourceEntry e, Runtime rt)
    {
        var tr = e.popupText.rectTransform;
        e.popupText.gameObject.SetActive(true);

        // Suffix: Für XP " XP" anzeigen
        string popupSuffix = (type == ResourceType.XP) ? " XP" : "";

        float dur = Mathf.Max(0.05f, flashDuration);
        rt.t = 0f;
        rt.aggregateDelta = 0;

        while (rt.t < dur || rt.pendingDelta != 0)
        {
            if (rt.pendingDelta != 0)
            {
                rt.aggregateDelta += rt.pendingDelta;
                rt.pendingDelta = 0;

                int show = rt.aggregateDelta;
                e.popupText.text = show > 0 ? $"+{show}{popupSuffix}" : $"{show}{popupSuffix}";

                // Beim neuen Input kurz "frischer" erscheinen lassen
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

        if (e.popupGroup != null) e.popupGroup.alpha = 0f;
        else e.popupText.alpha = 0f;

        e.popupText.gameObject.SetActive(false);
        rt.aggregateDelta = 0;
        rt.routine = null;
    }

    // ====== Event-Handler für XP ======

    /// <summary>
    /// Reagiert auf lokale XP-Gewinne: Zahl im Kreis updaten und Popup anzeigen.
    /// Unterdrückt negatives Popup beim Level-Up (nur Reset).
    /// </summary>
    private void HandleXPGain(int delta, int totalInLevel, bool levelChanged)
    {
        SetTotal(ResourceType.XP, totalInLevel);

        // Kein negatives Popup, wenn es nur durch Level-Up-Reset entsteht
        if (delta > 0 || !levelChanged)
            Flash(ResourceType.XP, delta);
    }
}
