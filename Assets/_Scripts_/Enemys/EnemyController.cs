using UnityEngine;
using Unity.Netcode;

public class EnemyController : MonoBehaviour
{
    public Transform Target { get; private set; }

    // NEU: Primärziel-Override (z. B. Base)
    private Transform primaryOverride;

    // Vom Spawner aufrufen:
    public void SetPrimaryOverride(Transform t) => primaryOverride = t;
    public void ClearPrimaryOverride() => primaryOverride = null;

    /// <summary>
    /// Wählt Primärziel (falls gesetzt/aktiv), sonst den nächsten Player.
    /// </summary>
    public void UpdateTarget()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // 1) Primärziel bevorzugen (falls vorhanden & aktiv)
        if (primaryOverride != null && primaryOverride.gameObject.activeInHierarchy)
        {
            Target = primaryOverride;
            return;
        }

        // 2) sonst nächster Spieler
        float minDist = Mathf.Infinity;
        Transform closest = null;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            float dist = Vector3.Distance(transform.position, playerObj.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = playerObj.transform;
            }
        }

        Target = closest;
    }
}
