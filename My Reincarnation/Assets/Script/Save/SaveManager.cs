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
    [SerializeField] private bool debugSave = false;
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

    public enum EstadoCarga { NaoIniciado, EmAndamento, Concluido, Falhou, RecuperacaoNecessaria, SessaoNova }
    public EstadoCarga EstadoCarregamento { get; private set; } = EstadoCarga.NaoIniciado;
    public string UltimoDiagnosticoCarga { get; private set; }
    public string ArquivoCarregado { get; private set; }

    public void NotificarFalhaRestauracao(string motivo)
    {
        EstadoCarregamento = EstadoCarga.RecuperacaoNecessaria;
        UltimoDiagnosticoCarga = motivo;
        Debug.LogError("[SaveManager] Salvamento bloqueado: " + motivo);
    }

    [Serializable]
    private class EnvelopeValidacao
    {
        public string versaoSave;
        public string dataSave;
        public PlayerSaveData player;
        public List<InventorySaveData> inventario;
        public List<SceneObjectSaveData> objetosCena;
    }

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
            { }
            Destroy(gameObject);
            return;
        }

        Instancia = this;
    }

    private void Start()
    {
        RegistrarObjetosSalvaveisNaCena();

        if (EstadoCarregamento != EstadoCarga.NaoIniciado) return;
        if (!ArquivoSaveSeguro.ExisteAlgumaGeracao(CaminhoSave))
            EstadoCarregamento = EstadoCarga.SessaoNova;
        else if (autoCarregar)
            CarregarJogo();
    }

    private void OnApplicationQuit()
    {
        if (salvarAoSair)
            SalvarJogo();
    }

    private void OnApplicationPause(bool pausado)
    {
        if (pausado && salvarAoSair)
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
        // Inclui chamadas automaticas de missoes e chamadas manuais: nenhuma pode contornar uma falha.
        if (Instancia != this || (EstadoCarregamento != EstadoCarga.Concluido && EstadoCarregamento != EstadoCarga.SessaoNova))
        {
            Debug.LogWarning("[SaveManager] Save nao gravado. Estado: " + EstadoCarregamento);
            return;
        }
        try
        {
            RegistrarObjetosSalvaveisNaCena();
            SaveData data = new SaveData
            {
                versaoSave = SaveData.VersaoAtual,
                dataSave = DateTime.Now.ToString("O"),
                player = CriarPlayerSaveData(),
                inventario = salvarInventario ? CapturarInventarioAtual() : new List<InventorySaveData>(),
                objetosCena = CapturarObjetosCena(),
                missoes = CapturarMissoes()
            };
            string json = JsonUtility.ToJson(data, true);
            string conteudo = usarBase64 ? Convert.ToBase64String(Encoding.UTF8.GetBytes(json)) : json;
            EscreverArquivoSaveSeguro(CaminhoSave, conteudo);
        }
        catch (Exception erro)
        {
            Debug.LogError($"[SaveManager] Save nao confirmado; geracoes existentes preservadas: {erro}");
        }
    }

    [ContextMenu("Carregar Jogo")]
    public void CarregarJogo()
    {
        if (Instancia != this || EstadoCarregamento == EstadoCarga.EmAndamento) return;
        // Ausencia de arquivo apos uma falha nao autoriza sobrescrita/novo jogo implicitamente.
        if (!ArquivoSaveSeguro.ExisteAlgumaGeracao(CaminhoSave))
        {
            if (EstadoCarregamento == EstadoCarga.NaoIniciado) EstadoCarregamento = EstadoCarga.SessaoNova;
            return;
        }
        EstadoCarregamento = EstadoCarga.EmAndamento;
        ArquivoCarregado = null;
        try
        {
            RegistrarObjetosSalvaveisNaCena();
            SaveData escolhido = null;
            var falhas = new List<string>();
            foreach (string caminho in ArquivoSaveSeguro.Candidatos(CaminhoSave))
            {
                if (!File.Exists(caminho)) continue;
                try
                {
                    SaveData candidato = InterpretarEValidarSave(File.ReadAllText(caminho, Encoding.UTF8));
                    ValidarAplicacao(candidato);
                    escolhido = candidato;
                    ArquivoCarregado = caminho;
                    break;
                }
                catch (Exception ex) { falhas.Add(caminho + ": " + ex.Message); }
            }
            if (escolhido == null)
                throw new InvalidDataException("Nenhuma geracao pode ser carregada. " + string.Join(" | ", falhas));

            // Nao tenta outro backup depois de iniciar mutacoes na cena.
            if (salvarInventario) RestaurarInventario(escolhido.inventario);
            RestaurarPlayer(escolhido.player);
            RestaurarObjetosCena(escolhido.objetosCena, ExtrairInstanciaIdsInventario(escolhido.inventario));
            RestaurarMissoes(escolhido.missoes);
            if (EstadoCarregamento != EstadoCarga.EmAndamento)
                throw new InvalidOperationException("Um componente informou falha durante a aplicacao.");
            EstadoCarregamento = EstadoCarga.Concluido;
            UltimoDiagnosticoCarga = "Carga concluida: " + ArquivoCarregado;
            Debug.Log("[SaveManager] " + UltimoDiagnosticoCarga +
                (falhas.Count > 0 ? ". Recuperacao; originais preservados. " + string.Join(" | ", falhas) : ""));
        }
        catch (Exception erro)
        {
            EstadoCarregamento = EstadoCarga.Falhou;
            NotificarFalhaRestauracao(erro.Message);
        }
    }

    private SaveData InterpretarEValidarSave(string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo)) throw new InvalidDataException("Arquivo vazio.");
        string json = conteudo.Trim().TrimStart('\uFEFF');
        if (!json.StartsWith("{", StringComparison.Ordinal))
            json = Encoding.UTF8.GetString(Convert.FromBase64String(json)).Trim().TrimStart('\uFEFF');
        if (!json.StartsWith("{", StringComparison.Ordinal) || !json.EndsWith("}", StringComparison.Ordinal))
            throw new InvalidDataException("Documento JSON incompleto.");
        EnvelopeValidacao envelope = JsonUtility.FromJson<EnvelopeValidacao>(json);
        if (envelope == null || string.IsNullOrWhiteSpace(envelope.versaoSave) ||
            string.IsNullOrWhiteSpace(envelope.dataSave) || envelope.player == null ||
            envelope.inventario == null || envelope.objetosCena == null)
            throw new InvalidDataException("Estrutura obrigatoria do save ausente.");
        string numero = envelope.versaoSave.Split('-')[0];
        if (!Version.TryParse(numero, out Version versao) || versao.Major != 0 || versao.Minor > 4)
            throw new InvalidDataException("Versao de save nao suportada: " + envelope.versaoSave);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        var slots = new HashSet<int>();
        var ids = new HashSet<string>();
        foreach (InventorySaveData item in data.inventario)
        {
            if (item == null || item.slot < 0 || !slots.Add(item.slot) || item.quantidade < 1 || item.quantidade > 99 ||
                (string.IsNullOrWhiteSpace(item.itemId) && string.IsNullOrWhiteSpace(item.nomeItem)) ||
                float.IsNaN(item.durabilidade) || float.IsInfinity(item.durabilidade))
                throw new InvalidDataException("Entrada de inventario invalida.");
            item.ValidarEstadosIndividuais();
            if (versao.Minor >= 4 && item.versaoEstadosIndividuais != 1)
                throw new InvalidDataException("Save novo sem estados individuais.");
            var locais = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(item.instanciaId)) locais.Add(item.instanciaId.Trim());
            var lista = new HashSet<string>();
            if (item.instanciaIds != null)
                foreach (string id in item.instanciaIds)
                {
                    if (string.IsNullOrWhiteSpace(id) || !lista.Add(id.Trim()))
                        throw new InvalidDataException("Lista de identidades invalida.");
                    locais.Add(id.Trim());
                }
            if (locais.Count > item.quantidade) throw new InvalidDataException("IDs excedem quantidade.");
            foreach (string id in locais)
                if (!InstanciaIdValido(id) || !ids.Add(id)) throw new InvalidDataException("ID invalido ou duplicado: " + id);
        }
        var objetos = new HashSet<string>();
        foreach (SceneObjectSaveData objeto in data.objetosCena)
        {
            if (objeto == null || string.IsNullOrWhiteSpace(objeto.objectId) || !objetos.Add(objeto.objectId) ||
                !VetorFinito(new Vector3(objeto.posX, objeto.posY, objeto.posZ)) ||
                !VetorFinito(new Vector3(objeto.rotX, objeto.rotY, objeto.rotZ)) ||
                !VetorFinito(new Vector3(objeto.scaleX, objeto.scaleY, objeto.scaleZ)))
                throw new InvalidDataException("Objeto de cena invalido.");
            if (EhDadoItemPersistente(objeto))
            {
                if (!string.IsNullOrWhiteSpace(objeto.estadoJson) && ItemPersistente.LerEstadoCenaJson(objeto.estadoJson) == null)
                    throw new InvalidDataException("Estado de item invalido: " + objeto.objectId);
                var estado = LerEstadoItemCena(objeto);
                if (string.IsNullOrWhiteSpace(estado.itemId)) throw new InvalidDataException("Tipo de item ausente.");
                if (!string.IsNullOrWhiteSpace(estado.instanciaId) &&
                    (!InstanciaIdValido(estado.instanciaId) || !ids.Add(estado.instanciaId)))
                    throw new InvalidDataException("Identidade de cena invalida ou repetida.");
            }
        }
        if (!VetorFinito(new Vector3(data.player.posX, data.player.posY, data.player.posZ)) ||
            !VetorFinito(new Vector3(data.player.rotX, data.player.rotY, data.player.rotZ)))
            throw new InvalidDataException("Transform do player nao finito.");
        if (data.missoes != null)
            foreach (MissionSaveData missao in data.missoes)
                if (missao == null || string.IsNullOrWhiteSpace(missao.gerenciadorId) || string.IsNullOrWhiteSpace(missao.idMissao) || missao.estado < 0 || missao.estado > 3)
                    throw new InvalidDataException("Estado de missao invalido.");
        return data;
    }

    private bool ConteudoSaveValido(string conteudo)
    {
        try { InterpretarEValidarSave(conteudo); return true; }
        catch (Exception) { return false; }
    }

    private void ValidarAplicacao(SaveData data)
    {
        if (salvarInventario)
        {
            var adapter = EncontrarInventarioSalvavel() as InventarioSaveAdapter;
            if (adapter == null) throw new InvalidOperationException("Adapter com confirmacao de carga indisponivel.");
            if (!adapter.ValidarRestauracao(data.inventario, out string erro)) throw new InvalidDataException(erro);
        }
        if (data.missoes != null && data.missoes.Count > 0)
        {
            var disponiveis = new HashSet<string>();
            foreach (var gerenciador in FindObjectsByType<GerenciadorMissoes>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var missao in gerenciador.CapturarEstadoParaSave())
                    disponiveis.Add(missao.gerenciadorId + "\n" + missao.idMissao);
            foreach (var missao in data.missoes)
                if (!disponiveis.Contains(missao.gerenciadorId + "\n" + missao.idMissao))
                    throw new InvalidDataException("Missao salva indisponivel: " + missao.idMissao);
        }
        var registrados = CriarMapaObjetosRegistrados();
        var existentes = ObterItensPersistentesSoltosCena();
        var idsReservados = ObterIdsReservadosParaCargaCena(data.objetosCena, ExtrairInstanciaIdsInventario(data.inventario));
        var usados = new HashSet<ItemPersistente>();
        var database = ItemDatabaseLocal.Instancia != null ? ItemDatabaseLocal.Instancia : FindFirstObjectByType<ItemDatabaseLocal>();
        foreach (SceneObjectSaveData objeto in data.objetosCena)
        {
            if (!EhDadoItemPersistente(objeto))
            {
                if (!registrados.ContainsKey(objeto.objectId))
                    throw new InvalidDataException("Objeto persistente ausente: " + objeto.objectId);
                continue;
            }
            var estado = LerEstadoItemCena(objeto);
            var item = EncontrarItemSoltoPorInstancia(existentes, estado.instanciaId, usados);
            if (item == null)
            {
                item = EncontrarItemSoltoComIdRuntimeParaMigrar(existentes, estado.itemId, usados, out int candidatos, idsReservados);
                if (item == null && (candidatos > 1 || EncontrarItemSoltoMesmoTipoParaBloquearDuplicacao(existentes, estado.itemId, usados, idsReservados) != null))
                    throw new InvalidDataException("Restauracao de cena ambigua: " + objeto.objectId);
            }
            if (item != null) usados.Add(item);
            else if (database == null || database.ObterPrefab(estado.itemId) == null)
                throw new InvalidDataException("Prefab de cena indisponivel: " + estado.itemId);
        }
    }

    private List<MissionSaveData> CapturarMissoes()
    {
        List<MissionSaveData> resultado = new List<MissionSaveData>();
        GerenciadorMissoes[] gerenciadores = FindObjectsByType<GerenciadorMissoes>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < gerenciadores.Length; i++)
        {
            GerenciadorMissoes gerenciador = gerenciadores[i];
            if (gerenciador == null) continue;

            List<MissionSaveData> estados = gerenciador.CapturarEstadoParaSave();
            if (estados != null)
                resultado.AddRange(estados);
        }

        return resultado;
    }

    private void RestaurarMissoes(List<MissionSaveData> missoesSalvas)
    {
        if (missoesSalvas == null || missoesSalvas.Count == 0)
            return;

        GerenciadorMissoes[] gerenciadores = FindObjectsByType<GerenciadorMissoes>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < gerenciadores.Length; i++)
        {
            if (gerenciadores[i] != null)
                gerenciadores[i].RestaurarEstadoDoSave(missoesSalvas);
        }
    }

    [ContextMenu("Apagar Save")]
    public void ApagarSave()
    {
        if (EstadoCarregamento == EstadoCarga.EmAndamento) return;
        try
        {
            string sufixo = ".novoJogo." + Guid.NewGuid().ToString("N");
            foreach (string caminho in ArquivoSaveSeguro.Candidatos(CaminhoSave))
                if (File.Exists(caminho)) File.Move(caminho, caminho + sufixo);
            EstadoCarregamento = EstadoCarga.SessaoNova;
            ArquivoCarregado = null;
            UltimoDiagnosticoCarga = "Save desvinculado explicitamente; arquivos anteriores preservados.";
        }
        catch (Exception ex) { NotificarFalhaRestauracao("Nao foi possivel iniciar novo save: " + ex.Message); }
    }

    [ContextMenu("Mostrar Caminho Do Save")]
    public void MostrarCaminhoDoSave()
    {
    }

    public bool ExisteSave()
    {
        return File.Exists(CaminhoSave);
    }

    private void EscreverArquivoSaveSeguro(string caminhoFinal, string conteudo)
    {
        bool recuperadoDeOutraGeracao = ArquivoCarregado != null && !string.Equals(ArquivoCarregado, caminhoFinal, StringComparison.Ordinal);
        ArquivoSaveSeguro.Gravar(caminhoFinal, conteudo, ConteudoSaveValido, preservarPrincipal: recuperadoDeOutraGeracao);
        ArquivoCarregado = caminhoFinal;
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

        StatusPlayer status = ResolverStatusPlayer();
        Transform playerResolvido = ResolverTransformPlayer(status);

        if (playerResolvido != null)
        {
            Vector3 posicao = playerResolvido.position;
            Vector3 rotacao = playerResolvido.eulerAngles;

            data.playerTransformSalvo = true;
            data.posX = posicao.x;
            data.posY = posicao.y;
            data.posZ = posicao.z;
            data.rotX = rotacao.x;
            data.rotY = rotacao.y;
            data.rotZ = rotacao.z;
        }

        if (status != null)
            CapturarStatusPlayer(status, data);

        return data;
    }

    private void RestaurarPlayer(PlayerSaveData data)
    {
        if (data == null)
            return;

        StatusPlayer status = ResolverStatusPlayer();
        Transform playerResolvido = ResolverTransformPlayer(status);

        // Saves antigos criados sem a referencia do Player possuem posicao e
        // rotacao zeradas. O marcador impede que esses zeros teleportem o Player.
        if (data.playerTransformSalvo && playerResolvido != null)
        {
            Vector3 posicao = new Vector3(data.posX, data.posY, data.posZ);
            if (salvarPosicaoPlayer && VetorFinito(posicao))
                playerResolvido.position = posicao;

            Vector3 rotacao = new Vector3(data.rotX, data.rotY, data.rotZ);
            if (salvarRotacaoPlayer && VetorFinito(rotacao))
                playerResolvido.rotation = Quaternion.Euler(rotacao);
        }

        if (status != null)
            RestaurarStatusPlayer(status, data);
    }

    private StatusPlayer ResolverStatusPlayer()
    {
        if (player != null)
        {
            StatusPlayer statusNoPlayer = player.GetComponentInChildren<StatusPlayer>(true);
            if (statusNoPlayer != null)
                return statusNoPlayer;
        }

        StatusPlayer[] statusEncontrados = FindObjectsByType<StatusPlayer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < statusEncontrados.Length; i++)
        {
            StatusPlayer status = statusEncontrados[i];
            if (status != null && status.gameObject.activeInHierarchy)
                return status;
        }

        return statusEncontrados.Length > 0 ? statusEncontrados[0] : null;
    }

    private Transform ResolverTransformPlayer(StatusPlayer status)
    {
        if (player != null)
            return player;

        if (status == null)
            return null;

        player = status.transform.root;
        return player;
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
                { }
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

            throw new InvalidOperationException("Objeto nao restaurado: " + data.objectId);
        }

        RestaurarItensSoltosCena(objetosCena, instanciaIdsInventario);
    }

    private void CapturarItensSoltosCena(List<SceneObjectSaveData> dados, HashSet<string> idsUsados)
    {
        ItemPersistente[] itens = FindObjectsByType<ItemPersistente>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

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
                { }
                continue;
            }

            string objectId = PrefixoObjetoItemPersistente + estadoItem.instanciaId;
            if (!idsUsados.Add(objectId))
            {
                { }
                continue;
            }

            SceneObjectSaveData data = CriarSceneObjectDataDoItem(item, estadoItem, objectId);
            dados.Add(data);
        }
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
            return;

        ItemDatabaseLocal database = ItemDatabaseLocal.Instancia != null
            ? ItemDatabaseLocal.Instancia
            : FindFirstObjectByType<ItemDatabaseLocal>();

        List<ItemPersistente> existentesAntes = ObterItensPersistentesSoltosCena();
        var idsReservados = ObterIdsReservadosParaCargaCena(objetosCena, instanciaIdsInventario);
        HashSet<ItemPersistente> itensUsados = new HashSet<ItemPersistente>();
        HashSet<string> instanciaIdsSalvos = new HashSet<string>();
        HashSet<string> instanciaIdsCarregados = new HashSet<string>();

        for (int i = 0; i < itensSalvos.Count; i++)
        {
            SceneObjectSaveData data = itensSalvos[i];
            ItemPersistente.EstadoCenaItem estadoItem = LerEstadoItemCena(data);
            if (estadoItem == null || string.IsNullOrWhiteSpace(estadoItem.itemId))
            {
                throw new InvalidOperationException("Item de cena nao restaurado: " + data.objectId);
            }

            string instanciaId = NormalizarInstanciaId(estadoItem.instanciaId);
            if (!InstanciaIdValido(instanciaId))
            {
                instanciaId = Guid.NewGuid().ToString("N");
            }

            estadoItem.instanciaId = instanciaId;
            if (instanciaIdsInventario != null && instanciaIdsInventario.Contains(instanciaId))
                continue;

            if (!instanciaIdsCarregados.Add(instanciaId))
            {
                throw new InvalidOperationException("Item de cena nao restaurado: " + data.objectId);
            }

            instanciaIdsSalvos.Add(instanciaId);

            ItemPersistente item = EncontrarItemSoltoPorInstancia(existentesAntes, instanciaId, itensUsados);

            if (item == null)
            {
                ItemPersistente itemMigravel = EncontrarItemSoltoComIdRuntimeParaMigrar(
                    existentesAntes,
                    estadoItem.itemId,
                    itensUsados,
                    out int candidatosMigracao, idsReservados);

                if (itemMigravel != null)
                {
                    { }
                    item = itemMigravel;
                }
                else if (candidatosMigracao > 1)
                {
                    throw new InvalidOperationException("Item de cena nao restaurado: " + data.objectId);
                }
            }

            if (item == null)
            {
                ItemPersistente itemMesmoTipo = EncontrarItemSoltoMesmoTipoParaBloquearDuplicacao(
                    existentesAntes,
                    estadoItem.itemId,
                    itensUsados, idsReservados);

                if (itemMesmoTipo != null)
                {
                    throw new InvalidOperationException("Item de cena nao restaurado: " + data.objectId);
                }

                if (database == null)
                {
                    throw new InvalidOperationException("Item de cena nao restaurado: " + data.objectId);
                }

                GameObject prefab = database.ObterPrefab(estadoItem.itemId);
                if (prefab == null)
                {
                    throw new InvalidOperationException("Item de cena nao restaurado: " + data.objectId);
                }

                GameObject instancia = Instantiate(prefab);
                instancia.name = SlotInventario.LimparNomeItem(prefab.name);
                item = instancia.GetComponent<ItemPersistente>();
                if (item == null)
                    item = instancia.AddComponent<ItemPersistente>();
            }

            RestaurarItemSoltoCena(item, data, estadoItem);
            itensUsados.Add(item);
        }

        RemoverDuplicatasItensSoltos(existentesAntes, itensUsados, instanciaIdsSalvos);
    }

    private HashSet<string> ObterIdsReservadosParaCargaCena(
        List<SceneObjectSaveData> objetos, HashSet<string> idsInventario)
    {
        var ids = idsInventario != null ? new HashSet<string>(idsInventario) : new HashSet<string>();
        if (objetos == null) return ids;
        foreach (var objeto in objetos)
        {
            if (objeto == null || !EhDadoItemPersistente(objeto)) continue;
            var estado = LerEstadoItemCena(objeto);
            if (estado != null) AdicionarInstanciaIdSeValido(ids, estado.instanciaId);
        }
        return ids;
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
        if (destino == null || !ItemPersistente.InstanciaIdEhGuidValido(instanciaId))
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
        return ItemPersistente.InstanciaIdEhGuidValido(instanciaId);
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

        Vector3 posicao = new Vector3(data.posX, data.posY, data.posZ);
        if (VetorFinito(posicao))
            itemTransform.position = posicao;

        Vector3 rotacao = new Vector3(data.rotX, data.rotY, data.rotZ);
        if (VetorFinito(rotacao))
            itemTransform.rotation = Quaternion.Euler(rotacao);

        Vector3 escala = new Vector3(data.scaleX, data.scaleY, data.scaleZ);
        if (EscalaValida(escala))
            itemTransform.localScale = escala;

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
        HashSet<ItemPersistente> itensUsados,
        HashSet<string> idsReservados = null)
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

            // Este objeto pertence a outro registro do mesmo save, mesmo que seja processado depois.
            if (idsReservados != null && idsReservados.Contains(item.ObterInstanciaIdSemGerar()))
                continue;

            if (item.CorrespondeAoItemId(itemIdNormalizado))
                return item;
        }

        return null;
    }

    private ItemPersistente EncontrarItemSoltoComIdRuntimeParaMigrar(
        List<ItemPersistente> itens,
        string itemId,
        HashSet<ItemPersistente> itensUsados,
        out int candidatos,
        HashSet<string> idsReservados = null)
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

            // Este objeto pertence a outro registro do mesmo save, mesmo que seja processado depois.
            if (idsReservados != null && idsReservados.Contains(item.ObterInstanciaIdSemGerar()))
                continue;

            if (!item.InstanciaIdFoiGeradoEmRuntime())
                continue;

            if (!item.CorrespondeAoItemId(itemIdNormalizado))
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
        }
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
            throw new InvalidOperationException("Inventario indisponivel para salvar.");
        }

        return inventarioSalvavel.SalvarInventario() ?? new List<InventorySaveData>();
    }

    private void RestaurarInventario(List<InventorySaveData> itens)
    {
        var adapter = EncontrarInventarioSalvavel() as InventarioSaveAdapter;
        if (adapter == null) throw new InvalidOperationException("Adapter do inventario indisponivel.");
        if (!adapter.TentarCarregarInventario(itens, out string erro))
            throw new InvalidOperationException(erro);
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
        data.experienciaParaProximoNivel = ObterInteiroStatus(
            status,
            "GetExperienciaParaProximoNivel",
            "experienciaParaProximoNivel");
        data.vidaAtual = ObterInteiroStatus(status, "GetVidaAtual", "vidaAtual");
        data.vidaMaxima = ObterInteiroStatus(status, "GetVidaMaxima", "vidaMaxima");
        data.manaAtual = ObterInteiroStatus(status, "GetManaAtual", "manaAtual");
        data.manaMaxima = ObterInteiroStatus(status, "GetManaMaxima", "manaMaxima");
        data.reinUnidades = ObterReinUnidadesStatus(status);
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
        if (!StatusSalvoEhValido(data))
            return;

        // O StatusPlayer atual nao possui setters publicos para todos os campos.
        // Para teste local, usamos reflexao nos campos serializados existentes.
        DefinirInteiroStatus(status, "nivel", data.level);
        DefinirInteiroStatus(status, "experienciaAtual", data.xp);
        if (data.experienciaParaProximoNivel > 0)
        {
            DefinirInteiroStatus(
                status,
                "experienciaParaProximoNivel",
                data.experienciaParaProximoNivel);
        }
        DefinirInteiroStatus(status, "vidaAtual", data.vidaAtual);
        DefinirInteiroStatus(status, "vidaMaxima", data.vidaMaxima);
        DefinirInteiroStatus(status, "manaAtual", data.manaAtual);
        DefinirInteiroStatus(status, "manaMaxima", data.manaMaxima);
        DefinirReinUnidadesStatus(status, data.reinUnidades);
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

    private bool StatusSalvoEhValido(PlayerSaveData data)
    {
        if (data == null)
            return false;

        if (data.statusDetalhadoSalvo)
            return true;

        // Compatibilidade com saves anteriores ao marcador. Um status realmente
        // capturado sempre possuia limites positivos de vida e mana. O arquivo
        // gerado sem Player referenciado deixa todos esses campos em zero.
        return data.vidaMaxima > 0 && data.manaMaxima > 0;
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

    private long ObterReinUnidadesStatus(StatusPlayer status)
    {
        if (status == null)
            return 0L;

        CarteiraReinPlayer carteira = status.ObterCarteiraRein();
        return carteira != null ? carteira.ReinUnidades : 0L;
    }

    private void DefinirReinUnidadesStatus(StatusPlayer status, long reinUnidades)
    {
        if (status == null)
            return;

        CarteiraReinPlayer carteira = status.ObterCarteiraRein();
        if (carteira != null)
            carteira.DefinirSaldoPorUnidades(reinUnidades);
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
        catch (Exception)
        {
            { }
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

    private static bool EscalaValida(Vector3 escala)
    {
        const float minimo = 0.0001f;
        return VetorFinito(escala) &&
               Mathf.Abs(escala.x) > minimo &&
               Mathf.Abs(escala.y) > minimo &&
               Mathf.Abs(escala.z) > minimo;
    }

    private static bool VetorFinito(Vector3 valor)
    {
        return ValorFinito(valor.x) && ValorFinito(valor.y) && ValorFinito(valor.z);
    }

    private static bool ValorFinito(float valor)
    {
        return !float.IsNaN(valor) && !float.IsInfinity(valor);
    }

}
