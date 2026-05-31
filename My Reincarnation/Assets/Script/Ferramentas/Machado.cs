using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class Machado : MonoBehaviour
{
    [Header("Impacto (opcional)")]
    [SerializeField] private float impactForce = 0f; // 0 = nao aplica forca extra

    [Header("Tags")]
    [Tooltip("Tag do objeto que pode receber impacto extra (opcional).")]
    [SerializeField] private string tagAlvo = "Tree";

    [Header("Debug temporario")]
    [SerializeField] private bool debugDiagnosticoContato = true;

    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;
    private Collider[] collidersMachado;
    private readonly HashSet<VidaArvore> arvoresDentroDoTrigger = new();
    private readonly Dictionary<VidaArvore, int> contatosPorArvore = new();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        collidersMachado = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnSelectEnteredDiagnostico);
            grabInteractable.selectExited.AddListener(OnSelectExitedDiagnostico);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEnteredDiagnostico);
            grabInteractable.selectExited.RemoveListener(OnSelectExitedDiagnostico);
        }

        arvoresDentroDoTrigger.Clear();
        contatosPorArvore.Clear();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // A arvore continua sendo responsavel pelo dano; aqui ha apenas diagnostico e impacto opcional.
        LogContatoMachado("OnCollisionEnter", collision != null ? collision.collider : null);

        if (impactForce > 0f && rb != null && collision.gameObject.CompareTag(tagAlvo))
        {
            ApplyImpactForce(collision);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        LogContatoMachado("OnTriggerEnter", other);
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

    private void OnSelectEnteredDiagnostico(SelectEnterEventArgs args)
    {
        Transform interactor = (args.interactorObject as MonoBehaviour)?.transform;
        LogEstadoMachado("OnSelectEntered", interactor);
    }

    private void OnSelectExitedDiagnostico(SelectExitEventArgs args)
    {
        Transform interactor = (args.interactorObject as MonoBehaviour)?.transform;
        LogEstadoMachado("OnSelectExited", interactor);
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

    private void LogContatoMachado(string evento, Collider outroCollider)
    {
        if (!debugDiagnosticoContato)
            return;

        Transform outroTransform = outroCollider != null ? outroCollider.transform : null;
        Transform outroRoot = outroTransform != null ? outroTransform.root : null;
        Rigidbody outroRb = outroCollider != null ? outroCollider.attachedRigidbody : null;
        VidaArvore vida = BuscarVidaArvore(outroCollider);
        int outroLayer = outroTransform != null ? outroTransform.gameObject.layer : -1;
        string outroLayerNome = outroLayer >= 0 ? LayerMask.LayerToName(outroLayer) : "sem layer";

        string outroColliderInfo = outroCollider != null
            ? $"{outroCollider.GetType().Name} enabled={outroCollider.enabled} isTrigger={outroCollider.isTrigger}"
            : "sem collider";

        string outroRbInfo = outroRb != null
            ? $"{outroRb.name} isKinematic={outroRb.isKinematic} useGravity={outroRb.useGravity} detectCollisions={outroRb.detectCollisions}"
            : "sem attachedRigidbody";

        Debug.Log(
            $"[Machado][{evento}] machado={name} tag={tag} layer={gameObject.layer}:{LayerMask.LayerToName(gameObject.layer)} " +
            $"movementType={(grabInteractable != null ? grabInteractable.movementType.ToString() : "sem XRGrabInteractable")} " +
            $"rb={DescreverRigidbody(rb)} collidersMachado={DescreverCollidersMachado()} " +
            $"tocou={(outroTransform != null ? outroTransform.name : "null")} tag={ObterTagSegura(outroTransform)} " +
            $"layer={outroLayer}:{outroLayerNome} root={(outroRoot != null ? outroRoot.name : "null")} " +
            $"outroCollider={outroColliderInfo} outroAttachedRigidbody={outroRbInfo} " +
            $"vidaArvoreEncontrada={(vida != null ? vida.name : "nao")}",
            this);
    }

    private void LogEstadoMachado(string evento, Transform interactor)
    {
        if (!debugDiagnosticoContato)
            return;

        Debug.Log(
            $"[Machado][{evento}] machado={name} tag={tag} layer={gameObject.layer}:{LayerMask.LayerToName(gameObject.layer)} " +
            $"interactor={(interactor != null ? interactor.name : "null")} " +
            $"movementType={(grabInteractable != null ? grabInteractable.movementType.ToString() : "sem XRGrabInteractable")} " +
            $"rb={DescreverRigidbody(rb)} collidersMachado={DescreverCollidersMachado()}",
            this);
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

    private static string DescreverRigidbody(Rigidbody alvoRb)
    {
        return alvoRb != null
            ? $"name={alvoRb.name} isKinematic={alvoRb.isKinematic} useGravity={alvoRb.useGravity} detectCollisions={alvoRb.detectCollisions} collisionDetection={alvoRb.collisionDetectionMode}"
            : "sem Rigidbody";
    }

    private string DescreverCollidersMachado()
    {
        if (collidersMachado == null || collidersMachado.Length == 0)
            return "0 colliders";

        int ativos = 0;
        int triggers = 0;

        for (int i = 0; i < collidersMachado.Length; i++)
        {
            Collider col = collidersMachado[i];
            if (col == null)
                continue;

            if (col.enabled)
                ativos++;

            if (col.isTrigger)
                triggers++;
        }

        return $"{collidersMachado.Length} total, {ativos} enabled, {triggers} trigger";
    }

    private static string ObterTagSegura(Transform alvo)
    {
        return alvo != null ? alvo.tag : "null";
    }
}
