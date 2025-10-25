using UnityEngine;

public class BillboardToCamera : MonoBehaviour
{
    private Transform cam;

    private void Start()
    {
        if (Camera.main) cam = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (!cam) return;
        // Blick zur Kamera, nur um Y drehen (schöner für 3D-Worldspace UI)
        Vector3 dir = transform.position - cam.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}
