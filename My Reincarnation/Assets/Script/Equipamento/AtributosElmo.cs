using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

// Compartilha os tipos de atributo e raridade com os demais equipamentos.
[DisallowMultipleComponent]
public class AtributosElmo : MonoBehaviour
{
    [Serializable]
    public class FaixaAtributo
    {
        public TipoAtributoManopla atributo;
        [Min(0)] public int minimo = 1;
        [Min(0)] public int maximo = 5;
    }

    [Serializable]
    public class ConfiguracaoClasse
    {
        public ClasseManopla classe;
        [Range(0f, 100f)] public float chance;
        [Header("Quantos tipos de atributos o item recebe (nao o valor dos pontos)")]
        [InspectorName("Quantidade de atributos - minima")]
        [Tooltip("Quantos atributos diferentes o item recebe. Ex.: 1 a 2 sorteia um ou dois tipos da lista abaixo.")]
        [Min(0)] public int quantidadeMinima;
        [InspectorName("Quantidade de atributos - maxima")]
        [Tooltip("Nao e o valor de Forca: e a quantidade de tipos sorteados. Nao pode superar o tamanho da lista Atributos.")]
        [Min(0)] public int quantidadeMaxima = 1;
        public List<FaixaAtributo> atributos = new List<FaixaAtributo>();
    }

    [Serializable]
    public class ValorAtributo
    {
        public TipoAtributoManopla atributo;
        public int valor;
    }

    [Serializable]
    public class TextoAtributo
    {
        public TMP_Text textoNome;
        [UnityEngine.Serialization.FormerlySerializedAs("texto")]
        public TMP_Text textoValor;
        [Header("Comparacao com o elmo equipado")]
        public GameObject setaUp;
        public GameObject setaDown;
        [Tooltip("Mostra somente o tamanho da diferenca, sem sinal. Ex.: 2 para +2 ou -2.")]
        public TMP_Text textoDiferenca;
        public GameObject sinalPositivo;
        public GameObject sinalNegativo;
    }

    [Serializable]
    public class EstadoGerado
    {
        public int versao = 1;
        public string nome;
        public string descricao;
        public ClasseManopla classe;
        public AfinidadeDivinaManopla afinidade;
        // Campo legado mantido somente para preservar o JSON dos saves existentes.
        public Color cor;
        public List<ValorAtributo> atributos = new List<ValorAtributo>();
    }

    [Header("Identidade")]
    [SerializeField] private GameObject prefabObjeto;
    [Tooltip("Nome mostrado no popup. Vazio usa o nome atual do prefab. Nao altera IDs nem atributos salvos.")]
    [SerializeField] private string nomeExibicao;
    [TextArea(3, 8)] [SerializeField] private string descricao;

    [Header("Classes: chance independente; zero desativa")]
    [SerializeField] private List<ConfiguracaoClasse> classes = CriarClassesPadrao();

    [Header("Durabilidade / vida do elmo")]
    [SerializeField, Min(0.01f)] private float vidaMaxima = 100f;
    [SerializeField, Min(0f)] private float vidaAtual = 100f;
    [Tooltip("Desgaste por minuto de jogo, somente enquanto equipada. Nao desgasta guardada ou solta.")]
    [SerializeField, Min(0f)] private float desgasteNaturalPorMinuto = 0.5f;
    [SerializeField] private TMP_Text textoDurabilidadeAtual;
    [SerializeField] private TMP_Text textoDurabilidadeTotal;
    private bool destruindoPorDurabilidade;
    private double vidaPrecisa;
    private float ultimaVidaPublicada = float.NaN;
    public float VidaAtual => vidaAtual;
    public float VidaMaxima => vidaMaxima;
    public float VidaFaltando => Mathf.Max(0f, vidaMaxima - vidaAtual);
    public bool EstaQuebrado => vidaAtual <= 0f || destruindoPorDurabilidade;

    private void Update()
    {
        if (destruindoPorDurabilidade) return;
        var estado = GetComponent<EstadoItemInventario>();
        if (estado != null && estado.estaNoInventario) return;
        if (vidaAtual <= 0f) { AplicarDesgaste(0d); return; }
        var equipamento = GetComponent<EquiparElmo>();
        if (equipamento == null || !equipamento.JaEquipado) return;
        AplicarDesgaste((double)desgasteNaturalPorMinuto * Time.deltaTime / 60d);
    }

