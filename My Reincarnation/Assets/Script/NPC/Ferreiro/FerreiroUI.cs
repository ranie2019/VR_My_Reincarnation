using System.Collections;
using UnityEngine;

/// <summary>
/// Fica no NPC Ferreiro (ou num objeto de UI dele).
///
/// Fluxo:
/// 1. Laser entra no NPC -> abre o Popup de Saudacao (Hello + YES + X).
/// 2. Clica X -> fecha tudo.
/// 3. Clica YES -> esconde a saudacao e mostra o Painel Inventario
///    (Criar / Reparar / Melhorar / Missao). Nenhum painel de conteudo fica aberto.
/// 4. Clica num dos 4 botoes -> mantém o Painel Inventario aberto e
///    troca apenas o painel de conteudo correspondente.
/// 5. Botao X fecha tudo, em qualquer etapa.
/// 6. Ao aceitar a missao (botao Yes dentro do dialogo) -> fecha o Painel Inventario
///    e os paineis de conteudo (via OnMissaoAceita).
/// 7. 30 segundos sem interacao -> fecha tudo sozinho.
///
/// Os botoes usam BotaoMissao (hover + botao do controle), com o evento
/// "Ao Confirmar" apontando para os metodos deste script.
/// </summary>
public class FerreiroUI : MonoBehaviour
{
    [Header("Popup de Saudacao (primeiro)")]
    [Tooltip("Canvas que aparece quando o laser entra no NPC (Hello + YES + X). Comeca desativado.")]
    [SerializeField] private GameObject popupSaudacao;

    [Header("Botoes de Categoria / Painel Inventario")]
    [Tooltip("Container com os 4 botoes (Criar/Reparar/Melhorar/Missao). Comeca desativado.")]
    [SerializeField] private GameObject botoesCategoria;

    [Header("Paineis de Conteudo")]
    [Tooltip("Painel de criacao de itens.")]
    [SerializeField] private GameObject painelCriar;

    [Tooltip("Painel de reparo de equipamentos.")]
    [SerializeField] private GameObject painelReparar;

    [Tooltip("Painel de melhoria de equipamentos.")]
    [SerializeField] private GameObject painelMelhorar;

    [Tooltip("Painel/Canvas de missao (o GerenciadorMissoes controla o conteudo dele).")]
    [SerializeField] private GameObject painelMissao;

    [Header("Sistema de Missao")]
    [Tooltip("Arraste o GerenciadorMissoes do PRÓPRIO Ferreiro (não use Find automático).")]
    [SerializeField] private GerenciadorMissoes gerenciadorMissoes;

    [Header("Configuracoes")]
    [Tooltip("Tempo (segundos) sem interacao ate fechar tudo sozinho.")]
    [SerializeField] private float tempoInatividade = 30f;

    private Coroutine timerInatividade;
    private GameObject painelAtivoAtual;

    private void Awake()
    {
        // Não procura mais automaticamente. O campo deve ser preenchido no Inspector.
        FecharTudo();
    }

    // =========================================================
    // CHAMADO PELO SISTEMA DE LASER / NPC
    // =========================================================

    /// <summary>
    /// Chame isso quando o laser entrar no Ferreiro.
    /// Não reinicia nada se já tiver algo aberto.
    /// </summary>
    public void AbrirPopupSaudacao()
    {
        if (popupSaudacao != null && popupSaudacao.activeSelf)
            return;

        if (botoesCategoria != null && botoesCategoria.activeSelf)
            return;

        if (painelAtivoAtual != null)
            return;

        FecharTudo();

        if (popupSaudacao != null)
            popupSaudacao.SetActive(true);

        ReiniciarTimer();
    }

    // =========================================================
    // BOTOES DO POPUP DE SAUDACAO
    // =========================================================

    /// <summary>
    /// Botao YES -> esconde a saudacao, mostra o Painel Inventario (4 botoes).
    /// Garante que nenhum painel de conteudo fique aberto.
    /// </summary>
    public void OnClickYes()
    {
        SetActiveSeguro(popupSaudacao, false);
        FecharPaineisConteudo();
        SetActiveSeguro(botoesCategoria, true);
        ReiniciarTimer();
    }

