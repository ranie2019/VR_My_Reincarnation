using UnityEngine;

public class Axe : MonoBehaviour
{
    // For�a da colis�o configur�vel via Inspector, que pode afetar a �rvore (opcional)
    [SerializeField] private float impactForce = 10f;

    // Refer�ncia ao Rigidbody do machado (se precisar de f�sica)
    private Rigidbody rb;

    // M�todo chamado quando o script � ativado
    private void Awake()
    {
        // Obt�m a refer�ncia ao Rigidbody, se existir
        rb = GetComponent<Rigidbody>();
    }

    // M�todo chamado quando o machado colide com a �rvore
    private void OnCollisionEnter(Collision collision)
    {
        // Verifica se o objeto que colidiu tem a tag "tree"
        if (collision.gameObject.CompareTag("Tree"))
        {
            // Exibe uma mensagem de depura��o quando colidir com uma �rvore
            Debug.Log("Machado colidiu com uma �rvore: " + collision.gameObject.name);

            // Tenta obter o script TreeDestruction no objeto da �rvore
            TreeDestruction tree = collision.gameObject.GetComponent<TreeDestruction>();

            // Certifica-se de que a �rvore possui o script TreeDestruction
            if (tree != null)
            {
                Debug.Log("A �rvore cont�m o script TreeDestruction. Iniciando o processo de colis�o.");

                // Aplicar for�a ou l�gica adicional, se necess�rio (opcional)
                ApplyImpactForce(collision);
            }
            else
            {
                Debug.LogWarning("O objeto colidido n�o cont�m o script TreeDestruction.");
            }
        }
        else
        {
            Debug.Log("Machado colidiu com outro objeto: " + collision.gameObject.name);
        }
    }

    // M�todo para aplicar for�a de impacto na �rvore (opcional, baseado na f�sica)
    private void ApplyImpactForce(Collision collision)
    {
        if (rb != null)
        {
            // Adiciona for�a na dire��o oposta ao ponto de contato (simulando o impacto do machado)
            Vector3 impactDirection = collision.contacts[0].normal;
            rb.AddForce(-impactDirection * impactForce, ForceMode.Impulse);
            Debug.Log("For�a de impacto aplicada � �rvore.");
        }
    }
}
