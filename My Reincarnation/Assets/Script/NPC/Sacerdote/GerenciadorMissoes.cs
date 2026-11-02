using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Cérebro do sistema de missões.
///
/// NotificarHoverNPC() é chamado pelo clique no botão "Missao"
/// (BotaoMissao → Ao Confirmar), não pelo hover do NPC.
/// </summary>
public class GerenciadorMissoes : MonoBehaviour
{
    private bool entregaEmAndamento;
    public enum TipoObjetivo
    {
        MatarInimigos,
        ColetarItens
    }

    public enum EstadoMissao
    {
        NaoIniciada,
        EmAndamento,
        ProntaParaEntregar,
        Concluida
    }

    [Serializable]
    public class MissaoDados
    {
        [Header("Identificacao")]
        public string idMissao;

        [Header("Enunciado (texto que aparece na UI do player)")]
        [TextArea(2, 4)]
        public string enunciadoMissao;

        [Header("Dialogo (falas em sequencia)")]
        public GameObject canvasDialogo;
        public List<TMP_Text> falasDialogo = new List<TMP_Text>();
        public float tempoEntreFalas = 2f;
        public GameObject botaoAceitar;
        public GameObject botaoRecusar;

        [Header("Popup: missao ja em andamento")]
        public GameObject canvasEmAndamento;

        [Header("Objetivo")]
        public TipoObjetivo tipoObjetivo;

        [Tooltip("MatarInimigos: idRespawnMonstro (ex: SlimeVerde)\nColetarItens: NomeItem (ex: Wood)")]
        public string idAlvo;

        [Min(1)]
        public int quantidadeAlvo = 1;

        [Header("Popup: missao pronta para entregar")]
        public GameObject canvasConcluida;

        [Header("Recompensa")]
        public GameObject prefabRecompensa;
        public Transform pontoSpawnRecompensa;
        public string recompensaRein = "0";
        [Min(0)] public int recompensaExperiencia = 0;
        [Min(0)] public int recompensaPontosPrestigio = 0;

        [NonSerialized] public int quantidadeAtualObjetivo;
        [NonSerialized] public EstadoMissao estado = EstadoMissao.NaoIniciada;
    }

    [Header("Cadeia de missoes")]
    [SerializeField] private List<MissaoDados> missoes = new List<MissaoDados>();

    [Header("Timer de inatividade")]
    [SerializeField] private float tempoInatividadeParaFechar = 30f;

    [Header("UI de Progresso (Player)")]
    [SerializeField] private PlayerMissoes uiProgresso;

    [Header("Extras para fechar ao ACEITAR")]
    [SerializeField] private GameObject[] objetosExtrasParaFecharAoAceitar;

    [Header("Atualizacao automatica (coleta)")]
    [SerializeField] private float intervaloChecagemInventario = 0.25f;

    public event Action<MissaoDados> OnProgressoAtualizado;
    public event Action<MissaoDados> OnMissaoProntaParaEntregar;

    private int missaoAtualIndex;
    private GameObject canvasAtivoNoMomento;
    private Coroutine rotinaDialogo;
    private Coroutine rotinaTimerInatividade;
    private float proximaChecagemInventario;
    private InventarioVR inventarioJogadorCache;

    private void Awake()
    {
        EsconderTodosOsCanvas();
    }

    private void Update()
    {
        MissaoDados missao = ObterMissaoAtual();
        if (missao == null) return;
        if (missao.estado != EstadoMissao.EmAndamento) return;
        if (missao.tipoObjetivo != TipoObjetivo.ColetarItens) return;

        if (Time.time < proximaChecagemInventario) return;
        proximaChecagemInventario = Time.time + intervaloChecagemInventario;

        ConferirInventarioEAtualizarSeMudou(missao);
    }

    // =========================================================
    // ABRIR / FECHAR (botão Missão)
    // =========================================================

    /// <summary>
    /// Chamado pelo clique no botão "Missao".
    /// Auto-corretivo: se canvasAtivoNoMomento aponta para um Canvas
    /// já desativado por fora, limpa a referência e segue o fluxo.
    /// </summary>
    public void NotificarHoverNPC()
    {
        // Só bloqueia se REALMENTE tem algo aberto na tela
        if (canvasAtivoNoMomento != null && canvasAtivoNoMomento.activeInHierarchy)
            return;

        // Estava preso ou não havia nada — reseta e continua
        canvasAtivoNoMomento = null;

        MissaoDados missaoAtual = ObterMissaoAtual();
        if (missaoAtual == null)
            return;

        switch (missaoAtual.estado)
        {
            case EstadoMissao.NaoIniciada:
                MostrarDialogo(missaoAtual);
                break;

            case EstadoMissao.EmAndamento:
                MostrarCanvas(missaoAtual.canvasEmAndamento);
                break;

            case EstadoMissao.ProntaParaEntregar:
                MostrarCanvas(missaoAtual.canvasConcluida);
                break;

            case EstadoMissao.Concluida:
                break;
        }
    }

