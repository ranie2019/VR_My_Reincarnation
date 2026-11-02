using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum LadoMaoEquipamento
{
    Direita = 0,
    Esquerda = 1
}

public class EquiparManoplas : MonoBehaviour
{

    [Header("Equipamento")]
    [SerializeField] private LadoMaoEquipamento ladoMao = LadoMaoEquipamento.Direita;
    [Header("Popup")]
    [SerializeField] private GameObject popupInfo;
    [SerializeField] private float tempoParaFecharPopup = 10f;

    [Tooltip("Camera usada como referência para posicionar o popup.")]
    [SerializeField] private Camera cameraJogador;

    [Tooltip(
        "Posição local do popup em relação à câmera. " +
        "X = esquerda/direita, Y = baixo/cima, Z = distância à frente."
    )]
    [SerializeField] private Vector3 posicaoPopupNaCamera = new Vector3(0f, 0f, 1.5f);

    [Tooltip("Rotação local manual do popup em relação à câmera.")]
    [SerializeField] private Vector3 rotacaoPopupNaCamera = Vector3.zero;

    [Tooltip("Enquanto estiver aberto, mantém o popup na posição relativa à câmera.")]
    [SerializeField] private bool manterPopupNaFrenteDaCamera = true;

    [Tooltip("Distância lateral entre popups abertos ao mesmo tempo.")]
    [SerializeField, Min(0f)] private float espacamentoHorizontalPopups = 0.45f;

    [Header("Prefab Completo")]
    [Tooltip(
        "Referência opcional ao prefab completo da manopla. " +
        "O objeto raiz deste componente será movido por inteiro."
    )]
    [SerializeField] private GameObject luvaVisual;

    [Header("Mão do Avatar")]
    [SerializeField] private Transform transformMaoAvatar;

    [Header("Eixo de Equipamento")]
    [Tooltip(
        "Objeto vazio filho que representa o ponto e a orientação da mão. " +
        "Deixe vazio para manter o comportamento antigo. Se não for atribuído, " +
        "o script procura automaticamente por EixoEquipamento ou PoseEquipamento."
    )]
    [SerializeField] private Transform eixoEquipamento;

    [Tooltip("Renderer opcional da mão nua do avatar.")]
    [SerializeField] private SkinnedMeshRenderer rendererMaoAvatar;

    [SerializeField] private bool esconderMaoAoEquipar = false;

    [Header("Escala")]
    [Tooltip(
        "Mantido por compatibilidade com prefabs antigos. " +
        "A escala original do prefab é preservada."
    )]
    public Vector3 escalaEquipado = Vector3.one;

    private XRBaseInteractable interactable;
    private Coroutine corotinaFecharPopup;

    private bool jaEquipado;
    private System.Action restaurarEstadoEquipamento;
    private System.Action restaurarVisibilidadeMao;

    private Transform paiOriginal;
    private Vector3 escalaOriginalLocal;
    private Vector3 posicaoOriginalLocal;
    private Quaternion rotacaoOriginalLocal;

    private Vector3 escalaOriginalMundo;
    private bool escalaOriginalMundoCapturada;

    public bool JaEquipado => jaEquipado;
    public LadoMaoEquipamento LadoMao => ladoMao;
    public Transform TransformMaoConfigurada => transformMaoAvatar;
    public SkinnedMeshRenderer RendererMao => rendererMaoAvatar;


    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();

        if (cameraJogador == null)
            cameraJogador = Camera.main;

        if (interactable == null)
        {
            Debug.LogWarning(
                "[EquiparManoplas] Nenhum XRBaseInteractable encontrado em: " +
                gameObject.name,
                this
            );
        }

        if (popupInfo != null)
            popupInfo.SetActive(false);

        if (eixoEquipamento == null)
            eixoEquipamento = EncontrarEixoEquipamento();