    /// <summary>
    /// Botao X (fechar). Funciona em qualquer etapa do fluxo.
    /// </summary>
    public void OnClickFechar()
    {
        FecharTudo();
    }

    // =========================================================
    // BOTOES DE CATEGORIA
    // =========================================================

    public void OnClickCriar()
    {
        AbrirPainelConteudo(painelCriar);
    }

    public void OnClickReparar()
    {
        AbrirPainelConteudo(painelReparar);
    }

    public void OnClickMelhorar()
    {
        AbrirPainelConteudo(painelMelhorar);
    }

    public void OnClickMissao()
    {
        AbrirPainelConteudo(painelMissao);

        if (gerenciadorMissoes != null)
            gerenciadorMissoes.NotificarHoverNPC();
        else
            Debug.LogWarning("FerreiroUI: GerenciadorMissoes não está atribuído no Inspector!");
    }

    // =========================================================
    // ACEITAR MISSÃO (botão Yes dentro do diálogo da missão)
    // =========================================================

    /// <summary>
    /// Chamado pelo botão Aceitar (Yes) da missão do Ferreiro.
    /// Fecha o Painel Inventario e os painéis de conteúdo.
    /// NÃO altera o GerenciadorMissoes (AceitarMissaoAtual continua responsável por aceitar e fechar o diálogo).
    /// </summary>
    public void OnMissaoAceita()
    {
        CancelarTimer();

        SetActiveSeguro(popupSaudacao, false);
        SetActiveSeguro(botoesCategoria, false);
        SetActiveSeguro(painelCriar, false);
        SetActiveSeguro(painelReparar, false);
        SetActiveSeguro(painelMelhorar, false);
        SetActiveSeguro(painelMissao, false);

        painelAtivoAtual = null;
    }

    // =========================================================
    // LOGICA INTERNA
    // =========================================================

    /// <summary>
    /// Abre um painel de conteudo SEM fechar o Painel Inventario.
    /// </summary>
    private void AbrirPainelConteudo(GameObject painel)
    {
        if (painelAtivoAtual != null && painelAtivoAtual != painel)
            SetActiveSeguro(painelAtivoAtual, false);

        // Se estiver saindo da missão, fecha o canvas interno do gerenciador
        if (painel != painelMissao && gerenciadorMissoes != null)
            gerenciadorMissoes.FecharPopupAtual();

        SetActiveSeguro(painel, true);
        painelAtivoAtual = painel;
        ReiniciarTimer();
    }

    /// <summary>
    /// Fecha todos os painéis de conteúdo e reseta o GerenciadorMissoes.
    /// </summary>
    private void FecharPaineisConteudo()
    {
        if (gerenciadorMissoes != null)
            gerenciadorMissoes.FecharPopupAtual();

        SetActiveSeguro(painelCriar, false);
        SetActiveSeguro(painelReparar, false);
        SetActiveSeguro(painelMelhorar, false);
        SetActiveSeguro(painelMissao, false);

        painelAtivoAtual = null;
    }

    private void FecharTudo()
    {
        CancelarTimer();
        FecharPaineisConteudo();
        SetActiveSeguro(popupSaudacao, false);
        SetActiveSeguro(botoesCategoria, false);
    }

    private static void SetActiveSeguro(GameObject alvo, bool ativo)
    {
        if (alvo != null)
            alvo.SetActive(ativo);
    }

    private void ReiniciarTimer()
    {
        CancelarTimer();
        timerInatividade = StartCoroutine(RotinaTimerInatividade());
    }

    private void CancelarTimer()
    {
        if (timerInatividade != null)
        {
            StopCoroutine(timerInatividade);
            timerInatividade = null;
        }
    }

    private IEnumerator RotinaTimerInatividade()
    {
        yield return new WaitForSeconds(tempoInatividade);
        timerInatividade = null;
        FecharTudo();
    }
}