using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RespawnNatureza : MonoBehaviour
{
    public static RespawnNatureza Instancia { get; private set; }

    [Serializable]
    public class ConfiguracaoRespawnNatureza
    {
        public string idNatureza;
        public GameObject prefabRespawn;
        [Tooltip("Pasta clone existente do recurso. Se vazio, procura em ---STATIC---/NATUREZA/RECURSOS.")]
        public Transform pastaClones;
        [Tooltip("Espera dos recursos comuns. Cogumelos usam Tempo Respawn do componente Cocumelo.")]
        public float tempoRespawn = 30f;
        public Vector3 offsetRespawn = Vector3.zero;
        public bool usarRotacaoDaMorte = true;
    }

    [Header("Configuracoes de respawn por natureza")]
    [SerializeField] private ConfiguracaoRespawnNatureza[] configuracoesNatureza;

    private readonly HashSet<string> respawnsAgendados = new();

    [Header("Organizacao dos cogumelos")]
    [Tooltip("Pasta existente sob ---STATIC---/NATUREZA/COGUMELO. Nunca cria outra pasta.")]
    public Transform pastaClonesCogumelos;
    public Transform grupoCogumelos;
    private bool originaisCogumelosRegistrados;
    private bool avisoPastaCogumelos;
    private bool encerrando;
    private sealed class PontoCogumelo
    {
        internal string id;
        internal Vector3 posicao;
        internal Quaternion rotacao;
        internal float distancia, espera, altura;
        internal bool agendado;
        internal bool coletaPendenteConfirmada;
        internal Coroutine rotina;
        internal readonly List<Cocumelo> referencias = new List<Cocumelo>();
    }
    private readonly Dictionary<string, PontoCogumelo> pontosCogumelos = new Dictionary<string, PontoCogumelo>();

    internal static bool PodeProcessarRespawn()
    {
        if (!Application.isPlaying || (Instancia != null && Instancia.encerrando)) return false;
        var save = SaveManager.Instancia;
        return save == null || (save.EstadoCarregamento != SaveManager.EstadoCarga.EmAndamento &&
            save.EstadoCarregamento != SaveManager.EstadoCarga.Falhou);
    }

    internal void RegistrarPontoCogumelo(Cocumelo cogumelo, bool autorizarPontoDaCena = false)
    {
        string id = cogumelo.ObterRespawnId();
        if (BuscarConfiguracao(id)?.prefabRespawn == null) return;
        string chave = CriarChaveRespawn(id, cogumelo.PontoOriginal);
        if (!pontosCogumelos.TryGetValue(chave, out var ponto))
        {
            if (!autorizarPontoDaCena) return;
            ponto = new PontoCogumelo { id = id, posicao = cogumelo.PontoOriginal, rotacao = cogumelo.RotacaoOriginal };
            pontosCogumelos.Add(chave, ponto);
        }
        if (!ponto.referencias.Contains(cogumelo)) ponto.referencias.Add(cogumelo);
        if (!cogumelo.EstaDesarmadoComoItemColetado())
        {
            ponto.distancia = cogumelo.distanciaParaRespawn;
            ponto.espera = cogumelo.tempoRespawn;
            ponto.altura = cogumelo.alturaRespawn;
        }
    }

    internal bool AgendarRespawnCogumelo(Cocumelo cogumelo)
    {
        if (!PodeProcessarRespawn() || !cogumelo.ColetaConfirmada) return false;
        RegistrarPontoCogumelo(cogumelo);
        if (!pontosCogumelos.TryGetValue(CriarChaveRespawn(cogumelo.ObterRespawnId(), cogumelo.PontoOriginal), out var ponto)) return false;
        ponto.coletaPendenteConfirmada = true;
        AgendarPontoCogumelo(ponto);
        return true;
    }

    private void Update()
    {
        if (!PodeProcessarRespawn()) return;
        foreach (var ponto in pontosCogumelos.Values)
        {
            ponto.referencias.RemoveAll(c => c == null);
            var presente = EncontrarCogumeloNoPonto(ponto);
            if (presente != null)
            {
                // Retornar antes do prazo cancela a espera. Uma nova saida deve
                // iniciar um periodo completo, nunca herdar o timer anterior.
                bool retornou = ponto.agendado || presente.EstaDesarmadoComoItemColetado();
                if (ponto.rotina != null) StopCoroutine(ponto.rotina);
                ponto.rotina = null; ponto.agendado = false; ponto.coletaPendenteConfirmada = false;
                if (retornou) presente.RearmarAposRetorno();
                continue;
            }
            if (ponto.agendado) continue;
            // Um ponto vazio por carga, fisica ou desativacao nao comprova coleta.
            // Apenas coleta real, ou seu registro persistido, pode iniciar a espera.
            if (ponto.coletaPendenteConfirmada) AgendarPontoCogumelo(ponto);
        }
    }

    private static bool OcupaPonto(Cocumelo item, PontoCogumelo ponto)
    {
        return item != null && item.gameObject.activeInHierarchy && item.ObterRespawnId() == ponto.id &&
            Vector3.Distance(item.PontoOriginal, ponto.posicao) < 0.001f && item.EstaNoPontoOriginal();
    }

    private static Cocumelo EncontrarCogumeloNoPonto(PontoCogumelo ponto)
    {
        foreach (var item in ponto.referencias)
            if (OcupaPonto(item, ponto)) return item;
        // Itens do save podem estar prontos antes de receberem Start/Update.
        // Reconhece a copia restaurada antes de agendar uma reposicao fantasma.
        foreach (var item in FindObjectsByType<Cocumelo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item.EstaDesarmadoComoItemColetado() || !OcupaPonto(item, ponto)) continue;
            if (!ponto.referencias.Contains(item)) ponto.referencias.Add(item);
            return item;
        }
        return null;
    }

    private void AgendarPontoCogumelo(PontoCogumelo ponto)
    {
        if (ponto.agendado || !isActiveAndEnabled) return;
        ponto.agendado = true;
        ponto.rotina = StartCoroutine(ReporCogumelo(ponto));
    }

    private IEnumerator ReporCogumelo(PontoCogumelo ponto)
    {
        if (ponto.espera > 0f) yield return new WaitForSeconds(ponto.espera);
        else yield return null;
        while (!PodeProcessarRespawn()) yield return null;
        try
        {
            var presente = EncontrarCogumeloNoPonto(ponto);
            if (presente != null)
            {
                presente.RearmarAposRetorno();
                ponto.coletaPendenteConfirmada = false;
                yield break;
            }
            var config = BuscarConfiguracao(ponto.id);
            if (config == null || config.prefabRespawn == null) yield break;
            var pasta = ResolverPastaCogumelos();
            if (pasta == null) yield break;
            GameObject novo = Instantiate(config.prefabRespawn, ponto.posicao + Vector3.up * ponto.altura, ponto.rotacao);
            var cogumelo = novo.GetComponent<Cocumelo>();
            if (cogumelo == null) { Destroy(novo); yield break; }
            cogumelo.PrepararNoPonto(ponto.posicao, ponto.rotacao, ponto.distancia, ponto.espera, ponto.altura);
            novo.transform.SetParent(pasta, true);
            ponto.referencias.Add(cogumelo);
            ponto.coletaPendenteConfirmada = false;
        }
        finally { ponto.agendado = false; ponto.rotina = null; }
    }

    internal Transform ObterPastaCogumelos() => ResolverPastaCogumelos();

    [Serializable]
    public class EstadoPontoCogumelo
    {
        public string id;
        public Vector3 posicao;
        public float distancia, espera, altura;
    }
    public List<EstadoPontoCogumelo> CapturarPontosPendentes()
    {
        var resultado = new List<EstadoPontoCogumelo>();
        foreach (var ponto in pontosCogumelos.Values)
            if (ponto.coletaPendenteConfirmada)
                resultado.Add(new EstadoPontoCogumelo { id = ponto.id, posicao = ponto.posicao,
                    distancia = ponto.distancia, espera = ponto.espera, altura = ponto.altura });
        return resultado;
    }
    public static void ValidarEstruturaPontosPendentes(List<EstadoPontoCogumelo> estados)
    {
        if (estados == null) return;
        var usados = new HashSet<string>();
        foreach (var estado in estados)
        {
            if (estado == null || string.IsNullOrWhiteSpace(estado.id)) throw new InvalidOperationException("Ponto pendente invalido.");
            ItemPersistente.ValidarPontoCogumelo(new ItemPersistente.EstadoCenaItem { possuiPontoCogumelo = true,
                pontoCogumelo = estado.posicao, distanciaPontoCogumelo = estado.distancia,
                tempoPontoCogumelo = estado.espera, alturaPontoCogumelo = estado.altura });
            if (!usados.Add(CriarChaveRespawn(estado.id, estado.posicao))) throw new InvalidOperationException("Ponto pendente duplicado.");
        }
    }
    public void ValidarPontosPendentes(List<EstadoPontoCogumelo> estados)
    {
        ValidarEstruturaPontosPendentes(estados);
        if (estados == null) return;
        foreach (var estado in estados)
            if (!pontosCogumelos.ContainsKey(CriarChaveRespawn(estado.id, estado.posicao)))
                throw new InvalidOperationException("Ponto pendente indisponivel.");
    }
    public void RestaurarPontosPendentes(List<EstadoPontoCogumelo> estados)
    {
        ValidarPontosPendentes(estados);
        foreach (var ponto in pontosCogumelos.Values)
        {
            if (ponto.rotina != null) StopCoroutine(ponto.rotina);
            ponto.rotina = null; ponto.agendado = false; ponto.coletaPendenteConfirmada = false;
        }
        if (estados == null) return;
        foreach (var estado in estados)
        {
            var ponto = pontosCogumelos[CriarChaveRespawn(estado.id, estado.posicao)];
            ponto.distancia = estado.distancia; ponto.espera = estado.espera; ponto.altura = estado.altura;
            ponto.coletaPendenteConfirmada = true;
        }
    }

    private static bool NomeIgual(Transform t, string nome) =>
        string.Equals(t.name, nome, StringComparison.OrdinalIgnoreCase);

    internal Transform ObterGrupoCogumelos()
    {
        if (grupoCogumelos != null && grupoCogumelos.gameObject.scene == gameObject.scene) return grupoCogumelos;
        foreach (var candidato in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidato.gameObject.scene != gameObject.scene ||
                !(NomeIgual(candidato, "COGUMELO") || NomeIgual(candidato, "CAGUMELO"))) continue;
            for (Transform pai = candidato.parent; pai != null; pai = pai.parent)
                if (NomeIgual(pai, "---STATIC---")) { grupoCogumelos = candidato; return candidato; }
        }
        return null;
    }

    private Transform ResolverPastaCogumelos()
    {
        var grupo = ObterGrupoCogumelos();
        if (grupo != null)
        {
            if (pastaClonesCogumelos != null && pastaClonesCogumelos.parent == grupo) return pastaClonesCogumelos;
            foreach (Transform filho in grupo)
                if (NomeIgual(filho, "Clone Cogumelo")) { pastaClonesCogumelos = filho; return filho; }
        }
        if (!avisoPastaCogumelos)
        {
            avisoPastaCogumelos = true;
            Debug.LogError("[RespawnNatureza] Pasta existente ---STATIC---/NATUREZA/COGUMELO/clone Cogumelo nao encontrada. Respawn suspenso; nenhuma pasta criada.", this);
        }
        return null;
    }

    internal bool PossuiPontoCogumelo(GameObject prefab, Vector3 posicao)
    {
        var cogumelo = prefab != null ? prefab.GetComponent<Cocumelo>() : null;
        return cogumelo != null && pontosCogumelos.ContainsKey(CriarChaveRespawn(cogumelo.ObterRespawnId(), posicao));
    }
    internal bool PossuiPontoOriginalNaPosicao(Vector3 posicao)
    {
        foreach (var ponto in pontosCogumelos.Values)
            if (Vector3.Distance(ponto.posicao, posicao) < 0.001f) return true;
        return false;
    }
    internal bool PodeClassificarLegados => originaisCogumelosRegistrados;

    private void OnEnable() { if (Instancia == null) Instancia = this; }
    private void OnApplicationQuit() { encerrando = true; }


    // Consulta somente referencias de prefab, sem instanciar nem agendar respawn.
    public GameObject ObterPrefabPorTipoPersistente(string tipoItemId)
    {
        if (string.IsNullOrWhiteSpace(tipoItemId) || configuracoesNatureza == null)
            return null;

        GameObject resultado = null;
        foreach (var config in configuracoesNatureza)
        {
            if (config == null || config.prefabRespawn == null) continue;
            var persistente = config.prefabRespawn.GetComponent<ItemPersistente>();
            if (persistente == null || !string.Equals(persistente.ObterTipoItemId(), tipoItemId.Trim(), StringComparison.Ordinal))
                continue;
            if (resultado != null && resultado != config.prefabRespawn)
                throw new InvalidOperationException("Mais de um prefab de respawn possui o tipo persistente: " + tipoItemId);
            resultado = config.prefabRespawn;
        }
        return resultado;
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            enabled = false;
            return;
        }

        Instancia = this;
        // Captura pontos da cena antes de o save mover/remover as instancias.
        var grupo = ObterGrupoCogumelos();
        var pasta = ResolverPastaCogumelos();
        if (grupo == null || pasta == null) return;
        foreach (var cogumelo in FindObjectsByType<Cocumelo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (cogumelo.gameObject.scene == gameObject.scene && cogumelo.transform.IsChildOf(grupo) &&
                !cogumelo.transform.IsChildOf(pasta)) RegistrarPontoCogumelo(cogumelo, true);
        originaisCogumelosRegistrados = pontosCogumelos.Count > 0;
    }

    private void OnDisable()
    {
        if (Instancia == this)
            Instancia = null;

        StopAllCoroutines();
        foreach (var ponto in pontosCogumelos.Values) { ponto.agendado = false; ponto.rotina = null; }
        respawnsAgendados.Clear();
    }

    private void OnValidate()
    {
        if (configuracoesNatureza == null)
            return;

        for (int i = 0; i < configuracoesNatureza.Length; i++)
        {
            ConfiguracaoRespawnNatureza config = configuracoesNatureza[i];
            if (config == null)
                continue;

            config.tempoRespawn = Mathf.Max(0f, config.tempoRespawn);
        }
    }

    public void AgendarRespawn(string idNatureza, Vector3 posicaoMorte, Quaternion rotacaoMorte)
    {
        string idNormalizado = idNatureza == null ? string.Empty : idNatureza.Trim();

        if (string.IsNullOrWhiteSpace(idNormalizado))
            return;

        ConfiguracaoRespawnNatureza config = BuscarConfiguracao(idNormalizado);

        if (config == null)
            return;

        if (config.prefabRespawn == null)
            return;

        float tempoRespawn = Mathf.Max(0f, config.tempoRespawn);
        Vector3 posicaoFinal = posicaoMorte + config.offsetRespawn;
        Quaternion rotacaoFinal = config.usarRotacaoDaMorte
            ? rotacaoMorte
            : config.prefabRespawn.transform.rotation;

        string chaveRespawn = CriarChaveRespawn(config.idNatureza.Trim(), posicaoFinal);
        if (!respawnsAgendados.Add(chaveRespawn))
            return;

        StartCoroutine(RotinaRespawn(config, tempoRespawn, posicaoFinal, rotacaoFinal, chaveRespawn));
    }

    private static string NomeRecurso(string nome)
    {
        string chave = (nome ?? "").Replace(" ", "").Replace("_", "").ToLowerInvariant();
        if (chave == "stone") return "stone";
        if (chave == "wood") return "wood";
        if (chave == "rochametalica" || chave == "metallicrock") return "metal";
        return null;
    }

    private Transform ResolverGrupoRecurso(string id)
    {
        string chave = NomeRecurso(id);
        if (chave == null) return null;
        foreach (var raiz in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (raiz.gameObject.scene != gameObject.scene || !NomeIgual(raiz, "RECURSOS") ||
                raiz.parent == null || !NomeIgual(raiz.parent, "NATUREZA") ||
                raiz.parent.parent == null || !NomeIgual(raiz.parent.parent, "---STATIC---")) continue;
            foreach (Transform grupo in raiz)
                if (NomeRecurso(grupo.name) == chave) return grupo;
        }
        return null;
    }

    private Transform ResolverPastaRecurso(ConfiguracaoRespawnNatureza config)
    {
        var grupo = ResolverGrupoRecurso(config.idNatureza);
        if (config.pastaClones != null && config.pastaClones.gameObject.scene == gameObject.scene &&
            (NomeRecurso(config.idNatureza) == null || config.pastaClones.parent == grupo)) return config.pastaClones;
        if (grupo == null) return null;
        foreach (Transform filho in grupo)
            if (NomeIgual(filho, grupo.name + " Clone") || NomeIgual(filho, "Clone " + grupo.name) ||
                NomeIgual(filho, "clone") ||
                (NomeIgual(filho, grupo.name) && filho.GetComponent<Respawnitem>() == null && filho.GetComponent<ItemPersistente>() == null))
                return filho;
        return null;
    }

    public void OrganizarRecursoRestaurado(ItemPersistente item)
    {
        if (item == null || item.EstaNoInventario() || item.GetComponent<Cocumelo>() != null) return;
        var recurso = item.GetComponent<Respawnitem>();
        if (recurso == null) return;
        var config = BuscarConfiguracao(recurso.ObterRespawnId());
        if (config == null || NomeRecurso(config.idNatureza) == null) return;
        var pai = string.IsNullOrEmpty(item.OrigemCenaId) ? ResolverPastaRecurso(config) : ResolverGrupoRecurso(config.idNatureza);
        if (pai != null) item.transform.SetParent(pai, true);
    }

    private IEnumerator RotinaRespawn(
        ConfiguracaoRespawnNatureza config,
        float tempoRespawn,
        Vector3 posicaoFinal,
        Quaternion rotacaoFinal,
        string chaveRespawn)
    {
        if (tempoRespawn > 0f)
            yield return new WaitForSeconds(tempoRespawn);
        else
            yield return null;

        if (config.prefabRespawn == null)
        {
            respawnsAgendados.Remove(chaveRespawn);
            yield break;
        }

        var pasta = ResolverPastaRecurso(config);
        if (NomeRecurso(config.idNatureza) != null && pasta == null)
        {
            Debug.LogError("[RespawnNatureza] Pasta clone existente do recurso nao encontrada: " + config.idNatureza + ". Configure Pasta Clones; nenhuma pasta ou clone solto foi criado.", this);
            respawnsAgendados.Remove(chaveRespawn);
            yield break;
        }
        GameObject instancia = Instantiate(config.prefabRespawn, posicaoFinal, rotacaoFinal);
        if (pasta != null) instancia.transform.SetParent(pasta, true);
        PrepararItensRespawnados(instancia);
        respawnsAgendados.Remove(chaveRespawn);
    }

    private void PrepararItensRespawnados(GameObject instancia)
    {
        if (instancia == null)
            return;

        Respawnitem[] itens = instancia.GetComponentsInChildren<Respawnitem>(true);
        for (int i = 0; i < itens.Length; i++)
        {
            if (itens[i] != null)
                itens[i].PrepararComoRecursoDisponivelNoMundo();
        }
    }

    private static string CriarChaveRespawn(string idNatureza, Vector3 posicao)
    {
        int x = Mathf.RoundToInt(posicao.x * 1000f);
        int y = Mathf.RoundToInt(posicao.y * 1000f);
        int z = Mathf.RoundToInt(posicao.z * 1000f);
        return idNatureza + "|" + x + "|" + y + "|" + z;
    }

    private ConfiguracaoRespawnNatureza BuscarConfiguracao(string idNatureza)
    {
        if (string.IsNullOrWhiteSpace(idNatureza) || configuracoesNatureza == null)
            return null;

        string idNormalizado = idNatureza.Trim();

        for (int i = 0; i < configuracoesNatureza.Length; i++)
        {
            ConfiguracaoRespawnNatureza config = configuracoesNatureza[i];
            if (config == null || string.IsNullOrWhiteSpace(config.idNatureza))
                continue;

            if (string.Equals(config.idNatureza.Trim(), idNormalizado, StringComparison.Ordinal) ||
                (NomeRecurso(idNormalizado) != null && NomeRecurso(config.idNatureza) == NomeRecurso(idNormalizado)))
                return config;
        }

        return null;
    }
}