        SalvarTransformOriginal();
        escalaOriginalMundo = transform.lossyScale;
        escalaOriginalMundoCapturada = true;
    }

    private void Start()
    {
        /*
         * Captura a escala real depois dos Awake dos outros componentes.
         * Não altera o tamanho visual original da manopla.
         */

        if (!EstaArmazenadoNoInventario() && !jaEquipado)
            AplicarEscalaMundoComoLocal(escalaOriginalMundo);
    }

    private bool EstaArmazenadoNoInventario()
    {
        var persistente = GetComponent<ItemPersistente>();
        return EstadoItemInventario.EstaNoInventario(this) ||
            (persistente != null && persistente.EstaNoInventario()) || GetComponentInParent<SlotInventario>(true) != null;
    }

    private void LateUpdate()
    {
        /*
         * Mantém a escala original enquanto a manopla não estiver equipada.
         */
        if (!jaEquipado && !EstaArmazenadoNoInventario() &&
            escalaOriginalMundoCapturada &&
            isActiveAndEnabled &&
            gameObject.activeInHierarchy)
        {
            AplicarEscalaMundoComoLocal(escalaOriginalMundo);
        }

        if (manterPopupNaFrenteDaCamera &&
            popupInfo != null &&
            popupInfo.activeInHierarchy)
        {
            PosicionarPopupNaCamera();
        }
    }

    private void OnEnable()
    {
        if (interactable == null)
            return;

        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
    }

    private void OnDisable()
    {
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

    private void SalvarTransformOriginal()
    {
        paiOriginal = transform.parent;
        escalaOriginalLocal = transform.localScale;
        posicaoOriginalLocal = transform.localPosition;
        rotacaoOriginalLocal = transform.localRotation;
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        GetComponent<AtributosManoplas>()?.GarantirGeracao();
        CancelarFechamentoPendente();

        if (popupInfo != null)
        {
            popupInfo.SetActive(true);
            RegistrarPopupVisivel();
            PosicionarPopupNaCamera();
        }
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

    private void OnHoverExited(HoverExitEventArgs args)
    {
        CancelarFechamentoPendente();

        /*
         * Ao guardar a manopla no inventário, o GameObject pode ser
         * desativado antes de o XRI terminar de disparar o hoverExited.
         * Nesse caso, não se pode iniciar uma coroutine.
         */
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        corotinaFecharPopup = StartCoroutine(FecharPopupAposEspera());
    }

    private IEnumerator FecharPopupAposEspera()
    {
        yield return new WaitForSeconds(tempoParaFecharPopup);

        /*
         * O item pode ter sido guardado no inventário durante a espera.
         */
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            corotinaFecharPopup = null;
            yield break;
        }

        if (popupInfo != null)
        {
            popupInfo.SetActive(false);
            RemoverPopupVisivel();
        }

        corotinaFecharPopup = null;
    }

    private void CancelarFechamentoPendente()
    {
        if (corotinaFecharPopup == null)
            return;

        StopCoroutine(corotinaFecharPopup);
        corotinaFecharPopup = null;
    }

    private void RegistrarPopupVisivel()
    {
        PosicionarPopupNaCamera();
    }

    private void RemoverPopupVisivel()
    {
        LayoutPopupsEquipamentos.Remover(popupInfo);
    }


    public void Equipar()
    {
        if (jaEquipado)
            return;

        GerenciadorEquipamentosPlayer gerenciador =
            GerenciadorEquipamentosPlayer.Instancia ?? FindFirstObjectByType<GerenciadorEquipamentosPlayer>();

        if (gerenciador == null)
        {
            Debug.LogWarning("[EquiparManoplas] GerenciadorEquipamentosPlayer não encontrado.", this);
            return;
        }

        gerenciador.TentarEquipar(this);
    }

    public bool PodeEquiparNaMao(Transform mao)
    {
        if (mao == null)
        {
            Debug.LogWarning("[EquiparManoplas] A mão de destino não foi configurada.", this);
            return false;
        }

        return true;
    }

    internal bool EquiparNaMao(Transform mao, SkinnedMeshRenderer rendererMao = null)
    {
        if (jaEquipado || !PodeEquiparNaMao(mao))
            return jaEquipado;

        transformMaoAvatar = mao;
        if (rendererMao != null)
            rendererMaoAvatar = rendererMao;
        GetComponent<AtributosManoplas>()?.GarantirGeracao();
        CapturarEstadosAntesDeEquipar();
        jaEquipado = true;

        EncaixarPrefabCompletoNaMao();

        DesativarInteracaoDoItem();
        DesativarFisicaDoItem();
        EsconderMaoSeConfigurado();

        AplicarAtributos();
        FinalizarEquipamento();
        return true;
    }

    private bool ValidarReferencias()
    {
        if (transformMaoAvatar == null)
        {
            Debug.LogWarning(
                "[EquiparManoplas] O campo 'Transform Mao Avatar' não foi atribuído em: " +
                gameObject.name,
                this
            );

            return false;
        }

        if (eixoEquipamento == null)
            eixoEquipamento = EncontrarEixoEquipamento();

        return true;
    }

    private void EncaixarPrefabCompletoNaMao()
    {
        if (transformMaoAvatar == null)
            return;

        gameObject.SetActive(true);

        /*
         * Mantém a escala de mundo durante a troca de pai.
         */
        transform.SetParent(transformMaoAvatar, true);

        /*
         * Não utiliza escalaEquipado nem Vector3.one.
         * A escala original do prefab é preservada.
         */
        if (escalaOriginalMundoCapturada)
        {
            AplicarEscalaMundoComoLocal(escalaOriginalMundo);
        }
        else
        {
            transform.localScale = escalaOriginalLocal;
        }

        AtivarHierarquiaCompleta(transform);

        if (eixoEquipamento != null)
        {
            AlinharEixoEquipamentoComAMao();
        }
        else
        {
            /*
             * Fallback para os prefabs antigos que ainda não possuem eixo.
             */
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
    }

    private Transform EncontrarEixoEquipamento()
    {
        Transform[] filhos = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < filhos.Length; i++)
        {
            Transform filho = filhos[i];
            if (filho == null || filho == transform)
                continue;

            if (filho.name == "EixoEquipamento" || filho.name == "PoseEquipamento")
                return filho;
        }

        return null;
    }

    private void AlinharEixoEquipamentoComAMao()
    {
        if (transformMaoAvatar == null || eixoEquipamento == null)
            return;

        /*
         * Descobre a rotação do eixo em relação à raiz antes de girar a raiz.
         * Isso funciona mesmo se o eixo estiver dentro de outro objeto filho.
         */
        Quaternion rotacaoEixoRelativa =
            Quaternion.Inverse(transform.rotation) * eixoEquipamento.rotation;

        /*
         * Faz o eixo ter exatamente a rotação da mão.
         */
        transform.rotation =
            transformMaoAvatar.rotation * Quaternion.Inverse(rotacaoEixoRelativa);

        /*
         * Depois da rotação, corrige a posição para que o eixo coincida
         * exatamente com o ponto da mão.
         */
        Vector3 deslocamento = transformMaoAvatar.position - eixoEquipamento.position;
        transform.position += deslocamento;
    }

    private void AtivarHierarquiaCompleta(Transform raiz)
    {
        if (raiz == null)
            return;

        raiz.gameObject.SetActive(true);

        for (int i = 0; i < raiz.childCount; i++)
        {
            AtivarHierarquiaCompleta(raiz.GetChild(i));
        }
    }

    private void DesativarInteracaoDoItem()
    {
        XRBaseInteractable[] interactables =
            GetComponentsInChildren<XRBaseInteractable>(true);

        for (int i = 0; i < interactables.Length; i++)
        {
            if (interactables[i] != null)
                interactables[i].enabled = false;
        }

        if (popupInfo != null)
        {
            popupInfo.SetActive(false);
            RemoverPopupVisivel();
        }
    }

    private void DesativarFisicaDoItem()
    {
        Rigidbody[] rigidbodies =
            GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];

            if (rb == null)
                continue;

            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        Collider[] colliders =
            GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    private void EsconderMaoSeConfigurado()
    {
        if (!esconderMaoAoEquipar)
            return;

        if (rendererMaoAvatar != null)
            rendererMaoAvatar.enabled = false;
    }

    private void AplicarAtributos()
    {
        // Reservado para futuros bônus e efeitos da manopla.
    }

    private void FinalizarEquipamento()
    {
        if (popupInfo != null)
            popupInfo.SetActive(false);

        gameObject.SetActive(true);
    }

    internal System.Action CapturarEstadoEquipadoParaCarga()
    {
        bool equipado = jaEquipado;
        var restauracao = restaurarEstadoEquipamento;
        var visibilidade = restaurarVisibilidadeMao;
        return () => { jaEquipado = equipado; restaurarEstadoEquipamento = restauracao; restaurarVisibilidadeMao = visibilidade; };
    }

    public void Desequipar()
    {
        if (!jaEquipado)
            return;

        restaurarEstadoEquipamento?.Invoke();
        restaurarEstadoEquipamento = null;

        transform.SetParent(paiOriginal, false);
        transform.localPosition = posicaoOriginalLocal;
        transform.localRotation = rotacaoOriginalLocal;

        if (escalaOriginalMundoCapturada)
            AplicarEscalaMundoComoLocal(escalaOriginalMundo);
        else
            transform.localScale = escalaOriginalLocal;

        jaEquipado = false;
    }

    private void CapturarEstadosAntesDeEquipar()
    {
        var estados = new List<System.Action>();
        foreach (var componente in GetComponentsInChildren<XRBaseInteractable>(true))
        {
            var alvo = componente; bool ativo = alvo.enabled;
            estados.Add(() => { if (alvo != null) alvo.enabled = ativo; });
        }
        foreach (var componente in GetComponentsInChildren<Collider>(true))
        {
            var alvo = componente; bool ativo = alvo.enabled;
            estados.Add(() => { if (alvo != null) alvo.enabled = ativo; });
        }
        foreach (var componente in GetComponentsInChildren<Rigidbody>(true))
        {
            var alvo = componente; bool cinematico = alvo.isKinematic, colisoes = alvo.detectCollisions;
            estados.Add(() => { if (alvo != null) { alvo.isKinematic = cinematico; alvo.detectCollisions = colisoes; } });
        }
        var mao = rendererMaoAvatar;
        if (mao != null)
        {
            bool visivel = mao.enabled;
            restaurarVisibilidadeMao = () => { if (mao != null) mao.enabled = visivel; };
            estados.Add(restaurarVisibilidadeMao);
        }
        restaurarEstadoEquipamento = () => { foreach (var restaurar in estados) restaurar(); };
    }

    internal void PrepararQuebraPorDurabilidade()
    {
        if (!jaEquipado) return;
        jaEquipado = false;
        restaurarVisibilidadeMao?.Invoke();
        restaurarVisibilidadeMao = null;
        restaurarEstadoEquipamento = null;
        var gerenciador = GetComponentInParent<GerenciadorEquipamentosPlayer>();
        if (gerenciador == null) gerenciador = GerenciadorEquipamentosPlayer.Instancia;
        gerenciador?.NotificarManoplaDestruida(this);
        LayoutPopupsEquipamentos.Remover(popupInfo);
    }

    private void AplicarEscalaMundoComoLocal(Vector3 escalaMundo)
    {
        Vector3 escalaDoPai = transform.parent != null
            ? transform.parent.lossyScale
            : Vector3.one;

        Vector3 novaEscalaLocal = new Vector3(
            DividirSeguro(escalaMundo.x, escalaDoPai.x),
            DividirSeguro(escalaMundo.y, escalaDoPai.y),
            DividirSeguro(escalaMundo.z, escalaDoPai.z)
        );

        if (novaEscalaLocal != transform.localScale)
            transform.localScale = novaEscalaLocal;
    }

    private static float DividirSeguro(float valor, float divisor)
    {
        if (
            Mathf.Approximately(divisor, 0f) ||
            float.IsNaN(divisor) ||
            float.IsInfinity(divisor)
        )
        {
            return valor;
        }

        float resultado = valor / divisor;

        if (float.IsNaN(resultado) || float.IsInfinity(resultado))
            return valor;

        return resultado;
    }
}

