using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class BeamController : MonoBehaviour
{
    private LineRenderer _lr;
    private Vector3 _a, _b;

    [Header("Jitter")]
    public int segments = 12;
    public float noiseAmplitude = 0.02f;
    public float noiseSpeed = 3f;

    private void Awake() => _lr = GetComponent<LineRenderer>();

    public void SetEndpoints(Vector3 from, Vector3 to, float width)
    {
        _a = from; _b = to;
        if (!_lr) return;

        _lr.positionCount = segments;
        _lr.startWidth = _lr.endWidth = width;

        float t = Time.time * noiseSpeed;
        for (int i = 0; i < segments; i++)
        {
            float u = i / (segments - 1f);
            Vector3 p = Vector3.Lerp(_a, _b, u);

            // seitliches Rauschen (senkrecht zur Verbindungsrichtung)
            Vector3 dir = (_b - _a).normalized;
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
            if (side == Vector3.zero) side = Vector3.Cross(dir, Vector3.right).normalized;

            float n = (Mathf.PerlinNoise(u * 3f, t) - 0.5f) * 2f;
            p += side * (n * noiseAmplitude * (1f - Mathf.Abs(0.5f - u) * 2f)); // Mitte stärker

            _lr.SetPosition(i, p);
        }
    }
}
