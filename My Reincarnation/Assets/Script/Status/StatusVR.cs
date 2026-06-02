using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class StatusVR : MonoBehaviour
{
    [Header("Entrada")]
    [SerializeField] private InputActionReference botaoStatus;

    [Header("Painel Status")]
    [SerializeField] private GameObject painelStatus;

    [Header("Configuração")]
    [SerializeField] private bool iniciarFechado = true;
    [SerializeField] private float tempoMinimoEntreCliques = 0.25f;

    private InputAction acaoStatus;
    private bool acaoAtivadaPorEsteScript;
    private float proximoCliquePermitido;

    private void OnEnable()
    {
        acaoStatus = botaoStatus != null ? botaoStatus.action : null;
        if (acaoStatus == null)
            return;

        acaoStatus.performed += AoBotaoStatusExecutado;

        if (!acaoStatus.enabled)
        {
            acaoStatus.Enable();
            acaoAtivadaPorEsteScript = true;
        }
    }

    private void OnDisable()
    {
        if (acaoStatus != null)
        {
            acaoStatus.performed -= AoBotaoStatusExecutado;

            if (acaoAtivadaPorEsteScript)
                acaoStatus.Disable();
        }

        acaoStatus = null;
        acaoAtivadaPorEsteScript = false;
    }

    private void Start()
    {
        if (painelStatus == null)
        {
            Debug.LogWarning("[StatusVR] Painel Status não configurado.", this);
            return;
        }

        if (iniciarFechado)
            FecharStatus();
    }

    private void OnValidate()
    {
        tempoMinimoEntreCliques = Mathf.Max(0f, tempoMinimoEntreCliques);
    }

    private void AoBotaoStatusExecutado(InputAction.CallbackContext contexto)
    {
        AlternarStatus();
    }

    public void AlternarStatus()
    {
        if (Time.time < proximoCliquePermitido)
            return;

        proximoCliquePermitido = Time.time + tempoMinimoEntreCliques;

        if (painelStatus == null)
        {
            Debug.LogWarning("[StatusVR] Painel Status não configurado.", this);
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
            Debug.LogWarning("[StatusVR] Painel Status não configurado.", this);
            return;
        }

        painelStatus.SetActive(true);
    }

    public void FecharStatus()
    {
        if (painelStatus == null)
        {
            Debug.LogWarning("[StatusVR] Painel Status não configurado.", this);
            return;
        }

        painelStatus.SetActive(false);
    }
}
