using UnityEngine;

public class Item : MonoBehaviour
{
    public bool inSlot = false; // Indica se o item est� em um slot
    public Vector3 slotRotation = Vector3.zero; // Rota��o padr�o no slot
    public Slot currentSlot; // Refer�ncia ao slot atual

    // M�todo para remover o item do slot
    public void RemoveFromSlot()
    {
        if (currentSlot != null)
        {
            currentSlot.ItemInSlot = null; // Remove a refer�ncia ao item no slot
            currentSlot = null; // Remove a refer�ncia ao slot atual
            inSlot = false; // Define o item como n�o estando em um slot
        }
    }

    // M�todo para adicionar o item a um novo slot
    public void AddToSlot(Slot newSlot)
    {
        if (newSlot != null)
        {
            currentSlot = newSlot;
            inSlot = true;
            transform.SetParent(newSlot.transform);
            transform.localPosition = Vector3.zero;
            transform.localEulerAngles = slotRotation;
            GetComponent<Rigidbody>().isKinematic = true; // Desativa a f�sica
        }
    }
}
