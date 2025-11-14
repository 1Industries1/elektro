using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;

[DisallowMultipleComponent]
public class OverclockQuickslotHUD : MonoBehaviour
{
    [Header("UI-Referenzen (im Canvas)")]
    [SerializeField] private Image             slot0Icon;
    [SerializeField] private Image             slot1Icon;
    [SerializeField] private TextMeshProUGUI   slot0Charges;
    [SerializeField] private TextMeshProUGUI slot1Charges;
    
    [Header("Overclock-Dauer (optional)")]
    [SerializeField] private Slider overclockSlider;
    [SerializeField] private GameObject overclockSliderRoot; // Container, den du an/aus schaltest


    [Header("Overclock-Definitionen (aus deinem Projekt)")]
    [SerializeField] private List<OverclockDef> knownDefs = new();

    private OverclockRuntime rt;
    private bool subscribed;
    private bool overclockActive;
    private float overclockEndTime;
    private float overclockDuration;

    // interner Cache (id -> def), aus knownDefs gebaut
    private Dictionary<string, OverclockDef> _defsById;

    private void OnEnable()
    {
        BuildDefCacheIfNeeded();

        if (overclockSliderRoot == null && overclockSlider != null)
            overclockSliderRoot = overclockSlider.gameObject;

        if (overclockSliderRoot != null)
            overclockSliderRoot.SetActive(false);

        TryBindNow();
        if (rt == null)
            StartCoroutine(WaitAndBindLocalPlayer());

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void Update()
    {
        if (!overclockActive || overclockSlider == null) return;

        float remaining = Mathf.Max(0f, overclockEndTime - Time.time);
        overclockSlider.value = remaining;

        if (remaining <= 0f)
        {
            overclockActive = false;
            if (overclockSliderRoot != null)
                overclockSliderRoot.SetActive(false);
        }
    }



    private void OnDisable()
    {
        if (rt != null && subscribed)
        {
            rt.OnQuickslotsUpdated -= Refresh;
            rt.OnOverclockStarted  -= HandleOverclockStarted;
            subscribed = false;
        }
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }


    private void OnClientConnected(ulong _) => TryBindNow();

    private IEnumerator WaitAndBindLocalPlayer()
    {
        while (rt == null)
        {
            TryBindNow();
            yield return null;
        }
    }

    private void TryBindNow()
    {
        if (rt != null) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
            rt = nm.LocalClient.PlayerObject.GetComponent<OverclockRuntime>();

        if (rt == null)
        {
            foreach (var cand in FindObjectsOfType<OverclockRuntime>())
                if (cand.IsOwner) { rt = cand; break; }
        }

        if (rt != null && !subscribed)
        {
            rt.OnQuickslotsUpdated += Refresh;
            rt.OnOverclockStarted  += HandleOverclockStarted;
            subscribed = true;
            Refresh();
        }
    }

    private void Refresh()
    {
        if (rt == null)
        {
            SetSlot(slot0Icon, slot0Charges, null, 0);
            SetSlot(slot1Icon, slot1Charges, null, 0);
            return;
        }

        // Slot 0
        var def0 = ResolveDefById(rt.clientSlotId0);
        int c0 = (!string.IsNullOrEmpty(rt.clientSlotId0) &&
                  rt.clientQuickCharges.TryGetValue(rt.clientSlotId0, out var tmp0)) ? tmp0 : 0;
        SetSlot(slot0Icon, slot0Charges, def0 ? def0.icon : null, c0);

        // Slot 1
        var def1 = ResolveDefById(rt.clientSlotId1);
        int c1 = (!string.IsNullOrEmpty(rt.clientSlotId1) &&
                  rt.clientQuickCharges.TryGetValue(rt.clientSlotId1, out var tmp1)) ? tmp1 : 0;
        SetSlot(slot1Icon, slot1Charges, def1 ? def1.icon : null, c1);
    }

    private static void SetSlot(Image icon, TextMeshProUGUI chargesText, Sprite sprite, int charges)
    {
        if (icon)
        {
            icon.sprite = sprite;
            icon.enabled = (sprite != null);   // aktiviert das Image automatisch, wenn ein Icon gefunden wurde
        }
        if (chargesText)
            chargesText.text = (sprite != null && charges > 0) ? charges.ToString() : "";
    }

    // ===== Defs ohne Resources laden: direkt aus Inspector-Liste =====
    private void BuildDefCacheIfNeeded()
    {
        if (_defsById != null) return;
        _defsById = new Dictionary<string, OverclockDef>(knownDefs != null ? knownDefs.Count : 0);

        if (knownDefs != null)
        {
            for (int i = 0; i < knownDefs.Count; i++)
            {
                var def = knownDefs[i];
                if (def != null && !string.IsNullOrEmpty(def.id))
                    _defsById[def.id] = def;
            }
        }
    }

    private void HandleOverclockStarted(float dur)
    {
        if (overclockSlider == null) return;

        overclockDuration = Mathf.Max(0.01f, dur);
        overclockEndTime = Time.time + overclockDuration;
        overclockActive = true;

        overclockSlider.minValue = 0f;
        overclockSlider.maxValue = overclockDuration;
        overclockSlider.value    = overclockDuration; // startet voll

        if (overclockSliderRoot != null)
            overclockSliderRoot.SetActive(true);
    }


    private OverclockDef ResolveDefById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        BuildDefCacheIfNeeded();
        return _defsById != null && _defsById.TryGetValue(id, out var def) ? def : null;
    }
}
