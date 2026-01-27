using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum HubTab { Weapons, Abilities, Upgrades, Shop }

public class HubUIController : MonoBehaviour
{
    [Header("Refs")]
    public WeaponRegistry registry;
    public AbilityRegistry abilityRegistry;

    [Header("Panels")]
    public GameObject weaponsPanel;
    public GameObject abilitiesPanel;
    public GameObject upgradesPanel;
    public GameObject shopPanel;

    [Header("Tabs")]
    public HubTab startTab = HubTab.Weapons;

    [Header("Tab Buttons (ColorTint Highlight)")]
    public Button weaponsTabButton;
    public Button abilitiesTabButton;
    public Button upgradesTabButton;
    public Button shopTabButton;

    [Tooltip("Multiplies the button's normal/selected colors when active (>= 1.0).")]
    [Range(1f, 2f)] public float activeTintMultiplier = 1.25f;

    [Header("Details Panel Root")]
    public GameObject detailsPanelRoot; // Parent vom Details-UI (Icon/Name/Info/BuyButton etc.)

    [Header("Weapon List")]
    public Transform listContent;
    public WeaponListItemUI itemPrefab;

    [Header("Ability List")]
    public Transform abilityListContent;
    public AbilityListItemUI abilityItemPrefab;

    [Header("Upgrades Tab UI")]
    public HubUpgradesTabUI upgradesTabUI;

    [Header("Currency")]
    public TextMeshProUGUI currencyText;

    [Header("Details")]
    public Image detailsIcon;
    public TextMeshProUGUI detailsName;
    public TextMeshProUGUI detailsInfo;
    public Button buyButton;
    public TextMeshProUGUI buyButtonText;

    [Header("Loadout Slots")]
    public LoadoutSlotUI slotA1;
    public LoadoutSlotUI slotA2;
    public LoadoutSlotUI slotP1;
    public LoadoutSlotUI slotP2;

    [Header("Ability Slots")]
    public LoadoutSlotUI slotB1;
    public LoadoutSlotUI slotB2;

    [Header("Run")]
    public string gameSceneName = "GameScene";

    private WeaponDefinition selectedWeapon;
    private AbilityDefinition selectedAbility;
    private HubTab currentTab;

    private readonly List<WeaponListItemUI> spawnedItems = new();
    private readonly List<AbilityListItemUI> spawnedAbilityItems = new();

    // Base colors per tab (so you can style each button differently if you want)
    private ColorBlock baseWeaponsColors;
    private ColorBlock baseAbilitiesColors;
    private ColorBlock baseUpgradesColors;
    private ColorBlock baseShopColors;

    private void Start()
    {
        if (!registry) registry = MetaProgression.I.registry;
        if (!abilityRegistry) abilityRegistry = MetaProgression.I.abilityRegistry;

        slotA1.Init(this);
        slotA2.Init(this);
        slotP1.Init(this);
        slotP2.Init(this);
        slotB1.Init(this);
        slotB2.Init(this);

        CacheTabButtonBaseColors();

        BuildWeaponList();
        BuildAbilityList();

        SetTab(startTab);
        RefreshAllUI();
    }

    private void CacheTabButtonBaseColors()
    {
        if (weaponsTabButton)  baseWeaponsColors  = weaponsTabButton.colors;
        if (abilitiesTabButton) baseAbilitiesColors = abilitiesTabButton.colors;
        if (upgradesTabButton) baseUpgradesColors = upgradesTabButton.colors;
        if (shopTabButton)     baseShopColors     = shopTabButton.colors;
    }

    // ===== Tab Buttons (für Unity Button OnClick) =====
    public void TabWeapons()  => SetTab(HubTab.Weapons);
    public void TabAbilities() => SetTab(HubTab.Abilities);
    public void TabUpgrades() => SetTab(HubTab.Upgrades);
    public void TabShop()     => SetTab(HubTab.Shop);

    public void SetTab(HubTab tab)
    {
        currentTab = tab;

        if (weaponsPanel) weaponsPanel.SetActive(tab == HubTab.Weapons);
        if (abilitiesPanel) abilitiesPanel.SetActive(tab == HubTab.Abilities);
        if (upgradesPanel) upgradesPanel.SetActive(tab == HubTab.Upgrades);
        if (shopPanel) shopPanel.SetActive(tab == HubTab.Shop);

        // Details nur für Weapons/Abilities
        bool showDetails = tab == HubTab.Weapons || tab == HubTab.Abilities;
        if (detailsPanelRoot) detailsPanelRoot.SetActive(showDetails);

        // Selections resetten beim Tabwechsel
        if (tab != HubTab.Weapons) selectedWeapon = null;
        if (tab != HubTab.Abilities) selectedAbility = null;

        if (tab == HubTab.Upgrades)
            upgradesTabUI?.Refresh();

        UpdateTabButtonsVisual();
        RefreshDetails();
    }

