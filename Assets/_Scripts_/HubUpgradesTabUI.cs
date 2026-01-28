using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HubUpgradesTabUI : MonoBehaviour
{
    [Header("PlayerXP Formula Source")]
    public PlayerXPFormula formula; // Scriptable/config oder einfach Werte hier rein

    [Header("UI")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI xpText;
    public Slider xpSlider; // optional

    public void Refresh()
    {
        if (MetaProgression.I == null || MetaProgression.I.Data == null) return;

        int lvl = MetaProgression.I.Data.metaLevel;
        int xp  = MetaProgression.I.Data.metaXP;

        int toNext = MetaProgression.I.GetMetaXpToNext(formula.baseCost, formula.costMult);

        if (levelText) levelText.text = $"{lvl}";
        if (xpText) xpText.text = $"XP {xp} / {toNext}";

        if (xpSlider)
        {
            xpSlider.minValue = 0;
            xpSlider.maxValue = toNext;
            xpSlider.value = xp;
        }
    }
}

// WERTE AUCH WOANDERST ÄNDERN?
[System.Serializable]
public class PlayerXPFormula
{
    public int baseCost = 10;
    public float costMult = 1.12f;
}
