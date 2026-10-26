using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class EquiparElmo : MonoBehaviour
{
    private static readonly List<EquiparElmo> popupsVisiveis = new List<EquiparElmo>();

    [Header("Equipamento")]
    [SerializeField] private Transform cabecaAvatar;
    [Tooltip("Ponto do elmo que será alinhado com o osso da cabeça.")]
    [SerializeField] private Transform pontoEquipamento;
    [SerializeField] private Vector3 posicaoLocalNaCabeca;
    [SerializeField] private Vector3 rotacaoLocalNaCabeca = new Vector3(0f, 180f, 0f);

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
    public Transform CabecaConfigurada => cabecaAvatar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparPopupsVisiveis()
    {
        popupsVisiveis.Clear();
    }

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
        CapturarComponentesOriginais();
    }

    private void Start()
    {
        escalaOriginalMundo = transform.lossyScale;
        escalaOriginalMundoCapturada = true;
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

    private void LateUpdate()
    {
        if (!jaEquipado && escalaOriginalMundoCapturada && isActiveAndEnabled && gameObject.activeInHierarchy)
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
        jaEquipado = true;
        gameObject.SetActive(true);
        AlinharPontoEquipamentoComCabeca(cabecaAvatar);

        DesativarInteracaoEFisica();
        if (popupInfo != null)
            popupInfo.SetActive(false);
        RemoverPopupVisivel();
        return true;
    }

    public void Desequipar()
    {
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

        popupInfo.SetActive(true);
        if (!popupsVisiveis.Contains(this))
            popupsVisiveis.Add(this);
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

        RemoverPopupsNulos();
        int indice = popupsVisiveis.IndexOf(this);
        int quantidade = popupsVisiveis.Count;
        float deslocamento = indice < 0 ? 0f :
            (indice - (quantidade - 1) * 0.5f) * espacamentoHorizontalPopups;
        Transform cameraTransform = cameraJogador.transform;
        popupInfo.transform.position = cameraTransform.TransformPoint(posicaoPopupNaCamera) +
                                       cameraTransform.right * deslocamento;
        popupInfo.transform.rotation = cameraTransform.rotation * Quaternion.Euler(rotacaoPopupNaCamera);
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
        popupsVisiveis.Remove(this);
    }

    private static void RemoverPopupsNulos()
    {
        for (int i = popupsVisiveis.Count - 1; i >= 0; i--)
        {
            EquiparElmo popup = popupsVisiveis[i];
            if (popup == null || popup.popupInfo == null || !popup.popupInfo.activeInHierarchy)
                popupsVisiveis.RemoveAt(i);
        }
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
