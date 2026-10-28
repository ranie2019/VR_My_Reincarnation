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
        [HideInInspector] public string tipoItemId;
        [HideInInspector] public string itemId;
        public GameObject prefab;
    }

    [SerializeField] private bool ignorarMaiusculasMinusculas = false;
    [SerializeField] private List<EntradaItem> itens = new List<EntradaItem>();

    private Dictionary<string, GameObject> cachePorId;

    private void ConstruirCacheSeNecessario()
    {
        if (cachePorId != null)
            return;

        cachePorId = new Dictionary<string, GameObject>(ObterComparador());

        if (itens == null)
            return;

        for (int i = 0; i < itens.Count; i++)
        {
            EntradaItem entrada = itens[i];
            if (entrada == null || entrada.prefab == null)
                continue;

            RegistrarNoCache(entrada.tipoItemId, entrada.prefab);
            RegistrarNoCache(entrada.itemId, entrada.prefab);
            RegistrarNoCache(entrada.prefab.name, entrada.prefab);

            ItemPersistente persistente = entrada.prefab.GetComponentInChildren<ItemPersistente>(true);
            if (persistente != null)
            {
                RegistrarNoCache(persistente.ObterTipoItemId(), entrada.prefab);
                RegistrarNoCache(persistente.ObterItemIdLegado(), entrada.prefab);
                RegistrarNoCache(persistente.ObterNomeItem(), entrada.prefab);
            }
        }
    }

    private void RegistrarNoCache(string id, GameObject prefab)
    {
        if (cachePorId == null || prefab == null || string.IsNullOrWhiteSpace(id))
            return;

        string normalizado = id.Trim();
        if (!cachePorId.ContainsKey(normalizado))
            cachePorId[normalizado] = prefab;
    }

    private void InvalidarCache()
    {
        cachePorId = null;
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            { }
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
        InvalidarCache();
        ValidarDatabaseInterno(false);
    }

    public GameObject ObterPrefab(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        ConstruirCacheSeNecessario();

        string idNormalizado = itemId.Trim();
        if (cachePorId.TryGetValue(idNormalizado, out GameObject prefab) && prefab != null)
            return prefab;

        // Uma referencia restaurada/importada pode nao ter existido ao montar o cache.
        InvalidarCache();
        ConstruirCacheSeNecessario();
        if (cachePorId.TryGetValue(idNormalizado, out prefab) && prefab != null)
            return prefab;

        // Os recursos de natureza ja possuem referencias serializadas para o build.
        // Reutiliza apenas correspondencias exatas do ID de tipo, nunca uma instancia da cena.
        foreach (var natureza in FindObjectsByType<RespawnNatureza>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GameObject candidato = natureza.ObterPrefabPorTipoPersistente(idNormalizado);
            if (candidato == null) continue;
            if (prefab != null && prefab != candidato)
                throw new InvalidOperationException("Prefabs de natureza conflitantes para o tipo: " + idNormalizado);
            prefab = candidato;
        }

#if UNITY_EDITOR
        // Recupera tambem o asset exato quando a cena aberta ainda tem um catalogo antigo.
        if (prefab == null && Guid.TryParseExact(idNormalizado, "N", out _))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(idNormalizado);
            GameObject candidato = string.IsNullOrEmpty(caminho) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            var persistente = candidato != null ? candidato.GetComponent<ItemPersistente>() : null;
            if (persistente != null && string.Equals(persistente.ObterTipoItemId(), idNormalizado, StringComparison.Ordinal))
                prefab = candidato;
        }
#endif

        if (prefab != null) RegistrarNoCache(idNormalizado, prefab);
        return prefab;
    }

    public bool ExisteItem(string itemId)
    {
        return ObterPrefab(itemId) != null;
    }

    [ContextMenu("Validar Database")]
    public void ValidarDatabase()
    {
        InvalidarCache();
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
                    Debug.LogWarning($"[ItemDatabaseLocal] Entrada nula no indice {i}.", this);
                continue;
            }

            SincronizarIdsDaEntrada(entrada);

            if (string.IsNullOrWhiteSpace(entrada.itemId))
            {
                if (validarCamposObrigatorios)
                    Debug.LogWarning($"[ItemDatabaseLocal] Entrada no indice {i} esta sem itemId.", this);
                continue;
            }

            if (validarCamposObrigatorios && entrada.prefab == null)
                Debug.LogWarning($"[ItemDatabaseLocal] Entrada '{entrada.itemId}' esta sem prefab atribuido.", this);

            string id = entrada.itemId.Trim();
            if (!ids.Add(id))
                Debug.LogWarning($"[ItemDatabaseLocal] itemId duplicado: '{id}'. Apenas a primeira entrada sera usada por ObterPrefab().", this);
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
            SincronizarIdsDaEntrada(entrada);
            alterados++;
        }

#if UNITY_EDITOR
        if (alterados > 0)
            EditorUtility.SetDirty(this);
#endif

        InvalidarCache();
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

    private void SincronizarIdsDaEntrada(EntradaItem entrada)
    {
        if (entrada == null || entrada.prefab == null)
            return;

        ItemPersistente persistente = entrada.prefab.GetComponentInChildren<ItemPersistente>(true);
        if (persistente != null)
        {
            entrada.tipoItemId = persistente.ObterTipoItemId();
            if (string.IsNullOrWhiteSpace(entrada.itemId))
                entrada.itemId = persistente.ObterItemIdLegado();
        }

        if (string.IsNullOrWhiteSpace(entrada.itemId))
            entrada.itemId = entrada.prefab.name;
    }
}
