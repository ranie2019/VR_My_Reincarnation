using UnityEngine;

[DisallowMultipleComponent]
public class Axe : MonoBehaviour
{
    [Header("Impacto (opcional)")]
    [SerializeField] private float impactForce = 0f; // 0 = não aplica força extra

    [Header("Tags")]
    [Tooltip("Tag do objeto que pode receber impacto extra (opcional).")]
    [SerializeField] private string tagAlvo = "Tree";

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Não precisa chamar VidaArvore aqui.
        // A árvore já vai detectar a colisão pelo script VidaArvore (tag 'machado').

        // Se você quiser manter só o efeito físico/impacto, faz aqui:
        if (impactForce > 0f && rb != null && collision.gameObject.CompareTag(tagAlvo))
        {
            ApplyImpactForce(collision);
        }
    }

    private void ApplyImpactForce(Collision collision)
    {
        if (collision.contactCount == 0) return;

        // direção do impacto (normal do contato)
        Vector3 impactDirection = collision.contacts[0].normal;

        // empurra o machado de leve (ou você pode empurrar a árvore se ela tiver Rigidbody)
        rb.AddForce(-impactDirection * impactForce, ForceMode.Impulse);
    }
}
