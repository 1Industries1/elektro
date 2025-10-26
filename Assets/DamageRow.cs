using TMPro;
using UnityEngine;

public class DamageRow : MonoBehaviour
{
    public TextMeshProUGUI header;
    public TextMeshProUGUI primaryLine;
    public TextMeshProUGUI altLine;
    public Color nextColor = new Color(0.4f, 1f, 0.4f);

    public void Set(string headerText, string prim, string primNextOrNull, string alt, string altNextOrNull)
    {
        if (header) header.text = headerText;

        if (primaryLine)
        {
            primaryLine.text = prim + (string.IsNullOrEmpty(primNextOrNull) ? "" : $"  →  <color=#{ColorUtility.ToHtmlStringRGB(nextColor)}>{primNextOrNull}</color>");
        }
        if (altLine)
        {
            altLine.text = alt + (string.IsNullOrEmpty(altNextOrNull) ? "" : $"  →  <color=#{ColorUtility.ToHtmlStringRGB(nextColor)}>{altNextOrNull}</color>");
        }
    }
}
