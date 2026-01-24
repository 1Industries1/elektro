using UnityEngine;

public class FlyCamera : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float lookSpeed = 2f;

    float yaw;
    float pitch;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Maus schauen
        yaw += Input.GetAxis("Mouse X") * lookSpeed * 100f * Time.deltaTime;
        pitch -= Input.GetAxis("Mouse Y") * lookSpeed * 100f * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.eulerAngles = new Vector3(pitch, yaw, 0f);

        // Bewegung
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        float up = 0;

        if (Input.GetKey(KeyCode.E)) up = 1;
        if (Input.GetKey(KeyCode.Q)) up = -1;

        Vector3 dir = transform.forward * v +
                      transform.right * h +
                      transform.up * up;

        transform.position += dir * moveSpeed * Time.deltaTime;
    }
}
