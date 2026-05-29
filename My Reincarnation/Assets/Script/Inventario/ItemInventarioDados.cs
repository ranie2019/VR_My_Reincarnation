using UnityEngine;

[DisallowMultipleComponent]
public class ItemInventarioDados : MonoBehaviour
{
    [SerializeField] private string nomeItem;
    [SerializeField] private GameObject prefabParaStack;

    public string NomeItem => string.IsNullOrWhiteSpace(nomeItem)
        ? SlotInventario.LimparNomeItem(gameObject.name)
        : nomeItem.Trim();

    public GameObject PrefabParaStack => prefabParaStack;
}
