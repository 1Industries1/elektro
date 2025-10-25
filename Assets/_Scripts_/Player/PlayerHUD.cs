using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlayerHUD : MonoBehaviour
{
    public static PlayerHUD Instance { get; private set; }

    [Header("Health UI (reuses former Battery refs)")]
    [SerializeField] private Slider batterySlider;          // now used for HP
    [SerializeField] private TextMeshProUGUI batteryText;   // now used for HP text

    [SerializeField] private Slider xpSlider;
    [SerializeField] private TextMeshProUGUI xpText;

    [Header("Feedback")]
    [SerializeField] private Image damageFlash;             // red full-screen flash
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private AnimationCurve flashCurve = AnimationCurve.EaseInOut(0,1,1,0);

    [SerializeField] private Image healFlash;               // optional green flash (assign in Inspector)
    [SerializeField] private float healFlashDuration = 0.18f;
    [SerializeField] private AnimationCurve healFlashCurve = AnimationCurve.EaseInOut(0,1,1,0);

    [SerializeField] private RectTransform hitIndicator;    // arrow at center
    [SerializeField] private float hitIndicatorTime = 0.6f;

    [SerializeField] private float shakeIntensity = 0.25f;
    [SerializeField] private float shakeTime = 0.12f;

    private CameraShake camShake;
    private Camera cam;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        cam = Camera.main;
        camShake = cam ? cam.GetComponent<CameraShake>() : null;

        // ensure flashes start invisible
        if (damageFlash) damageFlash.color = new Color(damageFlash.color.r, damageFlash.color.g, damageFlash.color.b, 0f);
        if (healFlash) healFlash.color = new Color(healFlash.color.r, healFlash.color.g, healFlash.color.b, 0f);

        if (hitIndicator)
        {
            var cg = hitIndicator.GetComponent<CanvasGroup>();
            if (!cg) cg = hitIndicator.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            hitIndicator.gameObject.SetActive(false);
        }
    }
    
    // =================== XP ===================

    public void SetXP(int level, int cur, int next)
    {
        if (xpSlider)
        {
            xpSlider.minValue = 0;
            xpSlider.maxValue = next;
            xpSlider.value = cur;
        }
        if (xpText)
            xpText.text = $"LV {level}  {cur}/{next}";
    }

    // =================== HEALTH UI ===================

    public void SetHealth(float hp, float max)
    {
        hp = Mathf.Max(0f, hp);
        max = Mathf.Max(1f, max);

        if (batterySlider)
        {
            batterySlider.minValue = 0f;
            batterySlider.maxValue = max;
            batterySlider.value = hp;
        }

        if (batteryText)
        {
            // Nur HP anzeigen (ohne Prozent / Status)
            batteryText.text = $"{hp:0}/{max:0} HP";
        }
}

    // =================== FEEDBACK ===================

    public void OnLocalPlayerHitHP(float damage, float newHP, float maxHP)
    {
        // HP-Balken
        SetHealth(newHP, maxHP);

        // Flash
        if (damageFlash) StartCoroutine(Flash(damageFlash, flashDuration, flashCurve));

        // Hit-Richtung (optional – falls du world dir/pos hast, kannst du hier erweitern)
        if (hitIndicator && cam) ShowHitIndicatorFromCameraForward(); // minimal: zeigt von vorne

        // Kamera-Feedback
        camShake?.Shake(shakeIntensity, shakeTime);
    }

    public void OnLocalPlayerHealHP(float heal, float newHP, float maxHP)
    {
        SetHealth(newHP, maxHP);
        if (healFlash) StartCoroutine(Flash(healFlash, healFlashDuration, healFlashCurve));
    }

    public void OnLocalPlayerDied()
    {
        // Optional: zeige „DEFEATED“, graue UI, etc.
        if (batteryText)
        {
            batteryText.text = "<size=120%><b>0%</b></size><alpha=#AA>DEFEATED\n<size=80%>0/??? HP</size>";
            batteryText.color = new Color(0.8f, 0.1f, 0.1f);
        }
        // Kleiner screen flash
        if (damageFlash) StartCoroutine(Flash(damageFlash, flashDuration * 2f, flashCurve));
    }

    // =================== INTERNAL ===================

    private IEnumerator Flash(Image img, float duration, AnimationCurve curve)
    {
        float t = 0f;
        var baseCol = img.color;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = curve.Evaluate(t / duration);
            img.color = new Color(baseCol.r, baseCol.g, baseCol.b, a);
            yield return null;
        }
        img.color = new Color(baseCol.r, baseCol.g, baseCol.b, 0f);
    }

    private void ShowHitIndicatorFromCameraForward()
    {
        // Ohne genaue Hit-Direction: kurzer Frontpfeil (alternativ: übergib dir world dir wie bisher)
        hitIndicator.gameObject.SetActive(true);
        hitIndicator.localRotation = Quaternion.identity;

        StopCoroutine(nameof(FadeHitIndicator));
        StartCoroutine(nameof(FadeHitIndicator));
    }

    public void ShowHitIndicator(Vector3 hitDirWorld)
    {
        if (!hitIndicator || !cam) return;

        // Richtung relativ zur Kamera auf die XZ-Ebene projizieren
        Vector3 fwd = cam.transform.forward; fwd.y = 0; fwd.Normalize();
        Vector3 right = cam.transform.right; right.y = 0; right.Normalize();

        Vector3 dir = hitDirWorld; dir.y = 0; dir.Normalize();
        float x = Vector3.Dot(dir, right);
        float y = Vector3.Dot(dir, fwd);

        float angle = Mathf.Atan2(x, y) * Mathf.Rad2Deg;
        hitIndicator.gameObject.SetActive(true);
        hitIndicator.localRotation = Quaternion.Euler(0, 0, -angle);

        StopCoroutine(nameof(FadeHitIndicator));
        StartCoroutine(nameof(FadeHitIndicator));
    }

    private IEnumerator FadeHitIndicator()
    {
        CanvasGroup cg = hitIndicator.GetComponent<CanvasGroup>();
        if (!cg) cg = hitIndicator.gameObject.AddComponent<CanvasGroup>();
        float t = 0f;
        while (t < hitIndicatorTime)
        {
            t += Time.deltaTime;
            float a = 1f - (t / hitIndicatorTime);
            cg.alpha = a;
            yield return null;
        }
        cg.alpha = 0f;
        hitIndicator.gameObject.SetActive(false);
    }

    // =================== BACKWARD COMPAT ===================
    // Falls es noch Battery-Aufrufe gibt, leiten wir sie auf Health um.

    public void SetBatteryInstant(float charge, float max) => SetHealth(charge, max);

    public void OnLocalPlayerHit(float damage, float newCharge, float maxCharge, Vector3 hitWorld, Vector3 hitDirWorld)
    {
        SetHealth(newCharge, maxCharge);
        if (damageFlash) StartCoroutine(Flash(damageFlash, flashDuration, flashCurve));
        if (hitIndicator && cam) ShowHitIndicator(hitDirWorld);
        camShake?.Shake(shakeIntensity, shakeTime);
    }
}
