using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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

        if (painelInventario != null && !painelInventario.activeSelf)
            painelInventario.SetActive(true);

        if (aberto)
        {
            SetPainelVisivel(true);
            AtualizarSlots(true);
            return;
        }

        AtualizarSlots(false);
        SetPainelVisivel(false);
    }

    private void SetPainelVisivel(bool visivel)
    {
        if (painelInventario == null)
            return;

        foreach (var canvas in painelInventario.GetComponentsInChildren<Canvas>(true))
            canvas.enabled = visivel;

        foreach (var graphic in painelInventario.GetComponentsInChildren<Graphic>(true))
            graphic.enabled = visivel;

        foreach (var renderer in painelInventario.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = visivel;
    }

    private void AtualizarSlots(bool inventarioAberto)
    {
        if (slots == null)
            return;

        foreach (var slot in slots)
        {
            if (slot != null)
                slot.SetInventarioAberto(inventarioAberto);
        }
    }
}