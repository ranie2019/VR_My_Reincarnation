using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BotaoFlechaInventarioArco : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private InventarioArcoUI inventarioArcoUI;
    [SerializeField] private Button botao;
    [SerializeField] private string idTipoFlecha;

    [Header("Diagnostico")]
    [SerializeField] private bool recebeuPointerEnter;
    [SerializeField] private bool recebeuPointerClick;

    public void Configurar(InventarioArcoUI novoInventarioArcoUI, Button novoBotao, string novoIdTipoFlecha)
    {
        inventarioArcoUI = novoInventarioArcoUI;
        botao = novoBotao;
        idTipoFlecha = novoIdTipoFlecha;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        recebeuPointerEnter = true;

        if (inventarioArcoUI != null)
            inventarioArcoUI.RegistrarPointerEnterBotaoFlecha(botao, idTipoFlecha);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (inventarioArcoUI != null)
            inventarioArcoUI.LimparBotaoFlechaEmHover(botao);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        recebeuPointerClick = true;

        if (inventarioArcoUI != null)
            inventarioArcoUI.RegistrarPointerClickBotaoFlecha(botao, idTipoFlecha);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (inventarioArcoUI != null)
            inventarioArcoUI.RegistrarPointerEnterBotaoFlecha(botao, idTipoFlecha);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (inventarioArcoUI != null)
            inventarioArcoUI.LimparBotaoFlechaEmHover(botao);
    }
}
