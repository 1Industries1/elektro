using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[DisallowMultipleComponent]
public class EnemyDropper : NetworkBehaviour
{
    [Serializable]
    public class DropEntry
    {
        [Tooltip("Prefab mit NetworkObject und PickupMagnet")]
        public GameObject prefab;

        [Range(0f, 1f), Tooltip("Chance pro Versuch (0..1)")]
        public float dropChance = 0.5f;

        [Tooltip("Min/Max Anzahl bei Erfolg")]
        public Vector2Int countRange = new Vector2Int(1, 1);

        [Tooltip("Zusätzlicher zufälliger Impuls beim Spawn")]
        public float spawnImpulse = 1.5f;
    }

    [Header("Drop-Tabelle")]
    public List<DropEntry> drops = new List<DropEntry>();

    [Header("Spawn-Settings")]
    public float spawnRadius = 0.6f;
    public LayerMask groundMask = ~0;
    public float groundCheckDown = 2f;

    /// <summary>
    /// Vom Death-System aufrufen, wenn der Enemy endgültig stirbt.
    /// </summary>
    public void HandleDeath()
    {
        if (!IsServer) return; // nur Server spawnt

        foreach (var entry in drops)
        {
            if (entry.prefab == null) continue;
            if (entry.dropChance <= 0f) continue;

            // Einmal würfeln, ob dieser Eintrag droppt
            if (UnityEngine.Random.value > entry.dropChance) continue;

            int count = UnityEngine.Random.Range(entry.countRange.x, entry.countRange.y + 1);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = transform.position + UnityEngine.Random.insideUnitSphere * spawnRadius;
                pos.y += 0.5f;

                // Optional: auf Boden projizieren
                if (Physics.Raycast(pos, Vector3.down, out var hit, groundCheckDown, groundMask, QueryTriggerInteraction.Ignore))
                    pos = hit.point + Vector3.up * 0.05f;

                var go = Instantiate(entry.prefab, pos, Quaternion.identity);
                var no = go.GetComponent<NetworkObject>();
                if (no == null)
                {
                    Debug.LogWarning($"Drop-Prefab '{entry.prefab.name}' hat kein NetworkObject!");
                    Destroy(go);
                    continue;
                }

                no.Spawn(true);

                // kleiner Impuls für „Juice“
                if (go.TryGetComponent<Rigidbody>(out var rb))
                {
                    Vector3 impulse = UnityEngine.Random.onUnitSphere;
                    impulse.y = Mathf.Abs(impulse.y); // nicht unter den Boden
                    rb.AddForce(impulse * entry.spawnImpulse, ForceMode.VelocityChange);
                }
            }
        }
    }
}