    /// <summary>
    /// Alias opcional (mesmo comportamento).
    /// </summary>
    public void AbrirPainelMissao()
    {
        NotificarHoverNPC();
    }

    /// <summary>
    /// Fecha todos os popups de missão de forma segura.
    /// Ligue ao abrir o inventário e nos botões Criar / Reparar / Melhorar / X.
    /// </summary>
    public void FecharTodosPopupsMissao()
    {
        FecharCanvasAtivo();
        EsconderTodosOsCanvas();
    }

    public void NotificarHoverSaiuNPC() { }

    public void FecharPopupAtual() => FecharTodosPopupsMissao();

    // =========================================================
    // CANVAS / DIÁLOGO
    // =========================================================

    private void EsconderTodosOsCanvas()
    {
        if (missoes == null) return;

        for (int i = 0; i < missoes.Count; i++)
        {
            MissaoDados missao = missoes[i];
            if (missao == null) continue;

            SetActiveSeguro(missao.canvasDialogo, false);
            SetActiveSeguro(missao.canvasEmAndamento, false);
            SetActiveSeguro(missao.canvasConcluida, false);
            SetActiveSeguro(missao.botaoAceitar, false);
            SetActiveSeguro(missao.botaoRecusar, false);
        }

        canvasAtivoNoMomento = null;
    }

    private static void SetActiveSeguro(GameObject alvo, bool ativo)
    {
        if (alvo != null) alvo.SetActive(ativo);
    }

    private void FecharObjetosExtrasAoAceitar()
    {
        if (objetosExtrasParaFecharAoAceitar == null) return;
        for (int i = 0; i < objetosExtrasParaFecharAoAceitar.Length; i++)
            SetActiveSeguro(objetosExtrasParaFecharAoAceitar[i], false);
    }

    private MissaoDados ObterMissaoAtual()
    {
        if (missoes == null || missaoAtualIndex < 0 || missaoAtualIndex >= missoes.Count)
            return null;
        return missoes[missaoAtualIndex];
    }

    public MissaoDados ObterMissaoAtualPublica() => ObterMissaoAtual();

    public string ObterIdPersistencia()
    {
        return gameObject != null ? gameObject.name : GetType().Name;
    }

    internal Action CapturarRecuperacaoSave()
    {
        int indice = missaoAtualIndex;
        var restauracoes = new List<Action>();
        if (missoes != null)
            foreach (var missao in missoes)
            {
                if (missao == null) continue;
                var alvo = missao;
                var estado = alvo.estado;
                int quantidade = alvo.quantidadeAtualObjetivo;
                restauracoes.Add(() => { alvo.estado = estado; alvo.quantidadeAtualObjetivo = quantidade; });
            }
        // A restauracao das missoes pode modificar a UI; preserva seus componentes e hierarquia.
        var visuais = new List<RecuperacaoItemCarga>();
        foreach (Transform filho in transform)
            visuais.Add(new RecuperacaoItemCarga(filho));
        return () =>
        {
            foreach (var visual in visuais) visual.Restaurar();
            foreach (var restaurar in restauracoes) restaurar();
            missaoAtualIndex = indice;
        };
    }

    public List<MissionSaveData> CapturarEstadoParaSave()
    {
        List<MissionSaveData> resultado = new List<MissionSaveData>();
        if (missoes == null) return resultado;

        for (int i = 0; i < missoes.Count; i++)
        {
            MissaoDados missao = missoes[i];
            if (missao == null) continue;

            resultado.Add(new MissionSaveData
            {
                gerenciadorId = ObterIdPersistencia(),
                idMissao = missao.idMissao,
                estado = (int)missao.estado,
                quantidadeAtualObjetivo = missao.quantidadeAtualObjetivo
            });
        }

        return resultado;
    }

