using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatRow : MonoBehaviour
{
    [Header("Refs")]
    public TextMeshProUGUI label;
    public TextMeshProUGUI value;
    public TextMeshProUGUI nextValue; // optional, grün
    public Slider progress;           // optional

    [Header("Style")]
    public Color nextColor = new Color(0.4f, 1f, 0.4f); // #66FF66

    public void Set(string labelText, string valueText, string nextTextOrNull, float progress01 = -1f)
    {
        if (label) label.text = labelText;
        if (value) value.text = valueText;

        if (nextValue)
        {
            bool showNext = !string.IsNullOrEmpty(nextTextOrNull);
            nextValue.gameObject.SetActive(showNext);
            if (showNext)
            {
                nextValue.text = nextTextOrNull;
                nextValue.color = nextColor;
            }
        }

        if (progress)
        {
            bool showProg = progress01 >= 0f;
            progress.gameObject.SetActive(showProg);
            if (showProg) progress.value = Mathf.Clamp01(progress01);
        }
    }
}
