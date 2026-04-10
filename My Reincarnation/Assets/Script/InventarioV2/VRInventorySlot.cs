using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Collider))]
public class VRInventorySlot : MonoBehaviour
{
    [Header("Configurações do Slot")]
    public Transform snapPoint;
    public GameObject currentItem = null;
    public Color activeColor = Color.green;
    public Color idleColor = Color.white;

    private MeshRenderer meshRenderer;
    private Material slotMaterial;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            slotMaterial = meshRenderer.material;
            slotMaterial.color = idleColor;
        }

        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (currentItem != null) return;

        VRInventoryItem item = other.GetComponent<VRInventoryItem>();
        if (item == null) return;

        // Se o item não estiver sendo segurado (solto no slot)
        XRGrabInteractable grab = item.GetComponent<XRGrabInteractable>();
        if (grab != null && !grab.isSelected)
        {
            StoreItem(item);
        }
    }

    public void StoreItem(VRInventoryItem item)
    {
        if (currentItem != null) return;

        currentItem = item.gameObject;
        item.SetStored(this);

        if (slotMaterial != null)
            slotMaterial.color = activeColor;

        Debug.Log($"Item {item.name} guardado no slot {name}");
    }

    public void RemoveItem()
    {
        if (currentItem == null) return;

        VRInventoryItem item = currentItem.GetComponent<VRInventoryItem>();
        if (item != null)
        {
            item.SetReleased();
        }

        currentItem = null;
        if (slotMaterial != null)
            slotMaterial.color = idleColor;

        Debug.Log($"Item removido do slot {name}");
    }
}