    private void UpdateTabButtonsVisual()
    {
        if (weaponsTabButton)
            weaponsTabButton.colors = (currentTab == HubTab.Weapons)
                ? MakeActive(baseWeaponsColors)
                : baseWeaponsColors;

        if (abilitiesTabButton)
            abilitiesTabButton.colors = (currentTab == HubTab.Abilities)
                ? MakeActive(baseAbilitiesColors)
                : baseAbilitiesColors;

        if (upgradesTabButton)
            upgradesTabButton.colors = (currentTab == HubTab.Upgrades)
                ? MakeActive(baseUpgradesColors)
                : baseUpgradesColors;

        if (shopTabButton)
            shopTabButton.colors = (currentTab == HubTab.Shop)
                ? MakeActive(baseShopColors)
                : baseShopColors;
    }

    private ColorBlock MakeActive(ColorBlock cb)
    {
        cb.normalColor   = MultiplyRGB(cb.normalColor, activeTintMultiplier);
        cb.selectedColor = MultiplyRGB(cb.selectedColor, activeTintMultiplier);
        return cb;
    }

    private static Color MultiplyRGB(Color c, float m)
    {
        return new Color(
            Mathf.Clamp01(c.r * m),
            Mathf.Clamp01(c.g * m),
            Mathf.Clamp01(c.b * m),
            c.a
        );
    }

    private void BuildWeaponList()
    {
        if (!listContent || !itemPrefab || registry == null) return;

        foreach (Transform child in listContent) Destroy(child.gameObject);
        spawnedItems.Clear();

        var sorted = registry.weapons
            .Where(w => w != null && !string.IsNullOrEmpty(w.id))
            .OrderBy(w => w.displayName)
            .ToList();

        foreach (var def in sorted)
        {
            var item = Instantiate(itemPrefab, listContent);
            item.Init(this, def);
            spawnedItems.Add(item);
        }
    }

    private void BuildAbilityList()
    {
        if (!abilityListContent || !abilityItemPrefab || abilityRegistry == null) return;

        foreach (Transform child in abilityListContent) Destroy(child.gameObject);
        spawnedAbilityItems.Clear();

        var sorted = abilityRegistry.abilities
            .Where(a => a != null && !string.IsNullOrEmpty(a.id))
            .OrderBy(a => a.displayName)
            .ToList();

        foreach (var def in sorted)
        {
            var item = Instantiate(abilityItemPrefab, abilityListContent);
            item.Init(this, def);
            spawnedAbilityItems.Add(item);
        }
    }

    public void SelectWeapon(WeaponDefinition def)
    {
        selectedWeapon = def;
        selectedAbility = null;
        RefreshDetails();
    }

    public void SelectAbility(AbilityDefinition def)
    {
        selectedAbility = def;
        selectedWeapon = null;
        RefreshDetails();
    }

    private void RefreshAllUI()
    {
        RefreshCurrency();
        RefreshDetails();
        RefreshLoadoutUI();

        foreach (var it in spawnedItems) it.Refresh();
        foreach (var it in spawnedAbilityItems) it.Refresh();

        if (currentTab == HubTab.Upgrades)
            upgradesTabUI?.Refresh();
    }

    private void RefreshCurrency()
    {
        if (currencyText)
            currencyText.text = $"Meta: {MetaProgression.I.Data.metaCurrency}";
    }

