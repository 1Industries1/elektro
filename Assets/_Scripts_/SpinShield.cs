using UnityEngine;

public class SpinShield : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 90f; // Grad pro Sekunde

    void Update()
    {
        // Dreht das Objekt kontinuierlich um die Y-Achse
        transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);
    }
}
