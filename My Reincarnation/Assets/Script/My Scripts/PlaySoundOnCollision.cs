using UnityEngine;

public class PlaySoundOnCollision : MonoBehaviour
{
    public AudioClip collisionSound; // O audio a ser reproduzido
    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource != null && collisionSound != null)
            audioSource.clip = collisionSound;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!AudioColisaoFiltro.PodeTocarSomDeColisao(collision))
            return;

        // Verifica se a colisao e com a bola
        if (collision.gameObject.CompareTag("Ball") && audioSource != null && collisionSound != null)
            audioSource.Play();
    }
}
