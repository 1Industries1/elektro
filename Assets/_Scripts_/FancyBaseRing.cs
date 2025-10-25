using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class FancyBaseRing : MonoBehaviour
{
    [Header("Shape")]
    [Min(0.01f)] public float radius = 6f;     // Kreis-Radius (XZ-Ebene)
    [Range(16, 256)] public int segments = 96;  // Glätte
    [Min(0.001f)] public float thickness = 0.15f;

    [Header("Animation")]
    public bool spin = true;
    public float spinDegPerSec = 15f;
    public bool pulse = true;
    public float pulseAmplitude = 0.12f;   // +/- Anteil an thickness
    public float pulseHz = 1.5f;

    [Header("Style")]
    public Gradient color = new Gradient
    {
        colorKeys = new[] {
            new GradientColorKey(new Color(0f,1f,1f,1f), 0f),
            new GradientColorKey(new Color(0.2f,0.8f,1f,1f), 1f)
        },
        alphaKeys = new[] {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f)
        }
    };
    public AnimationCurve widthCurve = AnimationCurve.EaseInOut(0,1,1,1);

    [Header("Subtle Wobble")]
    public bool noiseWobble = true;
    public float wobbleAmplitude = 0.06f; // Anteil am Radius
    public float wobbleSpeed = 0.6f;
    public float wobbleScale = 1.2f;

    LineRenderer lr;
    Vector3[] pts;

    void OnEnable()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        ApplyStaticStyling();
        Rebuild();
    }

    void OnValidate()
    {
        if (!lr) lr = GetComponent<LineRenderer>();
        ApplyStaticStyling();
        Rebuild();
    }

    void ApplyStaticStyling()
    {
        lr.colorGradient = color;
        lr.widthCurve = widthCurve;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        // Tipp: Material auf Unlit + Additive setzen, Texture Mode = Repeat
    }

    void Update()
    {
        if (spin) transform.Rotate(Vector3.up, spinDegPerSec * Time.deltaTime, Space.World);

        // dynamische Breite (Pulse)
        float baseWidth = thickness;
        if (pulse)
        {
            float p = 1f + Mathf.Sin(Time.time * Mathf.PI * 2f * pulseHz) * pulseAmplitude;
            lr.widthMultiplier = baseWidth * p;
        }
        else lr.widthMultiplier = baseWidth;

        // optionales Wobble
        if (noiseWobble)
            UpdateWobble();
    }

    void Rebuild()
    {
        segments = Mathf.Clamp(segments, 16, 512);
        lr.positionCount = segments;
        pts = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            pts[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }
        lr.SetPositions(pts);
    }

    void UpdateWobble()
    {
        if (pts == null || pts.Length != segments) Rebuild();

        float t = Time.time * wobbleSpeed;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            float nx = Mathf.PerlinNoise(Mathf.Cos(a) * wobbleScale + 13.37f, t);
            float nz = Mathf.PerlinNoise(Mathf.Sin(a) * wobbleScale + 42.42f, t + 10f);
            float n = (nx + nz) * 0.5f; // 0..1
            float r = radius * (1f + (n - 0.5f) * 2f * wobbleAmplitude);
            pts[i] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }
        lr.SetPositions(pts);
    }

    // Convenience: Editor-Button-Ersatz
    [ContextMenu("Rebuild Now")]
    void RebuildNow() => Rebuild();
}
