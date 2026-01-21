using System;
using UnityEngine;

public interface IEnemy
{
    event Action<IEnemy> OnEnemyDied;

    // Damage + Angreifer + HitPoint
    void TakeDamage(float damage, ulong attackerId, Vector3 hitPoint);
    ulong LastHitByClientId { get; }

    // für Wave-Scaling
    void SetHealth(float newHealth);
    float GetBaseHealth();
}