// Compartilhado por todos os tipos de equipamento; nao depende de componente na cena.
internal static class LayoutPopupsEquipamentos
{
    private sealed class Entrada
    {
        public GameObject popup;
        public Camera camera;
        public Vector3 posicao;
        public Vector3 rotacao;
        public float espacamento;
        public float meiaLargura;
        public float centroVisual;
        public float centroNaLinha;
    }

    private static readonly List<Entrada> entradas = new List<Entrada>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpar() { entradas.Clear(); }

    public static void Atualizar(GameObject popup, Camera camera, Vector3 posicao,
        Vector3 rotacao, float espacamento)
    {
        entradas.RemoveAll(e => e.popup == null || e.camera == null || !e.popup.activeInHierarchy);
        if (popup == null || !popup.activeInHierarchy || camera == null)
            return;
        Entrada entrada = entradas.Find(e => e.popup == popup);
        if (entrada == null)
        {
            entrada = new Entrada { popup = popup };
            entradas.Add(entrada);
        }
        entrada.camera = camera;
        entrada.posicao = posicao;
        entrada.rotacao = rotacao;
        entrada.espacamento = Mathf.Max(0f, espacamento);
        Reorganizar();
    }

    public static void Remover(GameObject popup)
    {
        entradas.RemoveAll(e => e.popup == null || e.popup == popup ||
            e.camera == null || !e.popup.activeInHierarchy);
        Reorganizar();
    }

