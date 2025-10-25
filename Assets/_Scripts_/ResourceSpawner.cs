using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourceSpawner : MonoBehaviour
{
    [System.Serializable]
    public class ResourcePrefab
    {
        public string resourceName;  // "Iron", "Nickel", "Cobalt", "Titan", "Platinum", "Deuterium", "Helium-3"
        public GameObject prefab;    // das zugehörige Prefab
    }

    // Im Inspector diese Liste mit den Ressourcen befüllen
    public List<ResourcePrefab> resourcePrefabs;

    public float spawnRadius = 5f;      // Radius, in dem die Prefabs gespawnt werden
    public float spawnForce = 100f;     // Optional: Kraft, mit der die Prefabs weggeschleudert werden
    public float conversionFactor = 1f;

    /// <summary>
    /// Spawnt für die angegebene Ressource eine Anzahl von Prefabs entsprechend der Menge.
    /// Das Verhältnis ergibt sich aus conversionFactor: spawnCount = Menge * conversionFactor.
    /// </summary>
    /// <param name="resourceKey">Der Name der Ressource (z.B. "Iron")</param>
    /// <param name="amount">Die Menge an abgebautem Material (Einheit)</param>


    private Dictionary<string, float> resourceBuffer = new Dictionary<string, float>(); // Speichert Teilmengen


    public void SpawnResource(string resourceKey, float amount)
    {
        // Suche das entsprechende Prefab
        GameObject prefabToSpawn = GetPrefab(resourceKey);
        if (prefabToSpawn == null) return;

        // Menge im Puffer aufsummieren
        if (!resourceBuffer.ContainsKey(resourceKey))
        {
            resourceBuffer[resourceKey] = 0f;
        }
        resourceBuffer[resourceKey] += amount;

        // Spawnen, wenn eine ganze Einheit erreicht wurde
        while (resourceBuffer[resourceKey] >= 1f)
        {
            Vector3 spawnPosition = GetSpawnPosition();
            GameObject spawnedResource = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
            resourceBuffer[resourceKey] -= 1f; // 1 Einheit entfernen
        }
    }

    private GameObject GetPrefab(string resourceKey)
    {
        foreach (var resource in resourcePrefabs)
        {
            if (resource.resourceName.Equals(resourceKey))
            {
                return resource.prefab;
            }
        }
        Debug.LogError("Kein Prefab gefunden für Ressource: " + resourceKey);
        return null;
    }

    private Vector3 GetSpawnPosition()
    {
        return transform.position + new Vector3(
            Random.Range(-spawnRadius, spawnRadius),
            Random.Range(-spawnRadius, spawnRadius),
            Random.Range(-spawnRadius, spawnRadius)
        );
    }
}