    // Gancho para um futuro evento de combate. Nao aplica desgaste de combate automaticamente.
    public void AplicarDesgasteCombate(float quantidade)
    {
        var equipamento = GetComponent<EquiparElmo>();
        if (equipamento != null && equipamento.JaEquipado) AplicarDesgaste(quantidade);
    }

    private void AplicarDesgaste(double quantidade)
    {
        if (destruindoPorDurabilidade || double.IsNaN(quantidade) || double.IsInfinity(quantidade) || quantidade < 0d) return;
        if (vidaAtual != ultimaVidaPublicada) vidaPrecisa = vidaAtual; // Save ou reparo alterou o campo.
        vidaPrecisa = CalcularVidaRestante(vidaPrecisa, quantidade);
        vidaAtual = (float)vidaPrecisa;
        ultimaVidaPublicada = vidaAtual;
        AtualizarDurabilidadeVisual();
        if (vidaAtual > 0f) return;
        destruindoPorDurabilidade = true;
        var equipamento = GetComponent<EquiparElmo>();
        if (equipamento != null) equipamento.PrepararQuebraPorDurabilidade();
        GetComponent<ItemPersistente>()?.RegistrarConsumoPermanente();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    internal static double CalcularVidaRestante(double atual, double desgaste)
    {
        return AtributosManoplas.CalcularVidaRestante(atual, desgaste);
    }

    public void RepararCompleto()
    {
        if (destruindoPorDurabilidade) return;
        vidaAtual = vidaMaxima;
        ultimaVidaPublicada = float.NaN;
        AtualizarDurabilidadeVisual();
    }

    public void AtualizarDurabilidadeVisual()
    {
        if (textoDurabilidadeAtual != null) textoDurabilidadeAtual.text = vidaAtual.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
        if (textoDurabilidadeTotal != null) textoDurabilidadeTotal.text = vidaMaxima.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
    }

    [Header("Popup existente (nao controla abertura ou posicionamento)")]
    [SerializeField] private GameObject popup;
    [SerializeField] private TMP_Text textoNome;
    [SerializeField] private TMP_Text textoClasse;
    [SerializeField] private TMP_Text textoDescricao;
    [Header("Cinco linhas do popup: Elemento 0 e a primeira linha (topo)")]
    [Tooltip("Associe o texto do nome e o texto do numero em cada linha. O tipo vem do sorteio, nao desta lista.")]
    [SerializeField] private List<TextoAtributo> textosValores = CriarLinhasPopup();

    private const int LimiteLinhasPopup = 5;

    private static List<TextoAtributo> CriarLinhasPopup()
    {
        var linhas = new List<TextoAtributo>();
        for (int i = 0; i < LimiteLinhasPopup; i++) linhas.Add(new TextoAtributo());
        return linhas;
    }

    private void OnValidate()
    {
        NormalizarLinhasPopup();
        if (float.IsNaN(vidaMaxima) || float.IsInfinity(vidaMaxima)) vidaMaxima = 100f;
        vidaMaxima = Mathf.Max(0.01f, vidaMaxima);
        if (float.IsNaN(vidaAtual) || float.IsInfinity(vidaAtual)) vidaAtual = vidaMaxima;
        vidaAtual = Mathf.Clamp(vidaAtual, 0f, vidaMaxima);
        if (float.IsNaN(desgasteNaturalPorMinuto) || float.IsInfinity(desgasteNaturalPorMinuto)) desgasteNaturalPorMinuto = 0.5f;
        desgasteNaturalPorMinuto = Mathf.Max(0f, desgasteNaturalPorMinuto);
    }

    private void NormalizarLinhasPopup()
    {
        if (textosValores == null) textosValores = CriarLinhasPopup();
        while (textosValores.Count < LimiteLinhasPopup) textosValores.Add(new TextoAtributo());
        if (textosValores.Count > LimiteLinhasPopup)
            textosValores.RemoveRange(LimiteLinhasPopup, textosValores.Count - LimiteLinhasPopup);
        for (int i = 0; i < textosValores.Count; i++)
            if (textosValores[i] == null) textosValores[i] = new TextoAtributo();
    }

    [SerializeField, HideInInspector] private EstadoGerado resultado;
    public bool Gerado => resultado != null;
    public ClasseManopla Classe => Gerado ? resultado.classe : ClasseManopla.Comum;
    public AfinidadeDivinaManopla Afinidade => Gerado ? resultado.afinidade : AfinidadeDivinaManopla.Nenhuma;

    private void Awake() { GarantirGeracao(); }
    private void Start() { GarantirGeracao(); }

    private void LateUpdate()
    {
        // A elmo continua ativa enquanto o popup esta aberto, mesmo se o laser sair.
        // Reconsulta a mesma mao para refletir trocas e retiradas sem gravar bonus.
        if (Gerado && popup != null && popup.activeInHierarchy)
        {
            AtualizarIdentidadePopup();
            AtualizarLinhasAtributos(resultado.atributos);
        }
    }
    private void OnEnable()
    {
        if (!Gerado) StartCoroutine(GerarAposRestauracao());
    }

    private IEnumerator GerarAposRestauracao()
    {
        yield return null;
        if (isActiveAndEnabled) GarantirGeracao();
    }

    public void PrepararMigracaoSaveLegado()
    {
        resultado = null;
        // A verificacao sincronica do save legado precisa continuar enxergando os extras antigos.
        if (isActiveAndEnabled) StartCoroutine(GerarAposRestauracao());
    }

    private void Reset()
    {
#if UNITY_EDITOR
        prefabObjeto = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
#endif
        classes = CriarClassesPadrao();
        BuscarPopup();
    }

    private void BuscarPopup()
    {
        if (popup != null) return;
        Transform filho = transform.Find("PopUp");
        if (filho != null) popup = filho.gameObject;
    }

    // Idempotente: abrir menu, reequipar ou reativar o objeto nunca refaz o sorteio.
    public bool GarantirGeracao()
    {
        if (Gerado) { AtualizarPopup(); return true; }
        var ativas = new List<ConfiguracaoClasse>();
        var vistas = new HashSet<ClasseManopla>();
        if (classes == null) classes = CriarClassesPadrao();
        foreach (var configuracao in classes)
        {
            if (configuracao == null || configuracao.chance <= 0f) continue;
            if (!Enum.IsDefined(typeof(ClasseManopla), configuracao.classe) ||
                !vistas.Add(configuracao.classe) || float.IsNaN(configuracao.chance) ||
                float.IsInfinity(configuracao.chance) || !FaixasValidas(configuracao))
            {
                Debug.LogError("[AtributosElmo] Classe repetida ou faixa de atributos invalida.", this);
                return false;
            }
            ativas.Add(configuracao);
        }
        if (ativas.Count == 0)
        {
            Debug.LogError("[AtributosElmo] Ative pelo menos uma classe com chance maior que zero.", this);
            return false;
        }
        var chances = new double[ativas.Count];
        for (int i = 0; i < chances.Length; i++) chances[i] = Math.Max(0.01d, Math.Min(100d, ativas[i].chance)) / 100d;
        int escolhida = SortearClasse(chances, () => UnityEngine.Random.value);
        ConfiguracaoClasse classe = ativas[escolhida];
        var pool = new List<FaixaAtributo>(classe.atributos);
        int quantidade = UnityEngine.Random.Range(classe.quantidadeMinima, classe.quantidadeMaxima + 1);
        var estado = new EstadoGerado
        {
            nome = ObterNomePrefab(), descricao = descricao, classe = classe.classe,
            afinidade = SortearAfinidade(classe.classe, () => UnityEngine.Random.value)
        };
        for (int i = 0; i < quantidade; i++)
        {
            int indice = UnityEngine.Random.Range(0, pool.Count);
            FaixaAtributo faixa = pool[indice];
            pool.RemoveAt(indice);
            estado.atributos.Add(new ValorAtributo { atributo = faixa.atributo,
                valor = UnityEngine.Random.Range(faixa.minimo, faixa.maximo + 1) });
        }
        resultado = estado;
        AtualizarPopup();
        return true;
    }

    private static bool FaixasValidas(ConfiguracaoClasse classe)
    {
        if (classe.atributos == null || classe.quantidadeMinima < 0 ||
            classe.quantidadeMaxima < classe.quantidadeMinima || classe.quantidadeMaxima > classe.atributos.Count)
            return false;
        var tipos = new HashSet<TipoAtributoManopla>();
        foreach (var faixa in classe.atributos)
            if (faixa == null || !Enum.IsDefined(typeof(TipoAtributoManopla), faixa.atributo) ||
                !tipos.Add(faixa.atributo) || faixa.minimo < 0 || faixa.maximo < faixa.minimo || faixa.maximo == int.MaxValue)
                return false;
        return true;
    }

    // Mesmo resultado probabilistico de repetir rodadas vazias, sem loop ilimitado
    // que congelaria o Unity quando as chances fossem muito baixas.
    internal static int SortearClasse(double[] chances, Func<double> aleatorio)
        => AtributosManoplas.SortearClasse(chances, aleatorio);

    internal static AfinidadeDivinaManopla SortearAfinidade(ClasseManopla classe, Func<double> aleatorio)
        => AtributosManoplas.SortearAfinidade(classe, aleatorio);

    private string ObterNomePrefab()
    {
        if (prefabObjeto != null) return prefabObjeto.name;
        var persistente = GetComponent<ItemPersistente>();
        if (persistente != null && persistente.PrefabReferencia != null) return persistente.PrefabReferencia.name;
        return gameObject.name.Replace("(Clone)", "").Trim();
    }

    public int ObterValor(TipoAtributoManopla atributo)
    {
        if (!Gerado) return 0;
        foreach (var valor in resultado.atributos) if (valor.atributo == atributo) return valor.valor;
        return 0;
    }

    public void AtualizarPopup()
    {
        if (!Gerado) return;
        BuscarPopup();
        AtualizarDurabilidadeVisual();
        AtualizarIdentidadePopup();
        if (textoClasse != null)
        {
            textoClasse.text = resultado.classe.ToString() +
                (resultado.afinidade == AfinidadeDivinaManopla.Nenhuma ? "" : " - " + resultado.afinidade);
        }
        AtualizarLinhasAtributos(resultado.atributos);
    }

    private void AtualizarIdentidadePopup()
    {
        // Textos editoriais usam a configuracao atual, nunca o snapshot do sorteio.
        if (textoNome != null)
            textoNome.text = string.IsNullOrWhiteSpace(nomeExibicao) ? ObterNomePrefab() : nomeExibicao.Trim();
        if (textoDescricao != null) textoDescricao.text = descricao ?? string.Empty;
    }

    private void AtualizarLinhasAtributos(IReadOnlyList<ValorAtributo> atributos)
    {
        NormalizarLinhasPopup();
        AtributosElmo equipada = ObterElmoComparacao(out bool comparacaoDisponivel);
        atributos = CriarAtributosComparacao(atributos, equipada);
        for (int i = 0; i < LimiteLinhasPopup; i++)
        {
            TextoAtributo linha = textosValores[i];
            ValorAtributo valor = atributos != null && i < atributos.Count ? atributos[i] : null;
            if (linha.textoNome != null)
                linha.textoNome.text = valor != null ? (valor.atributo == TipoAtributoManopla.Mana ? "MP" : valor.atributo.ToString()) : string.Empty;
            if (linha.textoValor != null)
                linha.textoValor.text = valor != null ? "+" + valor.valor.ToString(CultureInfo.InvariantCulture) : string.Empty;
            AtualizarComparacaoLinha(linha, valor, equipada, comparacaoDisponivel);
        }
    }

    private static IReadOnlyList<ValorAtributo> CriarAtributosComparacao(
        IReadOnlyList<ValorAtributo> candidata, AtributosElmo equipada)
    {
        var linhas = new List<ValorAtributo>();
        var tipos = new HashSet<TipoAtributoManopla>();
        if (candidata != null)
            foreach (var valor in candidata)
                if (valor != null && tipos.Add(valor.atributo)) linhas.Add(valor);
        if (equipada != null && equipada.resultado != null)
            foreach (var valor in equipada.resultado.atributos)
                if (valor != null && tipos.Add(valor.atributo))
                    linhas.Add(new ValorAtributo { atributo = valor.atributo, valor = 0 });
        return linhas;
    }

    private AtributosElmo ObterElmoComparacao(out bool disponivel)
    {
        disponivel = false;
        EquiparElmo candidata = GetComponent<EquiparElmo>();
        if (candidata == null) return null;
        var gerenciador = GetComponentInParent<GerenciadorEquipamentosPlayer>();
        if (gerenciador == null) gerenciador = GerenciadorEquipamentosPlayer.Instancia;
        if (gerenciador == null) return null;
        EquiparElmo atual = gerenciador.ElmoAtual;
        // Sem elmo equipado compara com zero, sem somar outros equipamentos ou atributos base.
        if (atual == null || !atual.JaEquipado)
        {
            disponivel = true;
            return null;
        }
        AtributosElmo atributos = atual.GetComponent<AtributosElmo>();
        // Nao inventa uma vantagem se o elmo antiga ainda nao possui dados de atributos.
        disponivel = atributos != null && atributos.Gerado;
        return atributos;
    }

    private static void AtualizarComparacaoLinha(TextoAtributo linha, ValorAtributo valor,
        AtributosElmo equipada, bool disponivel)
    {
        bool comparar = valor != null && disponivel;
        long diferenca = comparar ? (long)valor.valor - (equipada != null ? equipada.ObterValor(valor.atributo) : 0) : 0;
        bool maior = comparar && diferenca > 0;
        bool menor = comparar && diferenca < 0;
        DefinirIndicador(linha.setaUp, maior);
        DefinirIndicador(linha.setaDown, menor);
        DefinirIndicador(linha.sinalPositivo, maior);
        DefinirIndicador(linha.sinalNegativo, menor);
        if (linha.textoDiferenca != null)
            linha.textoDiferenca.text = comparar ? Math.Abs(diferenca).ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private static void DefinirIndicador(GameObject indicador, bool ativo)
    {
        if (indicador != null && indicador.activeSelf != ativo) indicador.SetActive(ativo);
    }

    public string ExportarEstadoJson() => Gerado ? JsonUtility.ToJson(resultado) : null;

    public void RestaurarEstadoJson(string json)
    {
        resultado = LerEValidarEstadoJson(json);
        AtualizarPopup();
    }

    public static EstadoGerado LerEValidarEstadoJson(string json)
    {
        EstadoGerado estado = JsonUtility.FromJson<EstadoGerado>(json);
        if (estado == null || estado.versao != 1 || string.IsNullOrWhiteSpace(estado.nome) || estado.atributos == null ||
            !Enum.IsDefined(typeof(ClasseManopla), estado.classe) ||
            !Enum.IsDefined(typeof(AfinidadeDivinaManopla), estado.afinidade))
            throw new System.IO.InvalidDataException("Estado de elmo invalido.");
        var tipos = new HashSet<TipoAtributoManopla>();
        foreach (var valor in estado.atributos)
            if (valor == null || valor.valor < 0 || !Enum.IsDefined(typeof(TipoAtributoManopla), valor.atributo) || !tipos.Add(valor.atributo))
                throw new System.IO.InvalidDataException("Atributo de elmo invalido.");
        return estado;
    }

    private static List<ConfiguracaoClasse> CriarClassesPadrao()
    {
        var lista = new List<ConfiguracaoClasse>();
        int[] minimos = { 0, 1, 2, 3, 4, 6, 15, 15 };
        int[] maximos = { 1, 2, 3, 4, 5, 8, 15, 15 };
        for (int i = 0; i < 8; i++)
        {
            var classe = new ConfiguracaoClasse { classe = (ClasseManopla)i,
                chance = i == 0 ? 70f : i == 1 ? 25f : i == 2 ? 5f : 0f,
                quantidadeMinima = minimos[i], quantidadeMaxima = maximos[i] };
            foreach (TipoAtributoManopla tipo in Enum.GetValues(typeof(TipoAtributoManopla)))
                classe.atributos.Add(new FaixaAtributo { atributo = tipo, minimo = 1, maximo = 5 });
            lista.Add(classe);
        }
        return lista;
    }
}
