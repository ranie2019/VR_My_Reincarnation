using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Cérebro do sistema de missões.
/// 
/// - Oferece a cadeia de missões na ordem.
/// - Controla os popups do NPC (diálogo, em andamento, pronta para entregar).
/// - Rastreia o progresso (ex: matar 10 Slimes Verdes).
/// - Atualiza a UI de progresso do player (PlayerMissoes).
/// - Ao entregar: spawna item + dá REIN + dá Experiência.
/// - Popup do NPC só fecha pelo botão Fechar ou pelo timer de 30s.
/// - Passar o laser de novo no NPC com o popup aberto NÃO reinicia nada.
/// </summary>
public class GerenciadorMissoes : MonoBehaviour
{
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
        [Tooltip("Nome/ID unico desta missao (aparece na UI do player).")]
        public string idMissao;

        [Header("Dialogo (falas em sequencia)")]
        [Tooltip("Canvas com o dialogo desta missao + os botoes Aceitar/Recusar. Comeca desativado.")]
        public GameObject canvasDialogo;

        [Tooltip("As falas, em ordem. So uma fica visivel por vez.")]
        public List<TMP_Text> falasDialogo = new List<TMP_Text>();

        [Tooltip("Tempo (segundos) que cada fala fica na tela antes de trocar pra proxima.")]
        public float tempoEntreFalas = 2f;

        [Tooltip("Botao Aceitar deste dialogo (fica escondido ate a ultima fala terminar).")]
        public GameObject botaoAceitar;

        [Tooltip("Botao Recusar deste dialogo (fica escondido ate a ultima fala terminar).")]
        public GameObject botaoRecusar;

        [Header("Popup: missao ja em andamento")]
        [Tooltip("Mostrado se o jogador interagir com o NPC de novo enquanto essa missao ja esta aceita. Comeca desativado.")]
        public GameObject canvasEmAndamento;

        [Header("Objetivo")]
        public TipoObjetivo tipoObjetivo;

        [Tooltip("Para MatarInimigos: o idRespawnMonstro do inimigo alvo (ex: \"SlimeVerde\"). Para ColetarItens: o NomeItem do item alvo.")]
        public string idAlvo;

        [Min(1)]
        public int quantidadeAlvo = 1;

        [Header("Popup: missao pronta para entregar / concluida")]
        [Tooltip("Mostrado quando o objetivo foi cumprido e o jogador volta a falar com o NPC. Comeca desativado.")]
        public GameObject canvasConcluida;

        [Header("Recompensa")]
        [Tooltip("Prefab do item que sera spawnado como recompensa (ex: uma espada).")]
        public GameObject prefabRecompensa;

        [Tooltip("Ponto onde a recompensa aparece.")]
        public Transform pontoSpawnRecompensa;

        [Tooltip("Quantidade de REIN dada como recompensa (ex: \"0.05\" ou \"200\").")]
        public string recompensaRein = "0";

        [Tooltip("Quantidade de Experiencia dada como recompensa.")]
        [Min(0)]
        public int recompensaExperiencia = 0;

        [Tooltip("Quantidade de Pontos de Prestigio dada como recompensa.")]
        [Min(0)]
        public int recompensaPontosPrestigio = 0;

