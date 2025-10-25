using UnityEngine;
using UnityEngine.UI;

public class HitmarkerUIController : MonoBehaviour
{
    public static HitmarkerUIController Instance;

    [SerializeField] private Image hitmarkerImage;
    [SerializeField] private float showDuration = 0.2f;

    private void Awake()
    {
        Instance = this;
        hitmarkerImage.enabled = false;
    }

    public void ShowHitmarker()
    {
        StopAllCoroutines();
        StartCoroutine(ShowCoroutine());
    }

    private System.Collections.IEnumerator ShowCoroutine()
    {
        hitmarkerImage.enabled = true;
        yield return new WaitForSeconds(showDuration);
        hitmarkerImage.enabled = false;
    }
}
