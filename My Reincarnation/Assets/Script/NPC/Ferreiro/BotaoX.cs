using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Script genérico para qualquer botão "X" de fechar.
/// Usa XRSimpleInteractable (hover do laser) para desativar
/// o Canvas mais próximo na hierarquia acima dele (seu "pai").
///
/// Funciona em qualquer Canvas (Popup, Inventário, etc), sem precisar
/// de um script diferente para cada um.
///
/// Requisito: o objeto precisa ter um Collider (BoxCollider) do tamanho do botao.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class BotaoX : MonoBehaviour
{
    private XRSimpleInteractable interactable;
    private Canvas canvasPai;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        // Procura o Canvas mais próximo na hierarquia acima deste botão
        canvasPai = GetComponentInParent<Canvas>(true);

        if (canvasPai == null)
        {
            Debug.LogWarning("[BotaoFecharCanvasXR] Nenhum Canvas encontrado nos pais de " + gameObject.name);
        }
    }

    private void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
    }

    private void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (canvasPai == null)
            return;

        Debug.Log("[BotaoFecharCanvasXR] Fechando Canvas: " + canvasPai.gameObject.name);
        canvasPai.gameObject.SetActive(false);
    }
}