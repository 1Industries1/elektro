using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HubUIController : MonoBehaviour
{
    [Header("Refs")]
    public WeaponRegistry registry;

    [Header("List")]
    public Transform listContent;
    public WeaponListItemUI itemPrefab;

    [Header("Top")]
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

    [Header("Run")]
    public string gameSceneName = "GameScene";

    private WeaponDefinition selected;
    private readonly List<WeaponListItemUI> spawnedItems = new();

    private void Start()
    {
        if (!registry) registry = MetaProgression.I.registry;

        slotA1.Init(this);
        slotA2.Init(this);
        slotP1.Init(this);
        slotP2.Init(this);

        BuildList();
        RefreshAllUI();
    }

    private void BuildList()
    {
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

    public void SelectWeapon(WeaponDefinition def)
    {
        selected = def;
        RefreshDetails();
    }

    private void RefreshAllUI()
    {
        RefreshCurrency();
        RefreshDetails();
        RefreshLoadoutUI();

        foreach (var it in spawnedItems)
            it.Refresh();
    }

    private void RefreshCurrency()
    {
        if (currencyText)
            currencyText.text = $"Meta: {MetaProgression.I.Data.metaCurrency}";
    }

    private void RefreshDetails()
    {
        if (!selected)
        {
            if (detailsName) detailsName.text = "Select a weapon";
            if (detailsInfo) detailsInfo.text = "";
            if (detailsIcon) detailsIcon.enabled = false;
            if (buyButton) buyButton.gameObject.SetActive(false);
            return;
        }

        bool unlocked = MetaProgression.I.IsUnlocked(selected.id);
        int money = MetaProgression.I.Data.metaCurrency;

        if (detailsIcon)
        {
            detailsIcon.sprite = selected.uiIcon;
            detailsIcon.enabled = selected.uiIcon != null;
        }

        if (detailsName) detailsName.text = selected.displayName;

        if (detailsInfo)
        {
            detailsInfo.text =
                $"Type: {selected.slotKind}\n" +
                $"MaxLv: {1 + (selected.steps?.Length ?? 0)}\n" +
                $"BaseDmg: {selected.baseDamage}\n" +
                $"SPS: {selected.shotsPerSecond}\n" +
                $"Pierce: {selected.basePierce}\n";
        }

        if (buyButton)
        {
            buyButton.gameObject.SetActive(!unlocked);
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(BuySelected);

            bool canAfford = money >= selected.unlockCost;
            buyButton.interactable = canAfford;
        }

        if (buyButtonText)
        {
            if (unlocked) buyButtonText.text = "Unlocked";
            else
            {
                int cost = selected.unlockCost;
                buyButtonText.text = (MetaProgression.I.Data.metaCurrency >= cost)
                    ? $"Unlock ({cost})"
                    : $"Need {cost - MetaProgression.I.Data.metaCurrency} more";
            }
        }
    }

    private void BuySelected()
    {
        if (!selected) return;

        var data = MetaProgression.I.Data;
        if (MetaProgression.I.IsUnlocked(selected.id)) return;

        if (data.metaCurrency < selected.unlockCost)
        {
            ToastUI.I?.Show("Not enough meta currency");
            return;
        }

        ToastUI.I?.Show($"Unlocked {selected.displayName}");

        data.metaCurrency -= selected.unlockCost;
        MetaProgression.I.UnlockWeapon(selected.id);
        MetaProgression.I.Save();

        RefreshAllUI();
    }

    // ===== Loadout =====
    public void TryEquipSelectedTo(LoadoutSlot slot)
    {
        if (!selected)
        {
            ToastUI.I?.Show("Select a weapon first");
            return;
        }

        if (!MetaProgression.I.IsUnlocked(selected.id))
        {
            ToastUI.I?.Show("Weapon is locked");
            FlashSlot(slot);
            return;
        }

        bool wantsActive = (slot == LoadoutSlot.A1 || slot == LoadoutSlot.A2);

        if (wantsActive && selected.slotKind != WeaponSlotKind.Active)
        {
            ToastUI.I?.Show("That weapon is PASSIVE. Use P1/P2.");
            FlashSlot(slot);
            return;
        }
        if (!wantsActive && selected.slotKind != WeaponSlotKind.Passive)
        {
            ToastUI.I?.Show("That weapon is ACTIVE. Use A1/A2.");
            FlashSlot(slot);
            return;
        }

        var d = MetaProgression.I.Data;

        // Auto-Move: aus anderen Slots entfernen
        bool wasSomewhereElse = IsInAnyOtherSlot(selected.id, slot);
        RemoveWeaponFromOtherSlots(selected.id, slot);

        // Zielslot setzen
        switch (slot)
        {
            case LoadoutSlot.A1: d.active1 = selected.id; break;
            case LoadoutSlot.A2: d.active2 = selected.id; break;
            case LoadoutSlot.P1: d.passive1 = selected.id; break;
            case LoadoutSlot.P2: d.passive2 = selected.id; break;
        }

        MetaProgression.I.Save();
        RefreshLoadoutUI();

        ToastUI.I?.Show(wasSomewhereElse
            ? $"Moved {selected.displayName} to {slot}"
            : $"Equipped {selected.displayName} in {slot}");
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
        }
    }


    public void QuickEquipSelected()
    {
        if (!selected)
        {
            ToastUI.I?.Show("Select a weapon first");
            return;
        }

        if (!MetaProgression.I.IsUnlocked(selected.id))
        {
            ToastUI.I?.Show("Weapon is locked");
            return;
        }

        // Bestimme passende Slots
        if (selected.slotKind == WeaponSlotKind.Active)
        {
            var d = MetaProgression.I.Data;
            if (string.IsNullOrEmpty(d.active1)) { TryEquipSelectedTo(LoadoutSlot.A1); return; }
            if (string.IsNullOrEmpty(d.active2)) { TryEquipSelectedTo(LoadoutSlot.A2); return; }

            // beide belegt -> überschreibe A2 (oder zeig Auswahl)
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
    }

    private void ApplySlot(LoadoutSlotUI slotUI, string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId))
        {
            slotUI.SetEmpty();
            return;
        }

        var def = registry.Get(weaponId);
        if (!def) { slotUI.SetEmpty(); return; }

        slotUI.SetWeapon(def);
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
        }

        MetaProgression.I.Save();
        RefreshLoadoutUI();
        ToastUI.I?.Show($"{slot} cleared");
    }


    // ===== Buttons =====

    public void StartRun()
    {
        // Minimaler Check: mindestens 1 Active
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
