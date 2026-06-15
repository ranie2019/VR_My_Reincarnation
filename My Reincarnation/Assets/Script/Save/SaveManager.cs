using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instancia { get; private set; }
    private const string TipoObjetoItemPersistente = "ItemPersistente";
    private const string PrefixoObjetoItemPersistente = "ITEM_";

    [Header("Configuracao")]
    [SerializeField] private bool autoCarregar = true;
    [SerializeField] private bool salvarAoSair = true;
    [SerializeField] private bool debugSave = true;
    [SerializeField] private string nomeArquivoSave = "save_teste.json";
    [Tooltip("Nao e seguranca real. Apenas dificulta leitura casual do JSON.")]
    [SerializeField] private bool usarBase64 = false;

    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private bool salvarPosicaoPlayer = true;
    [SerializeField] private bool salvarRotacaoPlayer = true;

    [Header("Inventario")]
    [SerializeField] private bool salvarInventario = true;

    private readonly List<ISalvavel> objetosRegistrados = new List<ISalvavel>();

    private string CaminhoSave
    {
        get
        {
            string arquivo = string.IsNullOrWhiteSpace(nomeArquivoSave)
                ? "save_teste.json"
                : nomeArquivoSave.Trim();

            return Path.Combine(Application.persistentDataPath, arquivo);
        }
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Debug.LogWarning("[SaveManager] Existe mais de um SaveManager na cena. Removendo duplicado.", this);
            Destroy(gameObject);
            return;
        }

        Instancia = this;
    }

    private void Start()
    {
        RegistrarObjetosSalvaveisNaCena();

        if (autoCarregar && ExisteSave())
            CarregarJogo();
    }

    private void OnApplicationQuit()
    {
        if (salvarAoSair)
            SalvarJogo();
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }

    [ContextMenu("Salvar Jogo")]
    public void SalvarJogo()
    {
        RegistrarObjetosSalvaveisNaCena();

        SaveData data = new SaveData
        {
            versaoSave = "0.1-teste",
            dataSave = DateTime.Now.ToString("O"),
            player = CriarPlayerSaveData(),
            inventario = salvarInventario ? CapturarInventarioAtual() : new List<InventorySaveData>(),
            objetosCena = CapturarObjetosCena()
        };

        string json = JsonUtility.ToJson(data, true);
        string conteudo = usarBase64
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            : json;

        string diretorio = Path.GetDirectoryName(CaminhoSave);
        if (!string.IsNullOrEmpty(diretorio))
            Directory.CreateDirectory(diretorio);

        File.WriteAllText(CaminhoSave, conteudo, Encoding.UTF8);
        Log($"Jogo salvo em: {CaminhoSave}");
    }

    [ContextMenu("Carregar Jogo")]
    public void CarregarJogo()
    {
        if (!ExisteSave())
        {
            Log("Nenhum save encontrado para carregar.");
            return;
        }

        RegistrarObjetosSalvaveisNaCena();

        string conteudo = File.ReadAllText(CaminhoSave, Encoding.UTF8);
        string json = DecodificarConteudoSave(conteudo);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        if (data == null)
        {
            Debug.LogWarning("[SaveManager] Falha ao ler o save. Dados vazios ou invalidos.", this);
            return;
        }

        HashSet<string> instanciaIdsInventario = salvarInventario
            ? ExtrairInstanciaIdsInventario(data.inventario)
            : new HashSet<string>();

        RestaurarPlayer(data.player);

        if (salvarInventario)
            RestaurarInventario(data.inventario);

        RestaurarObjetosCena(data.objetosCena, instanciaIdsInventario);

        Log($"Jogo carregado de: {CaminhoSave}");
    }

    [ContextMenu("Apagar Save")]
    public void ApagarSave()
    {
        if (!ExisteSave())
        {
            Log("Nao existe save para apagar.");
            return;
        }

        File.Delete(CaminhoSave);
        Log($"Save apagado: {CaminhoSave}");
    }

    [ContextMenu("Mostrar Caminho Do Save")]
    public void MostrarCaminhoDoSave()
    {
        Debug.Log($"[SaveManager] Caminho do save: {CaminhoSave}", this);
    }

    public bool ExisteSave()
    {
        return File.Exists(CaminhoSave);
    }

    public void RegistrarObjeto(ISalvavel objeto)
    {
        if (ObjetoEhNulo(objeto) || objetosRegistrados.Contains(objeto))
            return;

        objetosRegistrados.Add(objeto);
    }

    public void RemoverObjeto(ISalvavel objeto)
    {
        if (objeto == null)
            return;

        objetosRegistrados.Remove(objeto);
    }

    private PlayerSaveData CriarPlayerSaveData()
    {
        PlayerSaveData data = new PlayerSaveData
        {
            sceneName = SceneManager.GetActiveScene().name
        };

        if (player != null)
        {
            Vector3 posicao = player.position;
            Vector3 rotacao = player.eulerAngles;

            data.posX = posicao.x;
            data.posY = posicao.y;
            data.posZ = posicao.z;
            data.rotX = rotacao.x;
            data.rotY = rotacao.y;
            data.rotZ = rotacao.z;

            StatusPlayer status = player.GetComponentInChildren<StatusPlayer>(true);
            if (status != null)
                CapturarStatusPlayer(status, data);
        }

        return data;
    }

    private void RestaurarPlayer(PlayerSaveData data)
    {
        if (data == null || player == null)
            return;

        if (salvarPosicaoPlayer)
            player.position = new Vector3(data.posX, data.posY, data.posZ);

        if (salvarRotacaoPlayer)
            player.rotation = Quaternion.Euler(data.rotX, data.rotY, data.rotZ);

        StatusPlayer status = player.GetComponentInChildren<StatusPlayer>(true);
        if (status != null)
            RestaurarStatusPlayer(status, data);
    }

    private List<SceneObjectSaveData> CapturarObjetosCena()
    {
        List<SceneObjectSaveData> dados = new List<SceneObjectSaveData>();
        HashSet<string> idsUsados = new HashSet<string>();

        for (int i = objetosRegistrados.Count - 1; i >= 0; i--)
        {
            ISalvavel objeto = objetosRegistrados[i];
            if (ObjetoEhNulo(objeto))
            {
                objetosRegistrados.RemoveAt(i);
                continue;
            }

            if (ObjetoEstaNoInventario(objeto))
                continue;

            if (ObjetoPossuiItemPersistente(objeto))
                continue;

            string id = objeto.ObterId();
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!idsUsados.Add(id))
            {
                Debug.LogWarning($"[SaveManager] ID duplicado ignorado no save: {id}", this);
                continue;
            }

            SceneObjectSaveData estado = objeto.SalvarEstado();
            if (estado != null)
                dados.Add(estado);
        }

        CapturarItensSoltosCena(dados, idsUsados);
        return dados;
    }

    private void RestaurarObjetosCena(List<SceneObjectSaveData> objetosCena, HashSet<string> instanciaIdsInventario)
    {
        if (objetosCena == null)
            return;

        Dictionary<string, ISalvavel> porId = CriarMapaObjetosRegistrados();

        for (int i = 0; i < objetosCena.Count; i++)
        {
            SceneObjectSaveData data = objetosCena[i];
            if (data == null || string.IsNullOrWhiteSpace(data.objectId))
                continue;

            if (EhDadoItemPersistente(data))
                continue;

            if (porId.TryGetValue(data.objectId, out ISalvavel objeto) && !ObjetoEhNulo(objeto))
            {
                objeto.CarregarEstado(data);
                continue;
            }

            if (debugSave)
                Debug.LogWarning($"[SaveManager] Objeto salvo nao existe nesta cena: {data.objectId}", this);
        }

        RestaurarItensSoltosCena(objetosCena, instanciaIdsInventario);
    }

    private void CapturarItensSoltosCena(List<SceneObjectSaveData> dados, HashSet<string> idsUsados)
    {
        ItemPersistente[] itens = FindObjectsByType<ItemPersistente>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int totalSalvo = 0;

        for (int i = 0; i < itens.Length; i++)
        {
            ItemPersistente item = itens[i];
            if (!ItemDeveSerSalvoComoCena(item))
                continue;

            ItemPersistente.EstadoCenaItem estadoItem = item.CriarEstadoCena();
            if (estadoItem == null ||
                string.IsNullOrWhiteSpace(estadoItem.itemId) ||
                string.IsNullOrWhiteSpace(estadoItem.instanciaId))
            {
                Debug.LogWarning($"[SaveManager] Item solto ignorado por falta de itemId/instanciaId: {item.name}", this);
                continue;
            }

            string objectId = PrefixoObjetoItemPersistente + estadoItem.instanciaId;
            if (!idsUsados.Add(objectId))
            {
                Debug.LogWarning($"[SaveManager] Item solto com instanciaId duplicado ignorado: {estadoItem.instanciaId}", this);
                continue;
            }

            SceneObjectSaveData data = CriarSceneObjectDataDoItem(item, estadoItem, objectId);
            dados.Add(data);
            totalSalvo++;

            Log($"Item solto salvo: {estadoItem.itemId} | instancia {estadoItem.instanciaId} | inventario? false | pos {item.transform.position}");
        }

        Log($"Itens soltos salvos na cena: {totalSalvo}");
    }

    private SceneObjectSaveData CriarSceneObjectDataDoItem(
        ItemPersistente item,
        ItemPersistente.EstadoCenaItem estadoItem,
        string objectId)
    {
        Transform itemTransform = item.transform;
        Vector3 posicao = itemTransform.position;
        Vector3 rotacao = itemTransform.eulerAngles;
        Vector3 escala = itemTransform.localScale;

        return new SceneObjectSaveData
        {
            objectId = objectId,
            tipoObjeto = TipoObjetoItemPersistente,
            prefabItemId = estadoItem.itemId,
            instanciaId = estadoItem.instanciaId,
            ativo = item.gameObject.activeSelf,
            posX = posicao.x,
            posY = posicao.y,
            posZ = posicao.z,
            rotX = rotacao.x,
            rotY = rotacao.y,
            rotZ = rotacao.z,
            scaleX = escala.x,
            scaleY = escala.y,
            scaleZ = escala.z,
            estadoJson = JsonUtility.ToJson(estadoItem)
        };
    }

    private void RestaurarItensSoltosCena(List<SceneObjectSaveData> objetosCena, HashSet<string> instanciaIdsInventario)
    {
        if (objetosCena == null)
            return;

        List<SceneObjectSaveData> itensSalvos = FiltrarDadosItensPersistentes(objetosCena);
        if (itensSalvos.Count == 0)
        {
            Log("Save nao possui itens soltos da cena para restaurar.");
            return;
        }

        ItemDatabaseLocal database = ItemDatabaseLocal.Instancia != null
            ? ItemDatabaseLocal.Instancia
            : FindFirstObjectByType<ItemDatabaseLocal>();

        List<ItemPersistente> existentesAntes = ObterItensPersistentesSoltosCena();
        HashSet<ItemPersistente> itensUsados = new HashSet<ItemPersistente>();
        HashSet<string> instanciaIdsSalvos = new HashSet<string>();
        HashSet<string> instanciaIdsCarregados = new HashSet<string>();

        int itensCarregados = 0;

        for (int i = 0; i < itensSalvos.Count; i++)
        {
            SceneObjectSaveData data = itensSalvos[i];
            ItemPersistente.EstadoCenaItem estadoItem = LerEstadoItemCena(data);
            if (estadoItem == null || string.IsNullOrWhiteSpace(estadoItem.itemId))
            {
                Debug.LogWarning($"[SaveManager] Item solto sem itemId no save: {data.objectId}", this);
                continue;
            }

            string instanciaId = NormalizarInstanciaId(estadoItem.instanciaId);
            if (!InstanciaIdValido(instanciaId))
            {
                Debug.LogWarning($"[SaveManager] Item solto ignorado por falta de instanciaId valido. itemId: {estadoItem.itemId} | objectId: {data.objectId}", this);
                continue;
            }

            estadoItem.instanciaId = instanciaId;
            if (instanciaIdsInventario != null && instanciaIdsInventario.Contains(instanciaId))
            {
                Log($"Item de cena ignorado porque pertence ao inventario salvo: {estadoItem.itemId} | instancia {instanciaId}");
                continue;
            }

            if (!instanciaIdsCarregados.Add(instanciaId))
            {
                Debug.LogWarning($"[SaveManager] instanciaId duplicado no save ignorado no load: {instanciaId} | itemId: {estadoItem.itemId}", this);
                continue;
            }

            instanciaIdsSalvos.Add(instanciaId);

            ItemPersistente item = EncontrarItemSoltoPorInstancia(existentesAntes, instanciaId, itensUsados);

            if (item == null)
            {
                ItemPersistente itemMigravel = EncontrarItemSoltoComIdRuntimeParaMigrar(
                    existentesAntes,
                    estadoItem.itemId,
                    itensUsados,
                    out int candidatosMigracao);

                if (itemMigravel != null)
                {
                    Debug.LogWarning(
                        $"[SaveManager] Migrando item de cena com instanciaId temporario para o instanciaId salvo. itemId: {estadoItem.itemId} | instanciaId salvo: {instanciaId} | posicao salva: {new Vector3(data.posX, data.posY, data.posZ)}",
                        itemMigravel);
                    item = itemMigravel;
                }
                else if (candidatosMigracao > 1)
                {
                    Debug.LogWarning(
                        $"[SaveManager] Item solto nao restaurado porque existem {candidatosMigracao} itens com ID temporario e mesmo itemId na cena. itemId: {estadoItem.itemId} | instanciaId salvo: {instanciaId}. Rode Tools/Save/Gerar IDs dos Itens Persistentes da Cena e crie um save novo.",
                        this);
                    continue;
                }
            }

            if (item == null)
            {
                ItemPersistente itemMesmoTipo = EncontrarItemSoltoMesmoTipoParaBloquearDuplicacao(
                    existentesAntes,
                    estadoItem.itemId,
                    itensUsados);

                if (itemMesmoTipo != null)
                {
                    Debug.LogWarning(
                        $"[SaveManager] Item salvo nao foi instanciado para evitar duplicacao. O save aponta para itemId '{estadoItem.itemId}' instanciaId '{instanciaId}', mas ja existe um item solto do mesmo tipo na cena com instanciaId '{itemMesmoTipo.ObterInstanciaIdSemGerar()}'. Isso normalmente indica save antigo/contaminado ou IDs da cena gerados depois do save. Rode Tools/Save/Validar IDs Persistentes e crie um save novo/limpe o save antigo.",
                        itemMesmoTipo);
                    continue;
                }

                if (database == null)
                {
                    Debug.LogWarning($"[SaveManager] ItemDatabaseLocal nao encontrado. Item solto nao sera recriado. itemId: {estadoItem.itemId} | instanciaId: {estadoItem.instanciaId}", this);
                    continue;
                }

                GameObject prefab = database.ObterPrefab(estadoItem.itemId);
                if (prefab == null)
                {
                    Debug.LogWarning($"[SaveManager] Prefab nao encontrado no ItemDatabaseLocal para itemId: {estadoItem.itemId} | instanciaId: {estadoItem.instanciaId}", this);
                    continue;
                }

                GameObject instancia = Instantiate(prefab);
                instancia.name = SlotInventario.LimparNomeItem(prefab.name);
                item = instancia.GetComponent<ItemPersistente>();
                if (item == null)
                    item = instancia.AddComponent<ItemPersistente>();
            }

            RestaurarItemSoltoCena(item, data, estadoItem);
            itensUsados.Add(item);
            itensCarregados++;

            Log($"Item solto carregado/restaurado na cena: {estadoItem.itemId} | instancia {estadoItem.instanciaId} | inventario? false | pos {item.transform.position}");
        }

        RemoverDuplicatasItensSoltos(existentesAntes, itensUsados, instanciaIdsSalvos);
        Log($"Itens soltos carregados da cena: {itensCarregados}");
    }

    private HashSet<string> ExtrairInstanciaIdsInventario(List<InventorySaveData> itens)
    {
        HashSet<string> resultado = new HashSet<string>();
        if (itens == null)
            return resultado;

        for (int i = 0; i < itens.Count; i++)
        {
            InventorySaveData item = itens[i];
            if (item == null)
                continue;

            AdicionarInstanciaIdSeValido(resultado, item.instanciaId);

            if (item.instanciaIds == null)
                continue;

            for (int j = 0; j < item.instanciaIds.Count; j++)
                AdicionarInstanciaIdSeValido(resultado, item.instanciaIds[j]);
        }

        return resultado;
    }

    private void AdicionarInstanciaIdSeValido(HashSet<string> destino, string instanciaId)
    {
        if (destino == null || string.IsNullOrWhiteSpace(instanciaId))
            return;

        destino.Add(instanciaId.Trim());
    }

    private List<SceneObjectSaveData> FiltrarDadosItensPersistentes(List<SceneObjectSaveData> objetosCena)
    {
        List<SceneObjectSaveData> resultado = new List<SceneObjectSaveData>();

        for (int i = 0; i < objetosCena.Count; i++)
        {
            SceneObjectSaveData data = objetosCena[i];
            if (data != null && EhDadoItemPersistente(data))
                resultado.Add(data);
        }

        return resultado;
    }

    private ItemPersistente.EstadoCenaItem LerEstadoItemCena(SceneObjectSaveData data)
    {
        if (data == null)
            return null;

        ItemPersistente.EstadoCenaItem estadoItem = ItemPersistente.LerEstadoCenaJson(data.estadoJson);
        if (estadoItem == null)
            estadoItem = new ItemPersistente.EstadoCenaItem();

        if (string.IsNullOrWhiteSpace(estadoItem.itemId))
            estadoItem.itemId = data.prefabItemId;

        if (string.IsNullOrWhiteSpace(estadoItem.instanciaId))
            estadoItem.instanciaId = !string.IsNullOrWhiteSpace(data.instanciaId)
                ? NormalizarInstanciaId(data.instanciaId)
                : ObterInstanciaIdPeloObjectId(data.objectId);
        else
            estadoItem.instanciaId = NormalizarInstanciaId(estadoItem.instanciaId);

        return estadoItem;
    }

    private string ObterInstanciaIdPeloObjectId(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
            return string.Empty;

        if (!objectId.StartsWith(PrefixoObjetoItemPersistente, StringComparison.Ordinal))
            return string.Empty;

        return NormalizarInstanciaId(objectId.Substring(PrefixoObjetoItemPersistente.Length));
    }

    private bool InstanciaIdValido(string instanciaId)
    {
        return !string.IsNullOrWhiteSpace(instanciaId);
    }

    private string NormalizarInstanciaId(string instanciaId)
    {
        return string.IsNullOrWhiteSpace(instanciaId) ? string.Empty : instanciaId.Trim();
    }

    private void RestaurarItemSoltoCena(
        ItemPersistente item,
        SceneObjectSaveData data,
        ItemPersistente.EstadoCenaItem estadoItem)
    {
        item.AplicarEstadoCena(estadoItem);
        item.MarcarComoSoltoNaCena();

        Transform itemTransform = item.transform;
        itemTransform.SetParent(null, true);
        itemTransform.position = new Vector3(data.posX, data.posY, data.posZ);
        itemTransform.rotation = Quaternion.Euler(data.rotX, data.rotY, data.rotZ);
        itemTransform.localScale = new Vector3(data.scaleX, data.scaleY, data.scaleZ);

        if (item.gameObject.activeSelf != data.ativo)
            item.gameObject.SetActive(data.ativo);

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.detectCollisions = true;
            rb.WakeUp();
        }
    }

    private ItemPersistente EncontrarItemSoltoPorInstancia(
        List<ItemPersistente> itens,
        string instanciaId,
        HashSet<ItemPersistente> itensUsados)
    {
        if (string.IsNullOrWhiteSpace(instanciaId))
            return null;

        for (int i = 0; i < itens.Count; i++)
        {
            ItemPersistente item = itens[i];
            if (item == null || itensUsados.Contains(item))
                continue;

            if (ItemEstaNoInventarioOuSlot(item))
                continue;

            if (string.Equals(item.ObterInstanciaId(), instanciaId.Trim(), StringComparison.Ordinal))
                return item;
        }

        return null;
    }

    private ItemPersistente EncontrarItemSoltoMesmoTipoParaBloquearDuplicacao(
        List<ItemPersistente> itens,
        string itemId,
        HashSet<ItemPersistente> itensUsados)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        string itemIdNormalizado = itemId.Trim();

        for (int i = 0; i < itens.Count; i++)
        {
            ItemPersistente item = itens[i];
            if (item == null || itensUsados.Contains(item))
                continue;

            if (ItemEstaNoInventarioOuSlot(item))
                continue;

            string itemIdCena = item.ObterItemIdSemFallback();
            if (string.Equals(itemIdCena, itemIdNormalizado, StringComparison.Ordinal))
                return item;
        }

        return null;
    }

    private ItemPersistente EncontrarItemSoltoComIdRuntimeParaMigrar(
        List<ItemPersistente> itens,
        string itemId,
        HashSet<ItemPersistente> itensUsados,
        out int candidatos)
    {
        candidatos = 0;

        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        string itemIdNormalizado = itemId.Trim();
        ItemPersistente encontrado = null;

        for (int i = 0; i < itens.Count; i++)
        {
            ItemPersistente item = itens[i];
            if (item == null || itensUsados.Contains(item))
                continue;

            if (ItemEstaNoInventarioOuSlot(item))
                continue;

            if (!item.InstanciaIdFoiGeradoEmRuntime())
                continue;

            string itemIdCena = item.ObterItemIdSemFallback();
            if (!string.Equals(itemIdCena, itemIdNormalizado, StringComparison.Ordinal))
                continue;

            candidatos++;
            encontrado = item;
        }

        return candidatos == 1 ? encontrado : null;
    }

    private List<ItemPersistente> ObterItensPersistentesSoltosCena()
    {
        ItemPersistente[] itens = FindObjectsByType<ItemPersistente>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        List<ItemPersistente> resultado = new List<ItemPersistente>();
        for (int i = 0; i < itens.Length; i++)
        {
            if (itens[i] != null && !ItemEstaNoInventarioOuSlot(itens[i]))
                resultado.Add(itens[i]);
        }

        return resultado;
    }

    private void RemoverDuplicatasItensSoltos(
        List<ItemPersistente> existentesAntes,
        HashSet<ItemPersistente> itensUsados,
        HashSet<string> instanciaIdsSalvos)
    {
        int removidos = 0;

        for (int i = 0; i < existentesAntes.Count; i++)
        {
            ItemPersistente item = existentesAntes[i];
            if (item == null || itensUsados.Contains(item))
                continue;

            if (ItemEstaNoInventarioOuSlot(item))
                continue;

            string instanciaId = item.ObterInstanciaId();
            if (string.IsNullOrWhiteSpace(instanciaId) || !instanciaIdsSalvos.Contains(instanciaId.Trim()))
                continue;

            Destroy(item.gameObject);
            removidos++;
        }

        if (removidos > 0)
            Log($"Itens soltos duplicados removidos da cena: {removidos}");
    }

    private bool ItemDeveSerSalvoComoCena(ItemPersistente item)
    {
        return item != null &&
               item.DeveSalvarComoCena() &&
               !ItemEstaNoInventarioOuSlot(item);
    }

    private bool ItemEstaNoInventarioOuSlot(ItemPersistente item)
    {
        return item != null &&
               (item.EstaNoInventario() || item.GetComponentInParent<SlotInventario>(true) != null);
    }

    private bool EhDadoItemPersistente(SceneObjectSaveData data)
    {
        if (data == null)
            return false;

        if (string.Equals(data.tipoObjeto, TipoObjetoItemPersistente, StringComparison.Ordinal))
            return true;

        return !string.IsNullOrWhiteSpace(data.objectId) &&
               data.objectId.StartsWith(PrefixoObjetoItemPersistente, StringComparison.Ordinal);
    }

    private Dictionary<string, ISalvavel> CriarMapaObjetosRegistrados()
    {
        RegistrarObjetosSalvaveisNaCena();

        Dictionary<string, ISalvavel> mapa = new Dictionary<string, ISalvavel>();
        for (int i = 0; i < objetosRegistrados.Count; i++)
        {
            ISalvavel objeto = objetosRegistrados[i];
            if (ObjetoEhNulo(objeto))
                continue;

            string id = objeto.ObterId();
            if (string.IsNullOrWhiteSpace(id) || mapa.ContainsKey(id))
                continue;

            mapa.Add(id, objeto);
        }

        return mapa;
    }

    private void RegistrarObjetosSalvaveisNaCena()
    {
        MonoBehaviour[] componentes = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < componentes.Length; i++)
        {
            if (componentes[i] is ISalvavel salvavel)
                RegistrarObjeto(salvavel);
        }
    }

    private List<InventorySaveData> CapturarInventarioAtual()
    {
        IInventarioSalvavel inventarioSalvavel = EncontrarInventarioSalvavel();
        if (inventarioSalvavel == null)
        {
            if (debugSave)
                Debug.LogWarning("[SaveManager] Inventario salvavel nao encontrado. Adicione InventarioSaveAdapter na cena.", this);

            return new List<InventorySaveData>();
        }

        List<InventorySaveData> itens = inventarioSalvavel.SalvarInventario() ?? new List<InventorySaveData>();
        Log($"Itens salvos no inventario: {itens.Count}");

        for (int i = 0; i < itens.Count; i++)
        {
            InventorySaveData item = itens[i];
            if (item != null)
                Log($"Inventario salvo: {item.itemId} | instancia {item.instanciaId} | inventario? true | slot {item.slot} | quantidade {item.quantidade}");
        }

        return itens;
    }

    private void RestaurarInventario(List<InventorySaveData> itens)
    {
        if (itens == null)
            itens = new List<InventorySaveData>();

        IInventarioSalvavel inventarioSalvavel = EncontrarInventarioSalvavel();
        if (inventarioSalvavel == null)
        {
            if (debugSave)
                Debug.LogWarning("[SaveManager] Inventario salvavel nao encontrado. Nao foi possivel restaurar itens.", this);

            return;
        }

        Log($"Restaurando inventario com {itens.Count} entradas.");
        inventarioSalvavel.CarregarInventario(itens);
    }

    private IInventarioSalvavel EncontrarInventarioSalvavel()
    {
        InventarioSaveAdapter adapter = FindFirstObjectByType<InventarioSaveAdapter>();
        if (adapter != null)
            return adapter;

        MonoBehaviour[] componentes = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < componentes.Length; i++)
        {
            if (componentes[i] is IInventarioSalvavel inventarioSalvavel)
                return inventarioSalvavel;
        }

        return null;
    }

    private void CapturarStatusPlayer(StatusPlayer status, PlayerSaveData data)
    {
        data.level = ObterInteiroStatus(status, "GetNivel", "nivel");
        data.xp = ObterInteiroStatus(status, "GetExperienciaAtual", "experienciaAtual");
        data.vidaAtual = ObterInteiroStatus(status, "GetVidaAtual", "vidaAtual");
        data.vidaMaxima = ObterInteiroStatus(status, "GetVidaMaxima", "vidaMaxima");
        data.manaAtual = ObterInteiroStatus(status, "GetManaAtual", "manaAtual");
        data.manaMaxima = ObterInteiroStatus(status, "GetManaMaxima", "manaMaxima");
        data.statusDetalhadoSalvo = true;
        data.pontosStatusDisponiveis = ObterInteiroStatus(status, "GetPontosStatusDisponiveis", "pontosStatusDisponiveis");
        data.forca = ObterInteiroStatus(status, "GetForca", "forca");
        data.constituicao = ObterInteiroStatus(status, "GetConstituicao", "constituicao");
        data.agilidade = ObterInteiroStatus(status, "GetAgilidade", "agilidade");
        data.velocidade = ObterInteiroStatus(status, "GetVelocidade", "velocidade");
        data.inteligencia = ObterInteiroStatus(status, "GetInteligencia", "inteligencia");
        data.espirito = ObterInteiroStatus(status, "GetEspirito", "espirito");
        data.sorte = ObterInteiroStatus(status, "GetSorte", "sorte");
        data.destreza = ObterInteiroStatus(status, "GetDestreza", "destreza");
        data.vigor = ObterInteiroStatus(status, "GetVigor", "vigor");
        data.percepcao = ObterInteiroStatus(status, "GetPercepcao", "percepcao");
        data.resistencia = ObterInteiroStatus(status, "GetResistencia", "resistencia");
        data.mana = ObterInteiroStatus(status, "GetMana", "mana");
        data.carisma = ObterInteiroStatus(status, "GetCarisma", "carisma");
        data.critico = ObterInteiroStatus(status, "GetCritico", "critico");
        data.defesa = ObterInteiroStatus(status, "GetDefesa", "defesa");
    }

    private void RestaurarStatusPlayer(StatusPlayer status, PlayerSaveData data)
    {
        // O StatusPlayer atual nao possui setters publicos para todos os campos.
        // Para teste local, usamos reflexao nos campos serializados existentes.
        DefinirInteiroStatus(status, "nivel", data.level);
        DefinirInteiroStatus(status, "experienciaAtual", data.xp);
        DefinirInteiroStatus(status, "vidaAtual", data.vidaAtual);
        DefinirInteiroStatus(status, "vidaMaxima", data.vidaMaxima);
        DefinirInteiroStatus(status, "manaAtual", data.manaAtual);
        DefinirInteiroStatus(status, "manaMaxima", data.manaMaxima);
        DefinirInteiroStatus(status, "forca", data.forca);
        DefinirInteiroStatus(status, "constituicao", data.constituicao);
        DefinirInteiroStatus(status, "agilidade", data.agilidade);
        DefinirInteiroStatus(status, "velocidade", data.velocidade);
        DefinirInteiroStatus(status, "inteligencia", data.inteligencia);
        DefinirInteiroStatus(status, "espirito", data.espirito);
        DefinirInteiroStatus(status, "sorte", data.sorte);

        if (data.statusDetalhadoSalvo)
        {
            DefinirInteiroStatus(status, "pontosStatusDisponiveis", data.pontosStatusDisponiveis);
            DefinirInteiroStatus(status, "destreza", data.destreza);
            DefinirInteiroStatus(status, "vigor", data.vigor);
            DefinirInteiroStatus(status, "percepcao", data.percepcao);
            DefinirInteiroStatus(status, "resistencia", data.resistencia);
            DefinirInteiroStatus(status, "mana", data.mana);
            DefinirInteiroStatus(status, "carisma", data.carisma);
            DefinirInteiroStatus(status, "critico", data.critico);
            DefinirInteiroStatus(status, "defesa", data.defesa);
        }

        NotificarStatusAlterado(status);
    }

    private int ObterInteiroStatus(StatusPlayer status, string nomeGetter, string nomeCampo)
    {
        if (status == null)
            return 0;

        Type tipo = status.GetType();
        MethodInfo metodo = tipo.GetMethod(nomeGetter, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (metodo != null && metodo.ReturnType == typeof(int))
            return (int)metodo.Invoke(status, null);

        FieldInfo campo = tipo.GetField(nomeCampo, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (campo != null && campo.FieldType == typeof(int))
            return (int)campo.GetValue(status);

        return 0;
    }

    private void DefinirInteiroStatus(StatusPlayer status, string nomeCampo, int valor)
    {
        if (status == null)
            return;

        FieldInfo campo = status.GetType().GetField(
            nomeCampo,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (campo != null && campo.FieldType == typeof(int))
            campo.SetValue(status, valor);
    }

    private void NotificarStatusAlterado(StatusPlayer status)
    {
        MethodInfo metodo = status.GetType().GetMethod(
            "NotificarStatusAlterado",
            BindingFlags.Instance | BindingFlags.NonPublic);

        metodo?.Invoke(status, null);
    }

    private string DecodificarConteudoSave(string conteudo)
    {
        if (!usarBase64)
            return conteudo;

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(conteudo));
        }
        catch (Exception erro)
        {
            Debug.LogWarning($"[SaveManager] Falha ao decodificar Base64. Tentando ler como JSON puro. {erro.Message}", this);
            return conteudo;
        }
    }

    private bool ObjetoEhNulo(ISalvavel objeto)
    {
        if (objeto == null)
            return true;

        return objeto is UnityEngine.Object unityObject && unityObject == null;
    }

    private bool ObjetoEstaNoInventario(ISalvavel objeto)
    {
        if (!(objeto is Component componente))
            return false;

        ItemPersistente itemPersistente = componente.GetComponent<ItemPersistente>();
        if (itemPersistente != null && ItemEstaNoInventarioOuSlot(itemPersistente))
            return true;

        EstadoItemInventario estado = componente.GetComponent<EstadoItemInventario>();
        if (estado != null && estado.estaNoInventario)
            return true;

        return componente.GetComponentInParent<SlotInventario>(true) != null;
    }

    private bool ObjetoPossuiItemPersistente(ISalvavel objeto)
    {
        if (!(objeto is Component componente))
            return false;

        return componente.GetComponent<ItemPersistente>() != null;
    }

    private void Log(string mensagem)
    {
        if (debugSave)
            Debug.Log($"[SaveManager] {mensagem}", this);
    }
}
