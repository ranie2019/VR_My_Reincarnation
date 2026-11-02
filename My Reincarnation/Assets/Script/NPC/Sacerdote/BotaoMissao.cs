using System.Collections.Generic;
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
        UsoAcaoCompartilhada.LiberarTodas(this);
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

    private void HabilitarAcao(InputActionReference acaoRef)
    {
        if (acaoRef != null && acaoRef.action != null)
            UsoAcaoCompartilhada.Adquirir(acaoRef.action, this);
    }

    private void DesabilitarAcao(InputActionReference acaoRef)
    {
        if (acaoRef != null && acaoRef.action != null)
            UsoAcaoCompartilhada.Liberar(acaoRef.action, this);
    }
}

// Compartilhado pelos botoes e interfaces VR; reside em um arquivo ja compilado pelo Unity.
internal static class UsoAcaoCompartilhada
{
    private sealed class Uso
    {
        internal readonly HashSet<object> donos = new HashSet<object>();
        internal bool habilitadaAqui;
    }
    private static readonly Dictionary<InputAction, Uso> usos = new Dictionary<InputAction, Uso>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpar()
    {
        foreach (var par in usos) if (par.Value.habilitadaAqui) par.Key.Disable();
        usos.Clear();
    }
    internal static void Adquirir(InputAction acao, object dono)
    {
        if (acao == null || dono == null) return;
        if (!usos.TryGetValue(acao, out var uso))
        { uso = new Uso { habilitadaAqui = !acao.enabled }; usos.Add(acao, uso); }
        uso.donos.Add(dono);
        if (!acao.enabled) acao.Enable();
    }
    internal static void Liberar(InputAction acao, object dono)
    {
        if (acao == null || !usos.TryGetValue(acao, out var uso) || !uso.donos.Remove(dono)) return;
        if (uso.donos.Count != 0) return;
        usos.Remove(acao);
        if (uso.habilitadaAqui) acao.Disable();
    }
    internal static void LiberarTodas(object dono)
    {
        foreach (var acao in new List<InputAction>(usos.Keys)) Liberar(acao, dono);
    }
}
