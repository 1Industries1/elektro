using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Killed enemies that fall out of the navigable area (e.g. off the map).
/// Must run on the server so that EnemySpawner's EnemiesAlive counter is updated
/// via IEnemy.OnEnemyDied.
/// </summary>
[DisallowMultipleComponent]
public class EnemyOutOfBoundsKill : NetworkBehaviour
{
    [Header("Bounds")]
    [Tooltip("Wenn der Enemy tiefer als dieser Wert fällt, wird er gekillt.")]
    public float minY = -20f;

    [Tooltip("Maximale Entfernung vom Mittelpunkt, bevor der Enemy gekillt wird (0 = ignorieren).")]
    public float maxDistanceFromOrigin = 0f;

    [Tooltip("Referenzpunkt für maxDistanceFromOrigin.")]
    public Vector3 origin = Vector3.zero;

    [Header("Check")]
    [Tooltip("Wie oft geprüft wird (Sekunden).")]
    public float checkInterval = 1.5f;

    private IEnemy _enemy;

    private void Awake()
    {
        // IEnemy sitzt i.d.R. auf demselben GameObject oder einem Parent
        _enemy = GetComponentInParent<IEnemy>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Nur der Server darf töten, damit EnemiesAlive & Events korrekt laufen.
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        if (_enemy != null)
            StartCoroutine(CheckLoop());
        else
            enabled = false;
    }

    private IEnumerator CheckLoop()
    {
        var wait = new WaitForSeconds(checkInterval);

        while (true)
        {
            if (_enemy == null)
                yield break;

            var pos = transform.position;

            // 1) Y-Grenze
            bool outOfY = pos.y < minY;

            // 2) Optionale Distanz-Grenze
            bool outOfRange = false;
            if (maxDistanceFromOrigin > 0f)
            {
                float maxDist2 = maxDistanceFromOrigin * maxDistanceFromOrigin;
                if ((pos - origin).sqrMagnitude > maxDist2)
                    outOfRange = true;
            }

            if (outOfY || outOfRange)
            {
                // Extrem hoher Schaden, um sicher zu töten.
                float killDmg = _enemy.GetBaseHealth() * 9999f;

                // Killer-Id: ulong.MaxValue = „Umwelt“, kein Spieler.
                _enemy.TakeDamage(killDmg, ulong.MaxValue, pos);
                yield break;
            }

            yield return wait;
        }
    }
}
