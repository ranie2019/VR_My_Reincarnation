using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Componente ÚNICO na cena (ex: no Invetario Ferreiro).
///
/// Responsabilidade ÚNICA deste script:
/// - Guardar, para cada botão "Create", sua própria lista de custos (ex: Pedra x10, Madeira x10).
/// - A cada frame, verificar se o jogador tem recurso suficiente para cada botão.
/// - Ativar ou desativar cada botão (UI + XRSimpleInteractable) individualmente, de acordo com isso.
///
/// A lógica de spawnar o prefab e de confirmar com hover + botão do controle
/// fica no script BotaoCriarItem (em cada botão), que consulta este script
/// passando a referência do próprio Button.
/// </summary>
public class CriarItem : MonoBehaviour
{
    [Serializable]
    public class CustoRecurso
    {
        [Tooltip("Precisa bater exatamente com o NomeItem do ItemInventarioDados do recurso (ex: \"Pedra\", \"Madeira\").")]
        public string nomeRecurso;

        [Min(1)]
        public int quantidade = 1;

        [Tooltip("Arraste aqui o texto (TMP) que mostra a quantidade atual/necessária deste recurso (ex: \"Madeira 5/10\").")]
        public TMP_Text textoQuantidade;
    }

    [Serializable]
    public class ConfiguracaoBotao
    {
        [Tooltip("Arraste aqui o Button (Create) correspondente a este item.")]
        public Button botao;

        [Tooltip("Custo individual deste item (ex: Pedra x10, Madeira x10).")]
        public List<CustoRecurso> custos = new List<CustoRecurso>();

        [NonSerialized] public XRSimpleInteractable interactableXR;
        [NonSerialized] public bool referenciasCapturadas;
    }

    [Header("Botões e seus custos")]
    [SerializeField] private List<ConfiguracaoBotao> botoes = new List<ConfiguracaoBotao>();

    [Header("Inventário do jogador")]
    [Tooltip("Se vazio, tenta encontrar automaticamente na cena.")]
    [SerializeField] private InventarioVR inventarioJogador;

    [Header("Fechar por inatividade")]
    [Tooltip("Tempo (em segundos) sem nenhum botão em hover até este Canvas fechar sozinho.")]
    [SerializeField] private float tempoParaFecharPorInatividade = 30f;

    private float ultimaInteracao;

    private void OnEnable()
    {
        ultimaInteracao = Time.time;
    }

    private void Awake()
    {
        BuscarInventarioSeNecessario();
    }

    private void Update()
    {
        AtualizarEstadoDeTodosOsBotoes();
        VerificarFechamentoPorInatividade();
    }

    private void VerificarFechamentoPorInatividade()
    {
        var ferreiro = GetComponentInParent<FerreiroUI>(true);
        if (ferreiro != null && ferreiro.isActiveAndEnabled) return;
        if (Time.time - ultimaInteracao < tempoParaFecharPorInatividade)
            return;

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Reseta o contador de inatividade. Chamado por qualquer BotaoCriarItem
    /// quando o laser passa em cima dele (hover), já que este componente fica
    /// no mesmo Canvas do inventário.
    /// </summary>
    public void RegistrarInteracao()
    {
        ultimaInteracao = Time.time;
    }

    private bool BuscarInventarioSeNecessario()
    {
        if (inventarioJogador != null)
            return true;

        GameObject jogador = GameObject.FindGameObjectWithTag("Player");
        if (jogador != null)
            inventarioJogador = jogador.GetComponentInChildren<InventarioVR>(true);

        if (inventarioJogador == null)
        {
            InventarioVR[] candidatos = FindObjectsByType<InventarioVR>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int maiorQuantidade = -1;
            for (int i = 0; i < candidatos.Length; i++)
            {
                InventarioVR candidato = candidatos[i];
                if (candidato == null) continue;
                SlotInventario[] slotsCandidato = candidato.ObterSlotsParaSave();
                int quantidade = 0;
                for (int j = 0; j < slotsCandidato.Length; j++)
                    if (slotsCandidato[j] != null) quantidade += slotsCandidato[j].ObterQuantidadeAtual();
                if (quantidade > maiorQuantidade)
                {
                    maiorQuantidade = quantidade;
                    inventarioJogador = candidato;
                }
            }
        }
        return inventarioJogador != null;
    }

    private SlotInventario[] ObterSlotsDoJogador()
    {
        if (!BuscarInventarioSeNecessario())
            return Array.Empty<SlotInventario>();

        return inventarioJogador.ObterSlotsParaSave();
    }

    private ConfiguracaoBotao EncontrarConfiguracao(Button botao)
    {
        if (botao == null || botoes == null)
            return null;

        for (int i = 0; i < botoes.Count; i++)
        {
            if (botoes[i] != null && botoes[i].botao == botao)
                return botoes[i];
        }

        return null;
    }

    /// <summary>
    /// Conta quantas unidades de um recurso (pelo nome) o jogador tem no inventário,
    /// somando entre todos os slots que guardam esse item.
    /// </summary>
    private int ContarRecurso(string nomeRecurso)
    {
        if (string.IsNullOrWhiteSpace(nomeRecurso))
            return 0;

        SlotInventario[] slots = ObterSlotsDoJogador();
        int total = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            SlotInventario slot = slots[i];
            if (slot == null || !slot.PossuiItem())
                continue;

            if (CorrespondeAoRecurso(slot, nomeRecurso))
                total += slot.ObterQuantidadeAtual();
        }

        return total;
    }

