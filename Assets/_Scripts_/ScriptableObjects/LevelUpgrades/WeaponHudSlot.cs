using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponHudSlot : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("Optional: explizites Root-Objekt. Wenn leer, wird dieses GameObject genutzt.")]
    public GameObject root;

    [Header("UI Refs")]
    public Image icon;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI dpsText;

    [Header("Highlight (optional)")]
    public Image highlightFrame;
    public Color highlightColor = Color.white;

    [Tooltip("Farbe, wenn nicht gehighlighted (nur falls highlightFrame genutzt wird).")]
    public Color highlightOffColor = new Color(1f, 1f, 1f, 0f);

    public WeaponDefinition CurrentDef { get; private set; }

    GameObject RootGO => root != null ? root : gameObject;

    public void Set(WeaponDefinition def, int level, int maxLevel, float dps)
    {
        CurrentDef = def;

        if (def == null || level <= 0)
        {
            Clear();
            return;
        }

        RootGO.SetActive(true);

        if (icon)
        {
            if (def.uiIcon)
            {
                icon.sprite = def.uiIcon;
                icon.enabled = true;
            }
            else
            {
                icon.enabled = false;
            }
        }

        if (nameText)
            nameText.text = string.IsNullOrEmpty(def.displayName) ? "Weapon" : def.displayName;

        if (levelText)
            levelText.text = $"Lv {level}/{maxLevel}";

        if (dpsText)
            dpsText.text = $"{dps:0.#} DPS";
    }

    public void Clear()
    {
        CurrentDef = null;
        RootGO.SetActive(false);

        if (dpsText) dpsText.text = "";
        SetHighlighted(false);
    }

    public void SetHighlighted(bool on)
    {
        if (!highlightFrame) return;

        highlightFrame.color = on ? highlightColor : highlightOffColor;
    }
}
