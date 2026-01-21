using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponListItemUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private Button button;

    private WeaponDefinition def;
    private HubUIController hub;

    float lastClickTime;
    const float doubleClickWindow = 0.28f;

    public void Init(HubUIController hub, WeaponDefinition def)
    {
        this.hub = hub;
        this.def = def;

        if (icon) icon.sprite = def.uiIcon;
        if (nameText) nameText.text = def.displayName;

        if (!button) button = GetComponent<Button>();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            hub.SelectWeapon(def);

            float t = Time.unscaledTime;
            if (t - lastClickTime < doubleClickWindow)
            {
                hub.QuickEquipSelected();
            }
            lastClickTime = t;
        });

        Refresh();
    }

    public void Refresh()
    {
        bool unlocked = MetaProgression.I.IsUnlocked(def.id);

        if (stateText)
        {
            stateText.text = unlocked ? "UNLOCKED" : $"LOCKED ({def.unlockCost})";
        }

        // Optional: Icon abdunkeln wenn locked
        if (icon)
        {
            var c = icon.color;
            c.a = unlocked ? 1f : 0.35f;
            icon.color = c;
        }
    }
}
