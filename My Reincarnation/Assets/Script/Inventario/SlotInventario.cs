using System.Collections;
using System.Collections.Generic;
using TMPro;
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

    [Header("Margem de seguranca (0.9 = 90% do slot)")]
    [SerializeField] private float margemDeSeguranca = 0.9f;

    [Header("Contador")]
    [SerializeField] private TMP_Text contadorTMP;

    [Header("Stack")]
    [SerializeField] private int limiteStack = 99;

    private readonly List<XRGrabInteractable> pilhaItens = new();
    private readonly Dictionary<XRGrabInteractable, EstadoOriginalItem> estadosOriginais = new();

    private XRGrabInteractable itemGuardado;
    private string nomeItemAtual = string.Empty;
    private bool ignorarProximoExited = false;
    private bool inventarioAberto = true;
    private bool visivelNaRolagem = true;
    private bool operacaoInternaSocket;
    private bool atualizandoTopo;
    private Vector3 tamanhoDoSlot;

    private void Awake()
    {
        if (socketInteractor == null)
            socketInteractor = GetComponent<XRSocketInteractor>();

        if (contadorTMP == null)
            contadorTMP = GetComponentInChildren<TMP_Text>(true);

        var col = GetComponent<BoxCollider>();
        tamanhoDoSlot = col != null
            ? Vector3.Scale(col.size, transform.lossyScale)
            : Vector3.one * 0.1f;

        AtualizarContadorTMP();
    }

    private void OnEnable()
    {
        if (socketInteractor != null)
        {
            socketInteractor.selectEntered.AddListener(OnItemEncaixado);
            socketInteractor.selectExited.AddListener(OnItemRetirado);
            socketInteractor.hoverEntered.AddListener(OnItemHoverEntrouNoSocket);
            RegistrarFiltrosInteracao();
        }

        RegistrarFiltroNoItemGuardado();
    }

    private void OnDisable()
    {
        RemoverFiltroDoItemGuardado();

        if (socketInteractor != null)
        {
            RemoverFiltrosInteracao();
            socketInteractor.selectEntered.RemoveListener(OnItemEncaixado);
            socketInteractor.selectExited.RemoveListener(OnItemRetirado);
            socketInteractor.hoverEntered.RemoveListener(OnItemHoverEntrouNoSocket);
        }
    }

    private void OnValidate()
    {
        margemDeSeguranca = Mathf.Clamp(margemDeSeguranca, 0.01f, 1f);
        limiteStack = Mathf.Clamp(limiteStack, 1, 99);
    }

    public bool canProcess => isActiveAndEnabled;

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        SalvarEstadoOriginalSeItem(interactable?.transform);

        if (itemGuardado != null && interactable?.transform == itemGuardado.transform)
            return PodeSelecionarItemGuardado(interactor);

        return PodeInteragirCom(interactable?.transform);
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        SalvarEstadoOriginalSeItem(interactable?.transform);
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

    private bool PodeSelecionarItemGuardado(IXRSelectInteractor interactor)
    {
        if (EhSocketDoSlot(interactor))
            return PodeInteragirCom(itemGuardado.transform);

        return inventarioAberto && visivelNaRolagem && !atualizandoTopo;
    }

    private bool EhSocketDoSlot(IXRSelectInteractor interactor)
    {
        return socketInteractor != null && interactor is Component componente && componente == socketInteractor;
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

    private void RegistrarFiltroNoItemGuardado()
    {
        if (itemGuardado == null)
            return;

        var filtros = itemGuardado.selectFilters;
        for (int i = 0; i < filtros.count; i++)
        {
            if (filtros.GetAt(i) == this)
                return;
        }

        filtros.Add(this);
    }

    private void RemoverFiltroDoItemGuardado()
    {
        if (itemGuardado == null)
            return;

        itemGuardado.selectFilters.Remove(this);
    }

    // --- STACK LIFO -----------------------------------------------------------

    private XRGrabInteractable TopoDaPilha()
    {
        return pilhaItens.Count > 0 ? pilhaItens[pilhaItens.Count - 1] : null;
    }

    private void OnItemHoverEntrouNoSocket(HoverEnterEventArgs args)
    {
        SalvarEstadoOriginalSeItem(args.interactableObject?.transform);

        if (!inventarioAberto || !visivelNaRolagem || pilhaItens.Count == 0)
            return;

        var itemExtra = args.interactableObject.transform.GetComponent<XRGrabInteractable>();
        if (itemExtra == null || itemExtra == itemGuardado || pilhaItens.Contains(itemExtra))
            return;

        TentarEmpilharItemExtra(itemExtra);
    }

    private bool TentarEmpilharItemExtra(XRGrabInteractable itemExtra)
    {
        if (itemExtra == null || pilhaItens.Count == 0)
            return false;

        string nomeExtra = ObterNomeItem(itemExtra);
        if (string.IsNullOrEmpty(nomeExtra) || nomeExtra != nomeItemAtual)
            return false;

        if (pilhaItens.Count >= limiteStack)
            return false;

        SalvarEstadoOriginalSeNecessario(itemExtra);

        XRGrabInteractable topoAnterior = TopoDaPilha();
        pilhaItens.Add(itemExtra);
        AtualizarContadorTMP();

        TrocarTopoSelecionado(topoAnterior, itemExtra);
        return true;
    }

    private void TrocarTopoSelecionado(XRGrabInteractable topoAnterior, XRGrabInteractable novoTopo)
    {
        if (novoTopo == null)
            return;

        atualizandoTopo = true;
        operacaoInternaSocket = true;

        try
        {
            RemoverFiltroDoItemGuardado();

            var manager = socketInteractor != null ? socketInteractor.interactionManager : null;
            if (manager != null && topoAnterior != null && socketInteractor.IsSelecting(topoAnterior))
                manager.SelectExit((IXRSelectInteractor)socketInteractor, (IXRSelectInteractable)topoAnterior);

            if (topoAnterior != null && topoAnterior != novoTopo)
                EsconderItemNaPilha(topoAnterior);

            AplicarVisualNoSlot(novoTopo);

            SelecionarTopoNoSocket(novoTopo);
            RegistrarFiltroNoItemGuardado();
            AtualizarContadorTMP();
        }
        finally
        {
            operacaoInternaSocket = false;
            atualizandoTopo = false;
        }
    }

    private void EsconderItemNaPilha(XRGrabInteractable item)
    {
        if (item == null)
            return;

        SalvarEstadoOriginalSeNecessario(item);
        EstadoOriginalItem estado = estadosOriginais[item];
        RestaurarEscalaOriginalDoEstado(item, estado, false);

        foreach (var estadoRenderer in estado.Renderers)
        {
            if (estadoRenderer.Renderer != null)
                estadoRenderer.Renderer.enabled = false;
        }

        foreach (var estadoCollider in estado.Colliders)
        {
            if (estadoCollider.Collider != null)
                estadoCollider.Collider.enabled = false;
        }

        item.enabled = false;

        var rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
        }

        LogFisicaItem("Esconder item na pilha", item);
    }

    private void AplicarVisualNoSlot(XRGrabInteractable item)
    {
        if (item == null)
            return;

        SalvarEstadoOriginalSeNecessario(item);
        EstadoOriginalItem estado = estadosOriginais[item];

        itemGuardado = item;
        item.gameObject.SetActive(true);
        item.transform.SetParent(estado.ParentOriginal, true);
        RestaurarEscalaOriginalDoEstado(item, estado, false);

        item.enabled = estado.GrabEnabled;

        foreach (var estadoRenderer in estado.Renderers)
        {
            if (estadoRenderer.Renderer != null)
                estadoRenderer.Renderer.enabled = estadoRenderer.Enabled && inventarioAberto && visivelNaRolagem;
        }

        foreach (var estadoCollider in estado.Colliders)
        {
            if (estadoCollider.Collider != null)
                estadoCollider.Collider.enabled = estadoCollider.Enabled;
        }

        var rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = true;
        }

        GarantirEscalaOriginal(item.transform);
        PosicionarItemNoPontoDoSlot(item.transform);
        AjustarEscalaAdaptativa(item.transform);
        Physics.SyncTransforms();
    }

    private void RestaurarItemParaMundo(XRGrabInteractable item)
    {
        if (item == null)
            return;

        if (!estadosOriginais.TryGetValue(item, out EstadoOriginalItem estado))
        {
            RestaurarFisicaDoItem(item);
            StartCoroutine(RestaurarFisicaQuandoXRTerminar(item));
            Physics.SyncTransforms();
            return;
        }

        item.transform.SetParent(estado.ParentOriginal, true);
        RestaurarEscalaOriginalDoEstado(item, estado, true);

        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp != null && escalaComp.inicializado)
            escalaComp.LimparTravaDoSlot(this);

        foreach (var estadoRenderer in estado.Renderers)
        {
            if (estadoRenderer.Renderer != null)
                estadoRenderer.Renderer.enabled = estadoRenderer.Enabled;
        }

        RestaurarFisicaDoItem(item);
        StartCoroutine(RestaurarFisicaQuandoXRTerminar(item));

        Physics.SyncTransforms();
    }

    private IEnumerator RestaurarFisicaQuandoXRTerminar(XRGrabInteractable item)
    {
        yield return null;

        while (item != null && item.isSelected)
            yield return null;

        if (item == null)
            yield break;

        yield return new WaitForFixedUpdate();
        RestaurarFisicaDoItem(item);
    }

    private void RestaurarFisicaDoItem(XRGrabInteractable item)
    {
        if (item == null)
            return;

        item.gameObject.SetActive(true);

        if (!estadosOriginais.TryGetValue(item, out EstadoOriginalItem estado))
        {
            item.enabled = true;

            foreach (var collider in item.GetComponentsInChildren<Collider>(true))
            {
                if (collider != null)
                    collider.enabled = true;
            }

            var rbFallback = item.GetComponent<Rigidbody>();
            if (rbFallback != null)
            {
                rbFallback.isKinematic = false;
                rbFallback.useGravity = true;
                rbFallback.detectCollisions = true;
                rbFallback.WakeUp();
            }

            LogFisicaItem("Restaurar fisica sem estado salvo", item);
            return;
        }

        item.enabled = estado.GrabEnabled;

        foreach (var estadoCollider in estado.Colliders)
        {
            if (estadoCollider.Collider != null)
                estadoCollider.Collider.enabled = estadoCollider.Enabled;
        }

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (estado.TinhaRigidbody)
            {
                if (estado.ForcarFisicaDinamicaAoSair)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.detectCollisions = true;
                }
                else
                {
                    rb.isKinematic = estado.RigidbodyKinematic;
                    rb.useGravity = estado.RigidbodyUseGravity;
                    rb.detectCollisions = estado.RigidbodyDetectCollisions;
                }

                rb.constraints = estado.ConstraintsOriginal;
                rb.collisionDetectionMode = estado.CollisionDetectionModeOriginal;
                rb.interpolation = estado.InterpolationOriginal;
            }
            else
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.detectCollisions = true;
            }

            rb.WakeUp();
        }

        LogFisicaItem("Fisica restaurada", item);
    }

    private void RestaurarEscalaOriginalDoEstado(XRGrabInteractable item, EstadoOriginalItem estado, bool restaurarTargetOriginal)
    {
        if (item == null || estado == null)
            return;

        Transform itemTransform = item.transform;

        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp != null && escalaComp.inicializado)
            escalaComp.LimparTravaDoSlot(this);

        Vector3 escalaLocalOriginal = ConverterEscalaMundoParaLocal(estado.LossyScaleOriginal, itemTransform.parent);
        itemTransform.localScale = escalaLocalOriginal;
        item.SetTargetLocalScale(restaurarTargetOriginal ? estado.TargetLocalScaleOriginal : escalaLocalOriginal);
    }

    private void SelecionarTopoNoSocket(XRGrabInteractable item)
    {
        if (item == null || socketInteractor == null || socketInteractor.interactionManager == null)
            return;

        if (socketInteractor.IsSelecting(item))
            return;

        IXRSelectInteractor interactor = socketInteractor;
        IXRSelectInteractable interactable = item;
        XRInteractionManager manager = socketInteractor.interactionManager;

        if (manager.CanSelect(interactor, interactable))
        {
            manager.SelectEnter(interactor, interactable);
            return;
        }

        manager.SelectEnterUnconditionally(interactor, interactable);
    }

    private void SalvarEstadoOriginalSeNecessario(XRGrabInteractable item)
    {
        if (item == null || estadosOriginais.ContainsKey(item))
            return;

        var estado = new EstadoOriginalItem(item);
        estadosOriginais.Add(item, estado);
        LogFisicaItem("Salvar estado fisico", item);

        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp == null)
            escalaComp = item.gameObject.AddComponent<EscalaOriginalItem>();

        if (!escalaComp.inicializado)
        {
            escalaComp.escalaOriginal = estado.LossyScaleOriginal;
            escalaComp.inicializado = true;
        }
    }

    private void SalvarEstadoOriginalSeItem(Transform itemTransform)
    {
        if (itemTransform == null)
            return;

        var item = itemTransform.GetComponent<XRGrabInteractable>();
        if (item != null)
            SalvarEstadoOriginalSeNecessario(item);
    }

    private static string ObterNomeItem(XRGrabInteractable item)
    {
        if (item == null)
            return string.Empty;

        var dados = item.GetComponent<ItemInventarioDados>();
        if (dados != null && !string.IsNullOrWhiteSpace(dados.NomeItem))
            return LimparNomeItem(dados.NomeItem);

        return ObterNomeLimpo(item.transform);
    }

    private static string ObterNomeLimpo(Transform item)
    {
        return item == null ? string.Empty : LimparNomeItem(item.name);
    }

    public static string LimparNomeItem(string nome)
    {
        return string.IsNullOrEmpty(nome) ? string.Empty : nome.Replace("(Clone)", string.Empty).Trim();
    }

    // --- ENCAIXE --------------------------------------------------------------

    private void OnItemEncaixado(SelectEnterEventArgs args)
    {
        if (operacaoInternaSocket)
            return;

        if (!inventarioAberto || !visivelNaRolagem)
            return;

        var item = args.interactableObject.transform.GetComponent<XRGrabInteractable>();
        if (item == null)
            return;

        if (pilhaItens.Count > 0)
        {
            TentarEmpilharItemExtra(item);
            return;
        }

        SalvarEstadoOriginalSeNecessario(item);

        pilhaItens.Add(item);
        nomeItemAtual = ObterNomeItem(item);

        AplicarVisualNoSlot(item);
        RegistrarFiltroNoItemGuardado();
        AtualizarContadorTMP();
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
        if (operacaoInternaSocket || args.isCanceled)
            return;

        if (ignorarProximoExited || !inventarioAberto || !visivelNaRolagem || !gameObject.activeInHierarchy || !enabled)
        {
            ignorarProximoExited = false;
            return;
        }

        if (itemGuardado == null)
            return;

        var item = itemGuardado.transform;
        if (item == null || !item.gameObject.activeInHierarchy)
            return;

        if (args.interactableObject == null || args.interactableObject.transform != item)
            return;

        XRGrabInteractable itemRemovido = itemGuardado;
        if (TopoDaPilha() != itemRemovido)
            return;

        RemoverFiltroDoItemGuardado();

        pilhaItens.RemoveAt(pilhaItens.Count - 1);
        LogFisicaItem("Retirar item do inventario", itemRemovido);

        itemGuardado = null;
        RestaurarItemParaMundo(itemRemovido);
        AtualizarContadorTMP();

        if (pilhaItens.Count == 0)
        {
            nomeItemAtual = string.Empty;
            return;
        }

        PromoverNovoTopoAposRetirada();
    }

    private void PromoverNovoTopoAposRetirada()
    {
        XRGrabInteractable novoTopo = TopoDaPilha();
        if (novoTopo == null)
            return;

        atualizandoTopo = true;
        operacaoInternaSocket = true;

        try
        {
            AplicarVisualNoSlot(novoTopo);
            SelecionarTopoNoSocket(novoTopo);
            RegistrarFiltroNoItemGuardado();
            AtualizarContadorTMP();
        }
        finally
        {
            operacaoInternaSocket = false;
            atualizandoTopo = false;
        }
    }

    private void LiberarItem(Transform item)
    {
        var grab = item != null ? item.GetComponent<XRGrabInteractable>() : null;
        if (grab != null)
            RestaurarItemParaMundo(grab);
    }

    // --- ESCALA ---------------------------------------------------------------

    private void GarantirEscalaOriginal(Transform item)
    {
        var comp = item.GetComponent<EscalaOriginalItem>();

        if (comp == null)
        {
            comp = item.gameObject.AddComponent<EscalaOriginalItem>();
            comp.escalaOriginal = item.lossyScale;
            comp.inicializado = true;
            return;
        }

        if (!comp.inicializado)
        {
            comp.escalaOriginal = item.lossyScale;
            comp.inicializado = true;
        }
    }

    private void AjustarEscalaAdaptativa(Transform item)
    {
        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp == null || !escalaComp.inicializado)
            return;

        float margem = Mathf.Clamp(margemDeSeguranca, 0.01f, 1f);
        if (escalaComp.EscalaJaAplicadaPara(this, margem))
            return;

        BoxCollider slotCollider = GetComponent<BoxCollider>();
        if (slotCollider == null)
            return;

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

    private static Vector3 ConverterEscalaMundoParaLocal(Vector3 escalaMundo, Transform parent)
    {
        Vector3 escalaParent = parent != null ? parent.lossyScale : Vector3.one;
        return new Vector3(
            DividirSeguro(escalaMundo.x, escalaParent.x),
            DividirSeguro(escalaMundo.y, escalaParent.y),
            DividirSeguro(escalaMundo.z, escalaParent.z)
        );
    }

    private static int ContarCollidersAtivos(XRGrabInteractable item)
    {
        if (item == null)
            return 0;

        int ativos = 0;
        foreach (var collider in item.GetComponentsInChildren<Collider>(true))
        {
            if (collider != null && collider.enabled)
                ativos++;
        }

        return ativos;
    }

    private static void LogFisicaItem(string etapa, XRGrabInteractable item)
    {
        if (item == null)
        {
            Debug.Log($"[SlotInventario][Fisica] {etapa} | item=null");
            return;
        }

        Rigidbody rb = item.GetComponent<Rigidbody>();
        string fisica = rb != null
            ? $"isKinematic={rb.isKinematic} useGravity={rb.useGravity} detectCollisions={rb.detectCollisions}"
            : "sem Rigidbody";

        Debug.Log($"[SlotInventario][Fisica] {etapa} | item={item.name} | {fisica} | collidersAtivos={ContarCollidersAtivos(item)}");
    }

    private bool TentarObterBounds(Transform item, out Bounds bounds)
    {
        Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(item.position, Vector3.zero);
        bool achou = false;

        foreach (Renderer r in renderers)
        {
            if (r == null)
                continue;

            if (!achou)
            {
                bounds = r.bounds;
                achou = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return achou;
    }

    // --- ABRIR / FECHAR INVENTARIO -------------------------------------------

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
            {
                if (r != null)
                    r.enabled = visivel && RendererEraOriginalmenteAtivo(itemGuardado, r);
            }
        }

        AtualizarContadorTMP();
    }

    private bool RendererEraOriginalmenteAtivo(XRGrabInteractable item, Renderer renderer)
    {
        if (item == null || renderer == null || !estadosOriginais.TryGetValue(item, out EstadoOriginalItem estado))
            return true;

        for (int i = 0; i < estado.Renderers.Length; i++)
        {
            if (estado.Renderers[i].Renderer == renderer)
                return estado.Renderers[i].Enabled;
        }

        return true;
    }

    private void AtualizarContadorTMP()
    {
        if (contadorTMP == null)
            return;

        contadorTMP.text = pilhaItens.Count.ToString();
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

    private readonly struct EstadoRenderer
    {
        public readonly Renderer Renderer;
        public readonly bool Enabled;

        public EstadoRenderer(Renderer renderer)
        {
            Renderer = renderer;
            Enabled = renderer != null && renderer.enabled;
        }
    }

    private readonly struct EstadoCollider
    {
        public readonly Collider Collider;
        public readonly bool Enabled;

        public EstadoCollider(Collider collider)
        {
            Collider = collider;
            Enabled = collider != null && collider.enabled;
        }
    }

    private sealed class EstadoOriginalItem
    {
        public readonly Transform ParentOriginal;
        public readonly Vector3 LossyScaleOriginal;
        public readonly Vector3 TargetLocalScaleOriginal;
        public readonly bool GrabEnabled;
        public readonly Rigidbody Rigidbody;
        public readonly bool TinhaRigidbody;
        public readonly bool RigidbodyKinematic;
        public readonly bool RigidbodyUseGravity;
        public readonly bool RigidbodyDetectCollisions;
        public readonly bool ForcarFisicaDinamicaAoSair;
        public readonly RigidbodyConstraints ConstraintsOriginal;
        public readonly CollisionDetectionMode CollisionDetectionModeOriginal;
        public readonly RigidbodyInterpolation InterpolationOriginal;
        public readonly EstadoRenderer[] Renderers;
        public readonly EstadoCollider[] Colliders;

        public EstadoOriginalItem(XRGrabInteractable item)
        {
            Transform transformItem = item.transform;
            ParentOriginal = transformItem.parent;
            LossyScaleOriginal = transformItem.lossyScale;
            TargetLocalScaleOriginal = item.GetTargetLocalScale();
            GrabEnabled = item.enabled;

            Rigidbody = item.GetComponent<Rigidbody>();
            TinhaRigidbody = Rigidbody != null;
            RigidbodyKinematic = Rigidbody != null && Rigidbody.isKinematic;
            RigidbodyUseGravity = Rigidbody != null && Rigidbody.useGravity;
            RigidbodyDetectCollisions = Rigidbody == null || Rigidbody.detectCollisions;
            ForcarFisicaDinamicaAoSair = TinhaRigidbody && (!RigidbodyKinematic || RigidbodyUseGravity || item.isSelected);
            ConstraintsOriginal = Rigidbody != null ? Rigidbody.constraints : RigidbodyConstraints.None;
            CollisionDetectionModeOriginal = Rigidbody != null ? Rigidbody.collisionDetectionMode : CollisionDetectionMode.Discrete;
            InterpolationOriginal = Rigidbody != null ? Rigidbody.interpolation : RigidbodyInterpolation.None;

            Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
            Renderers = new EstadoRenderer[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                Renderers[i] = new EstadoRenderer(renderers[i]);

            Collider[] colliders = item.GetComponentsInChildren<Collider>(true);
            Colliders = new EstadoCollider[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
                Colliders[i] = new EstadoCollider(colliders[i]);
        }
    }
}
