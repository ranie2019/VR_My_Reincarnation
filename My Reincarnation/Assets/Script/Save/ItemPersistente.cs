using System;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class ItemPersistente : MonoBehaviour
{
    [Header("Identificacao do Item")]
    [Tooltip("ID automatico e imutavel do TIPO do item. Objetos do mesmo prefab compartilham este valor.")]
    [SerializeField, HideInInspector] private string tipoItemId = "";
    [Tooltip("Nome/ID antigo mantido somente para migrar saves existentes.")]
    [SerializeField, HideInInspector] private string itemId = "";
    [SerializeField] private string nomeItem = "";
    [SerializeField] private GameObject prefabReferencia;

    [Header("Save")]
    [SerializeField] private bool salvarNoInventario = true;
    [SerializeField] private bool usarNomeDoObjetoSeIdVazio = true;
    [SerializeField] private bool destruirOriginalAoCarregarSeEstiverNoInventario = true;

    [Header("Estado Runtime")]
    [SerializeField] private bool estaNoInventario;
    [SerializeField] private bool salvarQuandoSoltoNaCena = true;
    [Tooltip("GUID exclusivo desta copia. Prefabs devem manter este campo vazio.")]
    [SerializeField, HideInInspector] private string instanciaId = "";

    [Header("Estado")]
    [SerializeField] private float durabilidadeAtual = -1f;
    [SerializeField] private string dadosExtrasJson;

    public bool SalvarNoInventario => salvarNoInventario;
    public bool DestruirOriginalAoCarregarSeEstiverNoInventario => destruirOriginalAoCarregarSeEstiverNoInventario;
    public GameObject PrefabReferencia => prefabReferencia;

    private bool avisoFallbackIdMostrado;
    private bool avisoItemIdVazioMostrado;
    private bool avisoInstanciaIdRuntimeMostrado;
    private bool instanciaIdGeradoEmRuntime;

    [Serializable]
    public class EstadoCenaItem
    {
        public string itemId;
        public string nomeItem;
        public string instanciaId;
        public float durabilidade;
        public string dadosExtrasJson;
        public bool respawnItemDesarmado;
    }

    private void Awake()
    {
        GarantirTipoItemId();
        GarantirInstanciaIdRuntime();
        ValidarIdentificacaoRuntime();
    }

    private void Reset()
    {
        PreencherReferenciaPrefab();
        GarantirTipoItemId();
    }

    private void OnValidate()
    {
        PreencherReferenciaPrefab();
        GarantirTipoItemId();
    }

    public string ObterItemId()
    {
        return ObterTipoItemId();
    }

    public string ObterTipoItemId()
    {
        GarantirTipoItemId();
        return string.IsNullOrWhiteSpace(tipoItemId) ? string.Empty : tipoItemId.Trim();
    }

    public string ObterItemIdLegado()
    {
        return string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
    }

    public bool CorrespondeAoItemId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        string normalizado = id.Trim();
        return string.Equals(ObterTipoItemId(), normalizado, StringComparison.Ordinal) ||
               string.Equals(ObterItemIdLegado(), normalizado, StringComparison.Ordinal) ||
               string.Equals(ObterNomeItem(), normalizado, StringComparison.Ordinal);
    }

    private string ObterFonteLegadaDoTipo()
    {
        if (!string.IsNullOrWhiteSpace(itemId))
            return itemId.Trim();

        if (!usarNomeDoObjetoSeIdVazio)
            return string.Empty;

        string idFallback = gameObject.name.Replace("(Clone)", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(idFallback))
            return string.Empty;

        if (!avisoFallbackIdMostrado)
        {
            { }
            avisoFallbackIdMostrado = true;
        }

        return idFallback;
    }

    public string ObterItemIdSemFallback()
    {
        return ObterTipoItemId();
    }

    public string ObterInstanciaId()
    {
        GarantirInstanciaIdRuntime();
        return instanciaId;
    }

    public bool PossuiInstanciaIdValido()
    {
        return InstanciaIdEhGuidValido(instanciaId);
    }

    public bool InstanciaIdFoiGeradoEmRuntime()
    {
        return instanciaIdGeradoEmRuntime;
    }

    public string ObterInstanciaIdSemGerar()
    {
        return InstanciaIdEhGuidValido(instanciaId) ? instanciaId.Trim() : string.Empty;
    }

    public void DefinirInstanciaId(string novoId)
    {
        if (!InstanciaIdEhGuidValido(novoId))
        {
            instanciaId = Guid.NewGuid().ToString("N");
            instanciaIdGeradoEmRuntime = true;
            return;
        }

        instanciaId = novoId.Trim();
        instanciaIdGeradoEmRuntime = false;
    }

    public bool EstaNoInventario()
    {
        EstadoItemInventario estadoInventario = GetComponent<EstadoItemInventario>();
        return estaNoInventario || (estadoInventario != null && estadoInventario.estaNoInventario);
    }

    public void MarcarComoNoInventario()
    {
        estaNoInventario = true;
        GarantirInstanciaIdRuntime();
    }

    public void MarcarComoSoltoNaCena()
    {
        estaNoInventario = false;
        GarantirInstanciaIdRuntime();
    }

    public bool DeveSalvarComoCena()
    {
        return salvarQuandoSoltoNaCena && !EstaNoInventario();
    }

    public InventorySaveData CriarSaveData(int slot, int quantidade, bool equipado)
    {
        float durabilidade = LerDurabilidade();

        return new InventorySaveData
        {
            itemId = ObterItemId(),
            nomeItem = ObterNomeItem(),
            instanciaId = ObterInstanciaId(),
            instanciaIds = new System.Collections.Generic.List<string> { ObterInstanciaId() },
            instanciaCriadaEmRuntime = InstanciaIdFoiGeradoEmRuntime(),
            quantidade = Mathf.Max(1, quantidade),
            slot = slot,
            estaNoInventario = true,
            durabilidade = durabilidade,
            equipado = equipado,
            dadosExtrasJson = dadosExtrasJson
        };
    }

    public void AplicarSaveData(InventorySaveData data)
    {
        if (data == null)
            return;

        AplicarItemIdLegadoSeNecessario(data.itemId);

        if (!string.IsNullOrWhiteSpace(data.nomeItem))
            nomeItem = data.nomeItem;

        if (!string.IsNullOrWhiteSpace(data.instanciaId))
            DefinirInstanciaId(data.instanciaId);

        durabilidadeAtual = data.durabilidade;
        dadosExtrasJson = data.dadosExtrasJson;

        if (durabilidadeAtual >= 0f)
            EscreverDurabilidade(durabilidadeAtual);
    }

    public float LerDurabilidade()
    {
        float durabilidade = LerDurabilidadePorReflexao();
        durabilidadeAtual = durabilidade;
        return durabilidade;
    }

    public string ObterNomeItem()
    {
        if (!string.IsNullOrWhiteSpace(nomeItem))
            return nomeItem.Trim();

        return gameObject.name;
    }

    public string CriarEstadoCenaJson()
    {
        EstadoCenaItem estado = CriarEstadoCena();
        return JsonUtility.ToJson(estado);
    }

    public EstadoCenaItem CriarEstadoCena()
    {
        return new EstadoCenaItem
        {
            itemId = ObterItemId(),
            nomeItem = ObterNomeItem(),
            instanciaId = ObterInstanciaId(),
            durabilidade = LerDurabilidade(),
            dadosExtrasJson = dadosExtrasJson,
            respawnItemDesarmado = RespawnItemEstaDesarmado()
        };
    }

    public void AplicarEstadoCena(EstadoCenaItem estado)
    {
        if (estado == null)
            return;

        AplicarItemIdLegadoSeNecessario(estado.itemId);

        if (!string.IsNullOrWhiteSpace(estado.nomeItem))
            nomeItem = estado.nomeItem.Trim();

        if (!string.IsNullOrWhiteSpace(estado.instanciaId))
            DefinirInstanciaId(estado.instanciaId);

        durabilidadeAtual = estado.durabilidade;
        dadosExtrasJson = estado.dadosExtrasJson;

        if (durabilidadeAtual >= 0f)
            EscreverDurabilidade(durabilidadeAtual);

        if (estado.respawnItemDesarmado)
            AplicarRespawnItemDesarmado();
    }

    public void AplicarEstadoCenaJson(string estadoJsonCena)
    {
        AplicarEstadoCena(LerEstadoCenaJson(estadoJsonCena));
    }

    public static EstadoCenaItem LerEstadoCenaJson(string estadoJsonCena)
    {
        if (string.IsNullOrWhiteSpace(estadoJsonCena))
            return null;

        try
        {
            return JsonUtility.FromJson<EstadoCenaItem>(estadoJsonCena);
        }
        catch (Exception)
        {
            { }
            return null;
        }
    }

    [ContextMenu("Mostrar ID No Console")]
    private void MostrarIdNoConsole()
    {
    }

    [ContextMenu("Mostrar ID Da Instancia No Console")]
    private void MostrarInstanciaIdNoConsole()
    {
    }

    private void GarantirInstanciaIdRuntime()
    {
        if (!Application.isPlaying || InstanciaIdEhGuidValido(instanciaId))
            return;

        instanciaId = Guid.NewGuid().ToString("N");
        instanciaIdGeradoEmRuntime = true;

        if (!avisoInstanciaIdRuntimeMostrado)
        {
            { }
            avisoInstanciaIdRuntimeMostrado = true;
        }
    }

    public static bool InstanciaIdEhGuidValido(string id)
    {
        return !string.IsNullOrWhiteSpace(id) &&
               Guid.TryParseExact(id.Trim(), "N", out _);
    }

    private void GarantirTipoItemId()
    {
#if UNITY_EDITOR
        string guidPrefab = ObterGuidPrefabNoEditor();
        if (!string.IsNullOrWhiteSpace(guidPrefab))
        {
            tipoItemId = guidPrefab;
            return;
        }
#endif

        if (!string.IsNullOrWhiteSpace(tipoItemId))
            return;

        string fonte = ObterFonteLegadaDoTipo();
        if (!string.IsNullOrWhiteSpace(fonte))
            tipoItemId = Hash128.Compute("ItemTipo:" + fonte.Trim().ToLowerInvariant()).ToString();
    }

    private void AplicarItemIdLegadoSeNecessario(string idSalvo)
    {
        if (string.IsNullOrWhiteSpace(idSalvo) || CorrespondeAoItemId(idSalvo))
            return;

        if (string.IsNullOrWhiteSpace(itemId))
            itemId = idSalvo.Trim();

        GarantirTipoItemId();
    }

    private void ValidarIdentificacaoRuntime()
    {
        if (!Application.isPlaying)
            return;

        if (string.IsNullOrWhiteSpace(ObterTipoItemId()) && !avisoItemIdVazioMostrado)
        {
            { }
            avisoItemIdVazioMostrado = true;
        }
    }

    private void PreencherReferenciaPrefab()
    {
#if UNITY_EDITOR
        if (prefabReferencia == null)
        {
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
            if (prefab != null)
                prefabReferencia = prefab;
        }
#endif
    }

