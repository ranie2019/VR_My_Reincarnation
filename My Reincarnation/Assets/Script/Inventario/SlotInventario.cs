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
    private Vector3 tamanhoDoSlot;

    private void Awake()
    {
        if (socketInteractor == null)
            socketInteractor = GetComponent<XRSocketInteractor>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

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
        {
            LogEstadoItem("Rejeitado: outro socket tentou selecionar item guardado", itemGuardado);
            return false;
        }

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

        LogEstadoItem($"Rejeitado em {contexto}: item pertence a outro slot", item, estado);
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
        {
            LogEstadoItem($"Rejeitado em {contexto}: estado aponta para este slot, mas item nao esta na pilha", item, estado);
            return false;
        }

        return true;
    }

    private EstadoItemInventario MarcarItemAceitoPeloSlot(XRGrabInteractable item, bool escondido, string contexto)
    {
        var estado = ObterEstadoInventario(item, true);
        if (estado == null)
            return null;

        estado.MarcarAceito(this, escondido);
        LogEstadoItem($"Item aceito por este slot ({contexto})", item, estado);
        return estado;
    }

    private void MarcarItemComoTopo(XRGrabInteractable item)
    {
        var estado = ObterEstadoInventario(item, true);
        if (estado == null)
            return;

        estado.MarcarTopo(this);
        LogEstadoItem("Item virou topo da pilha", item, estado);
    }

    private void MarcarItemComoEscondidoNaPilha(XRGrabInteractable item)
    {
        var estado = ObterEstadoInventario(item, true);
        if (estado == null)
            return;

        estado.MarcarEscondido(this);
        LogEstadoItem("Item escondido na pilha", item, estado);
    }

    private void LiberarEstadoInventarioDoItem(XRGrabInteractable item)
    {
        var estado = ObterEstadoInventario(item, false);
        if (estado == null)
            return;

        estado.Liberar();
        LogEstadoItem("Item saiu do inventario", item, estado);
    }

    private void RejeitarSelecaoDoSocket(XRGrabInteractable item, string contexto)
    {
        if (item == null || socketInteractor == null || socketInteractor.interactionManager == null)
            return;

        if (!socketInteractor.IsSelecting(item))
            return;

        LogEstadoItem($"Rejeitando selecao do socket ({contexto})", item);

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
        LogFisicaItem("Esconder item na pilha", item);
    }

    private void AplicarVisualNoSlot(XRGrabInteractable item)
    {
        if (item == null)
            return;

        SalvarEstadoOriginalSeNecessario(item);
        EstadoOriginalItem estado = estadosOriginais[item];

        itemGuardado = item;
        MarcarItemComoTopo(item);
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
        LogEscalaItem("Aplicar escala visual do slot", item, estado);
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

    private void RestaurarEscalaEParentParaMundo(XRGrabInteractable item)
    {
        if (item == null)
            return;

        string parentAntes = ObterNomeParent(item.transform);
        Vector3 localScaleAntes = item.transform.localScale;
        Vector3 lossyScaleAntes = item.transform.lossyScale;

        if (!estadosOriginais.TryGetValue(item, out EstadoOriginalItem estado))
        {
            item.transform.SetParent(null, true);
            Debug.Log($"[SlotInventario][Escala] Restaurar escala/parent sem estado salvo | item={item.name} | parentAntes={parentAntes} | parentDepois={ObterNomeParent(item.transform)} | localScaleAntes={localScaleAntes} | lossyScaleAntes={lossyScaleAntes} | escalaOriginalSalva=sem estado | localScaleDepois={item.transform.localScale} | lossyScaleDepois={item.transform.lossyScale}");
            return;
        }

        item.transform.SetParent(null, true);

        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp != null && escalaComp.inicializado)
            escalaComp.LimparTravaDoSlot(this);

        item.transform.localScale = estado.LossyScaleOriginal;
        item.SetTargetLocalScale(estado.LossyScaleOriginal);

        Debug.Log($"[SlotInventario][Escala] Restaurar escala/parent ao sair | item={item.name} | parentAntes={parentAntes} | parentDepois={ObterNomeParent(item.transform)} | localScaleAntes={localScaleAntes} | lossyScaleAntes={lossyScaleAntes} | escalaOriginalSalva={estado.LossyScaleOriginal} | localScaleDepois={item.transform.localScale} | lossyScaleDepois={item.transform.lossyScale}");
    }

    private void RestaurarEscalaOriginalDoEstado(XRGrabInteractable item, EstadoOriginalItem estado, bool restaurarTargetOriginal)
    {
        if (item == null || estado == null)
            return;

        Transform itemTransform = item.transform;

        var escalaComp = item.GetComponent<EscalaOriginalItem>();
        if (escalaComp != null && escalaComp.inicializado)
            escalaComp.LimparTravaDoSlot(this);

        Vector3 escalaLocalOriginal = itemTransform.parent == estado.ParentOriginal
            ? estado.LocalScaleOriginal
            : ConverterEscalaMundoParaLocal(estado.LossyScaleOriginal, itemTransform.parent);

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
        LogEscalaItem("Salvar escala original", item, estado);
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
        TocarSom(somRetirarItem);
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

    private void TocarSom(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
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

    private void LogEstadoItem(string etapa, XRGrabInteractable item, EstadoItemInventario estado = null)
    {
        if (item == null)
        {
            Debug.Log($"[SlotInventario][StackLock] {etapa} | item=null | slotAtual={name} | pilha={pilhaItens.Count}");
            return;
        }

        if (estado == null)
            estado = ObterEstadoInventario(item, false);
        string slotDono = estado != null && estado.slotAtual != null ? estado.slotAtual.name : "null";
        bool estaNoInventario = estado != null && estado.estaNoInventario;
        bool estaEscondido = estado != null && estado.estaEscondidoNaPilha;
        bool processando = estado != null && estado.estaSendoProcessado;

        Debug.Log($"[SlotInventario][StackLock] {etapa} | item={item.name} | slotAtual={name} | slotDono={slotDono} | estaNoInventario={estaNoInventario} | estaEscondidoNaPilha={estaEscondido} | estaSendoProcessado={processando} | pilhaItens.Count={pilhaItens.Count}");
    }

    private static void LogEscalaItem(string etapa, XRGrabInteractable item, EstadoOriginalItem estado)
    {
        if (item == null)
        {
            Debug.Log($"[SlotInventario][Escala] {etapa} | item=null");
            return;
        }

        Transform itemTransform = item.transform;
        string escalaOriginal = estado != null
            ? $"localOriginal={estado.LocalScaleOriginal} lossyOriginal={estado.LossyScaleOriginal} targetOriginal={estado.TargetLocalScaleOriginal}"
            : "sem escala original salva";

        Debug.Log($"[SlotInventario][Escala] {etapa} | item={item.name} | localScaleAtual={itemTransform.localScale} | lossyScaleAtual={itemTransform.lossyScale} | {escalaOriginal} | parentAtual={ObterNomeParent(itemTransform)}");
    }

    private static string ObterNomeParent(Transform itemTransform)
    {
        return itemTransform != null && itemTransform.parent != null
            ? itemTransform.parent.name
            : "null";
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
        }
    }
}
