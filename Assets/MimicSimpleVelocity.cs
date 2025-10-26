using UnityEngine;
using MimicSpace;

[DefaultExecutionOrder(50)]
public class MimicSimpleVelocity : MonoBehaviour
{
    [SerializeField] private Mimic mimic;      // Child mit Mimic
    [SerializeField] private Transform tracked; // meist Spider-Root
    [SerializeField] private bool planar = true;

    Vector3 lastPos;

    void Awake()
    {
        if (!tracked) tracked = transform;
        if (!mimic)   mimic   = GetComponentInChildren<Mimic>();
        lastPos = tracked.position;
    }

    void LateUpdate()
    {
        if (!mimic || !tracked) return;
        float dt = Mathf.Max(Time.deltaTime, 1e-5f);
        Vector3 v = (tracked.position - lastPos) / dt;
        lastPos = tracked.position;
        if (planar) v = Vector3.ProjectOnPlane(v, Vector3.up);
        mimic.velocity = v; // wichtigste Zeile
    }
}
