using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public enum LoadoutSlot { A1, A2, P1, P2, B1, B2 }

public class LoadoutSlotUI : MonoBehaviour
{
    public LoadoutSlot slot;

    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Button button;
    [SerializeField] private Image frame; // optional: ein Image als Rahmen/Background des Buttons
    Coroutine flashCo;


    private HubUIController hub;

    public void Init(HubUIController hub)
    {
        this.hub = hub;

        if (!button) button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (slot == LoadoutSlot.B1 || slot == LoadoutSlot.B2)
                hub.TryEquipSelectedAbilityTo(slot);
            else
                hub.TryEquipSelectedTo(slot);
        });
    }

    public void SetEmpty()
    {
        if (icon) icon.sprite = null;
        if (icon) icon.enabled = false;
        if (label) label.text = slot.ToString();
    }

    public void SetWeapon(WeaponDefinition def)
    {
        if (icon)
        {
            icon.sprite = def.uiIcon;
            icon.enabled = def.uiIcon != null;
        }
        if (label) label.text = $"{slot}: {def.displayName}";
    }

    public void SetAbility(AbilityDefinition def)
    {
        if (icon)
        {
            icon.sprite = def.uiIcon;
            icon.enabled = def.uiIcon != null;
        }
        if (label) label.text = $"{slot}: {def.displayName}";
    }


    public void OnRightClick()
    {
        hub?.ClearSlot(slot);
    }

    public void FlashError()
    {
        if (!frame) return;
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(CoFlash());
    }

    IEnumerator CoFlash()
    {
        var baseCol = frame.color;
        frame.color = new Color(1f, 0.3f, 0.3f, baseCol.a);
        yield return new WaitForSeconds(0.15f);
        frame.color = baseCol;
        flashCo = null;
    }

}
