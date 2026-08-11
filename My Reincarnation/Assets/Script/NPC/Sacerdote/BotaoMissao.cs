using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Botao generico de missao: hover do laser + confirmacao com botao do
/// controle (mao esquerda ou direita). Ao confirmar, dispara um UnityEvent.
///
/// Reusavel para Aceitar, Recusar, e qualquer outro botao futuro do sistema
/// de missao (ex: confirmar entrega). Configure o evento "Ao Confirmar" no
/// Inspector apontando pro metodo certo do GerenciadorMissoes
/// (AceitarMissaoAtual / RecusarMissaoAtual / EntregarRecompensaEAvancar).
///
/// Requisito: o objeto precisa ter um Collider (BoxCollider, Is Trigger) do tamanho do botao.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class BotaoMissao : MonoBehaviour
{
    [Header("Confirmação (Hover + Botão do Controle)")]
    [SerializeField] private InputActionReference acaoConfirmarMaoEsquerda;
    [SerializeField] private InputActionReference acaoConfirmarMaoDireita;

    [Header("Ação")]
    [Tooltip("Chamado quando o jogador confirma este botão (hover + botão do controle).")]
    [SerializeField] private UnityEvent aoConfirmar;

    private XRSimpleInteractable interactable;
    private bool botaoEmHover;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
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

        aoConfirmar?.Invoke();
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
