using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class StatusVR : MonoBehaviour
{
    private const string CaminhoBotaoAnalogicoDireito = "<XRController>{RightHand}/{Primary2DAxisClick}";

    [Header("Entrada")]
    [SerializeField] private InputActionReference botaoStatus;

    [Header("Painel")]
    [SerializeField] private GameObject painelStatus;

    [Header("Posição ao abrir")]
    [Tooltip("Câmera do headset. Se vazio, usa a câmera MainCamera.")]
    [SerializeField] private Transform cameraJogador;
    [SerializeField, Min(0.25f)] private float distanciaMenuNaCamera = 1.2f;
    [Tooltip("Ajuste vertical adicional somado à altura original do Canvas Menu em relação à câmera.")]
    [SerializeField] private float alturaMenuNaCamera = 0f;

    [Header("Configuração")]
    [SerializeField] private bool iniciarFechado = true;
    [SerializeField] private float tempoMinimoEntreCliques = 0.25f;

    private InputAction acaoStatus;
    private InputAction acaoFallback;
    private bool acaoAtivadaPorEsteScript;
    private float proximoCliquePermitido;
    private bool avisouPainelNaoConfigurado;
    private bool avisouBotaoNaoConfigurado;
    private bool botaoPressionadoPorCallback;
    private bool alturaOriginalMenuCapturada;
    private float alturaOriginalMenuRelativaCamera;

    private void Awake()
    {
        if (painelStatus == null)
        {
            AvisarPainelNaoConfigurado();
            return;
        }

        ValidarLocalDoScript();
        ResolverCameraJogador();
        CapturarAlturaOriginalMenuSeNecessario();

        if (iniciarFechado)
            FecharStatus();
        else
            AbrirStatus();
    }

    private void OnEnable()
    {
        ConfigurarAction();
    }

    private void OnDisable()
    {
        if (acaoStatus != null)
            acaoStatus.performed -= AoBotaoStatusPressionado;

        if (acaoStatus != null && acaoAtivadaPorEsteScript)
            acaoStatus.Disable();

        if (acaoFallback != null)
        {
            acaoFallback.performed -= AoBotaoStatusPressionado;
            acaoFallback.Disable();
            acaoFallback.Dispose();
            acaoFallback = null;
        }

        acaoStatus = null;
        acaoAtivadaPorEsteScript = false;
        botaoPressionadoPorCallback = false;
    }

    private void Update()
    {
        InputAction action = ObterActionStatus();
        if (action == null)
            return;

        if (!action.enabled)
            HabilitarAction(action);

        if (!botaoPressionadoPorCallback && !action.WasPressedThisFrame())
            return;

        botaoPressionadoPorCallback = false;
        AlternarStatus();
    }

    private void LateUpdate()
    {
        if (painelStatus == null || !painelStatus.activeInHierarchy || cameraJogador == null || !alturaOriginalMenuCapturada)
            return;

        // Mantem X/Z e a rotacao atuais do menu; acompanha apenas a altura do headset.
        Vector3 posicao = painelStatus.transform.position;
        posicao.y = cameraJogador.position.y + alturaOriginalMenuRelativaCamera + alturaMenuNaCamera;
        painelStatus.transform.position = posicao;
    }

    private void OnValidate()
    {
        tempoMinimoEntreCliques = Mathf.Max(0f, tempoMinimoEntreCliques);
    }

    public void AlternarStatus()
    {
        if (Time.time < proximoCliquePermitido)
            return;

        proximoCliquePermitido = Time.time + tempoMinimoEntreCliques;

        if (painelStatus == null)
        {
            AvisarPainelNaoConfigurado();
            return;
        }

        if (painelStatus.activeSelf)
            FecharStatus();
        else
            AbrirStatus();
    }

    public void AbrirStatus()
    {
        if (painelStatus == null)
        {
            AvisarPainelNaoConfigurado();
            return;
        }

        PosicionarPainelNaFrenteDaCamera();
        painelStatus.SetActive(true);
        AgendarVerificacaoDeEstado(true);
    }

    private void PosicionarPainelNaFrenteDaCamera()
    {
        if (painelStatus == null)
            return;

        ResolverCameraJogador();

        if (cameraJogador == null)
        {
            LogWarning("StatusVR: não encontrei a câmera do jogador para posicionar o menu.");
            return;
        }

        CapturarAlturaOriginalMenuSeNecessario();

        Vector3 frenteHorizontal = Vector3.ProjectOnPlane(cameraJogador.forward, Vector3.up);
        if (frenteHorizontal.sqrMagnitude < 0.0001f)
            frenteHorizontal = Vector3.ProjectOnPlane(cameraJogador.up, Vector3.up);

        if (frenteHorizontal.sqrMagnitude < 0.0001f)
            frenteHorizontal = transform.forward;

        frenteHorizontal.Normalize();

        Vector3 posicao = cameraJogador.position +
                          frenteHorizontal * distanciaMenuNaCamera +
                          Vector3.up * (alturaOriginalMenuRelativaCamera + alturaMenuNaCamera);
        Quaternion rotacao = Quaternion.LookRotation(frenteHorizontal, Vector3.up);

        painelStatus.transform.SetPositionAndRotation(posicao, rotacao);
    }

    private void CapturarAlturaOriginalMenuSeNecessario()
    {
        if (alturaOriginalMenuCapturada || painelStatus == null || cameraJogador == null)
            return;

        // Preserva a altura configurada no prefab/cena antes do primeiro posicionamento.
        // Depois disso, o menu acompanha as mudanças verticais da câmera sem perder esse offset.
        alturaOriginalMenuRelativaCamera = painelStatus.transform.position.y - cameraJogador.position.y;
        alturaOriginalMenuCapturada = true;
    }

    private void ResolverCameraJogador()
    {
        if (cameraJogador != null)
            return;

        Camera cameraPrincipal = Camera.main;
        if (cameraPrincipal != null)
            cameraJogador = cameraPrincipal.transform;
    }

    public void FecharStatus()
    {
        if (painelStatus == null)
        {
            AvisarPainelNaoConfigurado();
            return;
        }

        painelStatus.SetActive(false);
        AgendarVerificacaoDeEstado(false);
    }

    // ===================== DIAGNOSTICO: detecta scripts que reviram o SetActive por conta propria =====================

    private Coroutine rotinaVerificacaoDeEstado;

    private void AgendarVerificacaoDeEstado(bool estadoEsperado)
    {
        if (!isActiveAndEnabled)
            return;

        if (rotinaVerificacaoDeEstado != null)
            StopCoroutine(rotinaVerificacaoDeEstado);

        rotinaVerificacaoDeEstado = StartCoroutine(VerificarEstadoAposFrame(estadoEsperado));
    }

    private IEnumerator VerificarEstadoAposFrame(bool estadoEsperado)
    {
        // Espera o restante do frame passar, dando tempo para qualquer Awake/OnEnable/Start
        // de outros scripts no painel (ex.: um script de menu que se fecha sozinho) rodar.
        yield return null;

        if (painelStatus != null && painelStatus.activeSelf != estadoEsperado)
        {
            LogWarning(
                $"StatusVR: tentei deixar '{painelStatus.name}' com SetActive({estadoEsperado}), mas logo em seguida " +
                $"ele ficou com activeSelf = {painelStatus.activeSelf}. Algum outro script nesse painel (provavelmente " +
                "no Awake/OnEnable dele) esta alterando o proprio estado ativo por conta propria, entrando em conflito " +
                "com o StatusVR. Isso costuma acontecer quando o painel referenciado tem seu proprio script de " +
                "abrir/fechar — nesse caso, o StatusVR deveria chamar os metodos desse script em vez de usar SetActive direto.");
        }

        rotinaVerificacaoDeEstado = null;
    }

    private void ConfigurarAction()
    {
        InputAction action = ObterActionStatus();
        if (action == null)
            return;

        HabilitarAction(action);
    }

    private InputAction ObterActionStatus()
    {
        if (botaoStatus == null || botaoStatus.action == null)
        {
            AvisarBotaoNaoConfigurado();
            return ObterActionFallback();
        }

        if (acaoStatus != botaoStatus.action)
        {
            if (acaoStatus != null)
                acaoStatus.performed -= AoBotaoStatusPressionado;

            if (acaoStatus != null && acaoAtivadaPorEsteScript)
                acaoStatus.Disable();

            acaoStatus = botaoStatus.action;
            acaoAtivadaPorEsteScript = false;
            acaoStatus.performed += AoBotaoStatusPressionado;
        }

        return acaoStatus;
    }

    private void HabilitarAction(InputAction action)
    {
        if (action == null || action.enabled)
            return;

        action.Enable();
        acaoAtivadaPorEsteScript = true;
    }

    private InputAction ObterActionFallback()
    {
        if (acaoFallback != null)
            return acaoFallback;

        acaoFallback = new InputAction("Right Controller Thumbstick Click", InputActionType.Button, CaminhoBotaoAnalogicoDireito);
        acaoFallback.performed += AoBotaoStatusPressionado;
        acaoFallback.Enable();

        return acaoFallback;
    }

    private void AoBotaoStatusPressionado(InputAction.CallbackContext contexto)
    {
        botaoPressionadoPorCallback = true;
    }

    private void ValidarLocalDoScript()
    {
        if (painelStatus == null)
            return;

        if (gameObject == painelStatus)
        {
            LogWarning("StatusVR: o script não pode ficar no mesmo GameObject do painelStatus. Coloque o StatusVR no Right Controller.");
            return;
        }

        if (transform.IsChildOf(painelStatus.transform))
            LogWarning("StatusVR: o script está dentro do painel que será desativado. Coloque o StatusVR no Right Controller.");
    }

    private void AvisarPainelNaoConfigurado()
    {
        if (avisouPainelNaoConfigurado)
            return;

        avisouPainelNaoConfigurado = true;
        LogWarning("StatusVR: painelStatus não configurado");
    }

    private void AvisarBotaoNaoConfigurado()
    {
        if (avisouBotaoNaoConfigurado)
            return;

        avisouBotaoNaoConfigurado = true;
        LogWarning("StatusVR: botaoStatus não configurado");
    }

    private void LogWarning(string mensagem)
    {
        Debug.LogWarning(mensagem);
    }
}
