using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ItemPersistenteIdEditorUtility
{
    private const string CampoItemId = "itemId";
    private const string CampoInstanciaId = "instanciaId";
    private const string CampoItensDatabase = "itens";
    private const string CampoDatabaseItemId = "itemId";
    private const string CampoDatabasePrefab = "prefab";

    [MenuItem("Tools/Save/Gerar IDs dos Itens Persistentes da Cena")]
    public static void GerarIdsDosItensPersistentesDaCena()
    {
        if (!PodeExecutarFerramentaDeCena("Gerar IDs dos Itens Persistentes da Cena"))
            return;

        Scene cena = SceneManager.GetActiveScene();
        if (!cena.IsValid() || !cena.isLoaded)
        {
            Debug.LogWarning("[ItemPersistenteIdEditorUtility] Nenhuma cena ativa carregada para gerar IDs.");
            return;
        }

        List<ItemPersistente> itens = ObterItensPersistentesDaCenaAtiva();
        HashSet<string> idsUsados = new HashSet<string>(StringComparer.Ordinal);
        int idsGerados = 0;
        int duplicadosCorrigidos = 0;

        for (int i = 0; i < itens.Count; i++)
        {
            ItemPersistente item = itens[i];
            string instanciaId = LerStringSerializada(item, CampoInstanciaId);

            if (!string.IsNullOrWhiteSpace(instanciaId) && idsUsados.Add(instanciaId))
                continue;

            bool eraDuplicado = !string.IsNullOrWhiteSpace(instanciaId);
            string novoId = CriarGuidUnico(idsUsados);

            Undo.RecordObject(item, "Gerar instanciaId persistente");
            DefinirStringSerializada(item, CampoInstanciaId, novoId);
            EditorUtility.SetDirty(item);

            idsGerados++;
            if (eraDuplicado)
                duplicadosCorrigidos++;
        }

        if (idsGerados > 0)
            EditorSceneManager.MarkSceneDirty(cena);

        Debug.Log(
            $"[ItemPersistenteIdEditorUtility] IDs gerados/corrigidos na cena ativa: {idsGerados}. Duplicados corrigidos: {duplicadosCorrigidos}.");
    }

    [MenuItem("Tools/Save/Limpar IDs dos Prefabs ItemPersistente")]
    public static void LimparIdsDosPrefabsItemPersistente()
    {
        if (!PodeExecutarForaDoPlayMode("Limpar IDs dos Prefabs ItemPersistente"))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int prefabsAlterados = 0;
        int idsLimpados = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string caminho = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject raiz = PrefabUtility.LoadPrefabContents(caminho);
                if (raiz == null)
                    continue;

                bool alterouPrefab = false;
                try
                {
                    ItemPersistente[] itens = raiz.GetComponentsInChildren<ItemPersistente>(true);
                    for (int j = 0; j < itens.Length; j++)
                    {
                        string instanciaId = LerStringSerializada(itens[j], CampoInstanciaId);
                        if (string.IsNullOrWhiteSpace(instanciaId))
                            continue;

                        DefinirStringSerializada(itens[j], CampoInstanciaId, string.Empty);
                        alterouPrefab = true;
                        idsLimpados++;
                    }

                    if (alterouPrefab)
                    {
                        PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
                        prefabsAlterados++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(raiz);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        if (prefabsAlterados > 0)
            AssetDatabase.SaveAssets();

        Debug.Log(
            $"[ItemPersistenteIdEditorUtility] Prefabs alterados: {prefabsAlterados}. instanciaIds removidos de prefab assets: {idsLimpados}.");
    }

    [MenuItem("Tools/Save/Validar IDs Persistentes")]
    public static void ValidarIdsPersistentes()
    {
        if (!PodeExecutarFerramentaDeCena("Validar IDs Persistentes"))
            return;

        List<ItemPersistente> itens = ObterItensPersistentesDaCenaAtiva();
        Dictionary<string, ItemPersistente> primeiroPorInstanciaId = new Dictionary<string, ItemPersistente>(StringComparer.Ordinal);
        HashSet<string> idsDatabase = ObterIdsDoDatabaseAtivo();

        int avisos = 0;

        for (int i = 0; i < itens.Count; i++)
        {
            ItemPersistente item = itens[i];
            string itemId = LerStringSerializada(item, CampoItemId);
            string instanciaId = LerStringSerializada(item, CampoInstanciaId);

            if (string.IsNullOrWhiteSpace(itemId))
            {
                avisos++;
                Debug.LogWarning($"[ItemPersistenteIdEditorUtility] itemId vazio em: {ObterCaminhoHierarquia(item.transform)}", item);
            }
            else if (idsDatabase.Count > 0 && !idsDatabase.Contains(itemId))
            {
                avisos++;
                Debug.LogWarning($"[ItemPersistenteIdEditorUtility] itemId nao existe no ItemDatabaseLocal: {itemId} em {ObterCaminhoHierarquia(item.transform)}", item);
            }

            if (string.IsNullOrWhiteSpace(instanciaId))
            {
                avisos++;
                Debug.LogWarning($"[ItemPersistenteIdEditorUtility] instanciaId vazio em: {ObterCaminhoHierarquia(item.transform)}", item);
                continue;
            }

            if (primeiroPorInstanciaId.TryGetValue(instanciaId, out ItemPersistente primeiro))
            {
                avisos++;
                Debug.LogWarning(
                    $"[ItemPersistenteIdEditorUtility] instanciaId duplicado: {instanciaId}. Primeiro: {ObterCaminhoHierarquia(primeiro.transform)} | Duplicado: {ObterCaminhoHierarquia(item.transform)}",
                    item);
            }
            else
            {
                primeiroPorInstanciaId.Add(instanciaId, item);
            }
        }

        avisos += ValidarDatabaseAtivo();
        Debug.Log($"[ItemPersistenteIdEditorUtility] Validacao finalizada. Avisos encontrados: {avisos}.");
    }

    private static List<ItemPersistente> ObterItensPersistentesDaCenaAtiva()
    {
        Scene cenaAtiva = SceneManager.GetActiveScene();
        ItemPersistente[] todos = Resources.FindObjectsOfTypeAll<ItemPersistente>();
        List<ItemPersistente> resultado = new List<ItemPersistente>();

        for (int i = 0; i < todos.Length; i++)
        {
            ItemPersistente item = todos[i];
            if (item == null || EditorUtility.IsPersistent(item))
                continue;

            if (PrefabUtility.IsPartOfPrefabAsset(item.gameObject))
                continue;

            if (item.gameObject.scene == cenaAtiva)
                resultado.Add(item);
        }

        return resultado;
    }

    private static bool PodeExecutarFerramentaDeCena(string nomeOperacao)
    {
        if (!PodeExecutarForaDoPlayMode(nomeOperacao))
            return false;

        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null && SceneManager.GetActiveScene() == prefabStage.scene)
        {
            Debug.LogWarning(
                $"[ItemPersistenteIdEditorUtility] Operacao cancelada: '{nomeOperacao}' deve ser executada em uma cena normal, nao dentro do Prefab Mode. Saia do Prefab Mode para nao salvar IDs fixos em prefab assets.");
            return false;
        }

        return true;
    }

    private static bool PodeExecutarForaDoPlayMode(string nomeOperacao)
    {
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            return true;

        Debug.LogWarning(
            $"[ItemPersistenteIdEditorUtility] Operacao cancelada: '{nomeOperacao}' nao pode ser executada durante Play Mode. IDs temporarios de runtime nao devem ser salvos como IDs persistentes.");
        return false;
    }

    private static HashSet<string> ObterIdsDoDatabaseAtivo()
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        ItemDatabaseLocal database = ObterDatabaseAtivo();
        if (database == null)
            return ids;

        SerializedObject serializedObject = new SerializedObject(database);
        SerializedProperty itens = serializedObject.FindProperty(CampoItensDatabase);
        if (itens == null || !itens.isArray)
            return ids;

        for (int i = 0; i < itens.arraySize; i++)
        {
            SerializedProperty entrada = itens.GetArrayElementAtIndex(i);
            SerializedProperty itemId = entrada.FindPropertyRelative(CampoDatabaseItemId);
            if (itemId != null && !string.IsNullOrWhiteSpace(itemId.stringValue))
                ids.Add(itemId.stringValue.Trim());
        }

        return ids;
    }

    private static int ValidarDatabaseAtivo()
    {
        ItemDatabaseLocal database = ObterDatabaseAtivo();
        if (database == null)
        {
            Debug.LogWarning("[ItemPersistenteIdEditorUtility] ItemDatabaseLocal nao encontrado na cena ativa.");
            return 1;
        }

        int avisos = 0;
        SerializedObject serializedObject = new SerializedObject(database);
        SerializedProperty itens = serializedObject.FindProperty(CampoItensDatabase);
        if (itens == null || !itens.isArray)
        {
            Debug.LogWarning("[ItemPersistenteIdEditorUtility] Lista de itens do ItemDatabaseLocal nao encontrada.");
            return 1;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < itens.arraySize; i++)
        {
            SerializedProperty entrada = itens.GetArrayElementAtIndex(i);
            SerializedProperty itemId = entrada.FindPropertyRelative(CampoDatabaseItemId);
            SerializedProperty prefab = entrada.FindPropertyRelative(CampoDatabasePrefab);

            bool itemIdVazio = itemId == null || string.IsNullOrWhiteSpace(itemId.stringValue);
            bool prefabVazio = prefab == null || prefab.objectReferenceValue == null;

            if (itemIdVazio && prefabVazio)
            {
                avisos++;
                Debug.LogWarning($"[ItemPersistenteIdEditorUtility] Entrada vazia no ItemDatabaseLocal no indice {i}.", database);
                continue;
            }

            if (itemIdVazio)
            {
                avisos++;
                Debug.LogWarning($"[ItemPersistenteIdEditorUtility] itemId vazio no ItemDatabaseLocal no indice {i}.", database);
                continue;
            }

            string id = itemId.stringValue.Trim();
            if (!ids.Add(id))
            {
                avisos++;
                Debug.LogWarning($"[ItemPersistenteIdEditorUtility] itemId duplicado no ItemDatabaseLocal: {id}", database);
            }

            if (prefabVazio)
            {
                avisos++;
                Debug.LogWarning($"[ItemPersistenteIdEditorUtility] prefab vazio no ItemDatabaseLocal para itemId: {id}", database);
            }
        }

        return avisos;
    }

    private static ItemDatabaseLocal ObterDatabaseAtivo()
    {
        Scene cenaAtiva = SceneManager.GetActiveScene();
        ItemDatabaseLocal[] databases = Resources.FindObjectsOfTypeAll<ItemDatabaseLocal>();
        for (int i = 0; i < databases.Length; i++)
        {
            ItemDatabaseLocal database = databases[i];
            if (database == null || EditorUtility.IsPersistent(database))
                continue;

            if (database.gameObject.scene == cenaAtiva)
                return database;
        }

        return null;
    }

    private static string LerStringSerializada(UnityEngine.Object alvo, string nomeCampo)
    {
        SerializedObject serializedObject = new SerializedObject(alvo);
        SerializedProperty propriedade = serializedObject.FindProperty(nomeCampo);
        return propriedade == null ? string.Empty : propriedade.stringValue.Trim();
    }

    private static void DefinirStringSerializada(UnityEngine.Object alvo, string nomeCampo, string valor)
    {
        SerializedObject serializedObject = new SerializedObject(alvo);
        SerializedProperty propriedade = serializedObject.FindProperty(nomeCampo);
        if (propriedade == null)
            return;

        propriedade.stringValue = valor;
        serializedObject.ApplyModifiedProperties();
    }

    private static string CriarGuidUnico(HashSet<string> idsUsados)
    {
        string id;
        do
        {
            id = Guid.NewGuid().ToString("N");
        }
        while (!idsUsados.Add(id));

        return id;
    }

    private static string ObterCaminhoHierarquia(Transform transform)
    {
        if (transform == null)
            return "(sem transform)";

        string caminho = transform.name;
        Transform atual = transform.parent;
        while (atual != null)
        {
            caminho = atual.name + "/" + caminho;
            atual = atual.parent;
        }

        return caminho;
    }
}
