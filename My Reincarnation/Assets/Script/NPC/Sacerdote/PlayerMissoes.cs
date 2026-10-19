using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Controla a lista de missões ativas no Panel Quest.
/// Cada missão ocupa uma linha livre, na ordem em que foi aceita.
/// A linha permanece visível até a entrega da missão no NPC.
/// </summary>
public class PlayerMissoes : MonoBehaviour
{
    [Serializable]
    private class LinhaMissao
    {
        public GameObject raiz;
        public TMP_Text descricao;
        public TMP_Text valorAtual;
        public TMP_Text valorFinal;
        public GameObject concluido;

        [NonSerialized] public GerenciadorMissoes.MissaoDados missao;
        [NonSerialized] public GerenciadorMissoes gerenciador;

        public bool Ocupada => missao != null;
    }

    [Header("UI - Panel Quest")]
    [Tooltip("Panel Quest. Se ficar vazio, o script procura automaticamente um objeto com esse nome.")]
    [SerializeField] private GameObject painelMissoes;

    [Tooltip("Linhas Mission 1, Mission 2... Mission 5. Se ficar vazio, são encontradas automaticamente dentro do Panel Quest.")]
    [SerializeField] private LinhaMissao[] linhas = new LinhaMissao[5];

    [Header("Compatibilidade com a UI antiga")]
    [SerializeField] private TMP_Text missaoTitulo;
    [SerializeField] private TMP_Text missaoTexto;
    [SerializeField] private TMP_Text missaoValorAtual;
    [SerializeField] private TMP_Text missaoValorFinal;

    private const int MaximoPadrao = 5;
    private readonly List<LinhaMissao> linhasAtivas = new List<LinhaMissao>();
    private bool inicializado;

    private void Awake()
    {
        InicializarUI();
        LimparTodasAsLinhas();
    }

    private void OnDisable()
    {
        DesinscreverTodosOsGerenciadores();
    }

    // =========================================================
    // API PÚBLICA chamada pelo GerenciadorMissoes
    // =========================================================

    public void MostrarMissao(GerenciadorMissoes.MissaoDados missao)
    {
        MostrarMissao(missao, null);
    }

    /// <summary>
    /// Adiciona a missão na primeira linha livre e registra os eventos do NPC de origem.
    /// Se a missão já estiver na lista, apenas atualiza a linha existente.
    /// </summary>
    public void MostrarMissao(GerenciadorMissoes.MissaoDados missao, GerenciadorMissoes origem)
    {
        if (missao == null) return;
        InicializarUI();

        LinhaMissao linha = EncontrarLinha(missao);
        if (linha == null)
            linha = EncontrarLinhaLivre();

        if (linha == null)
        {
            Debug.LogWarning("[Missões] Limite de linhas atingido. A missão não foi adicionada ao Panel Quest.");
            return;
        }

        DesinscreverLinha(linha);
        linha.missao = missao;
        linha.gerenciador = origem;

        if (origem != null)
        {
            origem.OnProgressoAtualizado += OnProgressoAtualizado;
            origem.OnMissaoProntaParaEntregar += OnMissaoProntaParaEntregar;
        }

        if (!linhasAtivas.Contains(linha))
            linhasAtivas.Add(linha);

        AtualizarLinha(linha);
        AtualizarVisibilidadeDoPainel();
    }

    public void AtualizarProgresso(GerenciadorMissoes.MissaoDados missao)
    {
        if (missao == null) return;

        LinhaMissao linha = EncontrarLinha(missao);
        if (linha == null)
        {
            MostrarMissao(missao, null);
            return;
        }

        AtualizarLinha(linha);
        AtualizarVisibilidadeDoPainel();
    }

    public void MostrarMissaoCompleta(GerenciadorMissoes.MissaoDados missao)
    {
        AtualizarProgresso(missao);
    }

    /// <summary>
    /// Compatibilidade com chamadas antigas. Remove a primeira missão da lista.
    /// Para a entrega correta, prefira Esconder(missao).
    /// </summary>
    public void Esconder()
    {
        if (linhasAtivas.Count > 0)
            RemoverMissao(linhasAtivas[0].missao);
        else
            AtualizarVisibilidadeDoPainel();
    }

    /// <summary>
    /// Remove exatamente a missão entregue, sem afetar as demais linhas.
    /// As missões seguintes são compactadas para ocupar a primeira linha livre.
    /// </summary>
    public void Esconder(GerenciadorMissoes.MissaoDados missao)
    {
        RemoverMissao(missao);
    }

    public bool PossuiMissao(GerenciadorMissoes.MissaoDados missao)
    {
        return EncontrarLinha(missao) != null;
    }

    // =========================================================
    // Configuração automática da UI
    // =========================================================

