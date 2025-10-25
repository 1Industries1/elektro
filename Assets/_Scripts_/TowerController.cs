using UnityEngine;
using Unity.Netcode;

public class TowerController : NetworkBehaviour
{
    [Header("Tower Settings")]
    public float rotationSpeed = 5f;
    public float shootInterval = 1f;
    public float detectionRange = 30f;

    [Header("Bullet Settings")]
    public Transform[] bulletSpawnPoints;
    public NetworkObject bulletPrefab;

    [Header("Audio Settings")]
    public AudioClip shootSound;

    private Transform nearestEnemy;
    private float timeSinceLastShot = 0f;
    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }

    private void Update()
    {
        if (!IsServer) return;

        FindNearestEnemy();
        RotateTowerTowardsEnemy();
        ShootAtEnemy();
    }

    private void FindNearestEnemy()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, detectionRange);
        nearestEnemy = null;

        foreach (Collider enemy in enemies)
        {
            if (enemy.CompareTag("Player") && (nearestEnemy == null || 
                Vector3.Distance(transform.position, enemy.transform.position) < 
                Vector3.Distance(transform.position, nearestEnemy.position)))
            {
                nearestEnemy = enemy.transform;
            }
        }
    }

    private void RotateTowerTowardsEnemy()
    {
        if (nearestEnemy == null) return;

        Vector3 targetDirection = nearestEnemy.position - transform.position;
        targetDirection.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void ShootAtEnemy()
    {
        if (nearestEnemy == null || Vector3.Distance(transform.position, nearestEnemy.position) > detectionRange)
        {
            nearestEnemy = null;
            return;
        }

        timeSinceLastShot += Time.deltaTime;
        if (timeSinceLastShot >= shootInterval && IsRotationCorrect())
        {
            ShootServerRpc(nearestEnemy.position);
            timeSinceLastShot = 0f;
        }
    }

    private bool IsRotationCorrect()
    {
        if (nearestEnemy == null) return false;

        Vector3 targetDirection = (nearestEnemy.position - transform.position).normalized;
        targetDirection.y = 0;

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

        float angleDifference = Quaternion.Angle(transform.rotation, targetRotation);
        
        return angleDifference <= rotationSpeed * 0.5f;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ShootServerRpc(Vector3 enemyPosition)
    {
        if (bulletPrefab == null || bulletSpawnPoints.Length == 0) 
        {
            Debug.LogError("No bullets or spawn points found!");
            return;
        }

        foreach (Transform spawnPoint in bulletSpawnPoints)
        {
            //Debug.Log($"Spawning bullet at {spawnPoint.position}");

            NetworkObject bulletNetObj = Instantiate(bulletPrefab, spawnPoint.position, spawnPoint.rotation).GetComponent<NetworkObject>();
            bulletNetObj.Spawn(true); // Server als Owner

            BulletController bullet = bulletNetObj.GetComponent<BulletController>();
            if (bullet != null)
            {
                Vector3 shootDir = (enemyPosition - spawnPoint.position).normalized;
                bullet.Init(shootDir, bullet.speed, bullet.damage, 0); // 0 = Server als Owner
            }
        }

        if (shootSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootSound);
        }
    }
}