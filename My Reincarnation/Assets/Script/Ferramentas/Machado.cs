using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class Machado : MonoBehaviour
{
    [Header("Impacto (opcional)")]
    [SerializeField] private float impactForce = 0f; // 0 = nao aplica forca extra

    [Header("Tags")]
    [Tooltip("Tag do objeto que pode receber impacto extra (opcional).")]
    [SerializeField] private string tagAlvo = "Tree";

    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;
    private readonly HashSet<VidaArvore> arvoresDentroDoTrigger = new();
    private readonly Dictionary<VidaArvore, int> contatosPorArvore = new();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

    }

    private void OnDisable()
    {
        arvoresDentroDoTrigger.Clear();
        contatosPorArvore.Clear();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (impactForce > 0f && rb != null && collision.gameObject.CompareTag(tagAlvo))
        {
            ApplyImpactForce(collision);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TentarAplicarDanoPorEntradaTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Sem dano continuo: um novo hit so e liberado depois de OnTriggerExit.
    }

    private void OnTriggerExit(Collider other)
    {
        VidaArvore arvore = BuscarVidaArvore(other);
        if (arvore == null)
            return;

        if (!contatosPorArvore.TryGetValue(arvore, out int contatos))
            return;

        contatos--;

        if (contatos > 0)
        {
            contatosPorArvore[arvore] = contatos;
            return;
        }

        contatosPorArvore.Remove(arvore);
        arvoresDentroDoTrigger.Remove(arvore);
    }

    private void ApplyImpactForce(Collision collision)
    {
        if (collision.contactCount == 0) return;

        Vector3 impactDirection = collision.contacts[0].normal;
        rb.AddForce(-impactDirection * impactForce, ForceMode.Impulse);
    }

    private void TentarAplicarDanoPorEntradaTrigger(Collider other)
    {
        VidaArvore arvore = BuscarVidaArvore(other);
        if (arvore == null)
            return;

        if (contatosPorArvore.TryGetValue(arvore, out int contatos))
            contatosPorArvore[arvore] = contatos + 1;
        else
            contatosPorArvore.Add(arvore, 1);

        if (!arvoresDentroDoTrigger.Add(arvore))
            return;

        arvore.ReceberDanoDeMachado(gameObject);
    }

    private VidaArvore BuscarVidaArvore(Collider alvo)
    {
        if (alvo == null)
            return null;

        VidaArvore vida = alvo.GetComponentInParent<VidaArvore>();
        if (vida != null)
            return vida;

        Rigidbody alvoRb = alvo.attachedRigidbody;
        if (alvoRb != null)
        {
            vida = alvoRb.GetComponentInParent<VidaArvore>();
            if (vida != null)
                return vida;
        }

        Transform root = alvo.transform.root;
        return root != null ? root.GetComponentInChildren<VidaArvore>() : null;
    }

}
