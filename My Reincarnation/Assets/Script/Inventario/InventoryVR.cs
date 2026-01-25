using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryVR : MonoBehaviour
{
    [Header("Referências")]
    public GameObject inventory;
    public Transform anchor;

    [Header("Input (XR / Input System)")]
    [Tooltip("A ação que abre/fecha o inventário (toggle). Ex: XRI RightHand Interaction/PrimaryButton")]
    public InputActionReference toggleAction;

    [Header("Posição")]
    public Vector3 positionOffset = new Vector3(0f, 0f, 0.4f); // na frente do anchor

    [Header("Rotação")]
    public Vector3 rotationOffsetEuler = new Vector3(15f, 0f, 0f); // inclina 15 graus
    public float followSmooth = 12f; // suavidade do follow

    bool uiActive;

    void Awake()
    {
        if (inventory)
            inventory.SetActive(false);

        uiActive = false;
    }

    void OnEnable()
    {
        if (toggleAction && toggleAction.action != null)
            toggleAction.action.Enable();
    }

    void OnDisable()
    {
        if (toggleAction && toggleAction.action != null)
            toggleAction.action.Disable();
    }

    void Update()
    {
        if (inventory == null || anchor == null) return;

        // Toggle (apertou o botão)
        if (toggleAction != null && toggleAction.action != null && toggleAction.action.WasPressedThisFrame())
        {
            uiActive = !uiActive;
            inventory.SetActive(uiActive);
        }

        // Follow enquanto estiver aberto
        if (uiActive)
        {
            // posição desejada (anchor + offset no espaço do anchor)
            Vector3 targetPos = anchor.TransformPoint(positionOffset);

            // rotação desejada (anchor + offset)
            Quaternion targetRot = anchor.rotation * Quaternion.Euler(rotationOffsetEuler);

            // suavização
            inventory.transform.position = Vector3.Lerp(inventory.transform.position, targetPos, followSmooth * Time.deltaTime);
            inventory.transform.rotation = Quaternion.Slerp(inventory.transform.rotation, targetRot, followSmooth * Time.deltaTime);
        }
    }
}
