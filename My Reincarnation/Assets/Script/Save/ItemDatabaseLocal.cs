using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class ItemDatabaseLocal : MonoBehaviour
{
    public static ItemDatabaseLocal Instancia { get; private set; }

    [Serializable]
    public class EntradaItem
    {
        public string itemId;
        public GameObject prefab;
    }

    [SerializeField] private bool ignorarMaiusculasMinusculas = false;
    [SerializeField] private List<EntradaItem> itens = new List<EntradaItem>();

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Debug.LogWarning("[ItemDatabaseLocal] Existe mais de um banco local de itens na cena. Mantendo o primeiro.", this);
            enabled = false;
            return;
        }

        Instancia = this;
        ValidarDatabase();
    }

    private void OnDisable()
    {
        if (Instancia == this)
            Instancia = null;
    }

    private void OnValidate()
    {
        ValidarDatabaseInterno(false);
    }

    public GameObject ObterPrefab(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || itens == null)
            return null;

        string idNormalizado = itemId.Trim();

        for (int i = 0; i < itens.Count; i++)
        {
            EntradaItem entrada = itens[i];
            if (entrada == null || string.IsNullOrWhiteSpace(entrada.itemId))
                continue;

            if (string.Equals(entrada.itemId.Trim(), idNormalizado, ObterComparacao()))
                return entrada.prefab;
        }

        return null;
    }

    public bool ExisteItem(string itemId)
    {
        return ObterPrefab(itemId) != null;
    }

    [ContextMenu("Validar Database")]
    public void ValidarDatabase()
    {
        ValidarDatabaseInterno(true);
    }

    private void ValidarDatabaseInterno(bool validarCamposObrigatorios)
    {
        if (itens == null)
            return;

        HashSet<string> ids = new HashSet<string>(ObterComparador());

        for (int i = 0; i < itens.Count; i++)
        {
            EntradaItem entrada = itens[i];
            if (entrada == null)
            {
                if (validarCamposObrigatorios)
                    Debug.LogWarning($"[ItemDatabaseLocal] Entrada vazia no indice {i}.", this);

                continue;
            }

            if (string.IsNullOrWhiteSpace(entrada.itemId))
            {
                if (validarCamposObrigatorios)
                    Debug.LogWarning($"[ItemDatabaseLocal] itemId vazio no indice {i}.", this);

                continue;
            }

            if (validarCamposObrigatorios && entrada.prefab == null)
                Debug.LogWarning($"[ItemDatabaseLocal] Prefab vazio para itemId: {entrada.itemId.Trim()}", this);

            string id = entrada.itemId.Trim();
            if (!ids.Add(id))
                Debug.LogWarning($"[ItemDatabaseLocal] itemId duplicado: {id}", this);
        }
    }

    [ContextMenu("Preencher IDs Pelo Nome Do Prefab")]
    public void PreencherIdsPeloNomeDoPrefab()
    {
        if (itens == null)
            return;

        int alterados = 0;
        for (int i = 0; i < itens.Count; i++)
        {
            EntradaItem entrada = itens[i];
            if (entrada == null || entrada.prefab == null || !string.IsNullOrWhiteSpace(entrada.itemId))
                continue;

            entrada.itemId = entrada.prefab.name;
            alterados++;
        }

#if UNITY_EDITOR
        if (alterados > 0)
            EditorUtility.SetDirty(this);
#endif

        Debug.Log($"[ItemDatabaseLocal] IDs preenchidos pelo nome do prefab: {alterados}", this);
        ValidarDatabase();
    }

    private StringComparison ObterComparacao()
    {
        return ignorarMaiusculasMinusculas
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private StringComparer ObterComparador()
    {
        return ignorarMaiusculasMinusculas
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }
}