    public void RestaurarEstadoDoSave(List<MissionSaveData> estadosSalvos)
    {
        if (estadosSalvos == null || missoes == null) return;

        string idGerenciador = ObterIdPersistencia();
        int indiceRestaurado = 0;

        for (int i = 0; i < missoes.Count; i++)
        {
            MissaoDados missao = missoes[i];
            if (missao == null) continue;

            MissionSaveData salvo = null;
            for (int j = 0; j < estadosSalvos.Count; j++)
            {
                MissionSaveData candidato = estadosSalvos[j];
                if (candidato == null) continue;
                if (!string.Equals(candidato.gerenciadorId, idGerenciador, StringComparison.Ordinal)) continue;
                if (!string.Equals(candidato.idMissao, missao.idMissao, StringComparison.Ordinal)) continue;
                salvo = candidato;
                break;
            }

            if (salvo == null) continue;

            missao.estado = (EstadoMissao)Mathf.Clamp(salvo.estado, (int)EstadoMissao.NaoIniciada, (int)EstadoMissao.Concluida);
            missao.quantidadeAtualObjetivo = Mathf.Clamp(salvo.quantidadeAtualObjetivo, 0, Mathf.Max(1, missao.quantidadeAlvo));

            if (missao.estado != EstadoMissao.Concluida)
                indiceRestaurado = i;
        }

        missaoAtualIndex = Mathf.Clamp(indiceRestaurado, 0, Mathf.Max(0, missoes.Count - 1));
        MissaoDados missaoAtiva = ObterMissaoAtual();
        if (missaoAtiva != null && uiProgresso != null &&
            (missaoAtiva.estado == EstadoMissao.EmAndamento || missaoAtiva.estado == EstadoMissao.ProntaParaEntregar))
        {
            uiProgresso.MostrarMissao(missaoAtiva, this);
        }
    }

    private void SalvarEstadoAtual()
    {
        if (SaveManager.Instancia != null)
            SaveManager.Instancia.SalvarJogo();
    }

    private void MostrarCanvas(GameObject canvas)
    {
        FecharCanvasAtivo();
        if (canvas == null) return;

        canvas.SetActive(true);
        canvasAtivoNoMomento = canvas;
        IniciarTimerInatividade();
    }

    private void FecharCanvasAtivo()
    {
        if (rotinaDialogo != null)
        {
            StopCoroutine(rotinaDialogo);
            rotinaDialogo = null;
        }

        CancelarTimerInatividade();

        if (canvasAtivoNoMomento != null)
            canvasAtivoNoMomento.SetActive(false);

        canvasAtivoNoMomento = null;
    }

    private void IniciarTimerInatividade()
    {
        CancelarTimerInatividade();
        // No ferreiro o atendimento inteiro usa o relogio de FerreiroUI.
        var ferreiro = GetComponent<FerreiroUI>();
        if (ferreiro != null && ferreiro.isActiveAndEnabled) return;
        rotinaTimerInatividade = StartCoroutine(RotinaTimerInatividade());
    }

    private void CancelarTimerInatividade()
    {
        if (rotinaTimerInatividade != null)
        {
            StopCoroutine(rotinaTimerInatividade);
            rotinaTimerInatividade = null;
        }
    }

    private IEnumerator RotinaTimerInatividade()
    {
        yield return new WaitForSeconds(tempoInatividadeParaFechar);
        rotinaTimerInatividade = null;
        FecharCanvasAtivo();
    }

    private void MostrarDialogo(MissaoDados missao)
    {
        MostrarCanvas(missao.canvasDialogo);
        SetActiveSeguro(missao.botaoAceitar, false);
        SetActiveSeguro(missao.botaoRecusar, false);

        if (rotinaDialogo != null) StopCoroutine(rotinaDialogo);
        rotinaDialogo = StartCoroutine(RotinaSequenciaDeFalas(missao));
    }

    private IEnumerator RotinaSequenciaDeFalas(MissaoDados missao)
    {
        if (missao.falasDialogo == null || missao.falasDialogo.Count == 0)
        {
            RevelarBotoesDeDecisao(missao);
            yield break;
        }

        for (int i = 0; i < missao.falasDialogo.Count; i++)
        {
            TMP_Text fala = missao.falasDialogo[i];
            if (fala != null) fala.gameObject.SetActive(i == 0);
        }

        int ultimoIndice = missao.falasDialogo.Count - 1;
        for (int i = 0; i < ultimoIndice; i++)
        {
            yield return new WaitForSeconds(missao.tempoEntreFalas);

            if (missao.falasDialogo[i] != null)
                missao.falasDialogo[i].gameObject.SetActive(false);

            if (missao.falasDialogo[i + 1] != null)
                missao.falasDialogo[i + 1].gameObject.SetActive(true);
        }

        rotinaDialogo = null;
        RevelarBotoesDeDecisao(missao);
    }

