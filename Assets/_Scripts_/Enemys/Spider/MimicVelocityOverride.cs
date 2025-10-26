// MimicVelocityOverride.cs
using UnityEngine;

public class MimicVelocityOverride : MonoBehaviour
{
    private Vector3 overrideVel;
    private float overrideUntil;

    public void PushOverride(Vector3 vel, float duration)
    {
        overrideVel = vel;
        overrideUntil = Time.time + duration;
    }

    public Vector3 Apply(Vector3 computed)
    {
        if (Time.time < overrideUntil) return overrideVel;
        return computed;
    }
}
