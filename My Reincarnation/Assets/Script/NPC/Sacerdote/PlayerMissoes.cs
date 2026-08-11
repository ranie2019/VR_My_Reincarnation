using TMPro;
using UnityEngine;

/// <summary>
/// Fica no Player (Canvas de missões).
/// Mostra o progresso da missão ativa.
/// 
/// O painel só fica visível enquanto houver missão:
/// - Em andamento (EmAndamento)
/// - Ou pronta para entregar (ProntaParaEntregar)
/// 
/// Só some quando a missão for entregue no NPC.
/// </summary>
public class PlayerMissoes : MonoBehaviour
{
    [Header("UI - Painel de Missão")]
    [Tooltip("O painel inteiro que contém os textos (começa desativado).")]
    [SerializeField] private GameObject painelMissoes;

    [Header("Textos")]
    [Tooltip("Título fixo (ex: MISSIONS). Não precisa ser preenchido pelo script.")]
    [SerializeField] private TMP_Text missaoTitulo;

    [Tooltip("Descrição do que precisa ser feito (ex: Eliminate 10 green slimes).")]
    [SerializeField] private TMP_Text missaoTexto;

    [Tooltip("Valor atual do progresso (ex: 5).")]
    [SerializeField] private TMP_Text missaoValorAtual;

    [Tooltip("Valor final necessário (ex: 10).")]
    [SerializeField] private TMP_Text missaoValorFinal;

    [Header("Referência")]
    [Tooltip("Se vazio, tenta encontrar automaticamente na cena.")]
    [SerializeField] private GerenciadorMissoes gerenciador;

    private GerenciadorMissoes.MissaoDados missaoAtiva;

    private void Awake()
    {
        if (gerenciador == null)
            gerenciador = FindFirstObjectByType<GerenciadorMissoes>();

        // Começa sempre desativado
        Esconder();
    }

    private void OnEnable()
    {
        if (gerenciador != null)
        {
            gerenciador.OnProgressoAtualizado += OnProgressoAtualizado;
            gerenciador.OnMissaoProntaParaEntregar += OnMissaoProntaParaEntregar;
        }
    }

    private void OnDisable()
    {
        if (gerenciador != null)
        {
            gerenciador.OnProgressoAtualizado -= OnProgressoAtualizado;
            gerenciador.OnMissaoProntaParaEntregar -= OnMissaoProntaParaEntregar;
        }
    }

    // =========================================================
    //  API PÚBLICA (chamada pelo GerenciadorMissoes)
    // =========================================================

    /// <summary>
    /// Chamado quando o jogador ACEITA uma missão.
    /// Ativa o painel e mostra os dados.
    /// </summary>
    public void MostrarMissao(GerenciadorMissoes.MissaoDados missao)
    {
        if (missao == null) return;

        missaoAtiva = missao;

        if (painelMissoes != null)
            painelMissoes.SetActive(true);

        AtualizarUI(missao);
    }

    /// <summary>
    /// Atualiza o progresso (ex: 5/10).
    /// Mantém o painel ativo.
    /// </summary>
    public void AtualizarProgresso(GerenciadorMissoes.MissaoDados missao)
    {
        if (missao == null) return;

        missaoAtiva = missao;

        // Garante que está visível
        if (painelMissoes != null && !painelMissoes.activeSelf)
            painelMissoes.SetActive(true);

        AtualizarUI(missao);
    }

    /// <summary>
    /// Chamado quando chega em 10/10.
    /// O painel CONTINUA visível (só muda o texto).
    /// </summary>
    public void MostrarMissaoCompleta(GerenciadorMissoes.MissaoDados missao)
    {
        if (missao == null) return;

        missaoAtiva = missao;

        if (painelMissoes != null && !painelMissoes.activeSelf)
            painelMissoes.SetActive(true);

        AtualizarUI(missao);

        if (missaoTexto != null)
            missaoTexto.text = GetDescricaoMissao(missao);
    }

    /// <summary>
    /// Só é chamado quando a missão é ENTREGUE no NPC.
    /// Aí sim o painel some.
    /// </summary>
    public void Esconder()
    {
        missaoAtiva = null;

        if (painelMissoes != null)
            painelMissoes.SetActive(false);
    }

    // =========================================================
    //  LÓGICA INTERNA
    // =========================================================

    private void AtualizarUI(GerenciadorMissoes.MissaoDados missao)
    {
        if (missaoTexto != null)
            missaoTexto.text = GetDescricaoMissao(missao);

        if (missaoValorAtual != null)
            missaoValorAtual.text = missao.quantidadeAtualObjetivo.ToString();

        if (missaoValorFinal != null)
            missaoValorFinal.text = missao.quantidadeAlvo.ToString();
    }

    private string GetDescricaoMissao(GerenciadorMissoes.MissaoDados missao)
    {
        if (missao == null) return "";

        switch (missao.tipoObjetivo)
        {
            case GerenciadorMissoes.TipoObjetivo.MatarInimigos:
                return GetDescricaoExterminio(missao);

            case GerenciadorMissoes.TipoObjetivo.ColetarItens:
                return GetDescricaoColeta(missao);

            default:
                return string.IsNullOrEmpty(missao.idMissao) ? "Missão ativa" : missao.idMissao;
        }
    }

    private string GetDescricaoExterminio(GerenciadorMissoes.MissaoDados missao)
    {
        string nomeAlvo = string.IsNullOrEmpty(missao.idAlvo) ? "inimigos" : missao.idAlvo;
        return $"Eliminate {missao.quantidadeAlvo} {nomeAlvo}.";
    }

    private string GetDescricaoColeta(GerenciadorMissoes.MissaoDados missao)
    {
        string nomeItem = string.IsNullOrEmpty(missao.idAlvo) ? "itens" : missao.idAlvo;
        return $"Colete {missao.quantidadeAlvo} {nomeItem}.";
    }

    // =========================================================
    //  EVENTOS DO GERENCIADOR
    // =========================================================

    private void OnProgressoAtualizado(GerenciadorMissoes.MissaoDados missao)
    {
        AtualizarProgresso(missao);
    }

    private void OnMissaoProntaParaEntregar(GerenciadorMissoes.MissaoDados missao)
    {
        MostrarMissaoCompleta(missao);
    }
}