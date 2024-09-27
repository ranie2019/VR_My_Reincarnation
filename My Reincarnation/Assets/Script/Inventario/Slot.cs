using UnityEngine;
using UnityEngine.UI;

public class Slot : MonoBehaviour
{
    public GameObject ItemInSlot; // Refer�ncia ao item que est� no slot
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
        // Se j� houver um item no slot, n�o fa�a nada
        if (ItemInSlot != null) return;

        // Verifica se o objeto � um item
        GameObject obj = other.gameObject;
        if (!IsItem(obj)) return;

        // Verifica se o bot�o lateral (Hand Trigger) foi liberado
        if (OVRInput.GetUp(OVRInput.Button.SecondaryHandTrigger) || OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger))
        {
            InsertItem(obj); // Insere o item no slot
        }
    }

    // Verifica se o objeto � um item
    bool IsItem(GameObject obj)
    {
        return obj.GetComponent<Item>() != null;
    }

    // M�todo para inserir o item no slot
    void InsertItem(GameObject obj)
    {
        // Desativa a f�sica do item
        obj.GetComponent<Rigidbody>().isKinematic = true;

        // Ajusta a posi��o e a rota��o do item no slot
        obj.transform.SetParent(transform, true); // Define o slot como pai do item
        obj.transform.localPosition = Vector3.zero; // Centraliza o item no slot
        obj.transform.localEulerAngles = obj.GetComponent<Item>().slotRotation; // Define a rota��o

        // Atualiza o status do item
        obj.GetComponent<Item>().inSlot = true; // Marca o item como guardado no slot
        obj.GetComponent<Item>().currentSlot = this; // Associa o slot ao item

        // Atualiza a refer�ncia no slot
        ItemInSlot = obj;

        // Opcional: muda a cor do slot para indicar que est� ocupado
        if (slotImage != null)
        {
            slotImage.color = Color.gray;
        }
    }

    // M�todo para resetar a cor do slot (opcional)
    public void ResetColor()
    {
        if (slotImage != null)
        {
            slotImage.color = originalColor;
        }
    }
}
