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
            // Tenta obter o script TreeDestruction no objeto da �rvore
            TreeDestruction tree = collision.gameObject.GetComponent<TreeDestruction>();

            // Certifica-se de que a �rvore possui o script TreeDestruction
            if (tree != null)
            {

                // Aplicar for�a ou l�gica adicional, se necess�rio (opcional)
                ApplyImpactForce(collision);
            }
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
        }
    }
}
