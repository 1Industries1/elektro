using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class TouchDamage : NetworkBehaviour
{
    public float dps = 10f;
    public float tickInterval = 0.25f;
    public LayerMask playerMask;

    private readonly Dictionary<PlayerHealth, float> _nextHitAt = new();

    private void OnTriggerStay(Collider other)
    {
        if (!IsServer) return;
        if (((1 << other.gameObject.layer) & playerMask) == 0) return;

        var hp = other.GetComponent<PlayerHealth>();
        if (hp == null || hp.IsDead()) return;

        float now = Time.time;
        if (!_nextHitAt.TryGetValue(hp, out float t) || now >= t)
        {
            float dmg = dps * tickInterval;        // VS-ähnlich: Tick-Schaden
            hp.Server_TakeDamage(dmg, OwnerClientId);
            _nextHitAt[hp] = now + tickInterval;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var hp = other.GetComponent<PlayerHealth>();
        if (hp) _nextHitAt.Remove(hp);
    }
}
