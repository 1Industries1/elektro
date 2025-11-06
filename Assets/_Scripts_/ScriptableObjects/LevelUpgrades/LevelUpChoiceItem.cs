using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelUpChoiceItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Button button;
    public Image rarityFrame;
    public Image rarityStampIcon;      // small emblem (optional)
    public TextMeshProUGUI title;      // Titel (Upgrade-/Mastery-Name)
    public TextMeshProUGUI subtitle;   // Beschreibung
    public TextMeshProUGUI badgeText;  // "Common", "Rare", "Epic", ...

    private int _choiceId;
    private LevelUpUI _ui;
    private Action<int> _onPick;

    /// <summary>
    /// Bindet diese Karte an eine Choice-ID.
    /// </summary>
    public void Bind(
        int choiceId,
        string descText,
        Action<int> onPick,
        PlayerUpgrades upgrades,
        Func<string, string> localize = null)
    {
        _choiceId = choiceId;
        _onPick   = onPick;

        // ViewModel aus UpgradeRoller holen – inkl. Mastery-Handling
        var vm = UpgradeRoller.GetChoiceViewModel(choiceId, upgrades, localize);

        if (title)
        {
            title.text  = vm.upgradeName;
            title.color = vm.rarityColor;
        }

        if (subtitle)
            subtitle.text = descText;

        if (badgeText)
        {
            badgeText.text  = vm.badgeText;
            badgeText.color = vm.rarityColor;
        }

        if (rarityFrame)
            rarityFrame.color = vm.rarityColor;

        if (rarityStampIcon && vm.rarityIcon)
            rarityStampIcon.sprite = vm.rarityIcon;

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _onPick?.Invoke(_choiceId));
            button.interactable = true;
        }
    }

    public void SetPreviewHook(LevelUpUI ui) => _ui = ui;

    public void SetInteractable(bool enabled)
    {
        if (button) button.interactable = enabled;
    }

    public void OnPointerEnter(PointerEventData _)
    {
        _ui?.ShowPreviewForChoice(_choiceId);
    }

    public void OnPointerExit(PointerEventData _)
    {
        _ui?.ClearPreview();
    }
}
