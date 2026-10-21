using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class BotaoEquipar : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Se vazio, tenta encontrar automaticamente subindo pelos pais.")]
    [SerializeField] private EquiparManoplas equiparManoplas;

    [Header("Confirmação")]
    [SerializeField] private InputActionReference acaoConfirmarMaoEsquerda;
    [SerializeField] private InputActionReference acaoConfirmarMaoDireita;

    private XRSimpleInteractable interactable;
    private bool botaoEmHover;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        if (equiparManoplas == null)
            equiparManoplas = GetComponentInParent<EquiparManoplas>(true);
    }

    private void OnEnable()
    {
        if (interactable == null)
            return;

        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);

        HabilitarAcao(acaoConfirmarMaoEsquerda);
        HabilitarAcao(acaoConfirmarMaoDireita);
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        DesabilitarAcao(acaoConfirmarMaoEsquerda);
        DesabilitarAcao(acaoConfirmarMaoDireita);

        botaoEmHover = false;
    }

    private void Update()
    {
        VerificarConfirmacao();
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        botaoEmHover = true;
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        botaoEmHover = false;
    }

    private void VerificarConfirmacao()
    {
        if (!botaoEmHover)
            return;

        if (!BotaoConfirmarPressionado())
            return;

        if (equiparManoplas != null)
            equiparManoplas.Equipar();
    }

    private bool BotaoConfirmarPressionado()
    {
        return AcaoFoiPressionada(acaoConfirmarMaoEsquerda) ||
               AcaoFoiPressionada(acaoConfirmarMaoDireita);
    }

    private static bool AcaoFoiPressionada(InputActionReference acaoRef)
    {
        if (acaoRef == null || acaoRef.action == null)
            return false;

        try
        {
            return acaoRef.action.WasPressedThisFrame();
        }
        catch
        {
            return false;
        }
    }

    private static void HabilitarAcao(InputActionReference acaoRef)
    {
        if (acaoRef != null && acaoRef.action != null)
            acaoRef.action.Enable();
    }

    private static void DesabilitarAcao(InputActionReference acaoRef)
    {
        if (acaoRef != null && acaoRef.action != null)
            acaoRef.action.Disable();
    }
}