    private static readonly Vector3[] cantosPopup = new Vector3[4];
    private const float FolgaEntreBordas = 0.03f;

    private static void MedirLargura(Entrada entrada)
    {
        float minimo = float.PositiveInfinity;
        float maximo = float.NegativeInfinity;
        Transform painel = entrada.popup.transform;
        Vector3 direita = entrada.camera.transform.right;
        // Mede somente UI visivel, incluindo o botao. Nao usa o tamanho do equipamento.
        foreach (UnityEngine.UI.Graphic visual in entrada.popup.GetComponentsInChildren<UnityEngine.UI.Graphic>(false))
        {
            if (!visual.isActiveAndEnabled) continue;
            visual.rectTransform.GetWorldCorners(cantosPopup);
            foreach (Vector3 canto in cantosPopup)
            {
                float x = Vector3.Dot(canto - painel.position, direita);
                minimo = Mathf.Min(minimo, x);
                maximo = Mathf.Max(maximo, x);
            }
        }
        bool valido = !float.IsInfinity(minimo) && !float.IsInfinity(maximo) &&
            !float.IsNaN(minimo) && !float.IsNaN(maximo);
        entrada.meiaLargura = valido ? (maximo - minimo) * 0.5f : 0f;
        entrada.centroVisual = valido ? (maximo + minimo) * 0.5f : 0f;
    }

