using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelUpChoiceItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Refs")]
    public Button button;
    public Image rarityFrame;
    public Image rarityStampIcon;      // small emblem (optional)
    public TextMeshProUGUI title;      // Titel (Upgrade-/Mastery-Name)
    public TextMeshProUGUI subtitle;   // Beschreibung
    public TextMeshProUGUI badgeText;  // "Common", "Rare", "Epic", ...

    [Header("Type Icon Row")]
    public Image typeIcon;             // NEU: zeigt Stat / Weapon / Mastery-Symbol

    [Tooltip("Icon für generische Stat-Upgrades (MaxHP, Armor, Magnet, MoveSpeed, Stamina, ...)")]
    public Sprite statIcon;

    [Tooltip("Icon für Waffen-Upgrades (Cannon, Blaster, Grenade, Lightning, ...)")]
    public Sprite weaponIcon;

    [Tooltip("Icon für Masteries")]
    public Sprite masteryIcon;

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

        // ViewModel aus UpgradeRoller holen – inkl. Mastery-/Weapon-Handling
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

        // ===== Typ-Icon setzen (Stat / Weapon / Mastery) =====
        SetupTypeIcon(choiceId, upgrades);

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _onPick?.Invoke(_choiceId));
            button.interactable = true;
        }
    }

    private void SetupTypeIcon(int choiceId, PlayerUpgrades upgrades)
    {
        if (!typeIcon)
            return;

        int baseId = ChoiceCodec.BaseId(choiceId);

        bool isWeapon  = UpgradeRoller.IsWeaponBaseId(baseId);
        bool isMastery = UpgradeRoller.IsMasteryBaseId(baseId);
        // alles andere: Stat

        Sprite spriteToUse = null;

        if (isWeapon)
            spriteToUse = weaponIcon;
        else if (isMastery)
            spriteToUse = masteryIcon;
        else
            spriteToUse = statIcon;

        if (spriteToUse != null)
        {
            typeIcon.sprite = spriteToUse;
            typeIcon.enabled = true;
            typeIcon.gameObject.SetActive(true);
        }
        else
        {
            // falls kein Icon gesetzt → einfach ausblenden
            typeIcon.enabled = false;
            typeIcon.gameObject.SetActive(false);
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
