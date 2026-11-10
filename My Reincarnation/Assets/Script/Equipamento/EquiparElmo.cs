using System;
using System.Collections;
using System.Globalization;
using TMPro;
using ConfiguracaoClasse = AtributosManoplas.ConfiguracaoClasse;
using FaixaAtributo = AtributosManoplas.FaixaAtributo;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class EquiparElmo : MonoBehaviour
{

    [Header("Equipamento")]
    [SerializeField] private Transform cabecaAvatar;
    [Tooltip("Ponto do elmo que será alinhado com o osso da cabeça.")]
    [SerializeField] private Transform pontoEquipamento;
    [SerializeField] private Vector3 posicaoLocalNaCabeca;
    [SerializeField] private Vector3 rotacaoLocalNaCabeca = new Vector3(0f, 180f, 0f);

    [Header("Visao local em VR")]
    [Tooltip("Oculta a geometria somente durante a renderizacao da camera local do proprietario, quando equipado.")]
    [SerializeField] private bool ocultarParaCameraLocalQuandoEquipado = true;
    [Tooltip("Opcional. Se vazio, usa a camera do AvatarBody ou XR Origin ao qual a cabeca equipada pertence. Nao usa Camera.main para esta ocultacao.")]
    [SerializeField] private Camera cameraLocalProprietario;
    [SerializeField] private bool diagnosticoOcultacaoLocal;

    [Header("Popup")]
    [SerializeField] private GameObject popupInfo;
    [SerializeField] private float tempoParaFecharPopup = 10f;
    [SerializeField] private Camera cameraJogador;
    [SerializeField] private Vector3 posicaoPopupNaCamera = new Vector3(0f, 0f, 1.5f);
    [SerializeField] private Vector3 rotacaoPopupNaCamera = new Vector3(0f, 180f, 0f);
    [SerializeField] private bool manterPopupNaFrenteDaCamera = true;
    [SerializeField, Min(0f)] private float espacamentoHorizontalPopups = 0.45f;

    [SerializeField] private TipoAtributoManopla atributoRestricao = TipoAtributoManopla.Resistencia;
    [SerializeField, Min(0)] private int valorMinimoParaUsar;
    [SerializeField] private GameObject grupoRestricao;
    [SerializeField] private TMP_Text textoAtributoRestricao;
    [SerializeField] private TMP_Text textoValorRestricao;
    [SerializeField] private TMP_Text textoMensagemRestricao;

    private XRBaseInteractable interactable;
    private Coroutine corotinaFecharPopup;
    private bool jaEquipado;
    private Transform paiOriginalPopup;
    private Vector3 posicaoOriginalPopup, escalaOriginalPopup, escalaMundoOriginalPopup;
    private Quaternion rotacaoOriginalPopup;
    private bool posePopupCapturada, popupIsolado;
    private GameObject raizPopupSemEscala;
    private Transform paiOriginal;
    private Vector3 posicaoOriginalLocal;
    private Quaternion rotacaoOriginalLocal;
    private Vector3 escalaOriginalLocal;
    private Vector3 escalaOriginalMundo;
    private bool escalaOriginalMundoCapturada;
    private XRBaseInteractable[] interactablesOriginais;
    private bool[] estadosInteractablesOriginais;
    private Collider[] collidersOriginais;
    private bool[] estadosCollidersOriginais;
    private Rigidbody[] rigidbodiesOriginais;
    private bool[] estadosKinematicOriginais;
    private bool[] estadosColisaoRigidbodiesOriginais;
    private bool[] estadosGravidadeRigidbodiesOriginais;

    private readonly List<Renderer> renderersOcultacaoLocal = new List<Renderer>();
    private readonly Dictionary<Renderer, bool> visibilidadeAntesDasCameras = new Dictionary<Renderer, bool>();
    private readonly List<EscopoCameraElmo> escoposCamera = new List<EscopoCameraElmo>();
    private int profundidadeCameras;
    private bool callbacksRenderizacaoRegistrados;
    private bool cameraDefinidaPorCodigo;
    private bool avisoCameraLocalMostrado;

    private sealed class EscopoCameraElmo
    {
        internal Camera camera;
        internal readonly Dictionary<Renderer, bool> anteriores = new Dictionary<Renderer, bool>();
        internal readonly Dictionary<Renderer, bool> aplicados = new Dictionary<Renderer, bool>();
    }

    public bool JaEquipado => jaEquipado;
    public Transform CabecaConfigurada => cabecaAvatar;


    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        ResolverPontoEquipamento();
        BuscarPopup();

        if (cameraJogador == null)
            cameraJogador = Camera.main;

        CapturarPoseOriginalPopup();
        SalvarTransformOriginal();
        escalaOriginalMundo = transform.lossyScale;
        escalaOriginalMundoCapturada = true;
        CapturarComponentesOriginais();
        GarantirGeracao();
    }

    private void Start()
    {
        GarantirGeracao();
        if (!EstaArmazenadoNoInventario() && !jaEquipado)
            AplicarEscalaMundoComoLocal(escalaOriginalMundo);

        if (popupInfo != null)
            popupInfo.SetActive(false);
    }

    private void OnEnable()
    {
        if (!Gerado) StartCoroutine(GerarAposRestauracao());
        if (interactable == null)
            interactable = GetComponent<XRBaseInteractable>();

        if (interactable == null)
            return;

        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        if (jaEquipado) RegistrarOcultacaoLocal();
    }

    private void OnDisable()
    {
        EncerrarOcultacaoLocal();
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        CancelarFechamentoPendente();
        RemoverPopupVisivel();
        if (popupInfo != null)
            popupInfo.SetActive(false);
    }

    private void OnDestroy()
    {
        EncerrarOcultacaoLocal();
        RemoverPopupVisivel();
        if (raizPopupSemEscala != null) Destroy(raizPopupSemEscala);
    }

    // Integracao futura de propriedade: camera nula desabilita a resolucao automatica
    // para esta copia (por exemplo, um avatar remoto sem camera local).
    public void DefinirCameraLocalDoProprietario(Camera camera)
    {
        RestaurarEscoposCamera();
        cameraLocalProprietario = camera;
        cameraDefinidaPorCodigo = true;
        avisoCameraLocalMostrado = false;
    }

    private Camera ResolverCameraLocalProprietario()
    {
        if (cameraDefinidaPorCodigo || cameraLocalProprietario != null) return cameraLocalProprietario;
        if (cabecaAvatar == null) return null;
        AvatarBody proprietario = cabecaAvatar.GetComponentInParent<AvatarBody>(true);
        if (proprietario != null && proprietario.cameraCabeca != null)
        {
            Camera camera = proprietario.cameraCabeca.GetComponent<Camera>();
            if (camera != null) return camera;
        }
        var origem = cabecaAvatar.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>(true);
        return origem != null ? origem.Camera : null;
    }

    private void RegistrarOcultacaoLocal()
    {
        if (callbacksRenderizacaoRegistrados) return;
        RenderPipelineManager.beginCameraRendering += AntesDaCameraSRP;
        RenderPipelineManager.endCameraRendering += DepoisDaCameraSRP;
        RenderPipelineManager.endContextRendering += DepoisDoContextoSRP;
        Camera.onPreCull += AntesDaCameraPadrao;
        Camera.onPostRender += DepoisDaCameraPadrao;
        callbacksRenderizacaoRegistrados = true;
    }

    private void EncerrarOcultacaoLocal()
    {
        if (callbacksRenderizacaoRegistrados)
        {
            RenderPipelineManager.beginCameraRendering -= AntesDaCameraSRP;
            RenderPipelineManager.endCameraRendering -= DepoisDaCameraSRP;
            RenderPipelineManager.endContextRendering -= DepoisDoContextoSRP;
            Camera.onPreCull -= AntesDaCameraPadrao;
            Camera.onPostRender -= DepoisDaCameraPadrao;
            callbacksRenderizacaoRegistrados = false;
        }
        RestaurarEscoposCamera();
    }

    private void AntesDaCameraSRP(ScriptableRenderContext contexto, Camera camera) => AplicarOcultacaoParaCamera(camera);
    private void DepoisDaCameraSRP(ScriptableRenderContext contexto, Camera camera) => RestaurarCamera(camera);
    private void DepoisDoContextoSRP(ScriptableRenderContext contexto, List<Camera> cameras) => RestaurarEscoposCamera();
    private void AntesDaCameraPadrao(Camera camera)
    { if (GraphicsSettings.currentRenderPipeline == null) AplicarOcultacaoParaCamera(camera); }
    private void DepoisDaCameraPadrao(Camera camera)
    { if (GraphicsSettings.currentRenderPipeline == null) RestaurarCamera(camera); }

    private void AplicarOcultacaoParaCamera(Camera camera)
    {
        if (!jaEquipado || EstaArmazenadoNoInventario() || !isActiveAndEnabled || !gameObject.activeInHierarchy || camera == null) return;
        Camera local = ResolverCameraLocalProprietario();
        bool ocultar = ocultarParaCameraLocalQuandoEquipado && local != null && camera == local;
        if (local == null && ocultarParaCameraLocalQuandoEquipado && diagnosticoOcultacaoLocal && !avisoCameraLocalMostrado)
        {
            Debug.LogWarning("[EquiparElmo] Camera local do proprietario nao encontrada; configure a referencia de visao local.", this);
            avisoCameraLocalMostrado = true;
        }
        // Uma camera secundaria aninhada suspende a ocultacao da camera local.
        if (!ocultar && profundidadeCameras == 0) return;
        if (profundidadeCameras == escoposCamera.Count) escoposCamera.Add(new EscopoCameraElmo());
        EscopoCameraElmo escopo = escoposCamera[profundidadeCameras++];
        escopo.camera = camera;
        escopo.anteriores.Clear(); escopo.aplicados.Clear();
        renderersOcultacaoLocal.Clear();
        GetComponentsInChildren<Renderer>(true, renderersOcultacaoLocal);
        foreach (Renderer renderer in renderersOcultacaoLocal)
        {
            if (renderer == null) continue;
            bool anterior = renderer.forceRenderingOff;
            if (!visibilidadeAntesDasCameras.ContainsKey(renderer)) visibilidadeAntesDasCameras.Add(renderer, anterior);
            bool novo = ocultar || visibilidadeAntesDasCameras[renderer];
            if (anterior == novo) continue;
            escopo.anteriores.Add(renderer, anterior);
            escopo.aplicados.Add(renderer, novo);
            renderer.forceRenderingOff = novo;
        }
    }

    private void RestaurarCamera(Camera camera)
    {
        int indice = profundidadeCameras - 1;
        while (indice >= 0 && escoposCamera[indice].camera != camera) indice--;
        if (indice < 0) return;
        while (profundidadeCameras > indice) RestaurarUltimoEscopoCamera();
    }

    private void RestaurarUltimoEscopoCamera()
    {
        EscopoCameraElmo escopo = escoposCamera[--profundidadeCameras];
        foreach (var estado in escopo.anteriores)
            // Nao altera enabled, materiais, Layers, ou um valor diferente aplicado por outro sistema.
            if (estado.Key != null && estado.Key.forceRenderingOff == escopo.aplicados[estado.Key])
                estado.Key.forceRenderingOff = estado.Value;
        escopo.camera = null; escopo.anteriores.Clear(); escopo.aplicados.Clear();
        if (profundidadeCameras == 0) visibilidadeAntesDasCameras.Clear();
    }

    private void RestaurarEscoposCamera()
    {
        while (profundidadeCameras > 0) RestaurarUltimoEscopoCamera();
    }

    private bool EstaArmazenadoNoInventario()
    {
        var persistente = GetComponent<ItemPersistente>();
        return EstadoItemInventario.EstaNoInventario(this) ||
            (persistente != null && persistente.EstaNoInventario()) || GetComponentInParent<SlotInventario>(true) != null;
    }

    private void LateUpdate()
    {
        // Protecao caso uma camera tenha interrompido sua renderizacao no frame anterior.
        RestaurarEscoposCamera();
        if (!jaEquipado && !EstaArmazenadoNoInventario() && escalaOriginalMundoCapturada && isActiveAndEnabled && gameObject.activeInHierarchy)
            AplicarEscalaMundoComoLocal(escalaOriginalMundo);

        if (popupIsolado)
        {
            var estado = GetComponent<EstadoItemInventario>();
            bool ocultoNoInventario = estado != null && estado.estaNoInventario &&
                (estado.estaEscondidoNaPilha || estado.slotAtual == null || !estado.slotAtual.VisualEstaDisponivel);
            if (popupInfo == null || !popupInfo.activeInHierarchy || ocultoNoInventario)
            {
                CancelarFechamentoPendente();
                if (popupInfo != null) popupInfo.SetActive(false);
                RemoverPopupVisivel();
            }
        }
        if (popupInfo != null && popupInfo.activeInHierarchy)
        {
            if (Gerado)
            {
                AtualizarIdentidadePopup();
                AtualizarLinhasAtributos(resultado.atributos);
                AtualizarRestricaoPopup();
            }
            if (manterPopupNaFrenteDaCamera) PosicionarPopupNaCamera();
        }
    }

    public void Equipar()
    {
        if (jaEquipado)
            return;

        GerenciadorEquipamentosPlayer gerenciador = GerenciadorEquipamentosPlayer.Instancia ??
            FindFirstObjectByType<GerenciadorEquipamentosPlayer>();
        if (gerenciador == null)
        {
            Debug.LogWarning("[EquiparElmo] GerenciadorEquipamentosPlayer não encontrado.", this);
            return;
        }

        gerenciador.TentarEquipar(this);
    }

    public bool PodeEquiparNaCabeca(Transform cabeca)
    {
        if (cabeca != null)
            return true;

        Debug.LogWarning("[EquiparElmo] A cabeça do avatar não foi configurada nem encontrada.", this);
        return false;
    }

    internal bool AtendeRestricao(StatusPlayer jogador)
    {
        if (valorMinimoParaUsar <= 0) return true;
        if (jogador == null) return false;

        var equipamentos = jogador.GetComponent<GerenciadorEquipamentosPlayer>();
        var elmoAtual = equipamentos != null ? equipamentos.ElmoAtual : null;
        int bonusRemovidoNaTroca = elmoAtual != null && elmoAtual.JaEquipado &&
            elmoAtual.gameObject.activeInHierarchy ? elmoAtual.ObterValor(atributoRestricao) : 0;
        return Espada.AtendeRestricaoAtributo(Espada.ObterAtributoTotal(jogador, atributoRestricao),
            bonusRemovidoNaTroca, valorMinimoParaUsar);
    }

    internal bool EquiparNaCabeca(Transform cabeca)
    {
        if (jaEquipado)
            return true;
        if (!PodeEquiparNaCabeca(cabeca))
            return false;

        cabecaAvatar = cabeca;
        GarantirGeracao();
        jaEquipado = true;
        gameObject.SetActive(true);
        AlinharPontoEquipamentoComCabeca(cabecaAvatar);

        DesativarInteracaoEFisica();
        RegistrarOcultacaoLocal();
        if (popupInfo != null)
            popupInfo.SetActive(false);
        RemoverPopupVisivel();
        return true;
    }

    internal void PrepararQuebraPorDurabilidade()
    {
        EncerrarOcultacaoLocal();
        if (!jaEquipado) return;
        jaEquipado = false;
        var gerenciador = GetComponentInParent<GerenciadorEquipamentosPlayer>();
        if (gerenciador == null) gerenciador = GerenciadorEquipamentosPlayer.Instancia;
        gerenciador?.NotificarElmoDestruido(this);
        RemoverPopupVisivel();
    }

    internal System.Action CapturarEstadoEquipadoParaCarga()
    {
        bool equipado = jaEquipado;
        return () =>
        {
            EncerrarOcultacaoLocal();
            jaEquipado = equipado;
            if (equipado && isActiveAndEnabled) RegistrarOcultacaoLocal();
        };
    }

    public void Desequipar()
    {
        EncerrarOcultacaoLocal();
        if (!jaEquipado)
            return;

        RestaurarComponentesOriginais();
        if (paiOriginal != null)
            transform.SetParent(paiOriginal, false);
        else
            transform.SetParent(null, false);

        transform.localPosition = posicaoOriginalLocal;
        transform.localRotation = rotacaoOriginalLocal;
        if (escalaOriginalMundoCapturada)
            AplicarEscalaMundoComoLocal(escalaOriginalMundo);
        else
            transform.localScale = escalaOriginalLocal;

        jaEquipado = false;
        cabecaAvatar = null;
    }

    private void SalvarTransformOriginal()
    {
        paiOriginal = transform.parent;
        posicaoOriginalLocal = transform.localPosition;
        rotacaoOriginalLocal = transform.localRotation;
        escalaOriginalLocal = transform.localScale;
    }

    private void OnValidate()
    {
        ResolverPontoEquipamento();
        ValidarAtributosEDurabilidade();
    }

    private void ResolverPontoEquipamento()
    {
        if (pontoEquipamento != null)
            return;

        Transform[] filhos = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < filhos.Length; i++)
        {
            Transform filho = filhos[i];
            if (filho == null || filho == transform)
                continue;

            if (filho.name == "PosicaoEquipe" || filho.name == "PosicaoEquipamento" ||
                filho.name == "PontoEquipamento" || filho.name == "EixoEquipamento")
            {
                pontoEquipamento = filho;
                return;
            }
        }
    }

    private void AlinharPontoEquipamentoComCabeca(Transform cabeca)
    {
        if (pontoEquipamento == null)
        {
            transform.SetParent(cabeca, false);
            transform.localPosition = posicaoLocalNaCabeca;
            transform.localRotation = Quaternion.Euler(rotacaoLocalNaCabeca);
            if (escalaOriginalMundoCapturada)
                AplicarEscalaMundoComoLocal(escalaOriginalMundo);
            else
                transform.localScale = escalaOriginalLocal;
            return;
        }

        Quaternion rotacaoPontoRelativa = Quaternion.Inverse(transform.rotation) * pontoEquipamento.rotation;
        Vector3 escalaMundo = escalaOriginalMundoCapturada ? escalaOriginalMundo : transform.lossyScale;

        transform.SetParent(cabeca, true);
        transform.rotation = cabeca.rotation * Quaternion.Inverse(rotacaoPontoRelativa);
        AplicarEscalaMundoComoLocal(escalaMundo);
        transform.position += cabeca.position - pontoEquipamento.position;
    }

    private void CapturarComponentesOriginais()
    {
        interactablesOriginais = GetComponentsInChildren<XRBaseInteractable>(true);
        estadosInteractablesOriginais = new bool[interactablesOriginais.Length];
        for (int i = 0; i < interactablesOriginais.Length; i++)
            estadosInteractablesOriginais[i] = interactablesOriginais[i] != null && interactablesOriginais[i].enabled;

        collidersOriginais = GetComponentsInChildren<Collider>(true);
        estadosCollidersOriginais = new bool[collidersOriginais.Length];
        for (int i = 0; i < collidersOriginais.Length; i++)
            estadosCollidersOriginais[i] = collidersOriginais[i] != null && collidersOriginais[i].enabled;

        rigidbodiesOriginais = GetComponentsInChildren<Rigidbody>(true);
        estadosKinematicOriginais = new bool[rigidbodiesOriginais.Length];
        estadosColisaoRigidbodiesOriginais = new bool[rigidbodiesOriginais.Length];
        estadosGravidadeRigidbodiesOriginais = new bool[rigidbodiesOriginais.Length];
        for (int i = 0; i < rigidbodiesOriginais.Length; i++)
        {
            Rigidbody rb = rigidbodiesOriginais[i];
            if (rb == null)
                continue;

            estadosKinematicOriginais[i] = rb.isKinematic;
            estadosColisaoRigidbodiesOriginais[i] = rb.detectCollisions;
            estadosGravidadeRigidbodiesOriginais[i] = rb.useGravity;
        }
    }

    private void DesativarInteracaoEFisica()
    {
        // Altera somente os componentes deste elmo. Nunca desative o renderer
        // da cabeça do AvatarBody: a cabeça deve continuar visível equipado ou não.
        for (int i = 0; i < interactablesOriginais.Length; i++)
        {
            if (interactablesOriginais[i] != null)
                interactablesOriginais[i].enabled = false;
        }
        for (int i = 0; i < collidersOriginais.Length; i++)
        {
            if (collidersOriginais[i] != null)
                collidersOriginais[i].enabled = false;
        }
        for (int i = 0; i < rigidbodiesOriginais.Length; i++)
        {
            Rigidbody rb = rigidbodiesOriginais[i];
            if (rb == null)
                continue;

            rb.isKinematic = true;
            rb.detectCollisions = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void RestaurarComponentesOriginais()
    {
        for (int i = 0; i < interactablesOriginais.Length; i++)
            if (interactablesOriginais[i] != null)
                interactablesOriginais[i].enabled = estadosInteractablesOriginais[i];

        for (int i = 0; i < collidersOriginais.Length; i++)
            if (collidersOriginais[i] != null)
                collidersOriginais[i].enabled = estadosCollidersOriginais[i];

        for (int i = 0; i < rigidbodiesOriginais.Length; i++)
        {
            Rigidbody rb = rigidbodiesOriginais[i];
            if (rb == null)
                continue;

            rb.isKinematic = estadosKinematicOriginais[i];
            rb.detectCollisions = estadosColisaoRigidbodiesOriginais[i];
            rb.useGravity = estadosGravidadeRigidbodiesOriginais[i];
        }
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy || jaEquipado)
            return;

        CancelarFechamentoPendente();
        if (popupInfo == null)
            return;

        if (!GarantirGeracao()) return;
        PrepararPopupSemDistorcao();
        AtualizarRestricaoPopup();
        popupInfo.SetActive(true);

        PosicionarPopupNaCamera();
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        CancelarFechamentoPendente();
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy || popupInfo == null)
            return;

        corotinaFecharPopup = StartCoroutine(FecharPopupAposEspera());
    }

    private IEnumerator FecharPopupAposEspera()
    {
        yield return new WaitForSeconds(tempoParaFecharPopup);
        if (popupInfo != null)
            popupInfo.SetActive(false);
        RemoverPopupVisivel();
        corotinaFecharPopup = null;
    }

    private void PosicionarPopupNaCamera()
    {
        if (popupInfo == null)
            return;
        if (cameraJogador == null)
            cameraJogador = Camera.main;
        if (cameraJogador == null)
            return;

        LayoutPopupsEquipamentos.Atualizar(popupInfo, cameraJogador,
            posicaoPopupNaCamera, rotacaoPopupNaCamera, espacamentoHorizontalPopups);
    }

    private void CancelarFechamentoPendente()
    {
        if (corotinaFecharPopup == null)
            return;
        StopCoroutine(corotinaFecharPopup);
        corotinaFecharPopup = null;
    }

    private void RemoverPopupVisivel()
    {
        LayoutPopupsEquipamentos.Remover(popupInfo);
        RecolherPopupAoElmo();
    }


    private void CapturarPoseOriginalPopup()
    {
        if (posePopupCapturada || popupInfo == null) return;
        Transform painel = popupInfo.transform;
        paiOriginalPopup = painel.parent;
        posicaoOriginalPopup = painel.localPosition;
        rotacaoOriginalPopup = painel.localRotation;
        escalaOriginalPopup = painel.localScale;
        Vector3 tamanho = painel.lossyScale;
        escalaMundoOriginalPopup = new Vector3(Mathf.Abs(tamanho.x), Mathf.Abs(tamanho.y), Mathf.Abs(tamanho.z));
        posePopupCapturada = true;
    }

    private void PrepararPopupSemDistorcao()
    {
        if (popupInfo == null || popupIsolado) return;
        CapturarPoseOriginalPopup();
        foreach (var botao in popupInfo.GetComponentsInChildren<BotaoEquipar>(true))
            botao.DefinirElmoDoPopup(this);
        if (raizPopupSemEscala == null)
        {
            raizPopupSemEscala = new GameObject("[Popup Elmo] " + name);
            raizPopupSemEscala.hideFlags = HideFlags.DontSave;
            var cenaDono = gameObject.scene;
            if (cenaDono.IsValid() && cenaDono.isLoaded)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raizPopupSemEscala, cenaDono);
        }
        Transform raiz = raizPopupSemEscala.transform;
        raiz.SetParent(null, false);
        raiz.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        raiz.localScale = Vector3.one;
        // A rotacao global da camera nao pode ser combinada com a escala desigual do modelo.
        Transform painel = popupInfo.transform;
        painel.SetParent(raiz, false);
        painel.localPosition = Vector3.zero;
        painel.localRotation = Quaternion.identity;
        painel.localScale = escalaMundoOriginalPopup;
        popupIsolado = true;
    }

    private void RecolherPopupAoElmo()
    {
        if (!popupIsolado) return;
        popupIsolado = false;
        if (popupInfo == null) return;
        // Fecha antes de reparentar para nao reaparecer fora do inventario/menu.
        popupInfo.SetActive(false);
        Transform painel = popupInfo.transform;
        painel.SetParent(paiOriginalPopup != null ? paiOriginalPopup : transform, false);
        painel.localPosition = posicaoOriginalPopup;
        painel.localRotation = rotacaoOriginalPopup;
        painel.localScale = escalaOriginalPopup;
    }

    private void AplicarEscalaMundoComoLocal(Vector3 escalaMundo)
    {
        Vector3 escalaPai = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            DividirSeguro(escalaMundo.x, escalaPai.x),
            DividirSeguro(escalaMundo.y, escalaPai.y),
            DividirSeguro(escalaMundo.z, escalaPai.z));
    }

    private static float DividirSeguro(float valor, float divisor)
    {
        if (Mathf.Approximately(divisor, 0f) || float.IsNaN(divisor) || float.IsInfinity(divisor))
            return valor;
        float resultado = valor / divisor;
        return float.IsNaN(resultado) || float.IsInfinity(resultado) ? valor : resultado;
    }

    [Serializable]
    public class ValorAtributo
    {
        public TipoAtributoManopla atributo;
        public int valor;
    }

    [Serializable]
    public class TextoAtributo
    {
        public TMP_Text textoNome;
        [UnityEngine.Serialization.FormerlySerializedAs("texto")]
        public TMP_Text textoValor;
        [Header("Comparacao com o elmo equipado")]
        public GameObject setaUp;
        public GameObject setaDown;
        [Tooltip("Mostra somente o tamanho da diferenca, sem sinal. Ex.: 2 para +2 ou -2.")]
        public TMP_Text textoDiferenca;
        public GameObject sinalPositivo;
        public GameObject sinalNegativo;
    }

    [Serializable]
    public class EstadoGerado
    {
        public int versao = 1;
        public string nome;
        public string descricao;
        public ClasseManopla classe;
        public AfinidadeDivinaManopla afinidade;
        // Campo legado mantido somente para preservar o JSON dos saves existentes.
        public Color cor;
        public List<ValorAtributo> atributos = new List<ValorAtributo>();
    }

    [Header("Identidade")]
    [SerializeField] private GameObject prefabObjeto;
    [Tooltip("Nome mostrado no popup. Vazio usa o nome atual do prefab. Nao altera IDs nem atributos salvos.")]
    [SerializeField] private string nomeExibicao;
    [TextArea(3, 8)] [SerializeField] private string descricao;

    [Header("Classes: chance independente; zero desativa")]
    [SerializeField] private List<ConfiguracaoClasse> classes = CriarClassesPadrao();

    [Header("Durabilidade / vida do elmo")]
    [SerializeField, Min(0.01f)] private float vidaMaxima = 100f;
    [SerializeField, Min(0f)] private float vidaAtual = 100f;
    [Tooltip("Desgaste por minuto de jogo, somente enquanto equipada. Nao desgasta guardada ou solta.")]
    [SerializeField, Min(0f)] private float desgasteNaturalPorMinuto = 0.5f;
    [SerializeField] private TMP_Text textoDurabilidadeAtual;
    [SerializeField] private TMP_Text textoDurabilidadeTotal;
    private bool destruindoPorDurabilidade;
    private double vidaPrecisa;
    private float ultimaVidaPublicada = float.NaN;
    public float VidaAtual => vidaAtual;
    public float VidaMaxima => vidaMaxima;
    public float VidaFaltando => Mathf.Max(0f, vidaMaxima - vidaAtual);
    public bool EstaQuebrado => vidaAtual <= 0f || destruindoPorDurabilidade;

    private void Update()
    {
        if (destruindoPorDurabilidade) return;
        var estado = GetComponent<EstadoItemInventario>();
        if (estado != null && estado.estaNoInventario) return;
        if (vidaAtual <= 0f) { AplicarDesgaste(0d); return; }
        if (!JaEquipado) return;
        AplicarDesgaste((double)desgasteNaturalPorMinuto * Time.deltaTime / 60d);
    }

    // Gancho para um futuro evento de combate. Nao aplica desgaste de combate automaticamente.
    public void AplicarDesgasteCombate(float quantidade)
    {
        if (JaEquipado) AplicarDesgaste(quantidade);
    }

    private void AplicarDesgaste(double quantidade)
    {
        if (destruindoPorDurabilidade || double.IsNaN(quantidade) || double.IsInfinity(quantidade) || quantidade < 0d) return;
        if (vidaAtual != ultimaVidaPublicada) vidaPrecisa = vidaAtual; // Save ou reparo alterou o campo.
        vidaPrecisa = CalcularVidaRestante(vidaPrecisa, quantidade);
        vidaAtual = (float)vidaPrecisa;
        ultimaVidaPublicada = vidaAtual;
        AtualizarDurabilidadeVisual();
        if (vidaAtual > 0f) return;
        destruindoPorDurabilidade = true;
        PrepararQuebraPorDurabilidade();
        GetComponent<ItemPersistente>()?.RegistrarConsumoPermanente();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    internal static double CalcularVidaRestante(double atual, double desgaste)
    {
        return AtributosManoplas.CalcularVidaRestante(atual, desgaste);
    }

    public void RepararCompleto()
    {
        if (destruindoPorDurabilidade) return;
        vidaAtual = vidaMaxima;
        ultimaVidaPublicada = float.NaN;
        AtualizarDurabilidadeVisual();
    }

    public void AtualizarDurabilidadeVisual()
    {
        if (textoDurabilidadeAtual != null) textoDurabilidadeAtual.text = vidaAtual.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
        if (textoDurabilidadeTotal != null) textoDurabilidadeTotal.text = vidaMaxima.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
    }

    [Header("Popup existente (nao controla abertura ou posicionamento)")]
    [SerializeField] private TMP_Text textoNome;
    [SerializeField] private TMP_Text textoClasse;
    [SerializeField] private TMP_Text textoDescricao;
    [Header("Cinco linhas do popup: Elemento 0 e a primeira linha (topo)")]
    [Tooltip("Associe o texto do nome e o texto do numero em cada linha. O tipo vem do sorteio, nao desta lista.")]
    [SerializeField] private List<TextoAtributo> textosValores = CriarLinhasPopup();

    private const int LimiteLinhasPopup = 5;

    private static List<TextoAtributo> CriarLinhasPopup()
    {
        var linhas = new List<TextoAtributo>();
        for (int i = 0; i < LimiteLinhasPopup; i++) linhas.Add(new TextoAtributo());
        return linhas;
    }

    private void ValidarAtributosEDurabilidade()
    {
        NormalizarLinhasPopup();
        if (float.IsNaN(vidaMaxima) || float.IsInfinity(vidaMaxima)) vidaMaxima = 100f;
        vidaMaxima = Mathf.Max(0.01f, vidaMaxima);
        if (float.IsNaN(vidaAtual) || float.IsInfinity(vidaAtual)) vidaAtual = vidaMaxima;
        vidaAtual = Mathf.Clamp(vidaAtual, 0f, vidaMaxima);
        if (float.IsNaN(desgasteNaturalPorMinuto) || float.IsInfinity(desgasteNaturalPorMinuto)) desgasteNaturalPorMinuto = 0.5f;
        desgasteNaturalPorMinuto = Mathf.Max(0f, desgasteNaturalPorMinuto);
    }

    private void NormalizarLinhasPopup()
    {
        if (textosValores == null) textosValores = CriarLinhasPopup();
        while (textosValores.Count < LimiteLinhasPopup) textosValores.Add(new TextoAtributo());
        if (textosValores.Count > LimiteLinhasPopup)
            textosValores.RemoveRange(LimiteLinhasPopup, textosValores.Count - LimiteLinhasPopup);
        for (int i = 0; i < textosValores.Count; i++)
            if (textosValores[i] == null) textosValores[i] = new TextoAtributo();
    }

    [SerializeField, HideInInspector] private EstadoGerado resultado;
    public bool Gerado => resultado != null && !EhResultadoVazioLegado(resultado);
    public ClasseManopla Classe => Gerado ? resultado.classe : ClasseManopla.Comum;
    public AfinidadeDivinaManopla Afinidade => Gerado ? resultado.afinidade : AfinidadeDivinaManopla.Nenhuma;

    private IEnumerator GerarAposRestauracao()
    {
        yield return null;
        if (isActiveAndEnabled) GarantirGeracao();
    }

    public void PrepararMigracaoSaveLegado()
    {
        resultado = null;
        // A verificacao sincronica do save legado precisa continuar enxergando os extras antigos.
        if (isActiveAndEnabled) StartCoroutine(GerarAposRestauracao());
    }

    private void Reset()
    {
#if UNITY_EDITOR
        prefabObjeto = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
#endif
        classes = CriarClassesPadrao();
        BuscarPopup();
    }

    private void BuscarPopup()
    {
        if (popupInfo != null) return;
        Transform filho = transform.Find("PopUp");
        if (filho != null) popupInfo = filho.gameObject;
    }

    private void AtualizarRestricaoPopup()
    {
        if (grupoRestricao == null)
            grupoRestricao = popupInfo.transform.Find("Fundo/Restricao")?.gameObject;
        if (grupoRestricao == null) return;

        var camera = cameraJogador != null ? cameraJogador : Camera.main;
        var jogador = camera != null ? camera.GetComponentInParent<StatusPlayer>() : null;
        if (jogador == null && GerenciadorEquipamentosPlayer.Instancia != null)
            jogador = GerenciadorEquipamentosPlayer.Instancia.GetComponent<StatusPlayer>();

        bool restrito = !AtendeRestricao(jogador);
        grupoRestricao.SetActive(restrito);
        if (!restrito) return;

        Transform grupo = grupoRestricao.transform;
        textoAtributoRestricao ??= grupo.Find("Atributo")?.GetComponent<TMP_Text>();
        textoValorRestricao ??= grupo.Find("Valor")?.GetComponent<TMP_Text>();
        textoMensagemRestricao ??= grupo.Find("Descricao")?.GetComponent<TMP_Text>();
        if (textoAtributoRestricao != null)
            textoAtributoRestricao.text = atributoRestricao == TipoAtributoManopla.Mana ? "MP" : atributoRestricao.ToString();
        if (textoValorRestricao != null)
            textoValorRestricao.text = valorMinimoParaUsar.ToString(CultureInfo.InvariantCulture);
        if (textoMensagemRestricao != null)
            textoMensagemRestricao.text = "Seu valor está abaixo do mínimo";
    }

    // Idempotente: abrir menu, reequipar ou reativar o objeto nunca refaz o sorteio.
    public bool GarantirGeracao()
    {
        if (Gerado) { AtualizarPopup(); return true; }
        var ativas = new List<ConfiguracaoClasse>();
        var vistas = new HashSet<ClasseManopla>();
        if (classes == null) classes = CriarClassesPadrao();
        foreach (var configuracao in classes)
        {
            if (configuracao == null || configuracao.chance <= 0f) continue;
            if (!Enum.IsDefined(typeof(ClasseManopla), configuracao.classe) ||
                !vistas.Add(configuracao.classe) || float.IsNaN(configuracao.chance) ||
                float.IsInfinity(configuracao.chance) || !FaixasValidas(configuracao))
            {
                Debug.LogError("[EquiparElmo] Classe repetida ou faixa de atributos invalida.", this);
                return false;
            }
            ativas.Add(configuracao);
        }
        if (ativas.Count == 0)
        {
            Debug.LogError("[EquiparElmo] Ative pelo menos uma classe com chance maior que zero.", this);
            return false;
        }
        ConfiguracaoClasse classe = Flecha.SortearClassePonderada(ativas.ToArray());
        var pool = new List<FaixaAtributo>(classe.atributos);
        int quantidade = UnityEngine.Random.Range(classe.quantidadeMinima, classe.quantidadeMaxima + 1);
        var estado = new EstadoGerado
        {
            nome = ObterNomePrefab(), descricao = descricao, classe = classe.classe,
            afinidade = AtributosManoplas.SortearAfinidade(classe.classe, () => UnityEngine.Random.value)
        };
        for (int i = 0; i < quantidade; i++)
        {
            int indice = UnityEngine.Random.Range(0, pool.Count);
            FaixaAtributo faixa = pool[indice];
            pool.RemoveAt(indice);
            estado.atributos.Add(new ValorAtributo { atributo = faixa.atributo,
                valor = UnityEngine.Random.Range(faixa.minimo, faixa.maximo + 1) });
        }
        resultado = estado;
        AtualizarPopup();
        return true;
    }

    private static bool FaixasValidas(ConfiguracaoClasse classe)
    {
        if (classe.atributos == null || classe.quantidadeMinima < 0 ||
            classe.quantidadeMaxima < classe.quantidadeMinima || classe.quantidadeMaxima > classe.atributos.Count)
            return false;
        var tipos = new HashSet<TipoAtributoManopla>();
        foreach (var faixa in classe.atributos)
            if (faixa == null || !Enum.IsDefined(typeof(TipoAtributoManopla), faixa.atributo) ||
                !tipos.Add(faixa.atributo) || faixa.minimo < 0 || faixa.maximo < faixa.minimo || faixa.maximo == int.MaxValue)
                return false;
        return true;
    }

    private string ObterNomePrefab()
    {
        if (prefabObjeto != null) return prefabObjeto.name;
        var persistente = GetComponent<ItemPersistente>();
        if (persistente != null && persistente.PrefabReferencia != null) return persistente.PrefabReferencia.name;
        return gameObject.name.Replace("(Clone)", "").Trim();
    }

    public int ObterValor(TipoAtributoManopla atributo)
    {
        if (!Gerado) return 0;
        foreach (var valor in resultado.atributos) if (valor.atributo == atributo) return valor.valor;
        return 0;
    }

    public void AtualizarPopup()
    {
        if (!Gerado) return;
        BuscarPopup();
        AtualizarDurabilidadeVisual();
        AtualizarIdentidadePopup();
        if (textoClasse != null)
        {
            textoClasse.text = resultado.classe.ToString() +
                (resultado.afinidade == AfinidadeDivinaManopla.Nenhuma ? "" : " - " + resultado.afinidade);
        }
        AtualizarLinhasAtributos(resultado.atributos);
    }

    private void AtualizarIdentidadePopup()
    {
        // Textos editoriais usam a configuracao atual, nunca o snapshot do sorteio.
        if (textoNome != null)
            textoNome.text = string.IsNullOrWhiteSpace(nomeExibicao) ? ObterNomePrefab() : nomeExibicao.Trim();
        if (textoDescricao != null) textoDescricao.text = descricao ?? string.Empty;
    }

    private void AtualizarLinhasAtributos(IReadOnlyList<ValorAtributo> atributos)
    {
        NormalizarLinhasPopup();
        EquiparElmo equipada = ObterElmoComparacao(out bool comparacaoDisponivel);
        atributos = CriarAtributosComparacao(atributos, equipada);
        for (int i = 0; i < LimiteLinhasPopup; i++)
        {
            TextoAtributo linha = textosValores[i];
            ValorAtributo valor = atributos != null && i < atributos.Count ? atributos[i] : null;
            if (linha.textoNome != null)
                linha.textoNome.text = valor != null ? (valor.atributo == TipoAtributoManopla.Mana ? "MP" : valor.atributo.ToString()) : string.Empty;
            if (linha.textoValor != null)
                linha.textoValor.text = valor != null ? "+" + valor.valor.ToString(CultureInfo.InvariantCulture) : string.Empty;
            AtualizarComparacaoLinha(linha, valor, equipada, comparacaoDisponivel);
        }
    }

    private static IReadOnlyList<ValorAtributo> CriarAtributosComparacao(
        IReadOnlyList<ValorAtributo> candidata, EquiparElmo equipada)
    {
        var linhas = new List<ValorAtributo>();
        var tipos = new HashSet<TipoAtributoManopla>();
        if (candidata != null)
            foreach (var valor in candidata)
                if (valor != null && tipos.Add(valor.atributo)) linhas.Add(valor);
        if (equipada != null && equipada.resultado != null)
            foreach (var valor in equipada.resultado.atributos)
                if (valor != null && tipos.Add(valor.atributo))
                    linhas.Add(new ValorAtributo { atributo = valor.atributo, valor = 0 });
        return linhas;
    }

    private EquiparElmo ObterElmoComparacao(out bool disponivel)
    {
        disponivel = false;
        var gerenciador = GetComponentInParent<GerenciadorEquipamentosPlayer>();
        if (gerenciador == null) gerenciador = GerenciadorEquipamentosPlayer.Instancia;
        if (gerenciador == null) return null;
        EquiparElmo atual = gerenciador.ElmoAtual;
        // Sem elmo equipado compara com zero, sem somar outros equipamentos ou atributos base.
        if (atual == null || !atual.JaEquipado)
        {
            disponivel = true;
            return null;
        }
        disponivel = atual.Gerado;
        return atual;
    }

    private static void AtualizarComparacaoLinha(TextoAtributo linha, ValorAtributo valor,
        EquiparElmo equipada, bool disponivel)
    {
        bool comparar = valor != null && disponivel;
        long diferenca = comparar ? (long)valor.valor - (equipada != null ? equipada.ObterValor(valor.atributo) : 0) : 0;
        bool maior = comparar && diferenca > 0;
        bool menor = comparar && diferenca < 0;
        DefinirIndicador(linha.setaUp, maior);
        DefinirIndicador(linha.setaDown, menor);
        DefinirIndicador(linha.sinalPositivo, maior);
        DefinirIndicador(linha.sinalNegativo, menor);
        if (linha.textoDiferenca != null)
            linha.textoDiferenca.text = comparar ? Math.Abs(diferenca).ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private static void DefinirIndicador(GameObject indicador, bool ativo)
    {
        if (indicador != null && indicador.activeSelf != ativo) indicador.SetActive(ativo);
    }

    public string ExportarEstadoJson() => Gerado ? JsonUtility.ToJson(resultado) : null;

    public void RestaurarEstadoJson(string json)
    {
        resultado = LerEValidarEstadoJson(json);
        if (resultado == null)
        {
            PrepararMigracaoSaveLegado();
            return;
        }
        AtualizarPopup();
    }

    // Unity serializa classes inline como objetos vazios, mesmo antes do primeiro sorteio.
    // Nao confundir esse placeholder com um Comum valido que sorteou zero atributos.
    internal static bool EhResultadoVazioLegado(EstadoGerado estado)
    {
        return estado != null && estado.versao == 1 &&
            string.IsNullOrEmpty(estado.nome) && string.IsNullOrEmpty(estado.descricao) &&
            estado.classe == ClasseManopla.Comum && estado.afinidade == AfinidadeDivinaManopla.Nenhuma &&
            estado.atributos != null && estado.atributos.Count == 0 &&
            estado.cor == default(Color);
    }

    public static EstadoGerado LerEValidarEstadoJson(string json)
    {
        EstadoGerado estado = JsonUtility.FromJson<EstadoGerado>(json);
        if (EhResultadoVazioLegado(estado)) return null; // Migra somente o placeholder conhecido.
        if (estado == null || estado.versao != 1 || string.IsNullOrWhiteSpace(estado.nome) || estado.atributos == null ||
            !Enum.IsDefined(typeof(ClasseManopla), estado.classe) ||
            !Enum.IsDefined(typeof(AfinidadeDivinaManopla), estado.afinidade))
            throw new System.IO.InvalidDataException("Estado de elmo invalido.");
        var tipos = new HashSet<TipoAtributoManopla>();
        foreach (var valor in estado.atributos)
            if (valor == null || valor.valor < 0 || !Enum.IsDefined(typeof(TipoAtributoManopla), valor.atributo) || !tipos.Add(valor.atributo))
                throw new System.IO.InvalidDataException("Atributo de elmo invalido.");
        return estado;
    }

    private static List<ConfiguracaoClasse> CriarClassesPadrao()
    {
        var lista = new List<ConfiguracaoClasse>();
        int[] minimos = { 0, 1, 2, 3, 4, 6, 14, 14 };
        int[] maximos = { 1, 2, 3, 4, 5, 8, 14, 14 };
        for (int i = 0; i < 8; i++)
        {
            var classe = new ConfiguracaoClasse { classe = (ClasseManopla)i,
                chance = i == 0 ? 70f : i == 1 ? 25f : i == 2 ? 5f : 0f,
                quantidadeMinima = minimos[i], quantidadeMaxima = maximos[i] };
            foreach (TipoAtributoManopla tipo in Enum.GetValues(typeof(TipoAtributoManopla)))
                if (tipo != TipoAtributoManopla.Critico && tipo != TipoAtributoManopla.Defesa)
                    classe.atributos.Add(new FaixaAtributo { atributo = tipo, minimo = 1, maximo = 5 });
            lista.Add(classe);
        }
        return lista;
    }
}
