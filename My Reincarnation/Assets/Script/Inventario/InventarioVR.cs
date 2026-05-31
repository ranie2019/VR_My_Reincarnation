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
    [SerializeField] private Collider[] collidersBotoesRolagem;
    [SerializeField] private BotaoRolagemInventarioVR[] botoesRolagem;

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
            AtualizarBotoesRolagem(true);
            SetPainelVisivel(true);
            AtualizarSlots(true);
            return;
        }

        AtualizarSlots(false);
        SetPainelVisivel(false);
        AtualizarBotoesRolagem(false);
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

    private void AtualizarBotoesRolagem(bool ativo)
    {
        if (collidersBotoesRolagem != null)
        {
            foreach (var colliderBotao in collidersBotoesRolagem)
            {
                if (colliderBotao != null)
                    colliderBotao.enabled = ativo;
            }
        }

        if (botoesRolagem == null)
            return;

        foreach (var botaoRolagem in botoesRolagem)
        {
            if (botaoRolagem != null)
                botaoRolagem.SetBotaoAtivo(ativo);
        }
    }
}
