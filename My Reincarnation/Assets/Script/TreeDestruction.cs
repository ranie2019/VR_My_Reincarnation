using UnityEngine;
using System.Collections;

public class TreeDestruction : MonoBehaviour
{
    // N�mero de colis�es necess�rias para destruir a �rvore, configur�vel pelo Inspector
    [SerializeField] public int requiredCollisions = 5;

    // Contador de colis�es
    public int collisionCount = 0;

    // Flag para verificar se est� no per�odo de cooldown
    private bool isInCooldown = false;

    // Refer�ncia ao AudioSource para tocar o efeito sonoro
    private AudioSource audioSource;

    // M�todo chamado quando o script � ativado
    private void Awake()
    {
        // Obt�m a refer�ncia ao componente AudioSource
        audioSource = GetComponent<AudioSource>();
    }

    // M�todo chamado quando outro collider entra em colis�o com o collider deste objeto
    private void OnCollisionEnter(Collision collision)
    {
        // Verifica se o objeto que colidiu tem a tag "axe" e se n�o est� em cooldown
        if (collision.gameObject.CompareTag("axe") && !isInCooldown)
        {
            HandleAxeCollision();
        }
    }

    // M�todo para lidar com a colis�o com o machado
    private void HandleAxeCollision()
    {
        // Incrementa o contador de colis�es
        collisionCount++;

        // Toca o efeito sonoro
        if (audioSource != null)
        {
            audioSource.Play();
        }

        // Verifica se o n�mero de colis�es atingiu o necess�rio para destruir a �rvore
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

    // Coroutine para gerenciar o cooldown entre colis�es
    private IEnumerator CollisionCooldown()
    {
        // Ativa o cooldown
        isInCooldown = true;

        // Espera por 1 segundo antes de permitir outra colis�o
        yield return new WaitForSeconds(1f);

        // Desativa o cooldown
        isInCooldown = false;
    }

    // M�todo para destruir a �rvore imediatamente
    private void DestroyTree()
    {
        // Destroi o objeto (a �rvore)
        Debug.Log("�rvore destru�da!");
        Destroy(gameObject);
    }
}
