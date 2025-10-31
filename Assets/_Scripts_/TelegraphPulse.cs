using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class TelegraphPulse : MonoBehaviour
{
    [SerializeField] private float speed = 3f;
    [SerializeField] private float alphaMin = 0.25f;
    [SerializeField] private float alphaMax = 0.8f;

    private Material _mat;

    void Awake()
    {
        var r = GetComponent<Renderer>();
        _mat = r ? r.material : null; // instanziiert Material für Laufzeit
    }

    void Update()
    {
        if (_mat == null) return;
        float a = Mathf.Lerp(alphaMin, alphaMax, 0.5f * (1f + Mathf.Sin(Time.time * speed)));
        var c = _mat.color; c.a = a; _mat.color = c;
    }
}