    private void RevelarBotoesDeDecisao(MissaoDados missao)
    {
        SetActiveSeguro(missao.botaoAceitar, true);
        SetActiveSeguro(missao.botaoRecusar, true);
    }

    // =========================================================
    // ACEITAR / RECUSAR
    // =========================================================

    public void AceitarMissaoAtual()
    {
        MissaoDados missaoAtual = ObterMissaoAtual();
        if (missaoAtual == null || missaoAtual.estado != EstadoMissao.NaoIniciada)
            return;

        missaoAtual.estado = EstadoMissao.EmAndamento;
        missaoAtual.quantidadeAtualObjetivo = 0;

        if (uiProgresso != null)
            uiProgresso.MostrarMissao(missaoAtual, this);

        OnProgressoAtualizado?.Invoke(missaoAtual);

        if (missaoAtual.tipoObjetivo == TipoObjetivo.ColetarItens)
            AtualizarMissaoColetaAtiva();

        // Fecha de forma completa (evita canvas preso)
        FecharTodosPopupsMissao();
        FecharObjetosExtrasAoAceitar();
        SalvarEstadoAtual();
    }

    public void RecusarMissaoAtual()
    {
        FecharTodosPopupsMissao();
        FecharObjetosExtrasAoAceitar();
    }

    // =========================================================
    // MATAR INIMIGOS
    // =========================================================

    public void NotificarInimigoMorto(string idRespawnMonstro)
    {
        MissaoDados missao = ObterMissaoAtual();
        if (missao == null) return;
        if (missao.estado != EstadoMissao.EmAndamento) return;
        if (missao.tipoObjetivo != TipoObjetivo.MatarInimigos) return;
        if (string.IsNullOrEmpty(idRespawnMonstro)) return;
        if (!string.Equals(missao.idAlvo, idRespawnMonstro, StringComparison.OrdinalIgnoreCase))
            return;

        missao.quantidadeAtualObjetivo++;
        missao.quantidadeAtualObjetivo = Mathf.Min(missao.quantidadeAtualObjetivo, missao.quantidadeAlvo);

        if (uiProgresso != null)
            uiProgresso.AtualizarProgresso(missao);

        OnProgressoAtualizado?.Invoke(missao);

        if (missao.quantidadeAtualObjetivo >= missao.quantidadeAlvo)
            MarcarProntaParaEntregar(missao);

        SalvarEstadoAtual();
    }

    // =========================================================
    // COLETAR ITENS
    // =========================================================

    public void AtualizarMissaoColetaAtiva()
    {
        MissaoDados missao = ObterMissaoAtual();
        if (missao == null) return;
        if (missao.estado != EstadoMissao.EmAndamento) return;
        if (missao.tipoObjetivo != TipoObjetivo.ColetarItens) return;

        ConferirInventarioEAtualizarSeMudou(missao);
    }

    public void NotificarItemColetado(string nomeItem)
    {
        AtualizarMissaoColetaAtiva();
    }

    public void AtualizarProgressoColetaDoInventario(string nomeItem, int quantidadeNoInventario)
    {
        MissaoDados missao = ObterMissaoAtual();
        if (missao == null) return;
        if (missao.estado != EstadoMissao.EmAndamento) return;
        if (missao.tipoObjetivo != TipoObjetivo.ColetarItens) return;
        if (string.IsNullOrEmpty(nomeItem)) return;
        if (!string.Equals(missao.idAlvo, nomeItem, StringComparison.OrdinalIgnoreCase))
            return;

        int qtdParaMissao = Mathf.Clamp(quantidadeNoInventario, 0, missao.quantidadeAlvo);
        if (qtdParaMissao == missao.quantidadeAtualObjetivo)
            return;

        missao.quantidadeAtualObjetivo = qtdParaMissao;

        if (uiProgresso != null)
            uiProgresso.AtualizarProgresso(missao);

        OnProgressoAtualizado?.Invoke(missao);

        if (missao.quantidadeAtualObjetivo >= missao.quantidadeAlvo)
            MarcarProntaParaEntregar(missao);

        SalvarEstadoAtual();
    }

