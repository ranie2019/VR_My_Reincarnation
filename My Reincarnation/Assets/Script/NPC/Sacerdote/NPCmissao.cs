using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Fica no NPC de missao. Avisa o GerenciadorMissoes quando o laser entra.
/// O laser saindo NÃO fecha mais o popup (fecha só pelo botão ou pelo timer de 30s).
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class NPCMissao : MonoBehaviour
{
    [Tooltip("Se vazio, tenta encontrar automaticamente na cena.")]
    [SerializeField] private GerenciadorMissoes gerenciador;

    private XRSimpleInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        if (gerenciador == null)
            gerenciador = FindFirstObjectByType<GerenciadorMissoes>();
    }

    private void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        // hoverExited removido de propósito: sair com o laser não fecha o popup
    }

    private void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (gerenciador != null)
            gerenciador.NotificarHoverNPC();
    }
}