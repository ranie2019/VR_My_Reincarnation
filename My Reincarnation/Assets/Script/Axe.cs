using UnityEngine;

public class Axe : MonoBehaviour
{
    // Força da colisão configurável via Inspector, que pode afetar a árvore (opcional)
    [SerializeField] private float impactForce = 10f;

    // Referência ao Rigidbody do machado (se precisar de física)
    private Rigidbody rb;

    // Método chamado quando o script é ativado
    private void Awake()
    {
        // Obtém a referência ao Rigidbody, se existir
        rb = GetComponent<Rigidbody>();
    }

    // Método chamado quando o machado colide com a árvore
    private void OnCollisionEnter(Collision collision)
    {
        // Verifica se o objeto que colidiu tem a tag "tree"
        if (collision.gameObject.CompareTag("Tree"))
        {
            // Tenta obter o script TreeDestruction no objeto da árvore
            TreeDestruction tree = collision.gameObject.GetComponent<TreeDestruction>();

            // Certifica-se de que a árvore possui o script TreeDestruction
            if (tree != null)
            {

                // Aplicar força ou lógica adicional, se necessário (opcional)
                ApplyImpactForce(collision);
            }
        }
    }

    // Método para aplicar força de impacto na árvore (opcional, baseado na física)
    private void ApplyImpactForce(Collision collision)
    {
        if (rb != null)
        {
            // Adiciona força na direção oposta ao ponto de contato (simulando o impacto do machado)
            Vector3 impactDirection = collision.contacts[0].normal;
            rb.AddForce(-impactDirection * impactForce, ForceMode.Impulse);
        }
    }
}
