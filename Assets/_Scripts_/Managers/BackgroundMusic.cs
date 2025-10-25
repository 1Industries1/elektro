using UnityEngine;

public class BackgroundMusic : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);     // bleibt über Szenen hinweg
        var src = GetComponent<AudioSource>();
        if (!src.isPlaying) src.Play();
    }
}
