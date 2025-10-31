// EnemyController.cs
using UnityEngine;
using Unity.Netcode;

public class EnemyController : MonoBehaviour
{
    public Transform Target { get; private set; }

    // Primärziel-Override (z. B. Base)
    private Transform primaryOverride;

    // Throttle
    [SerializeField] private float targetRefreshRate = 0.25f; // s
    private float _nextTargetRefreshTime;

    // Vom Spawner:
    public void SetPrimaryOverride(Transform t) => primaryOverride = t;
    public void ClearPrimaryOverride() => primaryOverride = null;

    /// <summary>
    /// Wählt Primärziel (falls gesetzt/aktiv), sonst den nächsten Player. Throttled.
    /// </summary>
    public void UpdateTarget()
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
        if (Time.time < _nextTargetRefreshTime) return;
        _nextTargetRefreshTime = Time.time + targetRefreshRate;

        // 1) Primärziel bevorzugen
        if (primaryOverride != null && primaryOverride.gameObject.activeInHierarchy)
        {
            Target = primaryOverride;
            return;
        }

        // 2) Nächster Spieler
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
