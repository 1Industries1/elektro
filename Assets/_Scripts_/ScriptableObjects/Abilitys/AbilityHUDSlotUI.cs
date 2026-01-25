using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbilityHUDSlotUI : MonoBehaviour
{
    [Header("UI")]
    public Image icon;
    public Image cooldownFill;          // Image Type = Filled
    public TextMeshProUGUI cooldownText;
    public TextMeshProUGUI keyText;

    [Header("Config")]
    public float showTextUnderSeconds = 9.9f; // nur anzeigen wenn < x Sekunden

    private int lastShownSeconds = -1;
    private float lastFill = -1f;

    public void SetKey(string k)
    {
        if (keyText) keyText.text = k;
    }

    public void SetAbility(AbilityDefinition def)
    {
        if (icon)
        {
            icon.sprite = def ? def.uiIcon : null;
            icon.enabled = def && def.uiIcon != null;
            icon.color = Color.white;
        }

        if (cooldownFill)
        {
            cooldownFill.fillAmount = 0f;
            cooldownFill.enabled = false;
        }

        if (cooldownText)
        {
            cooldownText.text = "";
            cooldownText.enabled = false;
        }
    }

    /// <param name="remaining">Sekunden bis ready</param>
    /// <param name="cooldownTotal">Gesamt-CD (für Fill)</param>
    public void SetCooldown(float remaining, float cooldownTotal)
    {
        bool cooling = remaining > 0.01f;

        if (cooldownFill)
        {
            float fill = 0f;
            if (cooling && cooldownTotal > 0.01f)
                fill = 1f - Mathf.Clamp01(remaining / cooldownTotal);

            cooldownFill.enabled = cooling;

            if (!Mathf.Approximately(fill, lastFill))
            {
                lastFill = fill;
                cooldownFill.fillAmount = fill;
            }
        }

        if (cooldownText)
        {
            bool show = cooling && remaining <= showTextUnderSeconds;
            cooldownText.enabled = show;

            if (show)
            {
                int s = Mathf.CeilToInt(remaining);
                if (s != lastShownSeconds)
                {
                    lastShownSeconds = s;
                    cooldownText.text = s.ToString();
                }
            }
            else
            {
                if (lastShownSeconds != -1)
                {
                    lastShownSeconds = -1;
                    cooldownText.text = "";
                }
            }
        }

        if (icon && icon.enabled)
        {
            var c = icon.color;
            c.a = cooling ? 0.6f : 1f;
            icon.color = c;
        }
    }
}
