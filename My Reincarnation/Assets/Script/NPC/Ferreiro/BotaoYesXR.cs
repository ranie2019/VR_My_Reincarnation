using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// VERSAO DE TESTE: fica no Button Yes e usa XRSimpleInteractable
/// (igual ao NPCPopup) para abrir o Inventario do Ferreiro so com hover,
/// sem precisar apertar o botao do controle.
///
/// Requisito: o objeto precisa ter um Collider (BoxCollider) do tamanho do botao,
/// porque XRSimpleInteractable detecta hover via fisica 3D, nao via raycast de UI.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class BotaoYesXR : MonoBehaviour
{
    [Tooltip("Arraste aqui o NPCPopup do Ferreiro.")]
    [SerializeField] private NPCPopup popup;

    private XRSimpleInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        if (popup == null)
            popup = GetComponentInParent<NPCPopup>(true);
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
        Debug.Log("[BotaoYesXR] Laser entrou (hover) no botao Yes. Popup encontrado: " + (popup != null));

        if (popup != null)
            popup.AbrirInventario();
    }
}