using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourceCollector : MonoBehaviour
{
    private Dictionary<string, float> storedResources = new Dictionary<string, float>();


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Resource"))
        {
            ResourceItem resourceItem = other.GetComponent<ResourceItem>();
            if (resourceItem != null)
            {
                AddResource(resourceItem.resourceName, resourceItem.amount);
                Destroy(other.gameObject);
            }
        }
    }

    private void AddResource(string resourceName, float amount)
    {
        if (storedResources.ContainsKey(resourceName))
        {
            storedResources[resourceName] += amount;
        }
        else
        {
            storedResources[resourceName] = amount;
        }

        StartCoroutine(TransferProcess());
    }

    private IEnumerator TransferProcess()
    {
        var resourcesCopy = new List<KeyValuePair<string, float>>(storedResources); // Kopie erstellen

        foreach (var resource in resourcesCopy)
        {
            EconomyManager.Instance.AddResources(resource.Key, resource.Value);
            yield return new WaitForSeconds(0.2f); // Simulierte Transferzeit pro Ressource
        }

        storedResources.Clear(); // Nach dem Transfer das Original leeren
    }
}