    private void RefreshDetails()
    {
        // Wenn Details-Panel ausgeblendet ist (Upgrades/Shop), nicht anfassen
        if (detailsPanelRoot && !detailsPanelRoot.activeSelf) return;

        if (!selectedWeapon && !selectedAbility)
        {
            if (detailsName) detailsName.text = "Select an item";
            if (detailsInfo) detailsInfo.text = "";
            if (detailsIcon) detailsIcon.enabled = false;
            if (buyButton) buyButton.gameObject.SetActive(false);
            return;
        }

        int money = MetaProgression.I.Data.metaCurrency;

        // ===== ABILITY =====
        if (selectedAbility)
        {
            bool unlocked = MetaProgression.I.IsAbilityUnlocked(selectedAbility.id);

            if (detailsIcon)
            {
                detailsIcon.sprite = selectedAbility.uiIcon;
                detailsIcon.enabled = selectedAbility.uiIcon != null;
            }

            if (detailsName) detailsName.text = selectedAbility.displayName;

            if (detailsInfo)
            {
                float cd = Mathf.Max(0f, selectedAbility.cooldownSeconds);
                float dur = selectedAbility.overclockEffect ? selectedAbility.overclockEffect.duration : 0f;

                detailsInfo.text =
                    $"Type: Ability\n" +
                    $"Cooldown: {cd:0.#}s\n" +
                    $"Duration: {dur:0.#}s\n\n" +
                    $"{selectedAbility.description}";
            }

            if (buyButton)
            {
                buyButton.gameObject.SetActive(!unlocked);
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(BuySelectedAbility);
                buyButton.interactable = money >= selectedAbility.unlockCost;
            }

            if (buyButtonText)
            {
                if (unlocked) buyButtonText.text = "Unlocked";
                else
                {
                    int cost = selectedAbility.unlockCost;
                    buyButtonText.text = (money >= cost)
                        ? $"Unlock ({cost})"
                        : $"Need {cost - money} more";
                }
            }

            return;
        }

        // ===== WEAPON =====
        bool wUnlocked = MetaProgression.I.IsUnlocked(selectedWeapon.id);

        if (detailsIcon)
        {
            detailsIcon.sprite = selectedWeapon.uiIcon;
            detailsIcon.enabled = selectedWeapon.uiIcon != null;
        }

        if (detailsName) detailsName.text = selectedWeapon.displayName;

        if (detailsInfo)
        {
            detailsInfo.text =
                $"Type: {selectedWeapon.slotKind}\n" +
                $"MaxLv: {1 + (selectedWeapon.steps?.Length ?? 0)}\n" +
                $"BaseDmg: {selectedWeapon.baseDamage}\n" +
                $"SPS: {selectedWeapon.shotsPerSecond}\n" +
                $"Pierce: {selectedWeapon.basePierce}\n";
        }

        if (buyButton)
        {
            buyButton.gameObject.SetActive(!wUnlocked);
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(BuySelectedWeapon);
            buyButton.interactable = money >= selectedWeapon.unlockCost;
        }

        if (buyButtonText)
        {
            if (wUnlocked) buyButtonText.text = "Unlocked";
            else
            {
                int cost = selectedWeapon.unlockCost;
                buyButtonText.text = (money >= cost)
                    ? $"Unlock ({cost})"
                    : $"Need {cost - money} more";
            }
        }
    }

    private void BuySelectedWeapon()
    {
        if (!selectedWeapon) return;

        var data = MetaProgression.I.Data;
        if (MetaProgression.I.IsUnlocked(selectedWeapon.id)) return;

        if (data.metaCurrency < selectedWeapon.unlockCost)
        {
            ToastUI.I?.Show("Not enough meta currency");
            return;
        }

        ToastUI.I?.Show($"Unlocked {selectedWeapon.displayName}");

        data.metaCurrency -= selectedWeapon.unlockCost;
        MetaProgression.I.UnlockWeapon(selectedWeapon.id);
        MetaProgression.I.Save();

        RefreshAllUI();
    }

    private void BuySelectedAbility()
    {
        if (!selectedAbility) return;

        var data = MetaProgression.I.Data;
        if (MetaProgression.I.IsAbilityUnlocked(selectedAbility.id)) return;

        if (data.metaCurrency < selectedAbility.unlockCost)
        {
            ToastUI.I?.Show("Not enough meta currency");
            return;
        }

        ToastUI.I?.Show($"Unlocked {selectedAbility.displayName}");

        data.metaCurrency -= selectedAbility.unlockCost;
        MetaProgression.I.UnlockAbility(selectedAbility.id);
        MetaProgression.I.Save();

        RefreshAllUI();
    }

