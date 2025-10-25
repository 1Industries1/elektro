using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance;

    [Header("Target & Follow")]
    public Transform target;
    public float smoothTime = 0.3f;
    private Vector3 velocity = Vector3.zero;

    [Header("Offset & Zoom")]
    public Vector3 offset = new Vector3(0, 12, -14); // Start-Offset
    public float zoomSpeed = 2f;
    public float minY = 5f;
    public float maxY = 50f;
    public float zoomZFactor = 0.5f;
    public float minZ = -30f;
    public float maxZ = 10f;

    [Header("Orbit (Mouse Look)")]
    public bool enableOrbit = true;
    public int orbitMouseButton = 1;      // 0=LMB, 1=RMB, 2=MMB
    public float sensitivityX = 3f;       // Yaw
    public float sensitivityY = 2f;       // Pitch (nur wenn lockPitch=false)
    public bool invertY = false;
    public float minPitch = -30f;         // nur relevant, wenn lockPitch=false
    public float maxPitch = 60f;          // nur relevant, wenn lockPitch=false
    public bool lockCursorWhileOrbit = false;

    [Header("Pitch Lock")]
    public bool lockPitch = true;
    public float fixedPitchDeg = 0f; // 0 = beim Start aus Offset übernehmen

    [Header("Player Rotation")]
    public bool rotateTargetWithOrbit = true;   // Player mitdrehen, wenn RMB gehalten

    // Zoom-Lock (z.B. wenn Upgrade-UI offen)
    private bool _lockUserZoom;
    private Coroutine _zoomRoutine;
    private Vector3 _storedOffset;

    // interne Orbit-Werte
    private float _yaw;   // Grad
    private float _pitch; // Grad

    private void Awake()
    {
        Instance = this;

        // initiale Yaw/Pitch aus Start-Offset ableiten
        OffsetToYawPitch(offset, out _yaw, out _pitch);

        if (lockPitch)
        {
            if (Mathf.Approximately(fixedPitchDeg, 0f))
                fixedPitchDeg = _pitch; // fixiere auf Start-Neigung
            _pitch = fixedPitchDeg;
            offset = YawPitchToOffset(_yaw, _pitch, offset.magnitude);
        }
    }

    public void SetTarget(Transform newTarget) => target = newTarget;

    public void SetZoomLocked(bool locked) => _lockUserZoom = locked;

    public void StoreOffset() => _storedOffset = offset;

    public void RestoreStoredOffset(float duration) => ZoomTo(_storedOffset, duration);

    // Smoothes Zoomen zu einem Offset in 'duration' Sekunden
    public void ZoomTo(Vector3 targetOffset, float duration)
    {
        if (_zoomRoutine != null) StopCoroutine(_zoomRoutine);
        _zoomRoutine = StartCoroutine(ZoomRoutine(targetOffset, duration));
    }

    private IEnumerator ZoomRoutine(Vector3 targetOffset, float duration)
    {
        // Wir lerpen den Radius, halten Yaw/Pitch konstant (Pitch ggf. fix)
        Vector3 start = offset;
        float startRadius = start.magnitude;
        float targetRadius = targetOffset.magnitude;

        if (lockPitch) _pitch = fixedPitchDeg;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float radius = Mathf.Lerp(startRadius, targetRadius, k);
            offset = YawPitchToOffset(_yaw, _pitch, Mathf.Max(0.1f, radius));
            yield return null;
        }
        offset = YawPitchToOffset(_yaw, _pitch, Mathf.Max(0.1f, targetRadius));
        _zoomRoutine = null;
    }

    private void Update()
    {
        if (enableOrbit && target != null)
            HandleOrbit();

        if (!_lockUserZoom)
            HandleZoom();

        if (target != null)
        {
            // Follow (Position)
            Vector3 targetPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);

            // LookAt (Rotation des Holders richtet die Child-Camera aus)
            Vector3 lookDir = target.position - transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir, Vector3.up), 1f);
        }
    }

    private void HandleZoom()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollInput) > 0.0001f)
        {
            // klassisches Y/Z-Zoomen
            float previousY = offset.y;
            offset.y -= scrollInput * zoomSpeed;
            offset.y = Mathf.Clamp(offset.y, minY, maxY);

            if (!Mathf.Approximately(offset.y, previousY))
            {
                offset.z += scrollInput * zoomSpeed * zoomZFactor;
                offset.z = Mathf.Clamp(offset.z, minZ, maxZ);
            }

            // neuen Radius berechnen und Offset aus fixem Pitch/Yaw rekonstruieren
            float radius = offset.magnitude;
            if (lockPitch) _pitch = fixedPitchDeg;
            offset = YawPitchToOffset(_yaw, _pitch, Mathf.Max(0.1f, radius));
        }
    }

    private void HandleOrbit()
    {
        bool orbiting = Input.GetMouseButton(orbitMouseButton);

        if (orbiting && lockCursorWhileOrbit)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (lockCursorWhileOrbit)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (!orbiting) return;

        float mx = Input.GetAxisRaw("Mouse X");
        float my = Input.GetAxisRaw("Mouse Y");

        if (Mathf.Abs(mx) < 0.0001f && Mathf.Abs(my) < 0.0001f) return;

        // Nur Yaw bewegen; Pitch bleibt fix (oder wird optional bewegt, wenn lockPitch=false)
        _yaw += mx * sensitivityX;

        if (!lockPitch)
        {
            _pitch += (invertY ? my : -my) * sensitivityY;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }
        else
        {
            _pitch = fixedPitchDeg;
        }

        float radius = offset.magnitude;
        offset = YawPitchToOffset(_yaw, _pitch, Mathf.Max(0.1f, radius));

        // Player an Kamera-Yaw ausrichten?
        if (rotateTargetWithOrbit && target != null)
        {
            // RICHTIG: camera.forward (nicht negieren), am Boden projizieren
            Vector3 face = transform.forward; 
            face.y = 0f;
            if (face.sqrMagnitude > 0.0001f)
            {
                face.Normalize();
                var pm = target.GetComponent<PlayerMovement>();
                if (pm != null)
                    pm.SetLookDirServerRpc(new Vector2(face.x, face.z));
            }
        }
    }

    // ===== Hilfsfunktionen: Offset <-> Yaw/Pitch =====
    private static void OffsetToYawPitch(Vector3 off, out float yawDeg, out float pitchDeg)
    {
        // yaw: Winkel um Y, pitch: Neigung relativ zu Horizontal (0° = horizontal, + nach oben)
        Vector3 flat = new Vector3(off.x, 0f, off.z);
        yawDeg = flat.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        float horizLen = flat.magnitude;
        pitchDeg = Mathf.Atan2(off.y, horizLen) * Mathf.Rad2Deg;
    }

    private static Vector3 YawPitchToOffset(float yawDeg, float pitchDeg, float radius)
    {
        float yaw = yawDeg * Mathf.Deg2Rad;
        float pitch = pitchDeg * Mathf.Deg2Rad;

        float y = Mathf.Sin(pitch) * radius;          // Höhe
        float r = Mathf.Cos(pitch) * radius;          // Projektion auf XZ
        float x = Mathf.Sin(yaw) * r;
        float z = Mathf.Cos(yaw) * r;

        return new Vector3(x, y, z);
    }

    // Public API (wie gehabt, plus Cursor-Freigabe)
    public void SetZoomLocked(bool locked, bool releaseCursor = true)
    {
        _lockUserZoom = locked;
        if (locked && lockCursorWhileOrbit && releaseCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
