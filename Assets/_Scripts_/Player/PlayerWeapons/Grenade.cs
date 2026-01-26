using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grenade : MonoBehaviour
{
    public float grenadeDamage = 1f;
    public float delay = 3f;
    public float explosionRadius = 5f;
    public float explosionForce = 700f;
    public GameObject explosionEffect;
    float countdown;
    bool hasExploded = false;

    // Start is called before the first frame update
    void Start()
    {
        countdown = delay;
    }

    // Update is called once per frame
    void Update()
    {
        countdown -= Time.deltaTime;
        if (countdown <= 0f && !hasExploded)
        {
            Explode();
            hasExploded = true;
        }
    }

    void Explode()
    {
        // Explosion-Effekt erzeugen
        Instantiate(explosionEffect, transform.position, transform.rotation);

        // Kollider im Explosionsradius finden
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider nearbyObject in colliders)
        {
            // Prüfen, ob der Collider ein Rigidbody hat
            Rigidbody rb = nearbyObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Explosionskraft anwenden
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);
            }

            // Schaden verursachen, wenn das Objekt ein Gegner ist
            if (nearbyObject.CompareTag("Enemy"))
            {
                EnemyMovement enemy = nearbyObject.GetComponent<EnemyMovement>();
                if (enemy != null)
                {
                    enemy.TakeDamage(grenadeDamage, 0, enemy.transform.position);
                }
            }
        }

        // Granate zerstören
        Destroy(gameObject);
    }
}
