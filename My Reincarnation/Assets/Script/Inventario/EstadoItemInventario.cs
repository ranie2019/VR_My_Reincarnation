using UnityEngine;

[DisallowMultipleComponent]
public class EstadoItemInventario : MonoBehaviour
{
    public bool estaNoInventario;
    public SlotInventario slotAtual;
    public bool estaEscondidoNaPilha;
    public bool estaSendoProcessado;

    public void MarcarAceito(SlotInventario slot, bool escondido)
    {
        estaNoInventario = true;
        slotAtual = slot;
        estaEscondidoNaPilha = escondido;
        estaSendoProcessado = true;
    }

    public void MarcarTopo(SlotInventario slot)
    {
        estaNoInventario = true;
        slotAtual = slot;
        estaEscondidoNaPilha = false;
        estaSendoProcessado = false;
    }

    public void MarcarEscondido(SlotInventario slot)
    {
        estaNoInventario = true;
        slotAtual = slot;
        estaEscondidoNaPilha = true;
        estaSendoProcessado = false;
    }

    public void Liberar()
    {
        estaNoInventario = false;
        slotAtual = null;
        estaEscondidoNaPilha = false;
        estaSendoProcessado = false;
    }
}
