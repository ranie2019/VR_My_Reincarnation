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

    private void Awake()
    {
        if (painelStatus == null)
        {
            AvisarPainelNaoConfigurado();
            return;
        }

        ValidarLocalDoScript();

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

        painelStatus.SetActive(true);
    }

    public void FecharStatus()
    {
        if (painelStatus == null)
        {
            AvisarPainelNaoConfigurado();
            return;
        }

        painelStatus.SetActive(false);
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
        { }
    }
}
