using UnityEngine;
using UnityEngine.UI;

public class GrenadeThrower : MonoBehaviour
{
    public GameObject grenadePrefab;
    public Transform throwPoint;
    public float throwForce = 15f; // Basiswurfkraft
    public float gravity = 9.81f; // Schwerkraft-Simulation
    public float throwAngle = 25f; // Flacherer Wurfwinkel in Grad
    public float grenadeCooldownTime = 15f; // Nachladezeit in Sekunden

    private float nextThrowTime = 0f;

    public Slider cooldownSlider; // Referenz zum Slider


    void Start()
    {
        if (cooldownSlider != null)
        {
            cooldownSlider.maxValue = grenadeCooldownTime;
            cooldownSlider.value = grenadeCooldownTime; // Startwert setzen
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && Time.time >= nextThrowTime)
        {
            Vector3 targetPoint = GetMouseWorldPosition();
            ThrowGrenade(targetPoint);
            nextThrowTime = Time.time + grenadeCooldownTime;
        }

        // Cooldown für den Slider umdrehen (0 → 15)
        if (cooldownSlider != null)
        {
            float cooldownElapsed = Mathf.Clamp(Time.time - (nextThrowTime - grenadeCooldownTime), 0, grenadeCooldownTime);
            cooldownSlider.value = cooldownElapsed;
        }
    }

    Vector3 GetMouseWorldPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            return hit.point; // Mausziel in 3D
        }
        return transform.position + transform.forward * 10f; // Falls kein Treffer, Standarddistanz
    }

    void ThrowGrenade(Vector3 targetPosition)
    {
        GameObject grenade = Instantiate(grenadePrefab, throwPoint.position, Quaternion.identity);
        Rigidbody rb = grenade.GetComponent<Rigidbody>();

        if (rb != null)
        {
            Vector3 throwVelocity = CalculateThrowVelocity(targetPosition, throwPoint.position);
            rb.linearVelocity = throwVelocity; // Geschwindigkeit setzen
        }
    }

    Vector3 CalculateThrowVelocity(Vector3 target, Vector3 origin)
    {
        float heightDifference = target.y - origin.y;
        target.y = origin.y; // Horizontale Distanz berechnen
        float distance = Vector3.Distance(target, origin);
        float angle = throwAngle * Mathf.Deg2Rad; // Flacherer Wurfwinkel (z. B. 25°)

        float velocity = Mathf.Sqrt((distance * gravity) / Mathf.Sin(2 * angle)); // Startgeschwindigkeit berechnen
        Vector3 direction = (target - origin).normalized;
        Vector3 throwVelocity = direction * velocity;
        throwVelocity.y = Mathf.Tan(angle) * velocity; // Vertikale Komponente

        return throwVelocity;
    }


    public void SetCooldownTime(float newCooldownTime)
    {
        grenadeCooldownTime = newCooldownTime;

        if (cooldownSlider != null)
        {
            cooldownSlider.maxValue = grenadeCooldownTime;
            cooldownSlider.value = Mathf.Clamp(cooldownSlider.value, 0, grenadeCooldownTime);
        }
    }
}
