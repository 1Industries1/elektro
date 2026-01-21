using System.Collections;
using TMPro;
using UnityEngine;

public class ToastUI : MonoBehaviour
{
    public static ToastUI I { get; private set; }

    [SerializeField] private CanvasGroup group;
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private float showTime = 1.3f;

    private Coroutine co;

    private void Awake()
    {
        I = this;
        if (!group) group = GetComponent<CanvasGroup>();
        if (group) group.alpha = 0f;
    }

    public void Show(string msg)
    {
        if (!group || !text) return;

        text.text = msg;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(Co());
    }

    private IEnumerator Co()
    {
        group.alpha = 1f;
        yield return new WaitForSeconds(showTime);
        group.alpha = 0f;
        co = null;
    }
}
