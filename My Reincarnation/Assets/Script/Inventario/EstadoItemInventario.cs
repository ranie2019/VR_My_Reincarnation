using UnityEngine;

[DisallowMultipleComponent]
public class EstadoItemInventario : MonoBehaviour
{
    public bool estaNoInventario;
    public SlotInventario slotAtual;
    public bool estaEscondidoNaPilha;
    public bool estaSendoProcessado;

    public event System.Action<bool> EstadoInventarioAlterado;

    public void MarcarAceito(SlotInventario slot, bool escondido)
    {
        bool estavaNoInventario = estaNoInventario;
        estaNoInventario = true;
        slotAtual = slot;
        estaEscondidoNaPilha = escondido;
        estaSendoProcessado = true;

        if (!estavaNoInventario)
            EstadoInventarioAlterado?.Invoke(true);
    }

    public void MarcarTopo(SlotInventario slot)
    {
        bool estavaNoInventario = estaNoInventario;
        estaNoInventario = true;
        slotAtual = slot;
        estaEscondidoNaPilha = false;
        estaSendoProcessado = false;

        if (!estavaNoInventario)
            EstadoInventarioAlterado?.Invoke(true);
    }

    public void MarcarEscondido(SlotInventario slot)
    {
        bool estavaNoInventario = estaNoInventario;
        estaNoInventario = true;
        slotAtual = slot;
        estaEscondidoNaPilha = true;
        estaSendoProcessado = false;

        if (!estavaNoInventario)
            EstadoInventarioAlterado?.Invoke(true);
    }

    public void Liberar()
    {
        bool estavaNoInventario = estaNoInventario;
        estaNoInventario = false;
        slotAtual = null;
        estaEscondidoNaPilha = false;
        estaSendoProcessado = false;

        if (estavaNoInventario)
            EstadoInventarioAlterado?.Invoke(false);
    }
}
