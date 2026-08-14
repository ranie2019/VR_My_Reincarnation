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

    [Tooltip("Descrição do que precisa ser feito (ex: Colete 10 madeiras).")]
    [SerializeField] private TMP_Text missaoTexto;

    [Tooltip("Valor atual do progresso (ex: 5).")]
    [SerializeField] private TMP_Text missaoValorAtual;

    [Tooltip("Valor final necessário (ex: 10).")]
    [SerializeField] private TMP_Text missaoValorFinal;

    private GerenciadorMissoes.MissaoDados missaoAtiva;
    private GerenciadorMissoes gerenciadorAtual;

    private void Awake()
    {
        Esconder();
    }

    private void OnDisable()
    {
        DesinscreverDoGerenciadorAtual();
    }

    // =========================================================
    // API PÚBLICA (chamada pelo GerenciadorMissoes)
    // =========================================================

    /// <summary>
    /// Chamado quando o jogador ACEITA uma missão.
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
    /// Versão recomendada: registra o gerenciador de origem (Ferreiro/Sacerdote).
    /// </summary>
    public void MostrarMissao(GerenciadorMissoes.MissaoDados missao, GerenciadorMissoes origem)
    {
        if (missao == null) return;

        DesinscreverDoGerenciadorAtual();

        gerenciadorAtual = origem;
        missaoAtiva = missao;

        if (gerenciadorAtual != null)
        {
            gerenciadorAtual.OnProgressoAtualizado += OnProgressoAtualizado;
            gerenciadorAtual.OnMissaoProntaParaEntregar += OnMissaoProntaParaEntregar;
        }

        if (painelMissoes != null)
            painelMissoes.SetActive(true);

        AtualizarUI(missao);
    }

    /// <summary>
    /// Atualiza o progresso (ex: 5/10). Mantém o painel ativo.
    /// </summary>
    public void AtualizarProgresso(GerenciadorMissoes.MissaoDados missao)
    {
        if (missao == null) return;

        missaoAtiva = missao;

        if (painelMissoes != null && !painelMissoes.activeSelf)
            painelMissoes.SetActive(true);

        AtualizarUI(missao);
    }

    /// <summary>
    /// Chamado quando chega no objetivo (ex: 10/10).
    /// O painel CONTINUA visível.
    /// </summary>
    public void MostrarMissaoCompleta(GerenciadorMissoes.MissaoDados missao)
    {
        if (missao == null) return;

        missaoAtiva = missao;

        if (painelMissoes != null && !painelMissoes.activeSelf)
            painelMissoes.SetActive(true);

        AtualizarUI(missao);
    }

    /// <summary>
    /// Só é chamado quando a missão é ENTREGUE no NPC.
    /// </summary>
    public void Esconder()
    {
        DesinscreverDoGerenciadorAtual();
        missaoAtiva = null;

        if (painelMissoes != null)
            painelMissoes.SetActive(false);
    }

    // =========================================================
    // LÓGICA INTERNA
    // =========================================================

    private void AtualizarUI(GerenciadorMissoes.MissaoDados missao)
    {
        if (missao == null) return;

        if (missaoTexto != null)
            missaoTexto.text = GetDescricaoMissao(missao);

        // Trava visual no máximo da missão (nunca 20/10)
        int atual = Mathf.Clamp(missao.quantidadeAtualObjetivo, 0, Mathf.Max(1, missao.quantidadeAlvo));
        int final = Mathf.Max(1, missao.quantidadeAlvo);

        if (missaoValorAtual != null)
            missaoValorAtual.text = atual.ToString();

        if (missaoValorFinal != null)
            missaoValorFinal.text = final.ToString();
    }

    private string GetDescricaoMissao(GerenciadorMissoes.MissaoDados missao)
    {
        if (missao == null) return "";

        if (!string.IsNullOrWhiteSpace(missao.enunciadoMissao))
            return missao.enunciadoMissao.Trim();

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

    private void DesinscreverDoGerenciadorAtual()
    {
        if (gerenciadorAtual != null)
        {
            gerenciadorAtual.OnProgressoAtualizado -= OnProgressoAtualizado;
            gerenciadorAtual.OnMissaoProntaParaEntregar -= OnMissaoProntaParaEntregar;
            gerenciadorAtual = null;
        }
    }

    // =========================================================
    // EVENTOS DO GERENCIADOR
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