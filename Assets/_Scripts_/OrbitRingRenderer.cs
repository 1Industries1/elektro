using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class OrbitRingRenderer : MonoBehaviour
{
    public int segments = 64;
    public float width = 0.02f;
    public float radius = 1.6f;
    public Color color = new Color(0.6f, 0.9f, 1f, 0.5f);

    private LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.loop = true;
        lr.positionCount = segments;
        lr.widthMultiplier = width;
        lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.material.EnableKeyword("_EMISSION");
        lr.material.SetColor("_BaseColor", color);
    }

    void LateUpdate()
    {
        for (int i = 0; i < segments; i++)
        {
            float t = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * radius;
            lr.SetPosition(i, transform.TransformPoint(p));
        }
    }
}