    private static void Reorganizar()
    {
        // Primeiro aplica a orientacao final para medir a largura projetada na camera,
        // mesmo com escalas diferentes ou pivots fora do centro.
        foreach (Entrada entrada in entradas)
        {
            entrada.popup.transform.rotation = entrada.camera.transform.rotation * Quaternion.Euler(entrada.rotacao);
            MedirLargura(entrada);
        }
        foreach (Entrada primeira in entradas)
        {
            if (entradas.Find(e => e.camera == primeira.camera) != primeira) continue;
            Entrada anterior = null;
            float distanciaMinima = 0f;
            foreach (Entrada entrada in entradas)
                if (entrada.camera == primeira.camera)
                    distanciaMinima = Mathf.Max(distanciaMinima, entrada.espacamento);

            foreach (Entrada entrada in entradas)
            {
                if (entrada.camera != primeira.camera) continue;
                // Preserva a distancia configurada como minimo entre centros e aumenta
                // automaticamente quando as larguras precisam de mais espaco.
                entrada.centroNaLinha = anterior == null ? 0f : anterior.centroNaLinha +
                    Mathf.Max(distanciaMinima, anterior.meiaLargura + entrada.meiaLargura + FolgaEntreBordas);
                anterior = entrada;
            }
            float centroConjunto = (-primeira.meiaLargura + anterior.centroNaLinha + anterior.meiaLargura) * 0.5f;
            Transform referencia = primeira.camera.transform;
            Vector3 origem = referencia.TransformPoint(primeira.posicao);
            foreach (Entrada entrada in entradas)
            {
                if (entrada.camera != primeira.camera) continue;
                entrada.popup.transform.position = origem + referencia.right *
                    (entrada.centroNaLinha - centroConjunto - entrada.centroVisual);
            }
        }
    }
}
