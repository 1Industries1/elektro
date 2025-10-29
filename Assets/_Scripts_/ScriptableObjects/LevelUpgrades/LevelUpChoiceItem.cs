using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;

public class LevelUpChoiceItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Button button;
    public Image rarityFrame;
    public Image rarityStampIcon;      // small emblem (optional)
    public TextMeshProUGUI title;      // was "label"
    public TextMeshProUGUI subtitle;   // was "desc"
    public TextMeshProUGUI badgeText;  // small "(I)" next to title

    private int _choiceId;
    private LevelUpUI _ui;
    private System.Action<int> _onPick;


    // LevelUpChoiceItem.cs
    public void Bind(int choiceId, string descText, Action<int> onPick, Func<string,string> localize = null)
    {
        _choiceId = choiceId;
        _onPick   = onPick;

        // ViewModel aus UpgradeRoller holen
        var vm = UpgradeRoller.GetChoiceViewModel(choiceId, localize);

        // Titel/Badge/Farbe setzen – passe die Feldnamen an deine Komponenten an
        if (title)       { title.text = vm.upgradeName; title.color = vm.rarityColor; }
        if (subtitle)    { subtitle.text = descText; }
        if (badgeText)   { badgeText.text = vm.badgeText; badgeText.color = vm.rarityColor; }
        if (rarityFrame) { rarityFrame.color = vm.rarityColor; }
        if (rarityStampIcon && vm.rarityIcon) rarityStampIcon.sprite = vm.rarityIcon;

        // Wenn du ein separates TMP für die Badge hast:
        // if (badgeText) { badgeText.text = vm.badgeText; badgeText.color = vm.rarityColor; }

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _onPick?.Invoke(_choiceId));
            button.interactable = true;
        }
    }


    public void SetPreviewHook(LevelUpUI ui) => _ui = ui;
    public void SetInteractable(bool enabled) { if (button) button.interactable = enabled; }
    public void OnPointerEnter(PointerEventData _) => _ui?.ShowPreviewForChoice(_choiceId);
    public void OnPointerExit (PointerEventData _) => _ui?.ClearPreview();
}
