using UnityEngine;
using Unity.Netcode;

public class EnemyAttack : NetworkBehaviour
{
    private EnemyController enemyController;
    private AudioSource audioSource;
    private float lastFireTime;

    [SerializeField] private GameObject enemyBulletPrefab;
    [SerializeField] private Transform bulletSpawnPoint;
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private float shootingRange = 12f;
    [SerializeField] private float inaccuracyAngle = 1f;
    [SerializeField] private AudioClip shootSound;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        enemyController = GetComponent<EnemyController>();
    }

    void Update()
    {
        if (!IsServer) return;

        // ✅ Immer den nächsten Spieler suchen
        enemyController.UpdateTarget();
        Transform target = enemyController.Target;
        if (target == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (InFront(target) && distanceToTarget <= shootingRange && Time.time >= lastFireTime + fireRate)
        {
            Fire();
            lastFireTime = Time.time;
        }
    }

    bool InFront(Transform target)
    {
        Vector3 directionToTarget = target.position - transform.position;
        float angle = Vector3.Angle(transform.forward, directionToTarget);
        return angle < 30f;
    }

    void Fire()
    {
        if (enemyBulletPrefab != null && bulletSpawnPoint != null)
        {
            Quaternion fireRotation = bulletSpawnPoint.rotation;
            float randomYaw = Random.Range(-inaccuracyAngle, inaccuracyAngle);
            float randomPitch = Random.Range(-inaccuracyAngle, inaccuracyAngle);
            Quaternion deviation = Quaternion.Euler(randomPitch, randomYaw, 0);
            fireRotation *= deviation;

            GameObject bullet = Instantiate(enemyBulletPrefab, bulletSpawnPoint.position, fireRotation);

            var netObj = bullet.GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Spawn();

            var bulletController = bullet.GetComponent<EnemyBulletController>();
            if (bulletController != null)
                bulletController.Init(bullet.transform.forward);

            if (shootSound != null && audioSource != null)
                audioSource.PlayOneShot(shootSound);
        }
    }

}
