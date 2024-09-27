using UnityEngine;
using UnityEngine.UI;

public class Slot : MonoBehaviour
{
    public GameObject ItemInSlot; // Referência ao item que está no slot
    public Image slotImage; // Imagem do slot (opcional)
    private Color originalColor; // Cor original do slot (opcional)

    void Start()
    {
        if (slotImage == null)
        {
            slotImage = GetComponentInChildren<Image>(); // Caso o slot tenha uma imagem
        }

        if (slotImage != null)
        {
            originalColor = slotImage.color; // Armazena a cor original
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Se já houver um item no slot, não faça nada
        if (ItemInSlot != null) return;

        // Verifica se o objeto é um item
        GameObject obj = other.gameObject;
        if (!IsItem(obj)) return;

        // Verifica se o botão lateral (Hand Trigger) foi liberado
        if (OVRInput.GetUp(OVRInput.Button.SecondaryHandTrigger) || OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger))
        {
            InsertItem(obj); // Insere o item no slot
        }
    }

    // Verifica se o objeto é um item
    bool IsItem(GameObject obj)
    {
        return obj.GetComponent<Item>() != null;
    }

    // Método para inserir o item no slot
    void InsertItem(GameObject obj)
    {
        // Desativa a física do item
        obj.GetComponent<Rigidbody>().isKinematic = true;

        // Ajusta a posição e a rotação do item no slot
        obj.transform.SetParent(transform, true); // Define o slot como pai do item
        obj.transform.localPosition = Vector3.zero; // Centraliza o item no slot
        obj.transform.localEulerAngles = obj.GetComponent<Item>().slotRotation; // Define a rotação

        // Atualiza o status do item
        obj.GetComponent<Item>().inSlot = true; // Marca o item como guardado no slot
        obj.GetComponent<Item>().currentSlot = this; // Associa o slot ao item

        // Atualiza a referência no slot
        ItemInSlot = obj;

        // Opcional: muda a cor do slot para indicar que está ocupado
        if (slotImage != null)
        {
            slotImage.color = Color.gray;
        }
    }

    // Método para resetar a cor do slot (opcional)
    public void ResetColor()
    {
        if (slotImage != null)
        {
            slotImage.color = originalColor;
        }
    }
}
