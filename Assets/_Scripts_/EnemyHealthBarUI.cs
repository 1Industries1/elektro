using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBarUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EnemyEliteMech enemy;     // Referenz auf den Mech
    [SerializeField] private Image fillImage;          // UI-Image, Type = Filled, Fill Method = Horizontal
    [SerializeField] private Canvas rootCanvas;        // World-Space Canvas (optional, wird sonst automatisch geholt)

    [Header("Behavior")]
    [SerializeField] private bool hideWhenFull = false;
    [SerializeField] private float minAlphaWhenFull = 0f; // 0 = komplett weg, z.B. 0.2 für leichte Anzeige

    private void Awake()
    {
        if (!rootCanvas) rootCanvas = GetComponentInChildren<Canvas>(true);
        if (!enemy) enemy = GetComponentInParent<EnemyEliteMech>();
    }

    private void OnEnable()
    {
        if (enemy != null)
        {
            enemy.OnHealthChanged01 += OnEnemyHealthChanged01;
            Apply(enemy.Health01);
        }
    }

    private void OnDisable()
    {
        if (enemy != null)
            enemy.OnHealthChanged01 -= OnEnemyHealthChanged01;
    }

    private void OnEnemyHealthChanged01(float old01, float new01)
    {
        Apply(new01);
    }

    private void Apply(float v01)
    {
        if (fillImage) fillImage.fillAmount = Mathf.Clamp01(v01);

        if (hideWhenFull && rootCanvas)
        {
            var cg = rootCanvas.GetComponent<CanvasGroup>();
            if (!cg) cg = rootCanvas.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = (v01 >= 0.999f) ? minAlphaWhenFull : 1f;
        }
    }
}
