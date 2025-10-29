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
    [SerializeField] private TextMeshProUGUI   slot0Charges;   // TMP
    [SerializeField] private TextMeshProUGUI   slot1Charges;   // TMP

    [Header("Overclock-Definitionen (aus deinem Projekt)")]
    [Tooltip("Zieh hier deine OverclockDef-Assets aus Assets/_Scripts_/ScriptableObjects hinein.")]
    [SerializeField] private List<OverclockDef> knownDefs = new();   // <<— einfach im Inspector befüllen

    private OverclockRuntime rt;
    private bool subscribed;

    // interner Cache (id -> def), aus knownDefs gebaut
    private Dictionary<string, OverclockDef> _defsById;

    private void OnEnable()
    {
        BuildDefCacheIfNeeded();

        TryBindNow();
        if (rt == null)
            StartCoroutine(WaitAndBindLocalPlayer());

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDisable()
    {
        if (rt != null && subscribed)
        {
            rt.OnQuickslotsUpdated -= Refresh;
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

    private OverclockDef ResolveDefById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        BuildDefCacheIfNeeded();
        return _defsById != null && _defsById.TryGetValue(id, out var def) ? def : null;
    }
}
