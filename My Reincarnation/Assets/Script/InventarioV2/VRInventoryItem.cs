using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Rigidbody))]
public class VRInventoryItem : MonoBehaviour
{
    [Header("Configurações do Item")]
    public Vector3 slotRotationOffset = Vector3.zero;
    public Vector3 slotPositionOffset = Vector3.zero;

    [HideInInspector] public bool isStored = false;
    [HideInInspector] public VRInventorySlot currentSlot = null;

    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrab);
            grabInteractable.selectExited.AddListener(OnRelease);
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (isStored && currentSlot != null)
        {
            currentSlot.RemoveItem();
        }
        isStored = false;
        rb.isKinematic = false;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        // A lógica de snap é tratada pelo Slot via OnTriggerStay ou OnTriggerEnter
    }

    public void SetStored(VRInventorySlot slot)
    {
        isStored = true;
        currentSlot = slot;
        rb.isKinematic = true;
        
        transform.SetParent(slot.snapPoint != null ? slot.snapPoint : slot.transform);
        transform.localPosition = slotPositionOffset;
        transform.localRotation = Quaternion.Euler(slotRotationOffset);
    }

    public void SetReleased()
    {
        isStored = false;
        currentSlot = null;
        rb.isKinematic = false;
        transform.SetParent(null);
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrab);
            grabInteractable.selectExited.RemoveListener(OnRelease);
        }
    }
}
