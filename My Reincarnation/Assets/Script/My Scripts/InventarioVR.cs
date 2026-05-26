using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

public class InventarioVR : MonoBehaviour
{
    [SerializeField] private XRInputButtonReader botaoInventario =
        new XRInputButtonReader("Inventario");

    [SerializeField] private GameObject painelInventario;
    [SerializeField] private SlotInventario[] slots;

    private bool aberto = false;

    private void OnEnable()
    {
        botaoInventario.EnableDirectActionIfModeUsed();
    }

    private void OnDisable()
    {
        botaoInventario.DisableDirectActionIfModeUsed();
    }

    private void Start()
    {
        SetEstado(false);
    }

    private void Update()
    {
        if (botaoInventario.ReadWasPerformedThisFrame())
        {
            SetEstado(!aberto);
        }

        // Apenas para testar no Editor com teclado
        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            SetEstado(!aberto);
        }
    }

    private void SetEstado(bool estado)
    {
        aberto = estado;

        if (painelInventario != null)
            painelInventario.SetActive(aberto);

        if (slots == null)
            return;

        foreach (var slot in slots)
        {
            if (slot != null)
                slot.SetInventarioAberto(aberto);
        }
    }
}