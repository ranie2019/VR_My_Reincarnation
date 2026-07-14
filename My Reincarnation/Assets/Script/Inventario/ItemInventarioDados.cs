using UnityEngine;

[DisallowMultipleComponent]
public class ItemInventarioDados : MonoBehaviour
{
    [SerializeField] private string nomeItem;
    [SerializeField] private GameObject prefabParaStack;

    [Header("Flecha")]
    [SerializeField] private bool ehFlecha;
    [SerializeField] private string idTipoFlecha;
    [SerializeField] private GameObject prefabFlechaParaDisparo;
    [SerializeField] private Sprite iconeFlecha;

    public string NomeItem => string.IsNullOrWhiteSpace(nomeItem)
        ? SlotInventario.LimparNomeItem(gameObject.name)
        : nomeItem.Trim();

    public GameObject PrefabParaStack => prefabParaStack;

    public bool EhFlecha()
    {
        return ehFlecha;
    }

    public string ObterIdTipoFlecha()
    {
        if (!string.IsNullOrWhiteSpace(idTipoFlecha))
            return idTipoFlecha.Trim();

        return NomeItem;
    }

    public GameObject ObterPrefabFlechaParaDisparo()
    {
        if (prefabFlechaParaDisparo != null)
            return prefabFlechaParaDisparo;

        if (prefabParaStack != null)
            return prefabParaStack;

        return gameObject;
    }

    public Sprite ObterIconeFlecha()
    {
        return iconeFlecha;
    }
}
