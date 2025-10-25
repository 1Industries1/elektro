using UnityEngine;

public class Fragment : MonoBehaviour
{
    private bool isCollected = false;
    //private float lifetime = 10f;  // Lebensdauer
    public string resourceName = "Fragment";  // Name des gesammelten Ressourcen-Typs
    public float resourceAmount = 1f;  // Anzahl der Fragmente)

    private void Start()
    {
        // Zerstöre den Splitter nach einer bestimmten Zeit, wenn er nicht gesammelt wurde
        //Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Überprüfe, ob der Splitter von der Harpune getroffen wird
        if (other.CompareTag("Harpoon") && !isCollected)
        {
            CollectFragment(other);
        }
    }

    private void CollectFragment(Collider harpoon)
    {
        isCollected = true;
        // Hier kann man Logik hinzufügen, wie z.B. das Fragment ins Inventar des Spielers zu legen
        Debug.Log("Splitter wurde von der Harpune aufgenommen.");

        // Zerstöre den Splitter nach der Sammlung
        //Destroy(gameObject);
        
    }
}