    private bool PossuiRecursosSuficientes(ConfiguracaoBotao configuracao)
    {
        if (!TentarAgruparCustos(configuracao, out var custos)) return false;
        foreach (var custo in configuracao.custos)
            if (custo != null) AtualizarTextoQuantidade(custo, ContarRecurso(custo.nomeRecurso));
        foreach (var custo in custos)
            if (ContarRecurso(custo.Key) < custo.Value) return false;
        return true;
    }

    private static bool TentarAgruparCustos(ConfiguracaoBotao configuracao, out Dictionary<string, int> custos)
    {
        custos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (configuracao == null || configuracao.custos == null) return false;
        foreach (var custo in configuracao.custos)
        {
            if (custo == null) continue;
            if (string.IsNullOrWhiteSpace(custo.nomeRecurso) || custo.quantidade <= 0) return false;
            string nome = custo.nomeRecurso.Trim();
            custos.TryGetValue(nome, out int atual);
            if ((long)atual + custo.quantidade > int.MaxValue) return false;
            custos[nome] = atual + custo.quantidade;
        }
        return true;
    }

    private static void AtualizarTextoQuantidade(CustoRecurso custo, int quantidadeAtual)
    {
        if (custo.textoQuantidade == null)
            return;

        custo.textoQuantidade.text = quantidadeAtual.ToString();
    }

    /// <summary>
    /// Verifica se o jogador tem os recursos necessários para o item deste botão específico.
    /// </summary>
    public bool PossuiRecursosSuficientes(Button botao)
    {
        return PossuiRecursosSuficientes(EncontrarConfiguracao(botao));
    }

    /// <summary>
    /// Remove do inventário do jogador os recursos usados pelo item deste botão específico.
    /// Chamado pelo BotaoCriarItem depois de confirmar a criação.
    /// </summary>
    private bool consumindoRecursos;

    public bool ConsumirRecursos(Button botao) => ExecutarConsumo(botao, null);

    public bool TentarCriarComRecursos(Button botao, Func<GameObject> criar)
    {
        return criar != null && ExecutarConsumo(botao, criar);
    }