    public void QuickEquipSelectedAbility()
    {
        if (!selectedAbility)
        {
            ToastUI.I?.Show("Select an ability first");
            return;
        }

        if (!MetaProgression.I.IsAbilityUnlocked(selectedAbility.id))
        {
            ToastUI.I?.Show("Ability is locked");
            return;
        }

        var d = MetaProgression.I.Data;

        if (string.IsNullOrEmpty(d.ability1)) d.ability1 = selectedAbility.id;
        else if (string.IsNullOrEmpty(d.ability2)) d.ability2 = selectedAbility.id;
        else d.ability2 = selectedAbility.id;

        if (d.ability1 == d.ability2) d.ability1 = null;

        MetaProgression.I.Save();
        RefreshLoadoutUI();
        ToastUI.I?.Show($"Equipped {selectedAbility.displayName}");
    }

    // ===== Loadout =====
    public void TryEquipSelectedTo(LoadoutSlot slot)
    {
        if (!selectedWeapon)
        {
            ToastUI.I?.Show("Select a weapon first");
            return;
        }

        if (!MetaProgression.I.IsUnlocked(selectedWeapon.id))
        {
            ToastUI.I?.Show("Weapon is locked");
            FlashSlot(slot);
            return;
        }

        bool wantsActive = (slot == LoadoutSlot.A1 || slot == LoadoutSlot.A2);

        if (wantsActive && selectedWeapon.slotKind != WeaponSlotKind.Active)
        {
            ToastUI.I?.Show("That weapon is PASSIVE. Use P1/P2.");
            FlashSlot(slot);
            return;
        }
        if (!wantsActive && selectedWeapon.slotKind != WeaponSlotKind.Passive)
        {
            ToastUI.I?.Show("That weapon is ACTIVE. Use A1/A2.");
            FlashSlot(slot);
            return;
        }

        var d = MetaProgression.I.Data;

        bool wasSomewhereElse = IsInAnyOtherSlot(selectedWeapon.id, slot);
        RemoveWeaponFromOtherSlots(selectedWeapon.id, slot);

        switch (slot)
        {
            case LoadoutSlot.A1: d.active1 = selectedWeapon.id; break;
            case LoadoutSlot.A2: d.active2 = selectedWeapon.id; break;
            case LoadoutSlot.P1: d.passive1 = selectedWeapon.id; break;
            case LoadoutSlot.P2: d.passive2 = selectedWeapon.id; break;
        }

        MetaProgression.I.Save();
        RefreshLoadoutUI();

        ToastUI.I?.Show(wasSomewhereElse
            ? $"Moved {selectedWeapon.displayName} to {slot}"
            : $"Equipped {selectedWeapon.displayName} in {slot}");
    }

    public void TryEquipSelectedAbilityTo(LoadoutSlot slot)
    {
        if (!selectedAbility)
        {
            ToastUI.I?.Show("Select an ability first");
            return;
        }

        if (!MetaProgression.I.IsAbilityUnlocked(selectedAbility.id))
        {
            ToastUI.I?.Show("Ability is locked");
            FlashSlot(slot);
            return;
        }

        if (slot != LoadoutSlot.B1 && slot != LoadoutSlot.B2)
        {
            ToastUI.I?.Show("Use B1/B2 for abilities.");
            FlashSlot(slot);
            return;
        }

        var d = MetaProgression.I.Data;

        if (slot != LoadoutSlot.B1 && d.ability1 == selectedAbility.id) d.ability1 = null;
        if (slot != LoadoutSlot.B2 && d.ability2 == selectedAbility.id) d.ability2 = null;

        if (slot == LoadoutSlot.B1) d.ability1 = selectedAbility.id;
        if (slot == LoadoutSlot.B2) d.ability2 = selectedAbility.id;

        MetaProgression.I.Save();
        RefreshLoadoutUI();

        ToastUI.I?.Show($"Equipped {selectedAbility.displayName} in {slot}");
    }

    private bool IsInAnyOtherSlot(string weaponId, LoadoutSlot targetSlot)
    {
        var d = MetaProgression.I.Data;
        if (string.IsNullOrEmpty(weaponId)) return false;

        if (targetSlot != LoadoutSlot.A1 && d.active1 == weaponId) return true;
        if (targetSlot != LoadoutSlot.A2 && d.active2 == weaponId) return true;
        if (targetSlot != LoadoutSlot.P1 && d.passive1 == weaponId) return true;
        if (targetSlot != LoadoutSlot.P2 && d.passive2 == weaponId) return true;
        return false;
    }

