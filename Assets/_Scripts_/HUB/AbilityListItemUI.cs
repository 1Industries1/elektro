using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbilityListItemUI : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private Button button;

    private AbilityDefinition def;
    private HubUIController hub;

    float lastClickTime;
    const float doubleClickWindow = 0.28f;

    public void Init(HubUIController hub, AbilityDefinition def)
    {
        this.hub = hub;
        this.def = def;

        if (icon) icon.sprite = def.uiIcon;
        if (nameText) nameText.text = def.displayName;

        if (!button) button = GetComponent<Button>();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            hub.SelectAbility(def);

            float t = Time.unscaledTime;
            if (t - lastClickTime < doubleClickWindow)
                hub.QuickEquipSelectedAbility(); // kommt später (B1/B2), erstmal stub ok

            lastClickTime = t;
        });

        Refresh();
    }

    public void Refresh()
    {
        bool unlocked = MetaProgression.I.IsAbilityUnlocked(def.id);

        if (stateText)
            stateText.text = unlocked ? "UNLOCKED" : $"LOCKED ({def.unlockCost})";

        // Icon dimmen
        if (icon) SetAlpha(icon, unlocked ? 1f : 0.25f);

        // Hintergrund dimmen (meistens weniger stark als Icon)
        if (background) SetAlpha(background, unlocked ? 0f : 1f);
    }

    private static void SetAlpha(Image img, float a)
    {
        var c = img.color;
        c.a = a;
        img.color = c;
    }
}
