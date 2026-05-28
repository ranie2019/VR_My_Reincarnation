using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class SlotInventario : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
{
    [Header("Filho visual do slot")]
    [SerializeField] private GameObject visualSlot;

    [Header("Socket do slot")]
    [SerializeField] private XRSocketInteractor socketInteractor;

    [Header("Margem de segurança (0.9 = 90% do slot)")]
    [SerializeField] private float margemDeSeguranca = 0.9f;

    private XRGrabInteractable itemGuardado;
    private bool ignorarProximoExited = false;
    private bool inventarioAberto = true;
    private bool visivelNaRolagem = true;
    private Vector3 tamanhoDoSlot;

    private void Awake()
    {
        if (socketInteractor == null)
            socketInteractor = GetComponent<XRSocketInteractor>();

        var col = GetComponent<BoxCollider>();
        tamanhoDoSlot = col != null
            ? Vector3.Scale(col.size, transform.lossyScale)
            : Vector3.one * 0.1f;
    }

    private void OnEnable()
    {
        if (socketInteractor != null)
        {
            socketInteractor.selectEntered.AddListener(OnItemEncaixado);
            socketInteractor.selectExited.AddListener(OnItemRetirado);
            RegistrarFiltrosInteracao();
        }
    }

    private void OnDisable()
    {
        if (socketInteractor != null)
        {
            RemoverFiltrosInteracao();
            socketInteractor.selectEntered.RemoveListener(OnItemEncaixado);
            socketInteractor.selectExited.RemoveListener(OnItemRetirado);
        }
    }

    public bool canProcess => isActiveAndEnabled;

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        return PodeInteragirCom(interactable?.transform);
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        return PodeInteragirCom(interactable?.transform);
    }

    private bool PodeInteragirCom(Transform interactableTransform)
    {
        if (inventarioAberto && visivelNaRolagem)
            return true;

        if (itemGuardado == null || interactableTransform == null)
            return false;

        return interactableTransform == itemGuardado.transform;
    }

    private void RegistrarFiltrosInteracao()
    {
        RegistrarFiltroSelecao();
        RegistrarFiltroHover();
    }

    private void RemoverFiltrosInteracao()
    {
        socketInteractor.selectFilters.Remove(this);
        socketInteractor.hoverFilters.Remove(this);
    }

    private void RegistrarFiltroSelecao()
    {
        var filtros = socketInteractor.selectFilters;

        for (int i = 0; i < filtros.count; i++)
        {
            if (filtros.GetAt(i) == this)
                return;
        }

        filtros.Add(this);
    }

    private void RegistrarFiltroHover()
    {
        var filtros = socketInteractor.hoverFilters;

        for (int i = 0; i < filtros.count; i++)
        {
            if (filtros.GetAt(i) == this)
                return;
        }

        filtros.Add(this);
    }

    // --- ENCAIXE --------------------------------------------------------------

    private void OnItemEncaixado(SelectEnterEventArgs args)
    {
        if (!inventarioAberto || !visivelNaRolagem) return;

        var item = args.interactableObject.transform
            .GetComponent<XRGrabInteractable>();
        if (item == null) return;

        itemGuardado = item;
        GarantirEscalaOriginal(item.transform);
        StartCoroutine(FinalizarEncaixe(item.transform));
    }

    private IEnumerator FinalizarEncaixe(Transform item)
    {
        yield return new WaitForFixedUpdate();
        yield return null;

        if (item == null || itemGuardado == null) yield break;

        item.SetParent(ObterContainerItensGuardados(), true);
        PosicionarItemNoPontoDoSlot(item);
        AjustarEscalaAdaptativa(item);
    }

    private Transform ObterContainerItensGuardados()
    {
        Transform paiDoContainer = transform.parent != null ? transform.parent : transform.root;
        Transform container = paiDoContainer.Find("ItensGuardadosRuntime");

        if (container == null)
        {
            container = new GameObject("ItensGuardadosRuntime").transform;
            container.SetParent(paiDoContainer, false);
        }

        container.localPosition = Vector3.zero;
        container.localRotation = Quaternion.identity;
        container.localScale = Vector3.one;

        return container;
    }

    private void PosicionarItemNoPontoDoSlot(Transform item)
    {
        Transform pontoEncaixe = socketInteractor != null ? socketInteractor.attachTransform : null;

        if (pontoEncaixe != null)
        {
            item.SetPositionAndRotation(pontoEncaixe.position, pontoEncaixe.rotation);
            return;
        }

        item.SetPositionAndRotation(transform.position, transform.rotation);
    }

    // --- RETIRADA -------------------------------------------------------------

    private void OnItemRetirado(SelectExitEventArgs args)
    {
        if (args.isCanceled)
            return;

        if (ignorarProximoExited || !inventarioAberto || !visivelNaRolagem || !gameObject.activeInHierarchy || !enabled)
        {
            ignorarProximoExited = false;
            return;
        }

        if (itemGuardado == null) return;

        var item = itemGuardado.transform;
        if (item == null || !item.gameObject.activeInHierarchy)
            return;

        if (args.interactableObject == null || args.interactableObject.transform != item)
            return;

        itemGuardado = null;

        LiberarItem(item);
    }

    private void LiberarItem(Transform item)
    {
        if (item == null) return;

        item.SetParent(null, true);

        // FIX ESCALA: restaura a escala de MUNDO salva (quando pai é null, lossyScale == localScale)
        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp != null && escalaComp.inicializado)
        {
            escalaComp.LimparTravaDoSlot(this);
            item.localScale = escalaComp.escalaOriginal;
        }

        var rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    // --- ESCALA ---------------------------------------------------------------

    private void GarantirEscalaOriginal(Transform item)
    {
        var comp = item.GetComponent<EscalaOriginalItem>();

        if (comp == null)
        {
            comp = item.gameObject.AddComponent<EscalaOriginalItem>();
            // FIX: salva lossyScale (escala de mundo), não localScale
            // localScale varia dependendo do pai — lossyScale é sempre absoluta
            comp.escalaOriginal = item.lossyScale;
            comp.inicializado = true;
            return;
        }

        if (!comp.inicializado)
        {
            comp.escalaOriginal = item.lossyScale;
            comp.inicializado = true;
        }

        // Se já inicializado: NUNCA sobrescreve — escalaOriginal está protegida
    }

    private void AjustarEscalaAdaptativa(Transform item)
    {
        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp == null || !escalaComp.inicializado) return;

        float margem = Mathf.Clamp(margemDeSeguranca, 0.01f, 1f);
        if (escalaComp.EscalaJaAplicadaPara(this, margem)) return;

        BoxCollider slotCollider = GetComponent<BoxCollider>();
        if (slotCollider == null) return;

        var itemEscalavel = item.GetComponent<ItemInventarioEscalavel>();
        if (itemEscalavel == null)
            itemEscalavel = item.gameObject.AddComponent<ItemInventarioEscalavel>();

        if (itemEscalavel.AjustarParaSlot(slotCollider, margem, transform))
            escalaComp.MarcarEscalaAplicada(this, margem);
    }
    private bool TentarObterBoundsDoSlot(out Bounds bounds)
    {
        var col = GetComponent<Collider>();
        if (col != null)
        {
            bounds = col.bounds;
            return true;
        }

        if (visualSlot != null && TentarObterBounds(visualSlot.transform, out bounds))
            return true;

        bounds = new Bounds(transform.position, tamanhoDoSlot);
        return TamanhoValido(tamanhoDoSlot);
    }

    private static bool TamanhoValido(Vector3 tamanho)
    {
        return tamanho.x > 0f && tamanho.y > 0f && tamanho.z > 0f;
    }

    private static float DividirSeguro(float valor, float divisor)
    {
        return Mathf.Approximately(divisor, 0f) ? valor : valor / divisor;
    }
    private bool TentarObterBounds(Transform item, out Bounds bounds)
    {
        Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(item.position, Vector3.zero);
        bool achou = false;

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (!achou) { bounds = r.bounds; achou = true; }
            else bounds.Encapsulate(r.bounds);
        }

        return achou;
    }

    // --- ABRIR / FECHAR INVENTÁRIO --------------------------------------------

    private void SetVisualSlotVisivel(bool visivel)
    {
        if (visualSlot == null)
            return;

        foreach (var canvas in visualSlot.GetComponentsInChildren<Canvas>(true))
            canvas.enabled = visivel;

        foreach (var graphic in visualSlot.GetComponentsInChildren<Graphic>(true))
            graphic.enabled = visivel;

        foreach (var renderer in visualSlot.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = visivel;
    }

    private void AtualizarVisibilidadeVisual()
    {
        bool visivel = inventarioAberto && visivelNaRolagem;

        SetVisualSlotVisivel(visivel);

        if (itemGuardado != null)
        {
            foreach (var r in itemGuardado.GetComponentsInChildren<Renderer>(true))
                r.enabled = visivel;
        }
    }

    public void SetVisivelNaRolagem(bool visivel)
    {
        visivelNaRolagem = visivel;
        AtualizarVisibilidadeVisual();
    }

    public void SetInventarioAberto(bool aberto)
    {
        inventarioAberto = aberto;

        if (aberto)
            ignorarProximoExited = false;

        AtualizarVisibilidadeVisual();
    }
}