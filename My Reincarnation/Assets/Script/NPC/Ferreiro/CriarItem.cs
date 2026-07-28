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

        inventarioJogador = FindFirstObjectByType<InventarioVR>();
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

            ItemInventarioDados dados = slot.ObterItemRepresentante();
            if (dados == null)
                continue;

            if (string.Equals(dados.NomeItem, nomeRecurso, StringComparison.OrdinalIgnoreCase))
                total += slot.ObterQuantidadeAtual();
        }

        return total;
    }

    private bool PossuiRecursosSuficientes(ConfiguracaoBotao configuracao)
    {
        if (configuracao == null || configuracao.custos == null)
            return false;

        bool suficiente = true;

        for (int i = 0; i < configuracao.custos.Count; i++)
        {
            CustoRecurso custo = configuracao.custos[i];
            if (custo == null)
                continue;

            int quantidadeAtual = ContarRecurso(custo.nomeRecurso);
            AtualizarTextoQuantidade(custo, quantidadeAtual);

            if (quantidadeAtual < custo.quantidade)
                suficiente = false;
        }

        return suficiente;
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
    public bool ConsumirRecursos(Button botao)
    {
        ConfiguracaoBotao configuracao = EncontrarConfiguracao(botao);
        if (configuracao == null || !PossuiRecursosSuficientes(configuracao))
            return false;

        for (int i = 0; i < configuracao.custos.Count; i++)
        {
            CustoRecurso custo = configuracao.custos[i];
            if (custo != null)
                ConsumirRecurso(custo.nomeRecurso, custo.quantidade);
        }

        return true;
    }

    private void ConsumirRecurso(string nomeRecurso, int quantidade)
    {
        if (quantidade <= 0)
            return;

        SlotInventario[] slots = ObterSlotsDoJogador();
        int restante = quantidade;

        for (int i = 0; i < slots.Length && restante > 0; i++)
        {
            SlotInventario slot = slots[i];
            if (slot == null || !slot.PossuiItem())
                continue;

            ItemInventarioDados dados = slot.ObterItemRepresentante();
            if (dados == null || !string.Equals(dados.NomeItem, nomeRecurso, StringComparison.OrdinalIgnoreCase))
                continue;

            while (restante > 0 && slot.PossuiItem())
            {
                if (!slot.ConsumirUmaUnidade(out _))
                    break;

                restante--;
            }
        }
    }

    /// <summary>
    /// Checa os recursos de cada botão da lista e liga/desliga cada um
    /// (UI + XRSimpleInteractable) individualmente.
    /// </summary>
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