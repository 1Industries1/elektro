using UnityEngine;

namespace Tarodev {
    public class Target : MonoBehaviour, IExplode {
        [SerializeField] private Rigidbody _rb;
        [SerializeField] private AudioSource _audioSource; // AudioSource für den Sound
        [SerializeField] private AudioClip _deathSound;    // Soundclip, der abgespielt wird

        public Rigidbody Rb => _rb;

        // Explodiert das Ziel und spielt den Todessound ab
        public void Explode() {
            // Überprüfe, ob der AudioSource und der Soundclip gesetzt sind
            if (_audioSource != null && _deathSound != null) {
                _audioSource.PlayOneShot(_deathSound); // Spiele den Sound ab
            }

            // Zerstöre das GameObject nach dem Sound
            Destroy(gameObject, _deathSound.length); // Zerstöre das Objekt nach der Dauer des Sounds
        }
    }
}
