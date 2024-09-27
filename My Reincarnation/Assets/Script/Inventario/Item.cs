using UnityEngine;

public class Item : MonoBehaviour
{
    public bool inSlot = false; // Indica se o item está em um slot
    public Vector3 slotRotation = Vector3.zero; // Rotação padrão no slot
    public Slot currentSlot; // Referência ao slot atual

    // Método para remover o item do slot
    public void RemoveFromSlot()
    {
        if (currentSlot != null)
        {
            currentSlot.ItemInSlot = null; // Remove a referência ao item no slot
            currentSlot = null; // Remove a referência ao slot atual
            inSlot = false; // Define o item como não estando em um slot
        }
    }

    // Método para adicionar o item a um novo slot
    public void AddToSlot(Slot newSlot)
    {
        if (newSlot != null)
        {
            currentSlot = newSlot;
            inSlot = true;
            transform.SetParent(newSlot.transform);
            transform.localPosition = Vector3.zero;
            transform.localEulerAngles = slotRotation;
            GetComponent<Rigidbody>().isKinematic = true; // Desativa a física
        }
    }
}