    private void InicializarUI()
    {
        if (inicializado) return;

        if (painelMissoes == null)
            painelMissoes = EncontrarObjetoNaCena("Panel Quest");

        if (linhas == null || linhas.Length == 0)
            linhas = new LinhaMissao[MaximoPadrao];

        if (painelMissoes != null)
        {
            List<Transform> raizes = new List<Transform>();
            for (int i = 0; i < painelMissoes.transform.childCount; i++)
            {
                Transform filho = painelMissoes.transform.GetChild(i);
                if (filho.name.StartsWith("Mission ", StringComparison.OrdinalIgnoreCase))
                    raizes.Add(filho);
            }

            raizes.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));

            int quantidade = Mathf.Min(raizes.Count, linhas.Length);
            for (int i = 0; i < quantidade; i++)
            {
                if (linhas[i] == null) linhas[i] = new LinhaMissao();
                if (linhas[i].raiz == null) linhas[i].raiz = raizes[i].gameObject;
            }
        }

        for (int i = 0; i < linhas.Length; i++)
        {
            if (linhas[i] == null) linhas[i] = new LinhaMissao();
            PrepararLinha(linhas[i]);
        }

        inicializado = true;
    }

    private void PrepararLinha(LinhaMissao linha)
    {
        if (linha.raiz == null) return;
        Transform raiz = linha.raiz.transform;

        if (linha.descricao == null)
            linha.descricao = EncontrarTexto(raiz, "Missao Texto", "Missão Texto", "Descricao", "Descrição");
        if (linha.valorAtual == null)
            linha.valorAtual = EncontrarTexto(raiz, "Missao Valor Atual", "Missão Valor Atual", "Valor Atual");
        if (linha.valorFinal == null)
            linha.valorFinal = EncontrarTexto(raiz, "Missao Valor Final", "Missão Valor Final", "Valor Final", "Total");
        if (linha.concluido == null)
        {
            Transform encontrado = EncontrarTransform(raiz, "Concluido", "Concluído");
            if (encontrado != null) linha.concluido = encontrado.gameObject;
        }
    }

    private void LimparTodasAsLinhas()
    {
        for (int i = 0; i < linhas.Length; i++)
        {
            LinhaMissao linha = linhas[i];
            if (linha == null) continue;
            linha.missao = null;
            DesinscreverLinha(linha);
            if (linha.raiz != null) linha.raiz.SetActive(false);
        }

        linhasAtivas.Clear();
        AtualizarVisibilidadeDoPainel();
    }

    // =========================================================
    // Atualização e organização
    // =========================================================

    private void AtualizarLinha(LinhaMissao linha)
    {
        if (linha == null || linha.missao == null) return;
        GerenciadorMissoes.MissaoDados missao = linha.missao;

        int atual = Mathf.Clamp(missao.quantidadeAtualObjetivo, 0, Mathf.Max(1, missao.quantidadeAlvo));
        int final = Mathf.Max(1, missao.quantidadeAlvo);
        bool concluida = missao.estado == GerenciadorMissoes.EstadoMissao.ProntaParaEntregar ||
                         missao.estado == GerenciadorMissoes.EstadoMissao.Concluida || atual >= final;

        if (linha.descricao != null)
            linha.descricao.text = GetDescricaoMissao(missao);
        if (linha.valorAtual != null)
            linha.valorAtual.text = atual.ToString();
        if (linha.valorFinal != null)
            linha.valorFinal.text = final.ToString();
        if (linha.concluido != null)
            linha.concluido.SetActive(concluida);
        if (linha.raiz != null)
            linha.raiz.SetActive(true);

        // Mantém a UI antiga funcional caso ainda esteja conectada no Inspector.
        if (linhas.Length == 0 || linha.raiz == null)
        {
            if (missaoTexto != null) missaoTexto.text = GetDescricaoMissao(missao);
            if (missaoValorAtual != null) missaoValorAtual.text = atual.ToString();
            if (missaoValorFinal != null) missaoValorFinal.text = final.ToString();
        }
    }

    private void RemoverMissao(GerenciadorMissoes.MissaoDados missao)
    {
        if (missao == null) return;
        LinhaMissao linha = EncontrarLinha(missao);
        if (linha == null) return;

        DesinscreverLinha(linha);
        linha.missao = null;
        if (linha.raiz != null) linha.raiz.SetActive(false);
        linhasAtivas.Remove(linha);

        CompactarLinhas();
        AtualizarVisibilidadeDoPainel();
    }

    private void CompactarLinhas()
    {
        List<LinhaMissao> ocupadas = new List<LinhaMissao>();
        for (int i = 0; i < linhas.Length; i++)
            if (linhas[i] != null && linhas[i].missao != null) ocupadas.Add(linhas[i]);

        for (int destino = 0; destino < ocupadas.Count; destino++)
        {
            LinhaMissao origem = ocupadas[destino];
            LinhaMissao linhaDestino = linhas[destino];
            if (origem == linhaDestino) continue;

            DesinscreverLinha(linhaDestino);
            linhaDestino.missao = origem.missao;
            linhaDestino.gerenciador = origem.gerenciador;
            if (linhaDestino.gerenciador != null)
            {
                linhaDestino.gerenciador.OnProgressoAtualizado += OnProgressoAtualizado;
                linhaDestino.gerenciador.OnMissaoProntaParaEntregar += OnMissaoProntaParaEntregar;
            }
            origem.missao = null;
            origem.gerenciador = null;
            if (origem.raiz != null) origem.raiz.SetActive(false);
            AtualizarLinha(linhaDestino);
        }

        linhasAtivas.Clear();
        for (int i = 0; i < ocupadas.Count; i++)
            linhasAtivas.Add(linhas[i]);
    }

    private void AtualizarVisibilidadeDoPainel()
    {
        bool possuiMissao = false;
        for (int i = 0; i < linhas.Length; i++)
        {
            if (linhas[i] != null && linhas[i].missao != null)
            {
                possuiMissao = true;
                break;
            }
        }

        // O botão Quest controla a abertura/fechamento do painel. Quando uma
        // missão é aceita, garantimos que o painel possa ser exibido; quando
        // a última é entregue, não o fechamos para não interferir no menu.
        if (possuiMissao && painelMissoes != null && painelMissoes.name == "Panel Quest")
            painelMissoes.SetActive(true);
    }

    // =========================================================
    // Eventos dos gerenciadores
    // =========================================================

    private void OnProgressoAtualizado(GerenciadorMissoes.MissaoDados missao)
    {
        AtualizarProgresso(missao);
    }

    private void OnMissaoProntaParaEntregar(GerenciadorMissoes.MissaoDados missao)
    {
        MostrarMissaoCompleta(missao);
    }

    private void DesinscreverTodosOsGerenciadores()
    {
        for (int i = 0; i < linhas.Length; i++)
            if (linhas[i] != null) DesinscreverLinha(linhas[i]);
    }

    private void DesinscreverLinha(LinhaMissao linha)
    {
        if (linha == null || linha.gerenciador == null) return;
        linha.gerenciador.OnProgressoAtualizado -= OnProgressoAtualizado;
        linha.gerenciador.OnMissaoProntaParaEntregar -= OnMissaoProntaParaEntregar;
        linha.gerenciador = null;
    }

    // =========================================================
    // Utilitários
    // =========================================================

    private LinhaMissao EncontrarLinha(GerenciadorMissoes.MissaoDados missao)
    {
        if (missao == null) return null;
        for (int i = 0; i < linhas.Length; i++)
            if (linhas[i] != null && linhas[i].missao == missao) return linhas[i];
        return null;
    }

    private LinhaMissao EncontrarLinhaLivre()
    {
        for (int i = 0; i < linhas.Length; i++)
            if (linhas[i] != null && linhas[i].missao == null) return linhas[i];
        return null;
    }

    private static string GetDescricaoMissao(GerenciadorMissoes.MissaoDados missao)
    {
        if (!string.IsNullOrWhiteSpace(missao.enunciadoMissao)) return missao.enunciadoMissao.Trim();
        string alvo = string.IsNullOrEmpty(missao.idAlvo) ? "inimigos" : missao.idAlvo;
        return missao.tipoObjetivo == GerenciadorMissoes.TipoObjetivo.ColetarItens
            ? $"Colete {missao.quantidadeAlvo} {alvo}."
            : $"Mate {missao.quantidadeAlvo} {alvo}.";
    }

    private static TMP_Text EncontrarTexto(Transform raiz, params string[] nomes)
    {
        Transform t = EncontrarTransform(raiz, nomes);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private static Transform EncontrarTransform(Transform raiz, params string[] nomes)
    {
        if (raiz == null) return null;
        for (int i = 0; i < nomes.Length; i++)
            if (string.Equals(raiz.name, nomes[i], StringComparison.OrdinalIgnoreCase)) return raiz;
        for (int i = 0; i < raiz.childCount; i++)
        {
            Transform encontrado = EncontrarTransform(raiz.GetChild(i), nomes);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    private static GameObject EncontrarObjetoNaCena(string nome)
    {
        GameObject[] objetos = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objetos.Length; i++)
        {
            GameObject objeto = objetos[i];
            if (objeto.name == nome && objeto.scene.IsValid()) return objeto;
        }
        return null;
    }
}
