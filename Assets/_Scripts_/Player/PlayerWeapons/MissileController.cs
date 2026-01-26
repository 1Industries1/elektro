using UnityEngine;
using Tarodev;

namespace Tarodev
{
    public class MissileController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _missilePrefab;
        [SerializeField] private Transform _firePoint;
        private Target _currentTarget;

        [Header("Timer Settings")]
        [SerializeField] private float fireInterval = 5f; // Zeitintervall in Sekunden zwischen Raketenfeuern
        private float timeSinceLastFire = 0f; // Zähler für die vergangene Zeit

        private void Update()
        {
            // Timer erhöhen
            timeSinceLastFire += Time.deltaTime;

            // Alle 5 Sekunden eine Rakete abfeuern
            if (timeSinceLastFire >= fireInterval)
            {
                FindClosestEnemy();
                FireMissile();
                //EconomyManager.Instance.currentRockets--; // Rakete verbrauchen
                timeSinceLastFire = 0f; // Timer zurücksetzen
            }
        }

        private void FireMissile()
        {
            GameObject missileObj = Instantiate(_missilePrefab, _firePoint.position, _firePoint.rotation);
            Missile missile = missileObj.GetComponent<Missile>();

            if (missile != null)
            {
                GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

                if (enemies.Length > 0)
                {
                    GameObject closestEnemy = null;
                    float closestDistance = Mathf.Infinity;

                    foreach (GameObject enemy in enemies)
                    {
                        Target target = enemy.GetComponent<Target>();
                        if (target != null)
                        {
                            float distance = Vector3.Distance(missile.transform.position, enemy.transform.position);
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                closestEnemy = enemy;
                            }
                        }
                    }

                    if (closestEnemy != null)
                    {
                        Target closestTarget = closestEnemy.GetComponent<Target>();
                        missile.SetTarget(closestTarget);  // Ziel wird gesetzt
                    }
                    else
                    {
                        Debug.LogWarning("No valid target found!");
                    }
                }
            }
        }

        private void FindClosestEnemy()
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            float closestDistance = Mathf.Infinity;
            Target closestTarget = null;

            foreach (GameObject enemy in enemies)
            {
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = enemy.GetComponent<Target>();
                }
            }

            if (closestTarget != null)
            {
                _currentTarget = closestTarget;
            }
        }
    }
}
