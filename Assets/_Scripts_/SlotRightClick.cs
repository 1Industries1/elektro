using UnityEngine;
using UnityEngine.EventSystems;

public class SlotRightClick : MonoBehaviour, IPointerClickHandler
{
    public LoadoutSlotUI slotUI;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
            slotUI?.OnRightClick();
    }
}
