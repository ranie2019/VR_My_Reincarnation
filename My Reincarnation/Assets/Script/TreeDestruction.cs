using UnityEngine;
using System.Collections;

public class TreeDestruction : MonoBehaviour
{
    // Número de colisões necessárias para destruir a árvore, configurável pelo Inspector
    [SerializeField] public int requiredCollisions = 5;

    // Contador de colisões
    public int collisionCount = 0;

    // Flag para verificar se está no período de cooldown
    private bool isInCooldown = false;

    // Referência ao AudioSource para tocar o efeito sonoro
    private AudioSource audioSource;

    // Método chamado quando o script é ativado
    private void Awake()
    {
        // Obtém a referência ao componente AudioSource
        audioSource = GetComponent<AudioSource>();
    }

    // Método chamado quando outro collider entra em colisão com o collider deste objeto
    private void OnCollisionEnter(Collision collision)
    {
        // Verifica se o objeto que colidiu tem a tag "axe" e se não está em cooldown
        if (collision.gameObject.CompareTag("axe") && !isInCooldown)
        {
            HandleAxeCollision();
        }
    }

    // Método para lidar com a colisão com o machado
    private void HandleAxeCollision()
    {
        // Incrementa o contador de colisões
        collisionCount++;

        // Toca o efeito sonoro
        if (audioSource != null)
        {
            audioSource.Play();
        }

        // Verifica se o número de colisões atingiu o necessário para destruir a árvore
        if (collisionCount >= requiredCollisions)
        {
            DestroyTree();
        }
        else
        {
            // Inicia o cooldown
            StartCoroutine(CollisionCooldown());
        }
    }

    // Coroutine para gerenciar o cooldown entre colisões
    private IEnumerator CollisionCooldown()
    {
        // Ativa o cooldown
        isInCooldown = true;

        // Espera por 1 segundo antes de permitir outra colisão
        yield return new WaitForSeconds(1f);

        // Desativa o cooldown
        isInCooldown = false;
    }

    // Método para destruir a árvore imediatamente
    private void DestroyTree()
    {
        // Destroi o objeto (a árvore)
        Debug.Log("Árvore destruída!");
        Destroy(gameObject);
    }
}
