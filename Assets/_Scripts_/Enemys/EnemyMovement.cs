using UnityEngine;
using System;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : NetworkBehaviour, IEnemy
{
    private Rigidbody rb;
    private bool isDead = false;


    public event Action<IEnemy> OnEnemyDied;
    public ulong LastHitByClientId => lastHitByClientId;
    [HideInInspector] public ulong lastHitByClientId;

    [SerializeField] private float detectionRange = 40f;
    [SerializeField] private float attackRange = 25f;
    [SerializeField] private float minMoveSpeed = 3f;
    [SerializeField] private float maxMoveSpeed = 7f;
    [SerializeField] private float rotationSpeed = 3f;
    [SerializeField] private float baseHealth = 1f;
    [SerializeField] private EnemyDamageNumbers dmgNums;

    private float moveSpeed;
    private Transform target;

    public float GetBaseHealth() => baseHealth;

    // Health als synchronisierte Variable
    private NetworkVariable<float> health =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        dmgNums = GetComponent<EnemyDamageNumbers>();
    }

    public override void OnNetworkSpawn()
    {
        rb.isKinematic = !IsServer;

        if (IsServer)
        {
            moveSpeed = UnityEngine.Random.Range(minMoveSpeed, maxMoveSpeed);
            FindTarget();
            health.Value = baseHealth;
        }

        health.OnValueChanged += OnHealthChanged;
    }

    void FixedUpdate()
    {
        if (!IsServer || isDead) return;

        // Immer aktuell den nächsten Spieler suchen
        FindTarget();
        if (target == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget < detectionRange)
        {
            RotateTowardsTarget(target);

            if (distanceToTarget > attackRange)
                MoveTowardsTarget(target);
        }
    }

    void FindTarget()
    {
        float minDist = Mathf.Infinity;
        Transform closest = null;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            float dist = Vector3.Distance(transform.position, client.PlayerObject.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = client.PlayerObject.transform;
            }
        }
        target = closest;
    }

    void RotateTowardsTarget(Transform target)
    {
        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
    }

    void MoveTowardsTarget(Transform target)
    {
        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0;
        rb.MovePosition(rb.position + direction * moveSpeed * Time.fixedDeltaTime);
    }

    public void TakeDamage(float amount, ulong attackerId, Vector3 hitPoint)
    {
        if (!IsServer) return;

        lastHitByClientId = attackerId;
        health.Value -= amount;

        // Damage Number nur für den Angreifer
        if (dmgNums != null)
            dmgNums.ShowForAttackerOnly(amount, hitPoint, attackerId, isCrit: false);
        else
            DamagePopupRelay.Instance?.ShowForAttackerOnly_Server(amount, hitPoint, attackerId, isCrit: false); // Fallback

        GetComponent<EnemyEffects>()?.PlayHitEffectClientRpc(hitPoint);
    }

    public void SetHealth(float newHealth)
    {
        if (IsServer)
            health.Value = newHealth;
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (newValue <= 0 && !isDead)
            Die();
    }

    private void Die()
    {
        isDead = true;
        OnEnemyDied?.Invoke(this);

        GetComponent<EnemyEffects>()?.PlayDeathEffectClientRpc(transform.position);
        GetComponent<EnemyDropper>().HandleDeath();

        //Debug.Log($"[EnemyMovement] Enemy died. LastHitByClientId={lastHitByClientId}");

        if (IsServer)
            GetComponent<NetworkObject>().Despawn(true);
    }

}
