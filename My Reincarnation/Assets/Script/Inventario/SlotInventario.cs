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
    [SerializeField] private Transform pontoEncaixe;

    [Header("Margem de seguranca (0.9 = 90% do slot)")]
    [SerializeField] private float margemDeSeguranca = 0.9f;

    [Header("Limite visual final do item")]
    [SerializeField] private Vector3 tamanhoVisualMaximoSlot = Vector3.zero;
    [SerializeField] private bool debugEscalaInventario = true;

    [Header("Contador")]
    [SerializeField] private TMP_Text contadorTMP;

    [Header("Stack")]
    [SerializeField] private int limiteStack = 99;

    [Header("Diagnostico Stack")]
    [SerializeField] private int diagnosticoQuantidadePilha;
    [SerializeField] private int diagnosticoRenderersAtivosTopo;
    [SerializeField] private int diagnosticoRenderersAtivosEscondidos;
    [SerializeField] private string diagnosticoTopoPilha;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somAdicionarItem;
    [SerializeField] private AudioClip somRetirarItem;

    private readonly List<XRGrabInteractable> pilhaItens = new();
    private readonly Dictionary<XRGrabInteractable, EstadoOriginalItem> estadosOriginais = new();
    private readonly HashSet<XRGrabInteractable> itensRestauradosDoSave = new();
    private readonly HashSet<XRGrabInteractable> itensComEscalaVisualPendente = new();
    private readonly Dictionary<XRGrabInteractable, Vector3> escalasVisuaisInventario = new();

    private XRGrabInteractable itemGuardado;
    private string nomeItemAtual = string.Empty;
    private bool ignorarProximoExited = false;
    private bool inventarioAberto = true;
    private bool visivelNaRolagem = true;
    private bool operacaoInternaSocket;
    private bool atualizandoTopo;
    private bool aguardarFrameVisualParaEscala;
    private Coroutine rotinaRecalculoEscalaQuandoVisivel;

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
        ParentarItemNoSlot(item);
        RestaurarEscalaOriginalDoEstado(item, estado, false);
        PosicionarItemNoPontoDoSlot(item.transform);

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

    private void AplicarVisualNoSlot(XRGrabInteractable item, bool forcarEscalaMesmoInvisivel = false)
    {
        if (item == null)
            return;

        SalvarEstadoOriginalSeNecessario(item);
        EstadoOriginalItem estado = estadosOriginais[item];

        itemGuardado = item;
        MarcarItemComoTopo(item);
        item.gameObject.SetActive(true);

        ParentarItemNoSlot(item);

        RestaurarEscalaOriginalDoEstado(item, estado, false);

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

        bool forcarRecalculo = forcarEscalaMesmoInvisivel ||
                               itensRestauradosDoSave.Contains(item) ||
                               itensComEscalaVisualPendente.Contains(item);
        AplicarOuAgendarEscalaVisual(item, estado, forcarRecalculo);
        AtualizarVisibilidadeDaPilha();

        Physics.SyncTransforms();
    }

    private void PrepararEscalaVisualPendenteNoSlot(XRGrabInteractable item, EstadoOriginalItem estado)
    {
        if (item == null || estado == null)
            return;

        RestaurarEscalaOriginalDoEstado(item, estado, false);
        GarantirEscalaOriginal(item.transform);
        PrepararEscalaParaSlot(item.transform, estado);
        PosicionarItemNoPontoDoSlot(item.transform);
    }

    private void AplicarOuAgendarEscalaVisual(XRGrabInteractable item, EstadoOriginalItem estado, bool forcarRecalculo)
    {
        if (item == null || estado == null)
            return;

        if (!SlotVisualProntoParaCalcularEscala(item, estado))
        {
            if (!forcarRecalculo && TentarAplicarEscalaVisualInventario(item, estado))
            {
                PosicionarItemNoPontoDoSlot(item.transform);
                return;
            }

            PrepararEscalaVisualPendenteNoSlot(item, estado);

            if (forcarRecalculo || itensRestauradosDoSave.Contains(item) || !escalasVisuaisInventario.ContainsKey(item))
                MarcarEscalaVisualPendente(item);

            AgendarRecalculoEscalaQuandoVisivel();
            return;
        }

        AplicarEscalaVisualFixaNoSlot(item, estado, forcarRecalculo);
    }

    private void MarcarEscalaVisualPendente(XRGrabInteractable item)
    {
        if (item == null)
            return;

        itensComEscalaVisualPendente.Add(item);
        escalasVisuaisInventario.Remove(item);
    }

    private bool SlotVisualProntoParaCalcularEscala(XRGrabInteractable item, EstadoOriginalItem estado)
    {
        if (item == null || estado == null || aguardarFrameVisualParaEscala)
            return false;

        if (!inventarioAberto || !visivelNaRolagem || !gameObject.activeInHierarchy || !item.gameObject.activeInHierarchy)
            return false;

        for (int i = 0; i < estado.Renderers.Length; i++)
        {
            Renderer renderer = estado.Renderers[i].Renderer;
            if (renderer == null || !estado.Renderers[i].Enabled)
                continue;

            if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    private void AgendarRecalculoEscalaQuandoVisivel()
    {
        if (!isActiveAndEnabled || rotinaRecalculoEscalaQuandoVisivel != null)
            return;

        rotinaRecalculoEscalaQuandoVisivel = StartCoroutine(RecalcularEscalaQuandoVisivel());
    }

    private IEnumerator RecalcularEscalaQuandoVisivel()
    {
        yield return null;
        aguardarFrameVisualParaEscala = false;
        TentarRecalcularEscalaPendenteVisivel();

        yield return null;
        TentarRecalcularEscalaPendenteVisivel();
        rotinaRecalculoEscalaQuandoVisivel = null;
    }

    private void TentarRecalcularEscalaPendenteVisivel()
    {
        XRGrabInteractable item = itemGuardado != null ? itemGuardado : TopoDaPilha();
        if (item == null || !estadosOriginais.TryGetValue(item, out EstadoOriginalItem estado))
            return;

        bool precisaRecalcular = itensRestauradosDoSave.Contains(item) ||
                                 itensComEscalaVisualPendente.Contains(item) ||
                                 !escalasVisuaisInventario.ContainsKey(item);

        if (!precisaRecalcular)
            return;

        ParentarItemNoSlot(item);
        PosicionarItemNoPontoDoSlot(item.transform);
        AplicarOuAgendarEscalaVisual(item, estado, true);
        AtualizarVisibilidadeVisual();
        Physics.SyncTransforms();
    }

    private void AplicarEscalaVisualFixaNoSlot(XRGrabInteractable item, EstadoOriginalItem estado, bool forcarRecalculo = false)
    {
        if (item == null || estado == null)
            return;

        if (!SlotVisualProntoParaCalcularEscala(item, estado))
        {
            PrepararEscalaVisualPendenteNoSlot(item, estado);
            MarcarEscalaVisualPendente(item);
            AgendarRecalculoEscalaQuandoVisivel();
            return;
        }

        if (!forcarRecalculo && TentarAplicarEscalaVisualInventario(item, estado))
        {
            PosicionarItemNoPontoDoSlot(item.transform);
            return;
        }

        escalasVisuaisInventario.Remove(item);
        RestaurarEscalaOriginalDoEstado(item, estado, false);
        GarantirEscalaOriginal(item.transform);
        PosicionarItemNoPontoDoSlot(item.transform);
        PrepararEscalaParaSlot(item.transform, estado);
        AjustarEscalaAdaptativa(item.transform);
        PosicionarItemNoPontoDoSlot(item.transform);
        if (!AjustarEscalaVisualFinalNoSlot(item.transform, estado))
        {
            MarcarEscalaVisualPendente(item);
            AgendarRecalculoEscalaQuandoVisivel();
            return;
        }

        RegistrarEscalaVisualInventario(item);
        itensComEscalaVisualPendente.Remove(item);
        itensRestauradosDoSave.Remove(item);
    }

    private void RestaurarItemParaMundo(XRGrabInteractable item)
    {
        if (item == null)
            return;

        LimparEscalaVisualInventario(item);

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
            return;
        }

        item.transform.SetParent(null, true);

        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp != null && escalaComp.inicializado)
            escalaComp.LimparTravaDoSlot(this);

        Vector3 escalaOriginalMundo = ObterEscalaOriginalMundo(item, estado);
        item.transform.localScale = escalaOriginalMundo;
        item.SetTargetLocalScale(escalaOriginalMundo);
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
            if (EscalaValida(estado.LossyScaleOriginal))
            {
                escalaComp.escalaOriginal = estado.LossyScaleOriginal;
                escalaComp.inicializado = true;
            }
        }

        var itemEscalavel = item.GetComponent<ItemInventarioEscalavel>();
        if (itemEscalavel != null && EscalaValida(escalaComp.escalaOriginal))
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
        RemoverItensNulosDaPilha();
        return new List<XRGrabInteractable>(pilhaItens);
    }

    public int ObterQuantidadeRealParaSave()
    {
        RemoverItensNulosDaPilha();
        return pilhaItens.Count;
    }

    public XRGrabInteractable ObterItemRepresentanteParaSave()
    {
        RemoverItensNulosDaPilha();
        return TopoDaPilha();
    }

    public bool PossuiItem()
    {
        RemoverItensNulosDaPilha();
        return pilhaItens.Count > 0;
    }

    public int ObterQuantidadeAtual()
    {
        return ObterQuantidadeRealParaSave();
    }

    public ItemInventarioDados ObterItemRepresentante()
    {
        XRGrabInteractable item = ObterItemRepresentanteParaSave();
        return item != null ? item.GetComponent<ItemInventarioDados>() : null;
    }

    public bool ConsumirUmaUnidade(out ItemInventarioDados itemConsumido)
    {
        itemConsumido = null;
        RemoverItensNulosDaPilha();

        XRGrabInteractable consumido = TopoDaPilha();
        if (consumido == null)
        {
            AtualizarContadorTMP();
            AtualizarVisibilidadeVisual();
            return false;
        }

        itemConsumido = consumido.GetComponent<ItemInventarioDados>();
        RemoverFiltroDoItemGuardado();

        pilhaItens.RemoveAt(pilhaItens.Count - 1);
        itensRestauradosDoSave.Remove(consumido);
        itensComEscalaVisualPendente.Remove(consumido);
        escalasVisuaisInventario.Remove(consumido);
        estadosOriginais.Remove(consumido);
        consumido.selectFilters.Remove(this);

        if (itemGuardado == consumido)
            itemGuardado = null;

        Destroy(consumido.gameObject);

        if (pilhaItens.Count == 0)
        {
            nomeItemAtual = string.Empty;
            ignorarProximoExited = false;
            AtualizarContadorTMP();
            AtualizarVisibilidadeVisual();
            Physics.SyncTransforms();
            return true;
        }

        PromoverNovoTopoAposRetirada();
        nomeItemAtual = ObterNomeItem(TopoDaPilha());
        AtualizarContadorTMP();
        AtualizarVisibilidadeVisual();
        Physics.SyncTransforms();
        return true;
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
        itensRestauradosDoSave.Clear();
        itensComEscalaVisualPendente.Clear();
        escalasVisuaisInventario.Clear();
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
        itensRestauradosDoSave.Add(item);
        escalasVisuaisInventario.Remove(item);

        MarcarItemAceitoPeloSlot(item, esconderNaPilha, "restauracao save");
        pilhaItens.Add(item);
        nomeItemAtual = nomeItem;

        if (esconderNaPilha)
        {
            ReaplicarEscalaBaseRestauradaDoSave(item, estadosOriginais[item]);
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

    public void FinalizarRestauracaoDoSave()
    {
        RemoverItensNulosDaPilha();

        if (pilhaItens.Count == 0)
        {
            RemoverFiltroDoItemGuardado();
            itemGuardado = null;
            nomeItemAtual = string.Empty;
            AtualizarContadorTMP();
            AtualizarVisibilidadeVisual();
            return;
        }

        XRGrabInteractable topo = TopoDaPilha();
        if (topo == null)
        {
            AtualizarContadorTMP();
            AtualizarVisibilidadeVisual();
            return;
        }

        atualizandoTopo = true;
        operacaoInternaSocket = true;

        try
        {
            if (itemGuardado != topo)
                RemoverFiltroDoItemGuardado();

            for (int i = 0; i < pilhaItens.Count - 1; i++)
            {
                XRGrabInteractable item = pilhaItens[i];
                if (item != null && item != topo)
                    EsconderItemNaPilha(item);
            }

            AplicarVisualNoSlot(topo);
            SelecionarTopoNoSocket(topo);
            RegistrarFiltroNoItemGuardado();
            nomeItemAtual = ObterNomeItem(topo);
        }
        finally
        {
            operacaoInternaSocket = false;
            atualizandoTopo = false;
        }

        AtualizarContadorTMP();
        AtualizarVisibilidadeVisual();
    }

    public void ForcarRecalculoVisualAposLoad()
    {
        RemoverItensNulosDaPilha();

        if (pilhaItens.Count == 0)
        {
            RemoverFiltroDoItemGuardado();
            itemGuardado = null;
            nomeItemAtual = string.Empty;
            AtualizarContadorTMP();
            AtualizarVisibilidadeVisual();
            return;
        }

        XRGrabInteractable topo = TopoDaPilha();
        if (topo == null)
        {
            AtualizarContadorTMP();
            AtualizarVisibilidadeVisual();
            return;
        }

        atualizandoTopo = true;
        operacaoInternaSocket = true;

        try
        {
            if (itemGuardado != topo)
                RemoverFiltroDoItemGuardado();

            for (int i = 0; i < pilhaItens.Count; i++)
            {
                XRGrabInteractable item = pilhaItens[i];
                if (item == null)
                    continue;

                escalasVisuaisInventario.Remove(item);
                MarcarEscalaVisualPendente(item);

                if (item != topo)
                {
                    EsconderItemNaPilha(item);
                }
            }

            itensRestauradosDoSave.Add(topo);
            MarcarEscalaVisualPendente(topo);
            AplicarVisualNoSlot(topo, true);
            SelecionarTopoNoSocket(topo);
            RegistrarFiltroNoItemGuardado();
            nomeItemAtual = ObterNomeItem(topo);
        }
        finally
        {
            operacaoInternaSocket = false;
            atualizandoTopo = false;
        }

        AtualizarContadorTMP();
        AtualizarVisibilidadeVisual();
        Physics.SyncTransforms();
    }

    private void RemoverItensNulosDaPilha()
    {
        for (int i = pilhaItens.Count - 1; i >= 0; i--)
        {
            if (pilhaItens[i] == null)
                pilhaItens.RemoveAt(i);
        }
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
            {
                itemGuardado = item;
                ReancorarItemAtualNoSlot();
                AtualizarContadorTMP();
                AtualizarVisibilidadeVisual();
                return;
            }

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
        // Fechar o inventario desativa temporariamente o XRSocketInteractor.
        // Essa saida tecnica nao representa uma retirada feita pelo jogador.
        if (operacaoInternaSocket || args.isCanceled || !inventarioAberto)
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

    private void ReaplicarEscalaBaseRestauradaDoSave(XRGrabInteractable item, EstadoOriginalItem estado)
    {
        if (item == null || estado == null)
            return;

        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp == null)
            escalaComp = item.gameObject.AddComponent<EscalaOriginalItem>();

        bool escalaCompValida = escalaComp.inicializado && EscalaValida(escalaComp.escalaOriginal);
        Vector3 escalaOriginalMundo = escalaCompValida
            ? escalaComp.escalaOriginal
            : ObterEscalaOriginalMundoDoEstado(item, estado);

        if (!EscalaValida(escalaOriginalMundo))
            return;

        if (!escalaCompValida)
        {
            escalaComp.escalaOriginal = escalaOriginalMundo;
            escalaComp.inicializado = true;
        }

        escalaComp.LimparTravaDoSlot(this);

        var itemEscalavel = item.GetComponent<ItemInventarioEscalavel>();
        if (itemEscalavel != null)
            itemEscalavel.DefinirEscalaOriginalMundo(escalaOriginalMundo);

        Vector3 escalaLocalOriginal = ConverterEscalaMundoParaLocal(escalaOriginalMundo, item.transform.parent);
        item.transform.localScale = escalaLocalOriginal;
        item.SetTargetLocalScale(escalaLocalOriginal);
        Physics.SyncTransforms();
    }

    private void RegistrarEscalaVisualInventario(XRGrabInteractable item)
    {
        if (item == null || !EscalaValida(item.transform.localScale))
            return;

        escalasVisuaisInventario[item] = item.transform.localScale;
    }

    private bool TentarAplicarEscalaVisualInventario(XRGrabInteractable item, EstadoOriginalItem estado)
    {
        if (item == null || !escalasVisuaisInventario.TryGetValue(item, out Vector3 escalaVisual))
            return false;

        if (itensRestauradosDoSave.Contains(item) || itensComEscalaVisualPendente.Contains(item))
            return false;

        if (!EscalaValida(escalaVisual))
            return false;

        item.transform.localScale = escalaVisual;
        item.SetTargetLocalScale(escalaVisual);
        Physics.SyncTransforms();
        return true;
    }

    private void LimparEscalaVisualInventario(XRGrabInteractable item)
    {
        if (item == null)
            return;

        itensRestauradosDoSave.Remove(item);
        itensComEscalaVisualPendente.Remove(item);
        escalasVisuaisInventario.Remove(item);
    }

    private void GarantirEscalaOriginal(Transform item)
    {
        if (item == null)
            return;

        var comp = item.GetComponent<EscalaOriginalItem>();
        if (comp != null && comp.inicializado && EscalaValida(comp.escalaOriginal))
            return;

        if (!item.gameObject.activeInHierarchy || !EscalaValida(item.lossyScale))
            return;

        if (comp == null)
        {
            comp = item.gameObject.AddComponent<EscalaOriginalItem>();
            comp.escalaOriginal = item.lossyScale;
            comp.inicializado = true;
            return;
        }

        if (!comp.inicializado || !EscalaValida(comp.escalaOriginal))
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
        {
            escalaComp.MarcarEscalaAplicada(this, margem);
        }
    }

    private bool AjustarEscalaVisualFinalNoSlot(Transform item, EstadoOriginalItem estado)
    {
        if (item == null)
            return false;

        var itemEscalavel = item.GetComponent<ItemInventarioEscalavel>();
        if (itemEscalavel == null)
            itemEscalavel = item.gameObject.AddComponent<ItemInventarioEscalavel>();

        if (!TryObterBoundsVisualPermitido(out Bounds boundsPermitido))
            return false;

        XRGrabInteractable grab = item.GetComponent<XRGrabInteractable>();
        Vector3 escalaOriginal = ObterEscalaOriginalMundo(grab, estado);
        if (EscalaValida(escalaOriginal))
            itemEscalavel.DefinirEscalaOriginalMundo(escalaOriginal);

        return itemEscalavel.AjustarRenderersParaSlotVisual(transform, boundsPermitido, escalaOriginal);
    }

    private bool TryObterBoundsVisualPermitido(out Bounds boundsPermitido)
    {
        float margem = Mathf.Clamp(margemDeSeguranca, 0.01f, 1f);
        BoxCollider slotCollider = GetComponent<BoxCollider>();

        Vector3 centro = slotCollider != null ? slotCollider.center : Vector3.zero;
        Vector3 tamanho = slotCollider != null ? slotCollider.size : Vector3.one;

        if (!VetorFinito(centro) || !TamanhoValido(tamanho))
        {
            boundsPermitido = default;
            return false;
        }

        if (TamanhoVisualConfiguradoValido())
        {
            tamanho = tamanhoVisualMaximoSlot;
        }
        else if (TryObterBoundsDoVisualSlot(out Bounds boundsVisual))
        {
            centro = new Vector3(boundsVisual.center.x, boundsVisual.center.y, centro.z);

            if (slotCollider != null)
            {
                tamanho = new Vector3(
                    Mathf.Min(tamanho.x, boundsVisual.size.x),
                    Mathf.Min(tamanho.y, boundsVisual.size.y),
                    tamanho.z);
            }
            else
            {
                tamanho = new Vector3(boundsVisual.size.x, boundsVisual.size.y, Mathf.Max(boundsVisual.size.z, 0.01f));
            }
        }

        tamanho *= margem;
        if (!VetorFinito(centro) || !TamanhoValido(tamanho))
        {
            boundsPermitido = default;
            return false;
        }

        boundsPermitido = new Bounds(centro, tamanho);
        return true;
    }

    private bool TryObterBoundsDoVisualSlot(out Bounds boundsVisual)
    {
        boundsVisual = default;
        if (visualSlot == null)
            return false;

        RectTransform rect = visualSlot.transform as RectTransform;
        if (rect == null)
            return false;

        Vector3[] cantos = new Vector3[4];
        rect.GetWorldCorners(cantos);

        bool iniciou = false;
        for (int i = 0; i < cantos.Length; i++)
        {
            if (!VetorFinito(cantos[i]))
                continue;

            Vector3 pontoLocal = transform.InverseTransformPoint(cantos[i]);
            if (!VetorFinito(pontoLocal))
                continue;

            if (!iniciou)
            {
                boundsVisual = new Bounds(pontoLocal, Vector3.zero);
                iniciou = true;
            }
            else
            {
                boundsVisual.Encapsulate(pontoLocal);
            }
        }

        return iniciou &&
               VetorFinito(boundsVisual.center) &&
               TamanhoValido(new Vector3(boundsVisual.size.x, boundsVisual.size.y, 0.01f));
    }

    private bool TamanhoVisualConfiguradoValido()
    {
        return VetorFinito(tamanhoVisualMaximoSlot) &&
               tamanhoVisualMaximoSlot.x > 0f &&
               tamanhoVisualMaximoSlot.y > 0f &&
               tamanhoVisualMaximoSlot.z > 0f;
    }

    private static bool TamanhoValido(Vector3 tamanho)
    {
        return VetorFinito(tamanho) && tamanho.x > 0f && tamanho.y > 0f && tamanho.z > 0f;
    }

    private void PrepararEscalaParaSlot(Transform item, EstadoOriginalItem estado)
    {
        if (item == null || estado == null)
            return;

        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        var itemEscalavel = item.GetComponent<ItemInventarioEscalavel>();

        bool escalaCompValida = escalaComp != null &&
                                escalaComp.inicializado &&
                                EscalaValida(escalaComp.escalaOriginal);
        bool escalaEscalavelValida = itemEscalavel != null &&
                                     itemEscalavel.PossuiEscalaOriginalMundoValida();

        Vector3 escalaOriginalMundo = escalaCompValida
            ? escalaComp.escalaOriginal
            : escalaEscalavelValida
                ? itemEscalavel.ObterEscalaOriginalMundo()
                : ObterEscalaOriginalMundo(item.GetComponent<XRGrabInteractable>(), estado);

        if (!EscalaValida(escalaOriginalMundo))
            return;

        if (escalaComp != null)
        {
            if (!escalaCompValida)
            {
                escalaComp.escalaOriginal = escalaOriginalMundo;
                escalaComp.inicializado = true;
            }

            escalaComp.LimparTravaDoSlot(this);
        }

        if (itemEscalavel != null)
            itemEscalavel.DefinirEscalaOriginalMundo(escalaOriginalMundo);
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

        Vector3 escalaAtual = item != null ? item.transform.lossyScale : Vector3.one;
        return EscalaValida(escalaAtual) ? escalaAtual : Vector3.one;
    }

    private Vector3 ObterEscalaOriginalMundoDoEstado(XRGrabInteractable item, EstadoOriginalItem estado)
    {
        if (estado != null && EscalaValida(estado.LossyScaleOriginal))
            return estado.LossyScaleOriginal;

        return ObterEscalaOriginalMundo(item, estado);
    }

    private static bool EscalaValida(Vector3 escala)
    {
        const float minimo = 0.0001f;
        return VetorFinito(escala) &&
               Mathf.Abs(escala.x) > minimo &&
               Mathf.Abs(escala.y) > minimo &&
               Mathf.Abs(escala.z) > minimo;
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
        if (!ValorFinito(valor))
            return 1f;

        if (!ValorFinito(divisor) || Mathf.Approximately(divisor, 0f))
            return valor;

        float resultado = valor / divisor;
        return ValorFinito(resultado) ? resultado : valor;
    }

    private static Vector3 ConverterEscalaMundoParaLocal(Vector3 escalaMundo, Transform parent)
    {
        if (!EscalaValida(escalaMundo))
            return Vector3.one;

        Vector3 escalaParent = parent != null ? parent.lossyScale : Vector3.one;
        Vector3 escalaLocal = new Vector3(
            DividirSeguro(escalaMundo.x, escalaParent.x),
            DividirSeguro(escalaMundo.y, escalaParent.y),
            DividirSeguro(escalaMundo.z, escalaParent.z)
        );

        return EscalaValida(escalaLocal) ? escalaLocal : Vector3.one;
    }

    private static bool VetorFinito(Vector3 valor)
    {
        return ValorFinito(valor.x) && ValorFinito(valor.y) && ValorFinito(valor.z);
    }

    private static bool ValorFinito(float valor)
    {
        return !float.IsNaN(valor) && !float.IsInfinity(valor);
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
        {
            if (ComponentePertenceAItemInventario(canvas))
                continue;

            canvas.enabled = visivel;
        }

        foreach (var graphic in visualSlot.GetComponentsInChildren<Graphic>(true))
        {
            if (ComponentePertenceAItemInventario(graphic))
                continue;

            graphic.enabled = visivel;
        }

        foreach (var renderer in visualSlot.GetComponentsInChildren<Renderer>(true))
        {
            if (ComponentePertenceAItemInventario(renderer))
                continue;

            renderer.enabled = visivel;
        }
    }

    private void AtualizarVisibilidadeVisual()
    {
        bool visivel = inventarioAberto && visivelNaRolagem;

        SetVisualSlotVisivel(visivel);
        AtualizarVisibilidadeDaPilha();

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

    private void AtualizarVisibilidadeDaPilha()
    {
        RemoverItensNulosDaPilha();

        XRGrabInteractable topo = TopoDaPilha();
        bool slotVisivel = inventarioAberto && visivelNaRolagem;

        diagnosticoQuantidadePilha = pilhaItens.Count;
        diagnosticoRenderersAtivosTopo = 0;
        diagnosticoRenderersAtivosEscondidos = 0;
        diagnosticoTopoPilha = topo != null ? topo.name : string.Empty;

        if (topo == null)
        {
            itemGuardado = null;
            return;
        }

        itemGuardado = topo;

        for (int i = 0; i < pilhaItens.Count; i++)
        {
            XRGrabInteractable item = pilhaItens[i];
            if (item == null)
                continue;

            bool ehTopo = item == topo;
            EstadoOriginalItem estado = estadosOriginais.TryGetValue(item, out EstadoOriginalItem estadoEncontrado)
                ? estadoEncontrado
                : null;

            if (ehTopo)
                MarcarItemComoTopo(item);
            else
                MarcarItemComoEscondidoNaPilha(item);

            item.gameObject.SetActive(true);
            AplicarRenderersDoItemDaPilha(item, estado, ehTopo && slotVisivel);
            AplicarUiDoItemDaPilha(item, estado, false);
            AplicarCollidersDoItemDaPilha(item, estado, ehTopo);
            AplicarFisicaDoItemDaPilha(item, estado, ehTopo);

            int renderersAtivos = ContarRenderersAtivos(item);
            if (ehTopo)
                diagnosticoRenderersAtivosTopo += renderersAtivos;
            else
                diagnosticoRenderersAtivosEscondidos += renderersAtivos;
        }
    }

    private void AplicarRenderersDoItemDaPilha(XRGrabInteractable item, EstadoOriginalItem estado, bool visivel)
    {
        if (item == null)
            return;

        if (estado != null)
        {
            for (int i = 0; i < estado.Renderers.Length; i++)
            {
                Renderer renderer = estado.Renderers[i].Renderer;
                if (renderer != null)
                    renderer.enabled = visivel && estado.Renderers[i].Enabled;
            }

            return;
        }

        foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null)
                renderer.enabled = visivel;
        }
    }

    private void AplicarUiDoItemDaPilha(XRGrabInteractable item, EstadoOriginalItem estado, bool visivel)
    {
        if (item == null)
            return;

        if (estado != null)
        {
            SetUiDoItemVisivel(estado, visivel);
            return;
        }

        foreach (Canvas canvas in item.GetComponentsInChildren<Canvas>(true))
        {
            if (canvas != null)
                canvas.enabled = visivel;
        }

        foreach (Graphic graphic in item.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic != null)
                graphic.enabled = visivel;
        }
    }

    private void AplicarCollidersDoItemDaPilha(XRGrabInteractable item, EstadoOriginalItem estado, bool ativo)
    {
        if (item == null)
            return;

        if (estado != null)
        {
            for (int i = 0; i < estado.Colliders.Length; i++)
            {
                Collider collider = estado.Colliders[i].Collider;
                if (collider != null)
                    collider.enabled = ativo && estado.Colliders[i].Enabled;
            }

            return;
        }

        foreach (Collider collider in item.GetComponentsInChildren<Collider>(true))
        {
            if (collider != null)
                collider.enabled = ativo;
        }
    }

    private void AplicarFisicaDoItemDaPilha(XRGrabInteractable item, EstadoOriginalItem estado, bool ehTopo)
    {
        if (item == null)
            return;

        item.enabled = ehTopo && (estado == null || estado.GrabEnabled);

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = ehTopo;
    }

    private int ContarRenderersAtivos(XRGrabInteractable item)
    {
        if (item == null)
            return 0;

        int total = 0;
        foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                total++;
        }

        return total;
    }

    private static bool ComponentePertenceAItemInventario(Component componente)
    {
        if (componente == null)
            return false;

        EstadoItemInventario estado = componente.GetComponentInParent<EstadoItemInventario>(true);
        return estado != null && estado.estaNoInventario;
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

        if (inventarioAberto && visivelNaRolagem)
        {
            aguardarFrameVisualParaEscala = true;
            ReancorarItemAtualNoSlot();
            AgendarRecalculoEscalaQuandoVisivel();
        }

        AtualizarVisibilidadeVisual();
    }

    private void ReancorarItemAtualNoSlot()
    {
        XRGrabInteractable item = itemGuardado != null ? itemGuardado : TopoDaPilha();
        if (item == null)
            return;

        itemGuardado = item;

        ParentarItemNoSlot(item);
        PosicionarItemNoPontoDoSlot(item.transform);

        if (estadosOriginais.TryGetValue(item, out EstadoOriginalItem estado))
        {
            if (inventarioAberto && visivelNaRolagem)
            {
                bool forcarRecalculo = itensRestauradosDoSave.Contains(item) ||
                                       itensComEscalaVisualPendente.Contains(item) ||
                                       !escalasVisuaisInventario.ContainsKey(item);
                AplicarOuAgendarEscalaVisual(item, estado, forcarRecalculo);
            }
            else
            {
                PrepararEscalaVisualPendenteNoSlot(item, estado);
            }
        }

        RegistrarFiltroNoItemGuardado();
        Physics.SyncTransforms();
    }

    public void SetInventarioAberto(bool aberto)
    {
        inventarioAberto = aberto;

        if (aberto)
        {
            ignorarProximoExited = false;
            aguardarFrameVisualParaEscala = true;
            ReancorarItemAtualNoSlot();
            AgendarRecalculoEscalaQuandoVisivel();
        }

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
