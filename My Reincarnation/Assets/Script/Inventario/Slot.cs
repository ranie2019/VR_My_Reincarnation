using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Input System

public class Slot : MonoBehaviour
{
    [Header("UI")]
    public Image slotImage;

    [Header("Estado")]
    public GameObject itemInSlot;

    [Header("Input (XR / Input System)")]
    [Tooltip("A ação usada para inserir o item no slot. Ex: XRI RightHand Interaction/Select ou Activate.")]
    public InputActionReference insertAction;

    [Header("Config")]
    [Tooltip("Quando true, o item vira filho do slot e fica travado (kinematic).")]
    public bool travarItemNoSlot = true;

    private Color _corOriginal;

    void Awake()
    {
        if (!slotImage)
            slotImage = GetComponentInChildren<Image>();

        if (slotImage)
            _corOriginal = slotImage.color;
    }

    void OnEnable()
    {
        if (insertAction && insertAction.action != null)
            insertAction.action.Enable();
    }

    void OnDisable()
    {
        if (insertAction && insertAction.action != null)
            insertAction.action.Disable();
    }

    private void OnTriggerStay(Collider other)
    {
        if (itemInSlot != null) return;

        GameObject obj = other.gameObject;

        // Precisa ser um Item
        Item item = obj.GetComponent<Item>();
        if (item == null) return;

        // Precisa ter action configurada
        if (insertAction == null || insertAction.action == null) return;

        // Botão pressionado?
        if (insertAction.action.WasPressedThisFrame())
        {
            InsertItem(obj, item);
        }
    }

    private void InsertItem(GameObject obj, Item item)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb && travarItemNoSlot)
            rb.isKinematic = true;

        obj.transform.SetParent(transform, true);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localEulerAngles = item.slotRotation;

        item.inSlot = true;
        item.currentSlot = this;

        itemInSlot = obj;

        if (slotImage)
            slotImage.color = Color.gray;
    }

    public void ResetColor()
    {
        if (slotImage)
            slotImage.color = _corOriginal;
    }
}