        [NonSerialized] public int quantidadeAtualObjetivo;
        [NonSerialized] public EstadoMissao estado = EstadoMissao.NaoIniciada;
    }

    [Header("Cadeia de missoes (na ordem que sao oferecidas)")]
    [SerializeField] private List<MissaoDados> missoes = new List<MissaoDados>();

    [Header("Timer de inatividade")]
    [Tooltip("Tempo em segundos para fechar o popup automaticamente se o jogador não interagir.")]
    [SerializeField] private float tempoInatividadeParaFechar = 30f;

    [Header("UI de Progresso (Player)")]
    [Tooltip("Arraste aqui o PlayerMissoes do player. Ele mostra o progresso (ex: 5/10).")]
    [SerializeField] private PlayerMissoes uiProgresso;

    // Eventos que outros scripts podem escutar
    public event Action<MissaoDados> OnProgressoAtualizado;
    public event Action<MissaoDados> OnMissaoProntaParaEntregar;

    private int missaoAtualIndex;
    private GameObject canvasAtivoNoMomento;
    private Coroutine rotinaDialogo;
    private Coroutine rotinaTimerInatividade;

    private void Awake()
    {
        EsconderTodosOsCanvas();
    }

    private void EsconderTodosOsCanvas()
    {
        if (missoes == null)
            return;

        for (int i = 0; i < missoes.Count; i++)
        {
            MissaoDados missao = missoes[i];
            if (missao == null)
                continue;

            SetActiveSeguro(missao.canvasDialogo, false);
            SetActiveSeguro(missao.canvasEmAndamento, false);
            SetActiveSeguro(missao.canvasConcluida, false);
            SetActiveSeguro(missao.botaoAceitar, false);
            SetActiveSeguro(missao.botaoRecusar, false);
        }
    }

    private static void SetActiveSeguro(GameObject alvo, bool ativo)
    {
        if (alvo != null)
            alvo.SetActive(ativo);
    }

    private MissaoDados ObterMissaoAtual()
    {
        if (missoes == null || missaoAtualIndex < 0 || missaoAtualIndex >= missoes.Count)
            return null;

        return missoes[missaoAtualIndex];
    }

    /// <summary>
    /// Retorna a missão atual (útil para a UI do player e outros scripts).
    /// </summary>
    public MissaoDados ObterMissaoAtualPublica()
    {
        return ObterMissaoAtual();
    }

    /// <summary>
    /// Chamado pelo NPCMissao quando o laser entra em hover no NPC.
    /// Se o popup já estiver aberto, não reinicia nada.
    /// </summary>
    public void NotificarHoverNPC()
    {
        if (canvasAtivoNoMomento != null)
            return;

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
    /// Sair com o laser NÃO fecha mais o popup.
    /// </summary>
    public void NotificarHoverSaiuNPC()
    {
        // Intencionalmente vazio.
    }

    private void MostrarCanvas(GameObject canvas)
    {
        FecharCanvasAtivo();

        if (canvas == null)
            return;

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

    /// <summary>
    /// Chame este método no botão "Fechar" de qualquer popup de missão.
    /// </summary>
    public void FecharPopupAtual()
    {
        FecharCanvasAtivo();
    }

    private void IniciarTimerInatividade()
    {
        CancelarTimerInatividade();
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

        if (rotinaDialogo != null)
            StopCoroutine(rotinaDialogo);

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
            if (fala != null)
                fala.gameObject.SetActive(i == 0);
        }

        int ultimoIndice = missao.falasDialogo.Count - 1;

        for (int i = 0; i < ultimoIndice; i++)
        {
            yield return new WaitForSeconds(missao.tempoEntreFalas);

            TMP_Text falaAtual = missao.falasDialogo[i];
            if (falaAtual != null)
                falaAtual.gameObject.SetActive(false);

            TMP_Text proximaFala = missao.falasDialogo[i + 1];
            if (proximaFala != null)
                proximaFala.gameObject.SetActive(true);
        }

        rotinaDialogo = null;
        RevelarBotoesDeDecisao(missao);
    }

    private void RevelarBotoesDeDecisao(MissaoDados missao)
    {
        SetActiveSeguro(missao.botaoAceitar, true);
        SetActiveSeguro(missao.botaoRecusar, true);
    }

    /// <summary>
    /// Chamado pelo botão "Aceitar" do diálogo da missão atual.
    /// </summary>
    public void AceitarMissaoAtual()
    {
        MissaoDados missaoAtual = ObterMissaoAtual();
        if (missaoAtual == null || missaoAtual.estado != EstadoMissao.NaoIniciada)
            return;

        missaoAtual.estado = EstadoMissao.EmAndamento;
        missaoAtual.quantidadeAtualObjetivo = 0;

        if (uiProgresso != null)
            uiProgresso.MostrarMissao(missaoAtual);

        OnProgressoAtualizado?.Invoke(missaoAtual);

        FecharCanvasAtivo();
    }

    /// <summary>
    /// Chamado pelo botão "Recusar" do diálogo da missão atual.
    /// </summary>
    public void RecusarMissaoAtual()
    {
        FecharCanvasAtivo();
    }

    /// <summary>
    /// Chamado pelos inimigos quando morrem.
    /// Exemplo no SlimeIA: gerenciador.NotificarInimigoMorto("SlimeVerde");
    /// </summary>
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
        {
            missao.estado = EstadoMissao.ProntaParaEntregar;

            if (uiProgresso != null)
                uiProgresso.MostrarMissaoCompleta(missao);

            OnMissaoProntaParaEntregar?.Invoke(missao);
        }
    }

    /// <summary>
    /// Chamado pelo botão de confirmar entrega no popup "Concluída".
    /// Spawna o item, dá REIN e Experiência, e avança a cadeia.
    /// </summary>
    public void EntregarRecompensaEAvancar()
    {
        MissaoDados missaoAtual = ObterMissaoAtual();
        if (missaoAtual == null || missaoAtual.estado != EstadoMissao.ProntaParaEntregar)
            return;

        // 1) Spawna o item de recompensa
        if (missaoAtual.prefabRecompensa != null)
        {
            Vector3 posicao = missaoAtual.pontoSpawnRecompensa != null
                ? missaoAtual.pontoSpawnRecompensa.position
                : transform.position;

            Quaternion rotacao = missaoAtual.pontoSpawnRecompensa != null
                ? missaoAtual.pontoSpawnRecompensa.rotation
                : Quaternion.identity;

            Instantiate(missaoAtual.prefabRecompensa, posicao, rotacao);
        }

        // 2) Entrega REIN e Experiência ao player
        EntregarRecompensasAoPlayer(missaoAtual);

        // 3) Finaliza a missão
        missaoAtual.estado = EstadoMissao.Concluida;

        if (uiProgresso != null)
            uiProgresso.Esconder();

        FecharCanvasAtivo();

        missaoAtualIndex++;
    }

    private void EntregarRecompensasAoPlayer(MissaoDados missao)
    {
        if (missao == null) return;

        StatusPlayer status = FindFirstObjectByType<StatusPlayer>();
        if (status == null)
        {
            Debug.LogWarning("[Missão] StatusPlayer não encontrado. REIN/XP não entregues.");
            return;
        }

        // Experiência
        if (missao.recompensaExperiencia > 0)
        {
            status.ReceberExperiencia(missao.recompensaExperiencia);
            Debug.Log($"[Missão] +{missao.recompensaExperiencia} XP entregue.");
        }

        // REIN (string no mesmo formato do SlimeIA, ex: "0.05" ou "200")
        if (!string.IsNullOrWhiteSpace(missao.recompensaRein) && missao.recompensaRein.Trim() != "0")
        {
            CarteiraReinPlayer carteira = status.ObterCarteiraRein();
            if (carteira != null)
            {
                long unidades = ConverterReinParaUnidades(missao.recompensaRein);
                if (unidades > 0)
                {
                    carteira.AdicionarReinUnidades(unidades);
                    Debug.Log($"[Missão] +{missao.recompensaRein} REIN entregue.");
                }
            }
            else
            {
                Debug.LogWarning("[Missão] CarteiraReinPlayer não encontrada.");
            }
        }

        // Pontos de Prestígio — ainda não implementado no StatusPlayer
        // if (missao.recompensaPontosPrestigio > 0) { ... }
    }

    /// <summary>
    /// Converte o texto de REIN (ex: "0.05" ou "200") para unidades internas da CarteiraReinPlayer.
    /// </summary>
    private static long ConverterReinParaUnidades(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return 0L;

        string normalizado = texto.Trim().Replace(',', '.');

        if (!decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal valor))
            return 0L;

        if (valor <= 0m)
            return 0L;

        return CarteiraReinPlayer.ConverterDecimalParaUnidades(valor);
    }
}