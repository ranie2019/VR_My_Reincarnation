using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class SlotInventario : MonoBehaviour
{
    [Header("Filho visual do slot")]
    [SerializeField] private GameObject visualSlot;

    [Header("Socket do slot")]
    [SerializeField] private XRSocketInteractor socketInteractor;

    [Header("Ponto central do encaixe")]
    [SerializeField] private Transform pontoEncaixe;

    [Header("Ajuste automático dentro do slot")]
    [SerializeField] private Vector3 rotacaoPadraoNoSlot = new Vector3(0f, 90f, 0f);
    [SerializeField] private Vector3 tamanhoMaximoNoSlot = new Vector3(0.12f, 0.12f, 0.12f);
    [SerializeField] private float margemDeSeguranca = 0.85f;
    [SerializeField] private bool ajustarEscalaAutomaticamente = true;
    [SerializeField] private bool centralizarPeloCentroVisual = true;

    [Header("Ao retirar o item")]
    [SerializeField] private bool restaurarEscalaAoRetirar = true;

    private XRGrabInteractable itemGuardado;
    private Rigidbody rbGuardado;

    private Transform parentOriginal;
    private Vector3 escalaOriginal;
    private bool estadoKinematicOriginal;

    public bool Ocupado => itemGuardado != null;

    private void Awake()
    {
        if (socketInteractor == null)
            socketInteractor = GetComponent<XRSocketInteractor>();

        if (pontoEncaixe == null)
            pontoEncaixe = transform;

        if (socketInteractor != null)
            socketInteractor.attachTransform = pontoEncaixe;
    }

    private void OnEnable()
    {
        if (socketInteractor != null)
        {
            socketInteractor.selectEntered.AddListener(OnItemEncaixado);
            socketInteractor.selectExited.AddListener(OnItemRetirado);
        }
    }

    private void OnDisable()
    {
        if (socketInteractor != null)
        {
            socketInteractor.selectEntered.RemoveListener(OnItemEncaixado);
            socketInteractor.selectExited.RemoveListener(OnItemRetirado);
        }
    }

    private void LateUpdate()
    {
        if (itemGuardado != null)
            AplicarTransformAutomaticoDoSlot();
    }

    private void OnItemEncaixado(SelectEnterEventArgs args)
    {
        XRGrabInteractable item = args.interactableObject.transform.GetComponent<XRGrabInteractable>();

        if (item == null)
            return;

        itemGuardado = item;
        rbGuardado = itemGuardado.GetComponent<Rigidbody>();

        parentOriginal = itemGuardado.transform.parent;
        escalaOriginal = itemGuardado.transform.localScale;

        if (rbGuardado != null)
        {
            estadoKinematicOriginal = rbGuardado.isKinematic;

            if (!rbGuardado.isKinematic)
            {
                rbGuardado.linearVelocity = Vector3.zero;
                rbGuardado.angularVelocity = Vector3.zero;
            }

            rbGuardado.isKinematic = true;
        }

        AplicarTransformAutomaticoDoSlot();
    }

    private void OnItemRetirado(SelectExitEventArgs args)
    {
        if (itemGuardado == null)
            return;

        if (restaurarEscalaAoRetirar)
            itemGuardado.transform.localScale = escalaOriginal;

        itemGuardado.transform.SetParent(parentOriginal, true);

        if (rbGuardado != null)
            rbGuardado.isKinematic = estadoKinematicOriginal;

        itemGuardado = null;
        rbGuardado = null;
        parentOriginal = null;
    }

    private void AplicarTransformAutomaticoDoSlot()
    {
        if (itemGuardado == null || pontoEncaixe == null)
            return;

        Transform itemTransform = itemGuardado.transform;

        itemTransform.SetParent(pontoEncaixe, true);

        itemTransform.position = pontoEncaixe.position;
        itemTransform.rotation = pontoEncaixe.rotation * Quaternion.Euler(rotacaoPadraoNoSlot);

        if (ajustarEscalaAutomaticamente)
            AjustarEscalaParaCaberNoSlot(itemTransform);

        if (centralizarPeloCentroVisual)
            CentralizarPeloRenderer(itemTransform);
    }

    private void AjustarEscalaParaCaberNoSlot(Transform itemTransform)
    {
        if (!TentarObterBounds(itemTransform, out Bounds bounds))
            return;

        Vector3 tamanhoAtual = bounds.size;

        if (tamanhoAtual.x <= 0f || tamanhoAtual.y <= 0f || tamanhoAtual.z <= 0f)
            return;

        Vector3 tamanhoAlvoMundo = new Vector3(
            Mathf.Abs(tamanhoMaximoNoSlot.x * pontoEncaixe.lossyScale.x),
            Mathf.Abs(tamanhoMaximoNoSlot.y * pontoEncaixe.lossyScale.y),
            Mathf.Abs(tamanhoMaximoNoSlot.z * pontoEncaixe.lossyScale.z)
        );

        float fatorX = tamanhoAlvoMundo.x / tamanhoAtual.x;
        float fatorY = tamanhoAlvoMundo.y / tamanhoAtual.y;
        float fatorZ = tamanhoAlvoMundo.z / tamanhoAtual.z;

        float fatorFinal = Mathf.Min(fatorX, fatorY, fatorZ) * margemDeSeguranca;

        itemTransform.localScale *= fatorFinal;
    }

    private void CentralizarPeloRenderer(Transform itemTransform)
    {
        if (!TentarObterBounds(itemTransform, out Bounds bounds))
            return;

        Vector3 deslocamento = pontoEncaixe.position - bounds.center;
        itemTransform.position += deslocamento;
    }

    private bool TentarObterBounds(Transform itemTransform, out Bounds bounds)
    {
        Renderer[] renderers = itemTransform.GetComponentsInChildren<Renderer>(true);

        bounds = new Bounds(itemTransform.position, Vector3.zero);

        bool encontrouRenderer = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!encontrouRenderer)
            {
                bounds = renderer.bounds;
                encontrouRenderer = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return encontrouRenderer;
    }

    public void SetInventarioAberto(bool aberto)
    {
        if (visualSlot != null)
            visualSlot.SetActive(aberto);

        if (socketInteractor != null)
            socketInteractor.socketActive = aberto;

        if (itemGuardado != null)
            itemGuardado.gameObject.SetActive(aberto);
    }
}