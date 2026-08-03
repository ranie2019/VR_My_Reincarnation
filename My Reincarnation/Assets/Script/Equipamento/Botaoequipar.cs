using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Fica no botão "EQUIP" dentro do PopUp da luva.
///
/// Detecta hover do laser + confirmação com botão do controle (mão esquerda
/// ou direita), e quando confirmado chama EquiparManoplas.Equipar() no
/// objeto pai (a luva).
///
/// Requisito: o objeto precisa ter um Collider (BoxCollider, Is Trigger) do tamanho do botao.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class BotaoEquipar : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Se vazio, tenta encontrar automaticamente subindo pelos pais.")]
    [SerializeField] private EquiparManoplas equiparManoplas;

    [Header("Confirmação (Hover + Botão do Controle)")]
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
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);

        HabilitarAcao(acaoConfirmarMaoEsquerda);
        HabilitarAcao(acaoConfirmarMaoDireita);
    }

    private void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);

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