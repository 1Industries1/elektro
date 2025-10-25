using UnityEngine;

public class PullReceiver : MonoBehaviour
{
    private Vector3 _accel;  // m/s² (pro Tick gesammelt)
    private Vector3 _vExt;   // externe Velocity (m/s)

    [Tooltip("Dämpfung der externen Geschwindigkeit (0..1 pro Sekunde). 0 = keine Dämpfung.")]
    public float velocityDampingPerSec = 0.15f;

    public void AddAccel(Vector3 a) => _accel += a;

    public Vector3 ConsumeStep(float dt, float maxSpeed)
    {
        // v += a * dt
        _vExt += _accel * dt;

        // Kappe
        if (maxSpeed > 0f && _vExt.sqrMagnitude > maxSpeed * maxSpeed)
            _vExt = _vExt.normalized * maxSpeed;

        // sanfte Dämpfung
        if (velocityDampingPerSec > 0f)
        {
            float k = Mathf.Clamp01(velocityDampingPerSec * dt);
            _vExt *= (1f - k);
        }

        // Schritt
        Vector3 step = _vExt * dt;

        // Reset Accel
        _accel = Vector3.zero;
        return step;
    }
}
