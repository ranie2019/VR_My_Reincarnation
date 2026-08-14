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
    [Tooltip("Arraste o GerenciadorMissoes DESTE NPC (Ferreiro ou Sacerdote). NÃO deixe vazio.")]
    [SerializeField] private GerenciadorMissoes gerenciador;

    private XRSimpleInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        // Removido o FindFirstObjectByType para evitar pegar o gerenciador errado
        if (gerenciador == null)
            Debug.LogWarning($"{gameObject.name}: GerenciadorMissoes não foi atribuído no Inspector!");
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