    private void ConferirInventarioEAtualizarSeMudou(MissaoDados missao)
    {
        if (missao == null || string.IsNullOrEmpty(missao.idAlvo)) return;

        int qtdReal = ObterQuantidadeItemNoInventario(missao.idAlvo);
        int qtdParaMissao = Mathf.Clamp(qtdReal, 0, missao.quantidadeAlvo);

        if (qtdParaMissao == missao.quantidadeAtualObjetivo)
            return;

        missao.quantidadeAtualObjetivo = qtdParaMissao;

        if (uiProgresso != null)
            uiProgresso.AtualizarProgresso(missao);

        OnProgressoAtualizado?.Invoke(missao);

        if (missao.quantidadeAtualObjetivo >= missao.quantidadeAlvo)
            MarcarProntaParaEntregar(missao);

        SalvarEstadoAtual();
    }

    private InventarioVR ObterInventarioJogador()
    {
        if (inventarioJogadorCache == null)
            inventarioJogadorCache = FindFirstObjectByType<InventarioVR>(FindObjectsInactive.Include);
        return inventarioJogadorCache;
    }

    public int ObterQuantidadeItemNoInventario(string nomeItem)
    {
        if (string.IsNullOrEmpty(nomeItem))
            return 0;

        InventarioVR inventario = ObterInventarioJogador();
        if (inventario == null)
            return 0;

        SlotInventario[] slots = inventario.ObterSlotsParaSave();
        if (slots == null || slots.Length == 0)
            return 0;

        int total = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            SlotInventario slot = slots[i];
            if (slot == null || !slot.PossuiItem())
                continue;

            string nomeNoSlot = ObterNomeItemDoSlot(slot);
            if (string.IsNullOrEmpty(nomeNoSlot))
                continue;

            if (string.Equals(nomeNoSlot, nomeItem, StringComparison.OrdinalIgnoreCase))
                total += slot.ObterQuantidadeAtual();
        }