#if UNITY_EDITOR
    private string ObterGuidPrefabNoEditor()
    {
        string caminho = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);

        if (string.IsNullOrWhiteSpace(caminho) && EditorUtility.IsPersistent(gameObject))
            caminho = AssetDatabase.GetAssetPath(gameObject);

        if (string.IsNullOrWhiteSpace(caminho) && prefabReferencia != null)
            caminho = AssetDatabase.GetAssetPath(prefabReferencia);

        return string.IsNullOrWhiteSpace(caminho)
            ? string.Empty
            : AssetDatabase.AssetPathToGUID(caminho);
    }
#endif

    private float LerDurabilidadePorReflexao()
    {
        Component componenteDuravel = ObterComponenteDuravel();
        if (componenteDuravel == null)
            return -1f;

        if (TentarLerNumero(componenteDuravel, "VidaAtual", out float valor))
            return valor;

        if (TentarLerNumero(componenteDuravel, "vidaAtual", out valor))
            return valor;

        if (TentarLerNumero(componenteDuravel, "DurabilidadeAtual", out valor))
            return valor;

        if (TentarLerNumero(componenteDuravel, "durabilidadeAtual", out valor))
            return valor;

        if (TentarLerNumero(componenteDuravel, "durabilidade", out valor))
            return valor;

        return -1f;
    }

    private bool RespawnItemEstaDesarmado()
    {
        Respawnitem respawnItem = GetComponent<Respawnitem>();
        return respawnItem != null && respawnItem.EstaDesarmadoComoItemColetado();
    }

    private void AplicarRespawnItemDesarmado()
    {
        Respawnitem respawnItem = GetComponent<Respawnitem>();
        if (respawnItem != null)
            respawnItem.DesarmarComoItemColetado();
    }

    private void EscreverDurabilidade(float valor)
    {
        Component componenteDuravel = ObterComponenteDuravel();
        if (componenteDuravel == null)
            return;

        if (!TentarEscreverNumero(componenteDuravel, "vidaAtual", valor) &&
            !TentarEscreverNumero(componenteDuravel, "VidaAtual", valor) &&
            !TentarEscreverNumero(componenteDuravel, "durabilidadeAtual", valor) &&
            !TentarEscreverNumero(componenteDuravel, "DurabilidadeAtual", valor) &&
            !TentarEscreverNumero(componenteDuravel, "durabilidade", valor))
        {
            return;
        }

        MethodInfo atualizarVisual = componenteDuravel.GetType().GetMethod(
            "AtualizarDurabilidadeVisual",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        atualizarVisual?.Invoke(componenteDuravel, null);
    }

    private Component ObterComponenteDuravel()
    {
        return GetComponent("Equipamento") ??
               GetComponent("Espada") ??
               GetComponent("Machado") ??
               GetComponent("Picareta") ??
               GetComponent("Arco") ??
               GetComponent("Escudo");
    }

    private bool TentarLerNumero(Component componente, string nome, out float valor)
    {
        valor = -1f;
        Type tipo = componente.GetType();

        PropertyInfo propriedade = tipo.GetProperty(nome, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (propriedade != null && LerValorNumerico(propriedade.GetValue(componente), out valor))
            return true;

        FieldInfo campo = tipo.GetField(nome, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return campo != null && LerValorNumerico(campo.GetValue(componente), out valor);
    }

    private bool TentarEscreverNumero(Component componente, string nome, float valor)
    {
        Type tipo = componente.GetType();

        FieldInfo campo = tipo.GetField(nome, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (campo != null)
            return EscreverValorNumerico(campo, componente, valor);

        PropertyInfo propriedade = tipo.GetProperty(nome, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (propriedade != null && propriedade.CanWrite)
            return EscreverValorNumerico(propriedade, componente, valor);

        return false;
    }

    private bool LerValorNumerico(object origem, out float valor)
    {
        valor = -1f;

        if (origem is int inteiro)
        {
            valor = inteiro;
            return true;
        }

        if (origem is float flutuante)
        {
            valor = flutuante;
            return true;
        }

        return false;
    }

    private bool EscreverValorNumerico(FieldInfo campo, Component componente, float valor)
    {
        if (campo.FieldType == typeof(int))
        {
            campo.SetValue(componente, Mathf.RoundToInt(valor));
            return true;
        }

        if (campo.FieldType == typeof(float))
        {
            campo.SetValue(componente, valor);
            return true;
        }

        return false;
    }

    private bool EscreverValorNumerico(PropertyInfo propriedade, Component componente, float valor)
    {
        if (propriedade.PropertyType == typeof(int))
        {
            propriedade.SetValue(componente, Mathf.RoundToInt(valor));
            return true;
        }

        if (propriedade.PropertyType == typeof(float))
        {
            propriedade.SetValue(componente, valor);
            return true;
        }

        return false;
    }
}