    private bool ExecutarConsumo(Button botao, Func<GameObject> criar)
    {
        if (consumindoRecursos) return false;
        var configuracao = EncontrarConfiguracao(botao);
        if (!TentarAgruparCustos(configuracao, out var custos) || !PossuiRecursosSuficientes(configuracao)) return false;
        consumindoRecursos = true;
        var save = SaveManager.Instancia;
        save?.IniciarOperacaoCritica();
        var recuperarSlots = new List<Action>();
        var recuperarItens = new List<RecuperacaoItemCarga>();
        var reservados = new List<XRGrabInteractable>();
        GameObject criado = null;
        bool sucesso = false;
        try
        {
            var plano = new List<KeyValuePair<SlotInventario, int>>();
            foreach (var custo in custos)
            {
                int restante = custo.Value;
                foreach (var slot in ObterSlotsDoJogador())
                {
                    if (slot == null || !CorrespondeAoRecurso(slot, custo.Key)) continue;
                    int quantidade = Mathf.Min(restante, slot.ObterQuantidadeAtual());
                    if (quantidade <= 0) continue;
                    plano.Add(new KeyValuePair<SlotInventario, int>(slot, quantidade));
                    recuperarSlots.Add(slot.CapturarRecuperacaoCarga());
                    foreach (var item in slot.ObterItensParaSave()) recuperarItens.Add(new RecuperacaoItemCarga(item));
                    restante -= quantidade;
                    if (restante == 0) break;
                }
                if (restante != 0) throw new InvalidOperationException("Materiais insuficientes durante a reserva.");
            }
            foreach (var parte in plano)
                for (int i = 0; i < parte.Value; i++)
                {
                    if (!parte.Key.ReservarUnidadeParaConsumo(out var item))
                        throw new InvalidOperationException("Nao foi possivel reservar todos os materiais.");
                    reservados.Add(item);
                }
            if (criar != null)
            {
                criado = criar();
                if (criado == null) throw new InvalidOperationException("A criacao nao retornou um item.");
                criado.GetComponent<Machado>()?.GarantirSorteio();
                criado.GetComponent<Picareta>()?.GarantirSorteio();
                criado.GetComponent<Escudo>()?.GarantirSorteio();
                var espada = criado.GetComponent<Espada>();
                if (espada != null && !espada.GarantirGeracao())
                    throw new InvalidOperationException("Configuracao de atributos da arma invalida.");
                var atributos = criado.GetComponent<AtributosManoplas>();
                if (atributos != null && !atributos.GarantirGeracao())
                    throw new InvalidOperationException("Configuracao de atributos invalida no item criado.");
                var peitoral = criado.GetComponent<EquiparPeitoral>();
                if (peitoral != null && !peitoral.GarantirGeracao())
                    throw new InvalidOperationException("Configuracao de atributos do peitoral invalida no item criado.");
                var elmo = criado.GetComponent<EquiparElmo>();
                if (elmo != null && !elmo.GarantirGeracao())
                    throw new InvalidOperationException("Configuracao de atributos do elmo invalida no item criado.");
            }
            // A partir daqui nao existem operacoes reversiveis pendentes.
            foreach (var item in reservados) item.GetComponent<ItemPersistente>()?.RegistrarConsumoPermanente();
            sucesso = true;
            foreach (var item in reservados) Destroy(item.gameObject);
            return true;
        }
        catch (Exception erro)
        {
            Debug.LogError("[CriarItem] Criacao cancelada: " + erro.Message, this);
            return false;
        }
        finally
        {
            try
            {
                if (!sucesso)
                {
                    if (criado != null) { criado.SetActive(false); Destroy(criado); }
                    foreach (var recuperar in recuperarSlots) recuperar();
                    foreach (var recuperar in recuperarItens) recuperar.Restaurar();
                }
            }
            catch (Exception erro)
            {
                save?.NotificarFalhaRestauracao("Recuperacao dos materiais incompleta: " + erro.Message);
                Debug.LogError(erro, this);
            }
            finally { consumindoRecursos = false; save?.FinalizarOperacaoCritica(); }
        }
    }

    /// <summary>
    /// Checa os recursos de cada botão da lista e liga/desliga cada um
    /// (UI + XRSimpleInteractable) individualmente.
    /// </summary>
    private static bool CorrespondeAoRecurso(SlotInventario slot, string nomeRecurso)
    {
        if (slot == null || string.IsNullOrWhiteSpace(nomeRecurso)) return false;
        XRGrabInteractable item = slot.ObterItemRepresentanteParaSave();
        return item != null && string.Equals(ObterNomeDoRecurso(item), nomeRecurso.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string ObterNomeDoRecurso(XRGrabInteractable item)
    {
        if (item == null) return string.Empty;
        ItemInventarioDados dados = item.GetComponent<ItemInventarioDados>() ?? item.GetComponentInChildren<ItemInventarioDados>(true);
        if (dados != null && !string.IsNullOrWhiteSpace(dados.NomeItem)) return SlotInventario.LimparNomeItem(dados.NomeItem);
        ItemPersistente persistente = item.GetComponent<ItemPersistente>() ?? item.GetComponentInChildren<ItemPersistente>(true);
        if (persistente != null && !string.IsNullOrWhiteSpace(persistente.ObterNomeItem())) return SlotInventario.LimparNomeItem(persistente.ObterNomeItem());
        return SlotInventario.LimparNomeItem(item.gameObject.name);
    }

    private void AtualizarEstadoDeTodosOsBotoes()
    {
        if (botoes == null)
            return;

        for (int i = 0; i < botoes.Count; i++)
        {
            ConfiguracaoBotao configuracao = botoes[i];
            if (configuracao == null || configuracao.botao == null)
                continue;

            CapturarReferenciasSeNecessario(configuracao);

            bool recursosSuficientes = PossuiRecursosSuficientes(configuracao);

            configuracao.botao.interactable = recursosSuficientes;

            if (configuracao.interactableXR != null)
                configuracao.interactableXR.enabled = recursosSuficientes;
        }
    }

    private void CapturarReferenciasSeNecessario(ConfiguracaoBotao configuracao)
    {
        if (configuracao.referenciasCapturadas)
            return;

        configuracao.interactableXR = configuracao.botao.GetComponent<XRSimpleInteractable>();
        configuracao.referenciasCapturadas = true;
    }
}