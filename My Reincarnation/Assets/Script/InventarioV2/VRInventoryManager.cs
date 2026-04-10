using UnityEngine;
using UnityEngine.InputSystem;

public class VRInventoryManager : MonoBehaviour
{
    [Header("Configurações do Inventário")]
    public GameObject inventoryPanel;
    public Transform anchor; // Onde o inventário vai aparecer (ex: mão esquerda ou frente do jogador)
    public InputActionProperty toggleAction;

    [Header("Posicionamento")]
    public Vector3 positionOffset = new Vector3(0f, 0f, 0.4f);
    public Vector3 rotationOffsetEuler = new Vector3(15f, 0f, 0f);
    public float followSmooth = 12f;

    private bool isActive = false;

    void Awake()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
    }

    void OnEnable()
    {
        if (toggleAction.action != null)
            toggleAction.action.Enable();
    }

    void OnDisable()
    {
        if (toggleAction.action != null)
            toggleAction.action.Disable();
    }

    void Update()
    {
        if (inventoryPanel == null || anchor == null) return;

        if (toggleAction.action != null && toggleAction.action.WasPressedThisFrame())
        {
            ToggleInventory();
        }

        if (isActive)
        {
            UpdatePosition();
        }
    }

    private void ToggleInventory()
    {
        isActive = !isActive;
        inventoryPanel.SetActive(isActive);

        if (isActive)
        {
            // Posiciona instantaneamente ao abrir
            inventoryPanel.transform.position = anchor.TransformPoint(positionOffset);
            inventoryPanel.transform.rotation = anchor.rotation * Quaternion.Euler(rotationOffsetEuler);
        }
    }

    private void UpdatePosition()
    {
        Vector3 targetPos = anchor.TransformPoint(positionOffset);
        Quaternion targetRot = anchor.rotation * Quaternion.Euler(rotationOffsetEuler);

        inventoryPanel.transform.position = Vector3.Lerp(inventoryPanel.transform.position, targetPos, followSmooth * Time.deltaTime);
        inventoryPanel.transform.rotation = Quaternion.Slerp(inventoryPanel.transform.rotation, targetRot, followSmooth * Time.deltaTime);
    }
}
