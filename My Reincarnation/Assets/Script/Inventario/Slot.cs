using UnityEngine;
using UnityEngine.UI;

public class Slot : MonoBehaviour
{
    public GameObject ItemInSlot;
    public Image SlotImage;
    Color originalColor;

    void Start()
    {
        SlotImage = GetComponentInChildren<Image>();
        originalColor = SlotImage.color;
    }

    private void OnTriggerStay(Collider other)
    {
        if (ItemInSlot != null) return;
        GameObject obj = other.gameObject;  
        if (!IsItem(obj)) return;
        if (OVRInput.GetUp(OVRInput.Button.SecondaryHandTrigger))
        {
            InsertItem(obj);
        }
    }

    bool IsItem(GameObject obj)
    {
        return obj.GetComponent<Item>();
    }

    void InsertItem(GameObject obj)
    {
        obj.GetComponent<Rigidbody>().isKinematic = true;
        obj.transform.SetParent(gameObject.transform, true);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localEulerAngles = obj.GetComponent<Item>().slotRotation;
        obj.GetComponent<Item>().inSlot = true;
        obj.GetComponent<Item>().currentSlot = this;
        ItemInSlot = obj;
        SlotImage.color = Color.gray;
    }

    public void ResetColor()
    {
        SlotImage.color = originalColor;
    }
}
