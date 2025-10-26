using UnityEngine;
using MimicSpace;

[DefaultExecutionOrder(50)]
public class MimicVelocityFromTransform : MonoBehaviour
{
    [SerializeField] private Mimic mimic;
    [SerializeField] private Transform tracked;
    [SerializeField] private bool planar = true;
    [SerializeField, Tooltip("höher = direkter")] private float smooth = 12f;
    [SerializeField] private float maxSpeed = 14f;

    private Vector3 lastPos;
    private Vector3 vSmoothed;

    void Awake()
    {
        if (tracked == null) tracked = transform;
        if (mimic == null) mimic = GetComponentInChildren<Mimic>();
        lastPos = tracked.position;
    }

    void LateUpdate()
    {
        if (mimic == null || tracked == null) return;

        Vector3 pos = tracked.position;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 v = (pos - lastPos) / dt;
        lastPos = pos;

        if (planar) v = Vector3.ProjectOnPlane(v, Vector3.up);
        if (v.sqrMagnitude > maxSpeed * maxSpeed) v = v.normalized * maxSpeed;

        float k = 1f - Mathf.Exp(-smooth * dt); // critically damped smoothing
        vSmoothed = Vector3.Lerp(vSmoothed, v, k);

        // optionaler Override
        var ov = GetComponent<MimicVelocityOverride>();
        if (ov != null) vSmoothed = ov.Apply(vSmoothed);

        mimic.velocity = vSmoothed;
    }
}