        return total;
    }

    private static string ObterNomeItemDoSlot(SlotInventario slot)
    {
        if (slot == null) return string.Empty;

        ItemInventarioDados dados = slot.ObterItemRepresentante();
        if (dados != null && !string.IsNullOrWhiteSpace(dados.NomeItem))
            return dados.NomeItem.Trim();

        var item = slot.ObterItemRepresentanteParaSave();
        if (item != null)
            return SlotInventario.LimparNomeItem(item.name);

        return string.Empty;
    }

    private void MarcarProntaParaEntregar(MissaoDados missao)
    {
        if (missao.estado == EstadoMissao.ProntaParaEntregar)
            return;

        missao.estado = EstadoMissao.ProntaParaEntregar;

        if (uiProgresso != null)
            uiProgresso.MostrarMissaoCompleta(missao);

        OnMissaoProntaParaEntregar?.Invoke(missao);
    }

    // =========================================================
    // CONSUMIR ITENS NA ENTREGA
    // =========================================================

    public void EntregarRecompensaEAvancar()
    {
        MissaoDados missao = ObterMissaoAtual();
        if (entregaEmAndamento || missao == null || missao.estado != EstadoMissao.ProntaParaEntregar) return;
        entregaEmAndamento = true;
        SaveManager save = SaveManager.Instancia;
        save?.IniciarOperacaoCritica();
        var recuperarSlots = new List<Action>();
        var recuperarItens = new List<RecuperacaoItemCarga>();
        var reservados = new List<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        GameObject recompensa = null;
        StatusPlayer status = FindFirstObjectByType<StatusPlayer>();
        CarteiraReinPlayer carteira = status != null ? status.ObterCarteiraRein() : null;
        string statusAnterior = null;
        long saldoAnterior = carteira != null ? carteira.ReinUnidades : 0;
        bool sucesso = false;
        int indiceAnterior = missaoAtualIndex;
        try
        {
            statusAnterior = status != null ? JsonUtility.ToJson(status) : null;
            if ((missao.recompensaExperiencia > 0 && status == null) ||
                (ConverterReinParaUnidades(missao.recompensaRein) > 0 && carteira == null)) return;
            if (missao.tipoObjetivo == TipoObjetivo.ColetarItens)
            {
                InventarioVR inventario = ObterInventarioJogador();
                int necessario = Mathf.Max(0, missao.quantidadeAlvo);
                if (inventario == null || ObterQuantidadeItemNoInventario(missao.idAlvo) < necessario) return;
                var plano = new List<KeyValuePair<SlotInventario, int>>();
                foreach (var slot in inventario.ObterSlotsParaSave())
                {
                    if (slot == null || !string.Equals(ObterNomeItemDoSlot(slot), missao.idAlvo, StringComparison.OrdinalIgnoreCase)) continue;
                    int quantidade = Mathf.Min(necessario, slot.ObterQuantidadeAtual());
                    if (quantidade <= 0) continue;
                    plano.Add(new KeyValuePair<SlotInventario, int>(slot, quantidade));
                    recuperarSlots.Add(slot.CapturarRecuperacaoCarga());
                    foreach (var item in slot.ObterItensParaSave())
                        recuperarItens.Add(new RecuperacaoItemCarga(item));
                    necessario -= quantidade;
                    if (necessario == 0) break;
                }
                if (necessario != 0) return;
                foreach (var par in plano)
                    for (int i = 0; i < par.Value; i++)
                    {
                        if (!par.Key.ReservarUnidadeParaConsumo(out var item))
                            throw new InvalidOperationException("Reserva de materiais incompleta.");
                        reservados.Add(item);
                    }
                if (reservados.Count != Mathf.Max(0, missao.quantidadeAlvo))
                    throw new InvalidOperationException("Quantidade consumida divergente.");
            }
            if (missao.prefabRecompensa != null)
                recompensa = Instantiate(missao.prefabRecompensa,
                    missao.pontoSpawnRecompensa != null ? missao.pontoSpawnRecompensa.position : transform.position,
                    missao.pontoSpawnRecompensa != null ? missao.pontoSpawnRecompensa.rotation : Quaternion.identity);
            EntregarRecompensasAoPlayer(missao);
            missao.estado = EstadoMissao.Concluida;
            missaoAtualIndex++;
            foreach (var item in reservados)
            {
                item.GetComponent<ItemPersistente>()?.RegistrarConsumoPermanente();
                Destroy(item.gameObject);
            }
            sucesso = true;
        }
        catch (Exception erro) { Debug.LogError("[Missao] Entrega nao confirmada: " + erro, this); }
        finally
        {
            try
            {
                if (!sucesso)
                {
                    foreach (var recuperar in recuperarSlots) recuperar();
                    foreach (var recuperar in recuperarItens) recuperar.Restaurar();
                    if (recompensa != null) { recompensa.SetActive(false); Destroy(recompensa); }
                    if (status != null && statusAnterior != null) JsonUtility.FromJsonOverwrite(statusAnterior, status);
                    if (carteira != null) carteira.DefinirSaldoPorUnidades(saldoAnterior);
                    missao.estado = EstadoMissao.ProntaParaEntregar; missaoAtualIndex = indiceAnterior;
                }
            }
            catch (Exception rollback)
            {
                save?.NotificarFalhaRestauracao("Entrega de missao: recuperacao incompleta. " + rollback.Message);
                Debug.LogError(rollback, this);
            }
            finally { save?.FinalizarOperacaoCritica(); entregaEmAndamento = false; }
        }
        if (!sucesso) return;
        if (uiProgresso != null) uiProgresso.Esconder(missao);
        FecharTodosPopupsMissao(); FecharObjetosExtrasAoAceitar();
        SalvarEstadoAtual();
    }

    private void EntregarRecompensasAoPlayer(MissaoDados missao)
    {
        if (missao == null) return;

        StatusPlayer status = FindFirstObjectByType<StatusPlayer>();
        if (status == null)
        {
            Debug.LogWarning("[Missão] StatusPlayer não encontrado.");
            return;
        }

        if (missao.recompensaExperiencia > 0)
            status.ReceberExperiencia(missao.recompensaExperiencia);

        if (!string.IsNullOrWhiteSpace(missao.recompensaRein) && missao.recompensaRein.Trim() != "0")
        {
            CarteiraReinPlayer carteira = status.ObterCarteiraRein();
            if (carteira != null)
            {
                long unidades = ConverterReinParaUnidades(missao.recompensaRein);
                if (unidades > 0)
                    carteira.AdicionarReinUnidades(unidades);
            }
        }
    }

    private static long ConverterReinParaUnidades(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return 0L;

        string normalizado = texto.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal valor))
            return 0L;

        if (valor <= 0m) return 0L;
        return CarteiraReinPlayer.ConverterDecimalParaUnidades(valor);
    }
}
