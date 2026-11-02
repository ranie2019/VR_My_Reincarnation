using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class EquiparPeitoral : MonoBehaviour
{

    [Header("Equipamento")]
    [SerializeField] private Transform corpoAvatar;
    [Tooltip("Ponto do peitoral que será alinhado com a referencia do corpo.")]
    [SerializeField] private Transform pontoEquipamento;
    [SerializeField] private Vector3 posicaoLocalNoCorpo;
    [SerializeField] private Vector3 rotacaoLocalNoCorpo = new Vector3(0f, 180f, 0f);

    [Header("Visibilidade do avatar")]
    [Tooltip("Esconde somente os renderers configurados enquanto o peitoral estiver equipado. Nao desativa o corpo nem seus ossos.")]
    [SerializeField] private bool esconderCorpoAoEquipar;
    [Tooltip("Renderers da parte do avatar coberta pela armadura. Se vazio, tenta o Renderer diretamente no Corpo Avatar.")]
    [SerializeField] private Renderer[] renderersCorpoAvatar;
    private readonly Dictionary<Renderer, bool> visibilidadeOriginalCorpo = new Dictionary<Renderer, bool>();

    [Header("Popup")]
    [SerializeField] private GameObject popupInfo;
    [SerializeField] private float tempoParaFecharPopup = 10f;
    [SerializeField] private Camera cameraJogador;
    [SerializeField] private Vector3 posicaoPopupNaCamera = new Vector3(0f, 0f, 1.5f);
    [SerializeField] private Vector3 rotacaoPopupNaCamera;
    [SerializeField] private bool manterPopupNaFrenteDaCamera = true;
    [SerializeField, Min(0f)] private float espacamentoHorizontalPopups = 0.45f;

    private XRBaseInteractable interactable;
    private Coroutine corotinaFecharPopup;
    private bool jaEquipado;
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

    public bool JaEquipado => jaEquipado;
    public Transform CorpoConfigurado => corpoAvatar;
    public bool EsconderCorpoAoEquipar => esconderCorpoAoEquipar;


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
        if (jaEquipado) AtualizarVisibilidadeCorpo();
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        if (jaEquipado && !gameObject.activeInHierarchy) RestaurarVisibilidadeCorpo();
        CancelarFechamentoPendente();
        RemoverPopupVisivel();
        if (popupInfo != null)
            popupInfo.SetActive(false);
    }

    private void OnDestroy()
    {
        RestaurarVisibilidadeCorpo();
        RemoverPopupVisivel();
    }

    private void AtualizarVisibilidadeCorpo()
    {
        if (!jaEquipado || !esconderCorpoAoEquipar)
        { RestaurarVisibilidadeCorpo(); return; }
        if ((renderersCorpoAvatar == null || renderersCorpoAvatar.Length == 0) && corpoAvatar != null)
        {
            Renderer rendererDireto = corpoAvatar.GetComponent<Renderer>();
            if (rendererDireto != null) renderersCorpoAvatar = new[] { rendererDireto };
        }
        if (renderersCorpoAvatar == null) return;
        foreach (Renderer parte in renderersCorpoAvatar)
        {
            // Nunca oculta a propria armadura, mesmo se configurada por engano.
            if (parte == null || parte.transform == transform || parte.transform.IsChildOf(transform)) continue;
            if (!visibilidadeOriginalCorpo.ContainsKey(parte)) visibilidadeOriginalCorpo.Add(parte, parte.enabled);
            parte.enabled = false;
        }
    }

    private void RestaurarVisibilidadeCorpo()
    {
        foreach (var parte in visibilidadeOriginalCorpo)
            if (parte.Key != null) parte.Key.enabled = parte.Value;
        visibilidadeOriginalCorpo.Clear();
    }

    private bool EstaArmazenadoNoInventario()
    {
        var persistente = GetComponent<ItemPersistente>();
        return EstadoItemInventario.EstaNoInventario(this) ||
            (persistente != null && persistente.EstaNoInventario()) || GetComponentInParent<SlotInventario>(true) != null;
    }

    private void LateUpdate()
    {
        if (jaEquipado) AtualizarVisibilidadeCorpo();
        if (!jaEquipado && !EstaArmazenadoNoInventario() && escalaOriginalMundoCapturada && isActiveAndEnabled && gameObject.activeInHierarchy)
            AplicarEscalaMundoComoLocal(escalaOriginalMundo);

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
            Debug.LogWarning("[EquiparPeitoral] GerenciadorEquipamentosPlayer não encontrado.", this);
            return;
        }

        gerenciador.TentarEquipar(this);
    }

    public bool PodeEquiparNoCorpo(Transform corpo)
    {
        if (corpo != null && corpo != transform && !corpo.IsChildOf(transform))
            return true;

        Debug.LogWarning("[EquiparPeitoral] O corpo do avatar não foi configurado, encontrado ou possui uma hierarquia invalida.", this);
        return false;
    }

    internal bool EquiparNoCorpo(Transform corpo)
    {
        if (jaEquipado)
            return true;
        if (!PodeEquiparNoCorpo(corpo))
            return false;

        corpoAvatar = corpo;
        gameObject.SetActive(true);
        ResolverPontoEquipamento();
        CapturarComponentesOriginais();
        GetComponent<AtributosPeitoral>()?.GarantirGeracao();
        jaEquipado = true;
        // Desativa o grab antes de alinhar: callbacks de soltura nao podem desfazer o encaixe.
        DesativarInteracaoEFisica();
        AlinharPontoEquipamentoComCorpo(corpoAvatar);
        AtualizarVisibilidadeCorpo();
        if (popupInfo != null)
            popupInfo.SetActive(false);
        RemoverPopupVisivel();
        return true;
    }

    internal void PrepararQuebraPorDurabilidade()
    {
        if (!jaEquipado) return;
        jaEquipado = false;
        RestaurarVisibilidadeCorpo();
        var gerenciador = GetComponentInParent<GerenciadorEquipamentosPlayer>();
        if (gerenciador == null) gerenciador = GerenciadorEquipamentosPlayer.Instancia;
        gerenciador?.NotificarPeitoralDestruido(this);
        RemoverPopupVisivel();
    }

    internal System.Action CapturarEstadoEquipadoParaCarga()
    {
        bool equipado = jaEquipado;
        var visibilidade = new Dictionary<Renderer, bool>(visibilidadeOriginalCorpo);
        return () =>
        {
            jaEquipado = equipado;
            visibilidadeOriginalCorpo.Clear();
            foreach (var parte in visibilidade) visibilidadeOriginalCorpo.Add(parte.Key, parte.Value);
            if (equipado) AtualizarVisibilidadeCorpo();
        };
    }

    public void Desequipar()
    {
        if (!jaEquipado)
            return;

        RestaurarVisibilidadeCorpo();
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
        // Mantem a referencia de configuracao do corpo para a proxima utilizacao.
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

            string nome = filho.name.Trim();
            if (nome == "PosicaoEquipe" || nome == "PosicaoEquipamento" ||
                nome == "PontoEquipamento" || nome == "EixoEquipamento")
            {
                pontoEquipamento = filho;
                return;
            }
        }
    }

    private void AlinharPontoEquipamentoComCorpo(Transform corpo)
    {
        if (pontoEquipamento == null)
        {
            transform.SetParent(corpo, false);
            transform.localPosition = posicaoLocalNoCorpo;
            transform.localRotation = Quaternion.Euler(rotacaoLocalNoCorpo);
            if (escalaOriginalMundoCapturada)
                AplicarEscalaMundoComoLocal(escalaOriginalMundo);
            else
                transform.localScale = escalaOriginalLocal;
            return;
        }

        Quaternion rotacaoPontoRelativa = Quaternion.Inverse(transform.rotation) * pontoEquipamento.rotation;
        Vector3 escalaMundo = escalaOriginalMundoCapturada ? escalaOriginalMundo : transform.lossyScale;

        transform.SetParent(corpo, true);
        transform.rotation = corpo.rotation * Quaternion.Inverse(rotacaoPontoRelativa);
        AplicarEscalaMundoComoLocal(escalaMundo);
        transform.position += corpo.position - pontoEquipamento.position;
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
        // Altera somente a interacao e fisica da armadura. A visibilidade do
        // avatar e controlada separadamente pela opcao esconderCorpoAoEquipar.
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

            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
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

        GetComponent<AtributosPeitoral>()?.GarantirGeracao();
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
