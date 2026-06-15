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
    private const string PrefixoDebugEscala = "[ESCALA INVENTARIO DEBUG]";

    [Header("Filho visual do slot")]
    [SerializeField] private GameObject visualSlot;

    [Header("Socket do slot")]
    [SerializeField] private XRSocketInteractor socketInteractor;
    [SerializeField] private Transform pontoEncaixe;

    [Header("Margem de seguranca (0.9 = 90% do slot)")]
    [SerializeField] private float margemDeSeguranca = 0.9f;

    [Header("Contador")]
    [SerializeField] private TMP_Text contadorTMP;

    [Header("Stack")]
    [SerializeField] private int limiteStack = 99;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somAdicionarItem;
    [SerializeField] private AudioClip somRetirarItem;

    private readonly List<XRGrabInteractable> pilhaItens = new();
    private readonly Dictionary<XRGrabInteractable, EstadoOriginalItem> estadosOriginais = new();

    private XRGrabInteractable itemGuardado;
    private string nomeItemAtual = string.Empty;
    private bool ignorarProximoExited = false;
    private bool inventarioAberto = true;
    private bool visivelNaRolagem = true;
    private bool operacaoInternaSocket;
    private bool atualizandoTopo;

    private void Awake()
    {
        if (socketInteractor == null)
            socketInteractor = GetComponent<XRSocketInteractor>();

        SincronizarPontoEncaixeDoSocket();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (contadorTMP == null)
            contadorTMP = GetComponentInChildren<TMP_Text>(true);

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

        if (socketInteractor == null)
            socketInteractor = GetComponent<XRSocketInteractor>();

        SincronizarPontoEncaixeDoSocket();
    }

    private void LateUpdate()
    {
        if (DeveManterPivotNoPontoEncaixe())
            PosicionarItemNoPontoDoSlot(itemGuardado.transform);
    }

    public bool canProcess => isActiveAndEnabled;

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        SalvarEstadoOriginalSeItem(interactable?.transform);

        var item = interactable?.transform != null
            ? interactable.transform.GetComponent<XRGrabInteractable>()
            : null;

        if (ItemPertenceAOutroSlot(item, "Filtro select"))
            return false;

        if (itemGuardado != null && interactable?.transform == itemGuardado.transform)
            return PodeSelecionarItemGuardado(interactor);

        return PodeInteragirCom(interactable?.transform);
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        SalvarEstadoOriginalSeItem(interactable?.transform);

        var item = interactable?.transform != null
            ? interactable.transform.GetComponent<XRGrabInteractable>()
            : null;

        if (ItemPertenceAOutroSlot(item, "Filtro hover"))
            return false;

        return PodeInteragirCom(interactable?.transform);
    }

    private bool PodeInteragirCom(Transform interactableTransform)
    {
        var item = interactableTransform != null
            ? interactableTransform.GetComponent<XRGrabInteractable>()
            : null;

        if (ItemPertenceAOutroSlot(item, "PodeInteragirCom"))
            return false;

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

        if (EhOutroSocket(interactor))
            return false;

        return inventarioAberto && visivelNaRolagem && !atualizandoTopo;
    }

    private bool EhSocketDoSlot(IXRSelectInteractor interactor)
    {
        return socketInteractor != null && interactor is Component componente && componente == socketInteractor;
    }

    private bool EhOutroSocket(IXRSelectInteractor interactor)
    {
        return socketInteractor != null &&
               interactor is XRSocketInteractor outroSocket &&
               outroSocket != socketInteractor;
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

    private void SincronizarPontoEncaixeDoSocket()
    {
        if (socketInteractor == null)
            return;

        if (pontoEncaixe == null)
            pontoEncaixe = BuscarPontoEncaixeNoSlot();

        if (pontoEncaixe == null)
            pontoEncaixe = socketInteractor.attachTransform;

        if (pontoEncaixe != null && socketInteractor.attachTransform != pontoEncaixe)
            socketInteractor.attachTransform = pontoEncaixe;
    }

    private Transform BuscarPontoEncaixeNoSlot()
    {
        Transform direto = transform.Find("PontoEncaixe");
        if (direto != null)
            return direto;

        Transform[] filhos = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < filhos.Length; i++)
        {
            if (filhos[i] != transform && filhos[i].name == "PontoEncaixe")
                return filhos[i];
        }

        return null;
    }

    private bool DeveManterPivotNoPontoEncaixe()
    {
        return itemGuardado != null &&
               socketInteractor != null &&
               socketInteractor.IsSelecting(itemGuardado) &&
               SelecionadoApenasPeloSocket(itemGuardado);
    }

    private bool SelecionadoApenasPeloSocket(XRGrabInteractable item)
    {
        if (item == null || item.interactorsSelecting.Count == 0)
            return false;

        for (int i = 0; i < item.interactorsSelecting.Count; i++)
        {
            if (!EhSocketDoSlot(item.interactorsSelecting[i]))
                return false;
        }

        return true;
    }

    // --- STACK LIFO -----------------------------------------------------------

    private XRGrabInteractable TopoDaPilha()
    {
        return pilhaItens.Count > 0 ? pilhaItens[pilhaItens.Count - 1] : null;
    }

    private EstadoItemInventario ObterEstadoInventario(XRGrabInteractable item, bool criarSeNecessario)
    {
        if (item == null)
            return null;

        var estado = item.GetComponent<EstadoItemInventario>();
        if (estado == null && criarSeNecessario)
            estado = item.gameObject.AddComponent<EstadoItemInventario>();

        return estado;
    }

    private bool ItemPertenceAOutroSlot(XRGrabInteractable item, string contexto)
    {
        var estado = ObterEstadoInventario(item, false);
        if (estado == null || !estado.estaNoInventario || estado.slotAtual == this)
            return false;

        return true;
    }

    private bool PodeAceitarItemNoSlot(XRGrabInteractable item, string contexto)
    {
        if (item == null)
            return false;

        if (ItemPertenceAOutroSlot(item, contexto))
            return false;

        var estado = ObterEstadoInventario(item, false);
        if (estado != null && estado.estaNoInventario && estado.slotAtual == this && !pilhaItens.Contains(item))
            return false;

        return true;
    }

    private EstadoItemInventario MarcarItemAceitoPeloSlot(XRGrabInteractable item, bool escondido, string contexto)
    {
        var estado = ObterEstadoInventario(item, true);
        if (estado == null)
            return null;

        estado.MarcarAceito(this, escondido);
        MarcarPersistenciaComoInventario(item);
        return estado;
    }

    private void MarcarItemComoTopo(XRGrabInteractable item)
    {
        var estado = ObterEstadoInventario(item, true);
        if (estado == null)
            return;

        estado.MarcarTopo(this);
        MarcarPersistenciaComoInventario(item);
    }

    private void MarcarItemComoEscondidoNaPilha(XRGrabInteractable item)
    {
        var estado = ObterEstadoInventario(item, true);
        if (estado == null)
            return;

        estado.MarcarEscondido(this);
        MarcarPersistenciaComoInventario(item);
    }

    private void LiberarEstadoInventarioDoItem(XRGrabInteractable item)
    {
        var estado = ObterEstadoInventario(item, false);
        if (estado != null)
            estado.Liberar();

        MarcarPersistenciaComoSoltoNaCena(item);
    }

    private void MarcarPersistenciaComoInventario(XRGrabInteractable item)
    {
        ItemPersistente persistente = item != null ? item.GetComponent<ItemPersistente>() : null;
        if (persistente != null)
            persistente.MarcarComoNoInventario();
    }

    private void MarcarPersistenciaComoSoltoNaCena(XRGrabInteractable item)
    {
        ItemPersistente persistente = item != null ? item.GetComponent<ItemPersistente>() : null;
        if (persistente != null)
            persistente.MarcarComoSoltoNaCena();
    }

    private void RejeitarSelecaoDoSocket(XRGrabInteractable item, string contexto)
    {
        if (item == null || socketInteractor == null || socketInteractor.interactionManager == null)
            return;

        if (!socketInteractor.IsSelecting(item))
            return;

        bool operacaoAnterior = operacaoInternaSocket;
        operacaoInternaSocket = true;

        try
        {
            socketInteractor.interactionManager.SelectExit(
                (IXRSelectInteractor)socketInteractor,
                (IXRSelectInteractable)item
            );
        }
        finally
        {
            operacaoInternaSocket = operacaoAnterior;
        }
    }

    private void OnItemHoverEntrouNoSocket(HoverEnterEventArgs args)
    {
        SalvarEstadoOriginalSeItem(args.interactableObject?.transform);

        if (!inventarioAberto || !visivelNaRolagem || pilhaItens.Count == 0)
            return;

        var itemExtra = args.interactableObject.transform.GetComponent<XRGrabInteractable>();
        if (itemExtra == null || itemExtra == itemGuardado || pilhaItens.Contains(itemExtra))
            return;

        if (!PodeAceitarItemNoSlot(itemExtra, "hover do socket"))
            return;

        TentarEmpilharItemExtra(itemExtra);
    }

    private bool TentarEmpilharItemExtra(XRGrabInteractable itemExtra)
    {
        if (itemExtra == null || pilhaItens.Count == 0)
            return false;

        if (pilhaItens.Contains(itemExtra))
            return false;

        if (!PodeAceitarItemNoSlot(itemExtra, "empilhar item extra"))
            return false;

        string nomeExtra = ObterNomeItem(itemExtra);
        if (string.IsNullOrEmpty(nomeExtra) || nomeExtra != nomeItemAtual)
            return false;

        if (pilhaItens.Count >= limiteStack)
            return false;

        SalvarEstadoOriginalSeNecessario(itemExtra);
        MarcarItemAceitoPeloSlot(itemExtra, false, "empilhar item extra");

        XRGrabInteractable topoAnterior = TopoDaPilha();
        pilhaItens.Add(itemExtra);
        AtualizarContadorTMP();

        TrocarTopoSelecionado(topoAnterior, itemExtra);
        TocarSom(somAdicionarItem);
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

        SetUiDoItemVisivel(estado, false);

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

        MarcarItemComoEscondidoNaPilha(item);
    }

    private void AplicarVisualNoSlot(XRGrabInteractable item, bool parentarNoSlot = false)
    {
        if (item == null)
            return;

        SalvarEstadoOriginalSeNecessario(item);
        EstadoOriginalItem estado = estadosOriginais[item];
        LogEscalaInventario("escala antes de entrar no slot", item, ObterEscalaOriginalMundo(item, estado));

        itemGuardado = item;
        MarcarItemComoTopo(item);
        item.gameObject.SetActive(true);

        if (parentarNoSlot)
            ParentarItemNoSlot(item);
        else
            item.transform.SetParent(estado.ParentOriginal, true);

        RestaurarEscalaOriginalDoEstado(item, estado, false);
        LogEscalaInventario("escala restaurada para base antes do slot", item, ObterEscalaOriginalMundo(item, estado));

        item.enabled = estado.GrabEnabled;

        foreach (var estadoRenderer in estado.Renderers)
        {
            if (estadoRenderer.Renderer != null)
                estadoRenderer.Renderer.enabled = estadoRenderer.Enabled && inventarioAberto && visivelNaRolagem;
        }

        SetUiDoItemVisivel(estado, false);

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
        PrepararEscalaParaSlot(item.transform, estado);
        AjustarEscalaAdaptativa(item.transform);
        LogEscalaInventario("escala apos aplicar slot", item, ObterEscalaOriginalMundo(item, estado));
        PosicionarItemNoPontoDoSlot(item.transform);
        Physics.SyncTransforms();
    }

    private void RestaurarItemParaMundo(XRGrabInteractable item)
    {
        if (item == null)
            return;

        if (!estadosOriginais.TryGetValue(item, out EstadoOriginalItem estado))
        {
            RestaurarEscalaEParentParaMundo(item);
            RestaurarFisicaDoItem(item);
            LiberarEstadoInventarioDoItem(item);
            StartCoroutine(RestaurarFisicaQuandoXRTerminar(item));
            Physics.SyncTransforms();
            return;
        }

        RestaurarEscalaEParentParaMundo(item);

        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp != null && escalaComp.inicializado)
            escalaComp.LimparTravaDoSlot(this);

        foreach (var estadoRenderer in estado.Renderers)
        {
            if (estadoRenderer.Renderer != null)
                estadoRenderer.Renderer.enabled = estadoRenderer.Enabled;
        }

        RestaurarUiOriginalDoItem(estado);

        RestaurarFisicaDoItem(item);
        LiberarEstadoInventarioDoItem(item);
        StartCoroutine(RestaurarEscalaOriginalNoProximoFrame(item));
        StartCoroutine(RestaurarFisicaQuandoXRTerminar(item));

        Physics.SyncTransforms();
    }

    private IEnumerator RestaurarEscalaOriginalNoProximoFrame(XRGrabInteractable item)
    {
        yield return null;

        if (item == null)
            yield break;

        RestaurarEscalaEParentParaMundo(item);

        yield return new WaitForFixedUpdate();

        if (item != null)
            RestaurarEscalaEParentParaMundo(item);

        while (item != null && item.isSelected)
            yield return null;

        if (item != null)
            RestaurarEscalaEParentParaMundo(item);
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

    }

    private void RestaurarEscalaEParentParaMundo(XRGrabInteractable item)
    {
        if (item == null)
            return;

        if (!estadosOriginais.TryGetValue(item, out EstadoOriginalItem estado))
        {
            item.transform.SetParent(null, true);
            LogEscalaInventario("escala ao retirar do inventario", item, item.transform.lossyScale);
            return;
        }

        item.transform.SetParent(null, true);

        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp != null && escalaComp.inicializado)
            escalaComp.LimparTravaDoSlot(this);

        Vector3 escalaOriginalMundo = ObterEscalaOriginalMundo(item, estado);
        item.transform.localScale = escalaOriginalMundo;
        item.SetTargetLocalScale(escalaOriginalMundo);
        LogEscalaInventario("escala ao retirar do inventario", item, escalaOriginalMundo);
    }

    private void RestaurarEscalaOriginalDoEstado(XRGrabInteractable item, EstadoOriginalItem estado, bool restaurarTargetOriginal)
    {
        if (item == null || estado == null)
            return;

        Transform itemTransform = item.transform;

        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp != null && escalaComp.inicializado)
            escalaComp.LimparTravaDoSlot(this);

        Vector3 escalaOriginalMundo = ObterEscalaOriginalMundo(item, estado);
        Vector3 escalaLocalOriginal = ConverterEscalaMundoParaLocal(escalaOriginalMundo, itemTransform.parent);

        itemTransform.localScale = escalaLocalOriginal;
        item.SetTargetLocalScale(restaurarTargetOriginal && itemTransform.parent == estado.ParentOriginal
            ? estado.TargetLocalScaleOriginal
            : escalaLocalOriginal);
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

        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp == null)
            escalaComp = item.gameObject.AddComponent<EscalaOriginalItem>();

        if (!escalaComp.inicializado || !EscalaValida(escalaComp.escalaOriginal))
        {
            escalaComp.escalaOriginal = estado.LossyScaleOriginal;
            escalaComp.inicializado = true;
        }

        var itemEscalavel = item.GetComponent<ItemInventarioEscalavel>();
        if (itemEscalavel != null)
            itemEscalavel.DefinirEscalaOriginalMundo(escalaComp.escalaOriginal);
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

    public List<XRGrabInteractable> ObterItensParaSave()
    {
        return new List<XRGrabInteractable>(pilhaItens);
    }

    public void LimparItensSalvosDoSlot(bool destruirItens)
    {
        RemoverFiltroDoItemGuardado();

        for (int i = pilhaItens.Count - 1; i >= 0; i--)
        {
            XRGrabInteractable item = pilhaItens[i];
            if (item == null)
                continue;

            item.selectFilters.Remove(this);

            if (destruirItens)
                Destroy(item.gameObject);
            else
                RestaurarItemParaMundo(item);
        }

        pilhaItens.Clear();
        estadosOriginais.Clear();
        itemGuardado = null;
        nomeItemAtual = string.Empty;
        ignorarProximoExited = false;
        AtualizarContadorTMP();
        AtualizarVisibilidadeVisual();
    }

    public bool RestaurarItemSalvoNoSlot(XRGrabInteractable item, bool esconderNaPilha)
    {
        if (item == null || pilhaItens.Count >= limiteStack)
            return false;

        string nomeItem = ObterNomeItem(item);
        if (pilhaItens.Count > 0 &&
            !string.IsNullOrWhiteSpace(nomeItemAtual) &&
            !string.Equals(nomeItemAtual, nomeItem))
        {
            return false;
        }

        SalvarEstadoOriginalSeNecessario(item);
        LogEscalaInventario("escala ao carregar do save", item, ObterEscalaOriginalMundo(item, estadosOriginais[item]));

        MarcarItemAceitoPeloSlot(item, esconderNaPilha, "restauracao save");
        pilhaItens.Add(item);
        nomeItemAtual = nomeItem;

        if (esconderNaPilha)
        {
            EsconderItemNaPilha(item);
        }
        else
        {
            if (itemGuardado != null && itemGuardado != item)
            {
                RemoverFiltroDoItemGuardado();
                EsconderItemNaPilha(itemGuardado);
            }

            AplicarVisualNoSlot(item);
            RegistrarFiltroNoItemGuardado();
        }

        AtualizarContadorTMP();
        AtualizarVisibilidadeVisual();
        return true;
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

        if (!PodeAceitarItemNoSlot(item, "selectEntered"))
        {
            RejeitarSelecaoDoSocket(item, "item pertence a outro slot");
            return;
        }

        if (pilhaItens.Count > 0)
        {
            if (pilhaItens.Contains(item))
                return;

            if (!TentarEmpilharItemExtra(item))
                RejeitarSelecaoDoSocket(item, "item recusado pela pilha");

            return;
        }

        SalvarEstadoOriginalSeNecessario(item);
        MarcarItemAceitoPeloSlot(item, false, "primeiro item do slot");

        pilhaItens.Add(item);
        nomeItemAtual = ObterNomeItem(item);

        AplicarVisualNoSlot(item);
        RegistrarFiltroNoItemGuardado();
        AtualizarContadorTMP();
        TocarSom(somAdicionarItem);
    }

    private void PosicionarItemNoPontoDoSlot(Transform item)
    {
        Transform ponto = ObterPontoEncaixe();

        if (ponto != null)
        {
            item.SetPositionAndRotation(ponto.position, ponto.rotation);
            return;
        }

        item.SetPositionAndRotation(transform.position, transform.rotation);
    }

    private Transform ObterPontoEncaixe()
    {
        if (pontoEncaixe != null)
            return pontoEncaixe;

        if (socketInteractor != null && socketInteractor.attachTransform != null)
            return socketInteractor.attachTransform;

        return transform;
    }

    private void ParentarItemNoSlot(XRGrabInteractable item)
    {
        if (item == null)
            return;

        Transform parentSlot = ObterPontoEncaixe();
        if (parentSlot == null)
            parentSlot = transform;

        item.transform.SetParent(parentSlot, true);
    }

    // --- RETIRADA -------------------------------------------------------------

    private void OnItemRetirado(SelectExitEventArgs args)
    {
        if (operacaoInternaSocket || args.isCanceled)
            return;

        if (ignorarProximoExited)
        {
            ignorarProximoExited = false;
            return;
        }

        if (itemGuardado == null)
            return;

        var item = itemGuardado.transform;
        if (item == null)
            return;

        if (args.interactableObject == null || args.interactableObject.transform != item)
            return;

        XRGrabInteractable itemRemovido = itemGuardado;
        if (TopoDaPilha() != itemRemovido)
            return;

        RemoverFiltroDoItemGuardado();

        pilhaItens.RemoveAt(pilhaItens.Count - 1);

        bool podeAtualizarEfeitos = inventarioAberto && visivelNaRolagem && gameObject.activeInHierarchy && enabled;
        if (podeAtualizarEfeitos)
            TocarSom(somRetirarItem);

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
            LogEscalaInventario("escala original capturada", item.GetComponent<XRGrabInteractable>(), comp.escalaOriginal);
            return;
        }

        if (!comp.inicializado || !EscalaValida(comp.escalaOriginal))
        {
            comp.escalaOriginal = item.lossyScale;
            comp.inicializado = true;
            LogEscalaInventario("escala original capturada", item.GetComponent<XRGrabInteractable>(), comp.escalaOriginal);
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

        LogEscalaInventario("escala antes de AjustarEscalaAdaptativa", item.GetComponent<XRGrabInteractable>(), escalaComp.escalaOriginal);

        if (itemEscalavel.AjustarParaSlot(slotCollider, margem, transform))
        {
            escalaComp.MarcarEscalaAplicada(this, margem);
            LogEscalaInventario("escala apos AjustarEscalaAdaptativa", item.GetComponent<XRGrabInteractable>(), escalaComp.escalaOriginal);
        }
    }

    private void PrepararEscalaParaSlot(Transform item, EstadoOriginalItem estado)
    {
        if (item == null || estado == null)
            return;

        Vector3 escalaOriginalMundo = ObterEscalaOriginalMundo(item.GetComponent<XRGrabInteractable>(), estado);

        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp != null)
        {
            escalaComp.escalaOriginal = escalaOriginalMundo;
            escalaComp.inicializado = true;
            escalaComp.LimparTravaDoSlot(this);
        }

        var itemEscalavel = item.GetComponent<ItemInventarioEscalavel>();
        if (itemEscalavel != null)
            itemEscalavel.DefinirEscalaOriginalMundo(escalaOriginalMundo);

        LogEscalaInventario("escalaOriginalMundo preparada para slot", item.GetComponent<XRGrabInteractable>(), escalaOriginalMundo);
    }

    private Vector3 ObterEscalaOriginalMundo(XRGrabInteractable item, EstadoOriginalItem estado)
    {
        if (item != null)
        {
            var escalaComp = item.GetComponent<EscalaOriginalItem>();
            if (escalaComp != null && escalaComp.inicializado && EscalaValida(escalaComp.escalaOriginal))
                return escalaComp.escalaOriginal;

            var itemEscalavel = item.GetComponent<ItemInventarioEscalavel>();
            if (itemEscalavel != null && itemEscalavel.PossuiEscalaOriginalMundoValida())
                return itemEscalavel.ObterEscalaOriginalMundo();
        }

        if (estado != null && EscalaValida(estado.LossyScaleOriginal))
            return estado.LossyScaleOriginal;

        return item != null ? item.transform.lossyScale : Vector3.one;
    }

    private static bool EscalaValida(Vector3 escala)
    {
        const float minimo = 0.0001f;
        return Mathf.Abs(escala.x) > minimo &&
               Mathf.Abs(escala.y) > minimo &&
               Mathf.Abs(escala.z) > minimo;
    }

    private void LogEscalaInventario(string evento, XRGrabInteractable item, Vector3 escalaOriginalMundo)
    {
        Vector3 escalaAtual = item != null ? item.transform.lossyScale : Vector3.zero;
        LogEscalaInventario(evento, item, escalaOriginalMundo, escalaAtual);
    }

    private void LogEscalaInventario(string evento, XRGrabInteractable item, Vector3 escalaOriginalMundo, Vector3 escalaAtual)
    {
        string itemId = ObterNomeItem(item);
        string instanciaId = "(sem instanciaId)";

        ItemPersistente persistente = item != null ? item.GetComponent<ItemPersistente>() : null;
        if (persistente != null)
        {
            itemId = persistente.ObterItemId();
            instanciaId = persistente.ObterInstanciaIdSemGerar();
            if (string.IsNullOrWhiteSpace(instanciaId))
                instanciaId = "(sem instanciaId)";
        }

        Debug.Log($"{PrefixoDebugEscala} {evento} | itemId={itemId} | instanciaId={instanciaId} | escalaOriginalMundo={escalaOriginalMundo} | escalaAtual={escalaAtual}", item);
    }

    private void SetUiDoItemVisivel(EstadoOriginalItem estado, bool visivel)
    {
        if (estado == null)
            return;

        for (int i = 0; i < estado.Canvases.Length; i++)
        {
            Canvas canvas = estado.Canvases[i].Canvas;
            if (canvas != null)
                canvas.enabled = visivel && estado.Canvases[i].Enabled;
        }

        for (int i = 0; i < estado.Graphics.Length; i++)
        {
            Graphic graphic = estado.Graphics[i].Graphic;
            if (graphic != null)
                graphic.enabled = visivel && estado.Graphics[i].Enabled;
        }
    }

    private void RestaurarUiOriginalDoItem(EstadoOriginalItem estado)
    {
        if (estado == null)
            return;

        for (int i = 0; i < estado.Canvases.Length; i++)
        {
            Canvas canvas = estado.Canvases[i].Canvas;
            if (canvas != null)
                canvas.enabled = estado.Canvases[i].Enabled;
        }

        for (int i = 0; i < estado.Graphics.Length; i++)
        {
            Graphic graphic = estado.Graphics[i].Graphic;
            if (graphic != null)
                graphic.enabled = estado.Graphics[i].Enabled;
        }
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

    private void TocarSom(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
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

            if (estadosOriginais.TryGetValue(itemGuardado, out EstadoOriginalItem estado))
                SetUiDoItemVisivel(estado, false);
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

    private readonly struct EstadoCanvas
    {
        public readonly Canvas Canvas;
        public readonly bool Enabled;

        public EstadoCanvas(Canvas canvas)
        {
            Canvas = canvas;
            Enabled = canvas != null && canvas.enabled;
        }
    }

    private readonly struct EstadoGraphic
    {
        public readonly Graphic Graphic;
        public readonly bool Enabled;

        public EstadoGraphic(Graphic graphic)
        {
            Graphic = graphic;
            Enabled = graphic != null && graphic.enabled;
        }
    }

    private sealed class EstadoOriginalItem
    {
        public readonly Transform ParentOriginal;
        public readonly Vector3 LocalScaleOriginal;
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
        public readonly EstadoCanvas[] Canvases;
        public readonly EstadoGraphic[] Graphics;

        public EstadoOriginalItem(XRGrabInteractable item)
        {
            Transform transformItem = item.transform;
            ParentOriginal = transformItem.parent;
            LocalScaleOriginal = transformItem.localScale;
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

            Canvas[] canvases = item.GetComponentsInChildren<Canvas>(true);
            Canvases = new EstadoCanvas[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
                Canvases[i] = new EstadoCanvas(canvases[i]);

            Graphic[] graphics = item.GetComponentsInChildren<Graphic>(true);
            Graphics = new EstadoGraphic[graphics.Length];
            for (int i = 0; i < graphics.Length; i++)
                Graphics[i] = new EstadoGraphic(graphics[i]);
        }
    }
}
