using System.Collections;
using UnityEngine;

public class Generator : MonoBehaviour
{
    public GameObject prefabToSpawn; // Das Prefab, das instanziiert werden soll
    public float spawnInterval = 5f; // Zeitintervall zwischen den Spawns (in Sekunden)

    private void Start()
    {
        StartCoroutine(GeneratePrefab());
    }

    private IEnumerator GeneratePrefab()
    {
        while (true) // Endloser Spawn
        {
            Instantiate(prefabToSpawn, transform.position, Quaternion.identity);
            yield return new WaitForSeconds(spawnInterval);
        }
    }
}
