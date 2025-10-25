using UnityEngine;

public class ExplosionDecalOrVFX : MonoBehaviour
{
    public float lifetime = 4f;

    public void PlayAndAutoDestroy()
    {
        // Hier ggf. ParticleSystem/Audio anwerfen
        var ps = GetComponent<ParticleSystem>();
        if (ps != null) ps.Play();

        var audio = GetComponent<AudioSource>();
        if (audio != null) audio.Play();

        Destroy(gameObject, lifetime);
    }
}