    private void FlashSlot(LoadoutSlot slot)
    {
        switch (slot)
        {
            case LoadoutSlot.A1: slotA1?.FlashError(); break;
            case LoadoutSlot.A2: slotA2?.FlashError(); break;
            case LoadoutSlot.P1: slotP1?.FlashError(); break;
            case LoadoutSlot.P2: slotP2?.FlashError(); break;
            case LoadoutSlot.B1: slotB1?.FlashError(); break;
            case LoadoutSlot.B2: slotB2?.FlashError(); break;
        }
    }

    public void QuickEquipSelected()
    {
        if (!selectedWeapon)
        {
            ToastUI.I?.Show("Select a weapon first");
            return;
        }

        if (!MetaProgression.I.IsUnlocked(selectedWeapon.id))
        {
            ToastUI.I?.Show("Weapon is locked");
            return;
        }

        if (selectedWeapon.slotKind == WeaponSlotKind.Active)
        {
            var d = MetaProgression.I.Data;
            if (string.IsNullOrEmpty(d.active1)) { TryEquipSelectedTo(LoadoutSlot.A1); return; }
            if (string.IsNullOrEmpty(d.active2)) { TryEquipSelectedTo(LoadoutSlot.A2); return; }
            TryEquipSelectedTo(LoadoutSlot.A2);
        }
        else
        {
            var d = MetaProgression.I.Data;
            if (string.IsNullOrEmpty(d.passive1)) { TryEquipSelectedTo(LoadoutSlot.P1); return; }
            if (string.IsNullOrEmpty(d.passive2)) { TryEquipSelectedTo(LoadoutSlot.P2); return; }
            TryEquipSelectedTo(LoadoutSlot.P2);
        }
    }

    private void RemoveWeaponFromOtherSlots(string weaponId, LoadoutSlot targetSlot)
    {
        var d = MetaProgression.I.Data;
        if (string.IsNullOrEmpty(weaponId)) return;

        if (targetSlot != LoadoutSlot.A1 && d.active1 == weaponId) d.active1 = null;
        if (targetSlot != LoadoutSlot.A2 && d.active2 == weaponId) d.active2 = null;
        if (targetSlot != LoadoutSlot.P1 && d.passive1 == weaponId) d.passive1 = null;
        if (targetSlot != LoadoutSlot.P2 && d.passive2 == weaponId) d.passive2 = null;
    }

    private void RefreshLoadoutUI()
    {
        var d = MetaProgression.I.Data;

        ApplySlot(slotA1, d.active1);
        ApplySlot(slotA2, d.active2);
        ApplySlot(slotP1, d.passive1);
        ApplySlot(slotP2, d.passive2);

        ApplyAbilitySlot(slotB1, d.ability1);
        ApplyAbilitySlot(slotB2, d.ability2);
    }

    private void ApplySlot(LoadoutSlotUI slotUI, string weaponId)
    {
        if (!slotUI) return;

        if (string.IsNullOrEmpty(weaponId))
        {
            slotUI.SetEmpty();
            return;
        }

        var def = registry.Get(weaponId);
        if (!def) { slotUI.SetEmpty(); return; }

        slotUI.SetWeapon(def);
    }

    private void ApplyAbilitySlot(LoadoutSlotUI slotUI, string abilityId)
    {
        if (!slotUI) return;

        if (string.IsNullOrEmpty(abilityId))
        {
            slotUI.SetEmpty();
            return;
        }

        var def = abilityRegistry != null ? abilityRegistry.Get(abilityId) : null;
        if (!def) { slotUI.SetEmpty(); return; }

        slotUI.SetAbility(def);
    }

    public void ClearSlot(LoadoutSlot slot)
    {
        var d = MetaProgression.I.Data;

        switch (slot)
        {
            case LoadoutSlot.A1: d.active1 = null; break;
            case LoadoutSlot.A2: d.active2 = null; break;
            case LoadoutSlot.P1: d.passive1 = null; break;
            case LoadoutSlot.P2: d.passive2 = null; break;
            case LoadoutSlot.B1: d.ability1 = null; break;
            case LoadoutSlot.B2: d.ability2 = null; break;
        }

        MetaProgression.I.Save();
        RefreshLoadoutUI();
        ToastUI.I?.Show($"{slot} cleared");
    }

    // ===== Buttons =====
    public void StartRun()
    {
        var d = MetaProgression.I.Data;
        if (string.IsNullOrEmpty(d.active1) && string.IsNullOrEmpty(d.active2))
        {
            Debug.LogWarning("Pick at least one active weapon.");
            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    public void BackToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
