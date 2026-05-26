using UnityEngine;

public class PlaySoundOnCollision : MonoBehaviour
{
    public AudioClip collisionSound; // O áudio a ser reproduzido
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (collisionSound != null)
        {
            audioSource.clip = collisionSound;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Verifica se a colisão é com a bola
        if (collision.gameObject.CompareTag("Ball"))
        {
            if (audioSource != null && collisionSound != null)
            {
                audioSource.Play();
            }
        }
    }
}
