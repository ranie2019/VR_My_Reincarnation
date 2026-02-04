using UnityEngine;

public class Slot : MonoBehaviour
{
    [Header("Estado")]
    public Item itemInSlot;

    [Header("Config")]
    public bool travarFisica = true;

    private void OnTriggerEnter(Collider other)
    {
        if (itemInSlot != null) return;

        Item item = other.GetComponent<Item>();
        if (item == null) return;
        if (item.inSlot) return;

        InserirItem(item);
    }

    private void InserirItem(Item item)
    {
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null && travarFisica)
            rb.isKinematic = true;

        item.transform.SetParent(transform);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        item.inSlot = true;
        item.currentSlot = this;
        itemInSlot = item;
    }

    public void RemoverItem()
    {
        if (itemInSlot == null) return;

        Item item = itemInSlot;

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = false;

        item.transform.SetParent(null);

        item.inSlot = false;
        item.currentSlot = null;
        itemInSlot = null;
    }
}
