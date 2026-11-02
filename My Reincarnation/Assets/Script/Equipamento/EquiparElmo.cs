using System.Collections;
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
        if (popupInfo == null)
        {
            Transform popup = transform.Find("PopUp");
            if (popup != null)
                popupInfo = popup.gameObject;
        }

        if (cameraJogador == null)
            cameraJogador = Camera.main;

        CapturarPoseOriginalPopup();
        SalvarTransformOriginal();
        escalaOriginalMundo = transform.lossyScale;
        escalaOriginalMundoCapturada = true;
        CapturarComponentesOriginais();
    }

    private void Start()
    {
        if (!EstaArmazenadoNoInventario() && !jaEquipado)
            AplicarEscalaMundoComoLocal(escalaOriginalMundo);

        if (popupInfo != null)
            popupInfo.SetActive(false);
    }

    private void OnEnable()
    {
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
        if (manterPopupNaFrenteDaCamera && popupInfo != null && popupInfo.activeInHierarchy)
            PosicionarPopupNaCamera();
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

    internal bool EquiparNaCabeca(Transform cabeca)
    {
        if (jaEquipado)
            return true;
        if (!PodeEquiparNaCabeca(cabeca))
            return false;

        cabecaAvatar = cabeca;
        GetComponent<AtributosElmo>()?.GarantirGeracao();
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

        GetComponent<AtributosElmo>()?.GarantirGeracao();
        PrepararPopupSemDistorcao();
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
}
