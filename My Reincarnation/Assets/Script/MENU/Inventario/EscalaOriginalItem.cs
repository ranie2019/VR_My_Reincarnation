using UnityEngine;

/// <summary>
/// Componente auxiliar que armazena a escala original real do item.
/// Adicionado automaticamente pelo SlotInventario na primeira vez que o item entra num slot.
/// Pode ser redefinido quando o inventario encontra uma base limpa do prefab.
/// </summary>
public class EscalaOriginalItem : MonoBehaviour
{
    [HideInInspector] public bool inicializado = false;
    [HideInInspector] public Vector3 escalaOriginal;
    [HideInInspector] public SlotInventario slotComEscalaAplicada;
    [HideInInspector] public bool escalaAplicadaNoSlot = false;
    [HideInInspector] public float margemDeSegurancaAplicada = -1f;

    public void DefinirEscalaOriginal(Vector3 novaEscalaOriginal)
    {
        escalaOriginal = novaEscalaOriginal;
        inicializado = true;
        slotComEscalaAplicada = null;
        escalaAplicadaNoSlot = false;
        margemDeSegurancaAplicada = -1f;
    }

    public bool EscalaJaAplicadaPara(SlotInventario slot)
    {
        return inicializado && escalaAplicadaNoSlot && slotComEscalaAplicada == slot;
    }

    public bool EscalaJaAplicadaPara(SlotInventario slot, float margemDeSegurancaAtual)
    {
        return EscalaJaAplicadaPara(slot) &&
               Mathf.Approximately(margemDeSegurancaAplicada, margemDeSegurancaAtual);
    }

    public void MarcarEscalaAplicada(SlotInventario slot)
    {
        slotComEscalaAplicada = slot;
        escalaAplicadaNoSlot = true;
        margemDeSegurancaAplicada = -1f;
    }

    public void MarcarEscalaAplicada(SlotInventario slot, float margemDeSegurancaUsada)
    {
        slotComEscalaAplicada = slot;
        escalaAplicadaNoSlot = true;
        margemDeSegurancaAplicada = margemDeSegurancaUsada;
    }

    public void LimparTravaDoSlot(SlotInventario slot)
    {
        if (slotComEscalaAplicada != slot) return;

        slotComEscalaAplicada = null;
        escalaAplicadaNoSlot = false;
        margemDeSegurancaAplicada = -1f;
    }
}
