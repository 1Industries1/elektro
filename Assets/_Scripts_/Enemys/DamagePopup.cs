using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text text;

    [Header("Motion")]
    public Vector3 startOffset = new Vector3(0, 0.6f, 0);
    public Vector3 moveDirection = new Vector3(0.15f, 1.2f, 0);
    public float moveSpeed = 2.0f;
    public float gravity = 2.5f;

    [Header("Lifetime")]
    public float lifetime = 0.9f;
    public AnimationCurve scaleOverLife = AnimationCurve.EaseInOut(0, 0.8f, 1, 1.1f);
    public AnimationCurve alphaOverLife = AnimationCurve.EaseInOut(0, 1f, 1, 0f);

    private float _t;
    private Color _baseColor;
    private Vector3 _vel;

    void Awake()
    {
        if (!text) text = GetComponentInChildren<TMP_Text>(true);
        if (text == null) Debug.LogWarning("[DamagePopup] TMP_Text fehlt!");
        else _baseColor = text.color;
    }

    // colorOverride und lifetimeOverride sind optional (nullable)
    public void Show(Vector3 worldPos, float amount, bool isCrit, Color? colorOverride = null, float? lifetimeOverride = null)
    {
        _t = 0f;
        transform.position = worldPos + startOffset;

        // Text & Größe
        if (text != null)
        {
            text.text = Mathf.RoundToInt(amount).ToString();
        }

        // Default-Farbe je nach Crit
        Color defaultColor = isCrit ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.3f, 0.3f);

        // FIX: kein .Value – Ergebnis von ?? ist bereits Color
        _baseColor = colorOverride ?? defaultColor;

        // Motion
        _vel = moveDirection.normalized * moveSpeed;

        if (lifetimeOverride.HasValue)
            lifetime = lifetimeOverride.Value;

        gameObject.SetActive(true);
    }

    void Update()
    {
        _t += Time.deltaTime;
        if (_t >= lifetime)
        {
            DamagePopupPool.Instance?.Release(this);
            return;
        }

        // einfache Parabelbewegung
        _vel += Vector3.down * gravity * Time.deltaTime;
        transform.position += _vel * Time.deltaTime;

        // Scale/Alpha über Zeit
        float n = Mathf.Clamp01(_t / lifetime);
        float scale = scaleOverLife.Evaluate(n);
        transform.localScale = Vector3.one * scale;

        if (text != null)
        {
            var col = _baseColor;
            col.a = Mathf.Clamp01(alphaOverLife.Evaluate(n));
            text.color = col;
        }
    }
}
