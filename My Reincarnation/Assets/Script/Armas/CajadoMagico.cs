using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public enum EixoLocalDirecaoMagia
{
    FrenteZ,
    TrasZ,
    DireitaX,
    EsquerdaX,
    CimaY,
    BaixoY
}

[DisallowMultipleComponent]
public class CajadoMagico : MonoBehaviour
{
    [Header("Ponto de Desenho")]
    [SerializeField] private Transform pontoLancamento;

    [Header("Desenho Runico")]
    [SerializeField] private bool usarDesenhoRunico = true;
    [SerializeField] private float distanciaMinimaEntrePontos = 0.025f;
    [SerializeField] private int maximoPontosPorTraco = 256;
    [SerializeField] private float larguraTraco = 0.015f;
    [SerializeField] private Material materialTraco;
    [SerializeField] private Color corTracoDesenhando = new Color(0.2f, 0.85f, 1f, 1f);
    [Min(0.1f)]
    [SerializeField] private float tempoParaApagarRuna = 3f;
    [SerializeField] private bool mostrarDiagnostico;

    [Header("Reconhecimento de Runas")]
    [SerializeField] private bool usarReconhecimentoRunico = true;
    [SerializeField] private float diametroMinimoRuna = 0.18f;
    [SerializeField] private float toleranciaFechamentoRelativa = 0.20f;
    [SerializeField] private float toleranciaConexaoRelativa = 0.15f;
    [SerializeField] private float variacaoRaioMaxima = 0.40f;
    [SerializeField] private float voltaMinimaGraus = 280f;
    [SerializeField] private float voltaMaximaGraus = 440f;
    [SerializeField] private float anguloMinimoDoMais = 65f;
    [SerializeField] private float anguloMaximoDoMais = 115f;
    [SerializeField] private float toleranciaRetilinearRelativa = 0.10f;
    [SerializeField] private float toleranciaCentroDoMaisRelativa = 0.30f;
    [SerializeField] private float margemMinimaEntreElementosRelativa = 0.035f;
    [SerializeField] private float margemMinimaAbsoluta = 0.01f;
    [SerializeField] private float comprimentoMinimoElementoRelativo = 0.05f;
    [SerializeField] private float pontuacaoMinima = 0.85f;

    [Header("Magia Preparada")]
    [SerializeField] private ModoReconhecimentoRuna modoReconhecimentoRuna = ModoReconhecimentoRuna.CirculoSimplesBolaDeFogo;
    [SerializeField] private Transform pontoMagiaPreparada;
    [SerializeField] private BolaDeFogo prefabBolaDeFogo;
    [SerializeField] private bool procurarPontoMagiaPreparadaAutomaticamente = true;

    [Header("Consumo de Mana")]
    [SerializeField, Min(0)] private int custoManaBolaDeFogo = 10;

    [Header("Direcao de Lancamento")]
    [SerializeField] private Transform pontoDirecaoMagia;
    [SerializeField] private bool procurarPontoDirecaoAutomaticamente = true;
    [SerializeField] private EixoLocalDirecaoMagia eixoLocalDirecaoMagia = EixoLocalDirecaoMagia.FrenteZ;

    [Header("Mira da Magia")]
    [SerializeField] private bool usarMiraLaser = true;
    [SerializeField] private LineRenderer linhaMiraLaser;
    [SerializeField] private Material materialMiraLaser;
    [SerializeField] private Color corMiraLaser = Color.red;
    [SerializeField, Min(0.001f)] private float larguraMiraLaser = 0.008f;
    [SerializeField, Min(0.1f)] private float distanciaMaximaMiraLaser = 50f;
    [SerializeField] private LayerMask camadasDetectadasPelaMira = ~0;
    [SerializeField] private QueryTriggerInteraction detectarTriggersNaMira = QueryTriggerInteraction.Ignore;
    [SerializeField] private bool limitarMiraNoPrimeiroImpacto = true;
    [SerializeField, Min(1)] private int capacidadeImpactosMira = 32;

    [Header("Diagnostico")]
    [SerializeField] private bool cajadoSegurado;
    [SerializeField] private bool estaDesenhando;
    [SerializeField] private int quantidadeTracos;
    [SerializeField] private int quantidadePontosTracoAtual;
    [SerializeField] private bool temporizadorAtivo;
    [SerializeField] private float tempoRestanteParaLimpar;
    [SerializeField] private Transform ultimoInteractor;
    [SerializeField] private string ultimaRunaReconhecida;
    [SerializeField] private float pontuacaoUltimaAnalise;
    [SerializeField] private int circulosDetectados;
    [SerializeField] private int linhasDetectadas;
    [SerializeField] private bool circuloExternoValido;
    [SerializeField] private bool sinalMaisValido;
    [SerializeField] private bool quadrantesValidos;
    [SerializeField] private bool separacaoValida;
    [SerializeField] private string motivoUltimaAnalise;
    [SerializeField] private string statusDiagnostico;

    [Header("Diagnostico Magia Preparada")]
    [SerializeField] private bool temBolaDeFogoPreparada;
    [SerializeField] private BolaDeFogo bolaDeFogoPreparadaAtual;
    [SerializeField] private string ultimaMagiaPreparada;
    [SerializeField] private string ultimaMagiaLancada;
    [SerializeField] private Vector3 ultimaDirecaoLancamento;
    [SerializeField] private string statusPreparacaoMagia;
    [SerializeField] private string statusLancamentoMagia;
    [SerializeField] private string statusDirecaoLancamento;

    [Header("Diagnostico Mana Magia")]
    [SerializeField] private StatusPlayer statusPlayerDonoAtual;
    [SerializeField] private int manaAtualDoDono;
    [SerializeField] private int manaNecessariaBolaDeFogo;
    [SerializeField] private bool ultimoConsumoManaSucesso;
    [SerializeField] private int manaConsumidaNaUltimaPreparacao;
    [SerializeField] private bool preparacaoBloqueadaPorFaltaDeMana;
    [SerializeField] private string statusManaDaMagia;

    [Header("Diagnostico Mira")]
    [SerializeField] private bool miraLaserVisivel;
    [SerializeField] private Vector3 origemAtualMira;
    [SerializeField] private Vector3 direcaoAtualMira;
    [SerializeField] private Vector3 pontoFinalAtualMira;
    [SerializeField] private float distanciaAtualMira;
    [SerializeField] private GameObject ultimoObjetoDetectadoPelaMira;
    [SerializeField] private string statusMiraLaser;

    private readonly List<LineRenderer> tracosDaRuna = new();
    private readonly List<List<Vector3>> pontosPorTraco = new();
    private readonly ReconhecedorRunas reconhecedorRunas = new();
    private List<Vector3> pontosTracoAtual;
    private XRBaseInteractable interagivelXR;
    private XRBaseInteractable interagivelRegistrado;
    private GameObject raizDesenhoRunico;
    private LineRenderer tracoAtual;
    private Material materialTracoRuntime;
    private float apagarRunaEm = -1f;
    private int indiceTracoCriado;
    private bool suprimirAnaliseAoFinalizar;
    private bool avisoSemInteragivelMostrado;
    private bool avisoSemPontoLancamentoMostrado;
    private bool manterRunaAposFalhaPreparacao;
    private bool activateFoiUsadoParaLancar;
    private bool preparandoBolaDeFogo;
    private RaycastHit[] bufferImpactosMira;
    private Material materialMiraLaserRuntime;

    public Transform PontoLancamento => pontoLancamento;
    public Transform PontoMagiaPreparada => pontoMagiaPreparada;
    public Transform PontoDirecaoMagia => pontoDirecaoMagia;
    public BolaDeFogo BolaDeFogoPreparadaAtual => bolaDeFogoPreparadaAtual;
    public bool CajadoSegurado => cajadoSegurado;
    public bool EstaDesenhando => estaDesenhando;
    public IReadOnlyList<LineRenderer> TracosDaRuna => tracosDaRuna;

    private void Awake()
    {
        EncontrarPontoLancamentoSeNecessario();
        EncontrarPontoMagiaPreparadaSeNecessario();
        EncontrarPontoDirecaoMagiaSeNecessario();
        EncontrarInteragivelXRSeNecessario();
        GarantirMiraLaser();
        AtualizarEstadoSelecao();
        EsconderMiraLaser();
        AtualizarDiagnostico();
    }

    private void OnEnable()
    {
        EncontrarPontoLancamentoSeNecessario();
        EncontrarPontoMagiaPreparadaSeNecessario();
        EncontrarPontoDirecaoMagiaSeNecessario();
        EncontrarInteragivelXRSeNecessario();
        GarantirMiraLaser();
        RegistrarEventosXR();
        AtualizarEstadoSelecao();
        EsconderMiraLaser();
        AtualizarDiagnostico();
    }

    private void OnDisable()
    {
        if (estaDesenhando)
            FinalizarTracoAtual();

        RemoverEventosXR();
        cajadoSegurado = false;
        ultimoInteractor = null;
        statusPlayerDonoAtual = null;
        EsconderMiraLaser();
        AtualizarDiagnostico();
    }

    private void OnDestroy()
    {
        RemoverEventosXR();
        DestruirRunaVisual();
        EsconderMiraLaser();

        if (materialTracoRuntime != null)
            Destroy(materialTracoRuntime);

        if (materialMiraLaserRuntime != null)
            Destroy(materialMiraLaserRuntime);
    }

    private void OnValidate()
    {
        distanciaMinimaEntrePontos = Mathf.Max(0.001f, distanciaMinimaEntrePontos);
        maximoPontosPorTraco = Mathf.Max(2, maximoPontosPorTraco);
        larguraTraco = Mathf.Max(0.001f, larguraTraco);
        tempoParaApagarRuna = Mathf.Max(0.1f, tempoParaApagarRuna);
        diametroMinimoRuna = Mathf.Max(0.01f, diametroMinimoRuna);
        toleranciaFechamentoRelativa = Mathf.Max(0.01f, toleranciaFechamentoRelativa);
        toleranciaConexaoRelativa = Mathf.Max(0.01f, toleranciaConexaoRelativa);
        variacaoRaioMaxima = Mathf.Max(0.01f, variacaoRaioMaxima);
        voltaMinimaGraus = Mathf.Clamp(voltaMinimaGraus, 0f, 720f);
        voltaMaximaGraus = Mathf.Clamp(voltaMaximaGraus, voltaMinimaGraus, 720f);
        anguloMinimoDoMais = Mathf.Clamp(anguloMinimoDoMais, 0f, 180f);
        anguloMaximoDoMais = Mathf.Clamp(anguloMaximoDoMais, anguloMinimoDoMais, 180f);
        toleranciaRetilinearRelativa = Mathf.Max(0.001f, toleranciaRetilinearRelativa);
        toleranciaCentroDoMaisRelativa = Mathf.Max(0.001f, toleranciaCentroDoMaisRelativa);
        margemMinimaEntreElementosRelativa = Mathf.Max(0f, margemMinimaEntreElementosRelativa);
        margemMinimaAbsoluta = Mathf.Max(0.001f, margemMinimaAbsoluta);
        comprimentoMinimoElementoRelativo = Mathf.Max(0.001f, comprimentoMinimoElementoRelativo);
        pontuacaoMinima = Mathf.Clamp01(pontuacaoMinima);
        custoManaBolaDeFogo = Mathf.Max(0, custoManaBolaDeFogo);
        larguraMiraLaser = Mathf.Max(0.001f, larguraMiraLaser);
        distanciaMaximaMiraLaser = Mathf.Max(0.1f, distanciaMaximaMiraLaser);
        capacidadeImpactosMira = Mathf.Max(1, capacidadeImpactosMira);
        EncontrarPontoLancamentoSeNecessario();
        EncontrarPontoMagiaPreparadaSeNecessario();
        EncontrarPontoDirecaoMagiaSeNecessario();
        EncontrarInteragivelXRSeNecessario();
        ConfigurarLinhaMiraLaserSeExistir();
        AtualizarDiagnostico();
    }

    private void Update()
    {
        AtualizarEstadoSelecao();

        if (estaDesenhando)
            AtualizarTracoAtual();
        else
            AtualizarTemporizadorLimpeza();

        if (mostrarDiagnostico)
            AtualizarDiagnostico();
    }

    private void LateUpdate()
    {
        AtualizarMiraLaser();
    }

    private void RegistrarEventosXR()
    {
        if (interagivelRegistrado != null)
            return;

        if (interagivelXR == null)
        {
            AvisarUmaVez(
                "CajadoMagico precisa de um XRBaseInteractable ou XRGrabInteractable no cajado ou na hierarquia.",
                ref avisoSemInteragivelMostrado);
            return;
        }

        interagivelXR.selectEntered.AddListener(AoSelecionarCajado);
        interagivelXR.selectExited.AddListener(AoSoltarCajado);
        interagivelXR.activated.AddListener(AoAtivarCajado);
        interagivelXR.deactivated.AddListener(AoDesativarCajado);
        interagivelRegistrado = interagivelXR;
    }

    private void RemoverEventosXR()
    {
        if (interagivelRegistrado == null)
            return;

        interagivelRegistrado.selectEntered.RemoveListener(AoSelecionarCajado);
        interagivelRegistrado.selectExited.RemoveListener(AoSoltarCajado);
        interagivelRegistrado.activated.RemoveListener(AoAtivarCajado);
        interagivelRegistrado.deactivated.RemoveListener(AoDesativarCajado);
        interagivelRegistrado = null;
    }

    private void AoSelecionarCajado(SelectEnterEventArgs args)
    {
        cajadoSegurado = true;
        ultimoInteractor = ObterTransformInteractor(args != null ? args.interactorObject : null);
        AtualizarStatusPlayerDonoAtual();
        AtualizarDiagnostico();

        if (mostrarDiagnostico)
            Debug.Log($"{name}: cajado segurado por {(ultimoInteractor != null ? ultimoInteractor.name : "interactor desconhecido")}.", this);
    }

    private void AoSoltarCajado(SelectExitEventArgs args)
    {
        if (estaDesenhando)
            FinalizarTracoAtual();

        AtualizarEstadoSelecao();
        statusPlayerDonoAtual = null;
        AtualizarDiagnostico();

        if (mostrarDiagnostico)
            Debug.Log($"{name}: cajado solto.", this);
    }

    private void AoAtivarCajado(ActivateEventArgs args)
    {
        if (activateFoiUsadoParaLancar)
            return;

        Transform interactorAtivacao = ObterTransformInteractor(args != null ? args.interactorObject : null);

        if (interactorAtivacao != null)
            ultimoInteractor = interactorAtivacao;

        AtualizarStatusPlayerDonoAtual();

        if (bolaDeFogoPreparadaAtual != null)
        {
            activateFoiUsadoParaLancar = LancarBolaDeFogoPreparada();
            return;
        }

        IniciarNovoTraco();
    }

    private void AoDesativarCajado(DeactivateEventArgs args)
    {
        if (activateFoiUsadoParaLancar)
        {
            activateFoiUsadoParaLancar = false;
            return;
        }

        if (estaDesenhando)
            FinalizarTracoAtual();
    }

    public void IniciarNovoTraco()
    {
        AtualizarEstadoSelecao();

        if (!usarDesenhoRunico || !cajadoSegurado || estaDesenhando)
            return;

        if (pontoLancamento == null)
        {
            AvisarUmaVez(
                "CajadoMagico nao pode iniciar desenho porque PontoLancamento nao foi configurado.",
                ref avisoSemPontoLancamentoMostrado);
            return;
        }

        CancelarTemporizadorLimpeza();
        GarantirRaizDesenhoRunico();

        tracoAtual = CriarLineRendererDoTraco();
        pontosTracoAtual = new List<Vector3>();
        tracosDaRuna.Add(tracoAtual);
        pontosPorTraco.Add(pontosTracoAtual);
        estaDesenhando = true;

        RegistrarPontoNoTracoAtual(pontoLancamento.position, true);
        AtualizarDiagnostico();

        if (mostrarDiagnostico)
            Debug.Log($"{name}: novo traco runico iniciado.", this);
    }

    public void FinalizarTracoAtual()
    {
        if (!estaDesenhando)
            return;

        estaDesenhando = false;
        tracoAtual = null;
        pontosTracoAtual = null;
        IniciarTemporizadorLimpeza();

        if (!suprimirAnaliseAoFinalizar)
            AnalisarRunaAposTracoFinalizado();

        AtualizarDiagnostico();

        if (mostrarDiagnostico)
            Debug.Log($"{name}: traco finalizado. Runa atual tem {quantidadeTracos} tracos.", this);
    }

    public void ConcluirRunaValida()
    {
        if (estaDesenhando)
        {
            bool suprimirAnterior = suprimirAnaliseAoFinalizar;
            suprimirAnaliseAoFinalizar = true;
            FinalizarTracoAtual();
            suprimirAnaliseAoFinalizar = suprimirAnterior;
        }

        CancelarTemporizadorLimpeza();
        LimparRunaCompleta();
        AtualizarDiagnostico();
    }

    private void AnalisarRunaAposTracoFinalizado()
    {
        if (!usarReconhecimentoRunico || pontosPorTraco.Count == 0)
            return;

        ResultadoReconhecimentoRuna resultado = reconhecedorRunas.Analisar(pontosPorTraco, CriarConfiguracaoReconhecimento());
        AplicarResultadoReconhecimento(resultado);

        if (!resultado.reconhecida)
        {
            if (mostrarDiagnostico)
                Debug.Log($"{name}: runa ainda nao reconhecida. {resultado.motivo}", this);

            return;
        }

        ultimaRunaReconhecida = resultado.idRuna;

        if (mostrarDiagnostico)
            Debug.Log($"{name}: runa reconhecida: {resultado.idRuna} ({resultado.pontuacao:0.00}).", this);

        ProcessarRunaReconhecida(resultado);
    }

    private ConfiguracaoReconhecimentoRuna CriarConfiguracaoReconhecimento()
    {
        return new ConfiguracaoReconhecimentoRuna
        {
            usarReconhecimentoRunico = usarReconhecimentoRunico,
            modoReconhecimentoRuna = modoReconhecimentoRuna,
            diametroMinimoRuna = diametroMinimoRuna,
            toleranciaFechamentoRelativa = toleranciaFechamentoRelativa,
            toleranciaConexaoRelativa = toleranciaConexaoRelativa,
            variacaoRaioMaxima = variacaoRaioMaxima,
            voltaMinimaGraus = voltaMinimaGraus,
            voltaMaximaGraus = voltaMaximaGraus,
            anguloMinimoDoMais = anguloMinimoDoMais,
            anguloMaximoDoMais = anguloMaximoDoMais,
            toleranciaRetilinearRelativa = toleranciaRetilinearRelativa,
            toleranciaCentroDoMaisRelativa = toleranciaCentroDoMaisRelativa,
            margemMinimaEntreElementosRelativa = margemMinimaEntreElementosRelativa,
            margemMinimaAbsoluta = margemMinimaAbsoluta,
            comprimentoMinimoElementoRelativo = comprimentoMinimoElementoRelativo,
            pontuacaoMinima = pontuacaoMinima
        };
    }

    private ConfiguracaoReconhecimentoRuna CriarConfiguracaoReconhecimentoCirculoSimples()
    {
        ConfiguracaoReconhecimentoRuna config = CriarConfiguracaoReconhecimento();
        config.modoReconhecimentoRuna = ModoReconhecimentoRuna.CirculoSimplesBolaDeFogo;
        return config;
    }

    private void AplicarResultadoReconhecimento(ResultadoReconhecimentoRuna resultado)
    {
        if (resultado == null)
            return;

        if (resultado.reconhecida)
            ultimaRunaReconhecida = resultado.idRuna;

        pontuacaoUltimaAnalise = resultado.pontuacao;
        circulosDetectados = resultado.quantidadeCirculosEncontrados;
        linhasDetectadas = resultado.quantidadeLinhasEncontradas;
        circuloExternoValido = resultado.circuloExternoValido;
        sinalMaisValido = resultado.sinalMaisValido;
        quadrantesValidos = resultado.quadrantesValidos;
        separacaoValida = resultado.separacaoValida;
        motivoUltimaAnalise = resultado.motivo;
    }

    public void LimparRunaInvalida()
    {
        if (estaDesenhando)
        {
            bool suprimirAnterior = suprimirAnaliseAoFinalizar;
            suprimirAnaliseAoFinalizar = true;
            FinalizarTracoAtual();
            suprimirAnaliseAoFinalizar = suprimirAnterior;
        }

        CancelarTemporizadorLimpeza();
        LimparRunaCompleta();
        AtualizarDiagnostico();
    }

    private void AtualizarTracoAtual()
    {
        if (pontoLancamento == null || tracoAtual == null || pontosTracoAtual == null)
        {
            FinalizarTracoAtual();
            return;
        }

        RegistrarPontoNoTracoAtual(pontoLancamento.position, false);
    }

    private void RegistrarPontoNoTracoAtual(Vector3 posicao, bool forcar)
    {
        if (tracoAtual == null || pontosTracoAtual == null)
            return;

        if (pontosTracoAtual.Count >= maximoPontosPorTraco)
            return;

        if (!forcar && pontosTracoAtual.Count > 0)
        {
            float distancia = Vector3.Distance(pontosTracoAtual[pontosTracoAtual.Count - 1], posicao);

            if (distancia < distanciaMinimaEntrePontos)
                return;
        }

        pontosTracoAtual.Add(posicao);
        tracoAtual.positionCount = pontosTracoAtual.Count;
        tracoAtual.SetPosition(pontosTracoAtual.Count - 1, posicao);
    }

    private void IniciarTemporizadorLimpeza()
    {
        apagarRunaEm = Time.time + tempoParaApagarRuna;
        temporizadorAtivo = true;
        tempoRestanteParaLimpar = tempoParaApagarRuna;
    }

    private void CancelarTemporizadorLimpeza()
    {
        apagarRunaEm = -1f;
        temporizadorAtivo = false;
        tempoRestanteParaLimpar = 0f;
    }

    private void AtualizarTemporizadorLimpeza()
    {
        if (apagarRunaEm < 0f)
            return;

        tempoRestanteParaLimpar = Mathf.Max(0f, apagarRunaEm - Time.time);
        temporizadorAtivo = true;

        if (Time.time >= apagarRunaEm)
        {
            if (modoReconhecimentoRuna == ModoReconhecimentoRuna.Todos && TentarConfirmarCirculoSimplesAoExpirarTemporizador())
                return;

            LimparRunaInvalida();
        }
    }

    private bool TentarConfirmarCirculoSimplesAoExpirarTemporizador()
    {
        if (!usarReconhecimentoRunico || pontosPorTraco.Count == 0)
            return false;

        ResultadoReconhecimentoRuna resultado = reconhecedorRunas.Analisar(pontosPorTraco, CriarConfiguracaoReconhecimentoCirculoSimples());
        AplicarResultadoReconhecimento(resultado);

        if (!resultado.reconhecida || resultado.idRuna != ResultadoReconhecimentoRuna.IdCirculoBolaDeFogo)
            return false;

        return ProcessarRunaReconhecida(resultado);
    }

    private bool ProcessarRunaReconhecida(ResultadoReconhecimentoRuna resultado)
    {
        if (resultado == null || !resultado.reconhecida)
            return false;

        if (resultado.idRuna == ResultadoReconhecimentoRuna.IdCirculoBolaDeFogo)
        {
            bool preparou = PrepararBolaDeFogo();

            if (!preparou)
            {
                if (manterRunaAposFalhaPreparacao)
                    CancelarTemporizadorLimpeza();
                else
                    ConcluirRunaValida();

                AtualizarDiagnostico();
                return manterRunaAposFalhaPreparacao;
            }

            ultimaMagiaPreparada = "Bola de Fogo";
        }

        ConcluirRunaValida();
        return true;
    }

    private bool PrepararBolaDeFogo()
    {
        manterRunaAposFalhaPreparacao = false;

        if (preparandoBolaDeFogo)
        {
            statusPreparacaoMagia = "Preparacao de Bola de Fogo ja esta em andamento.";
            AtualizarDiagnosticoManaMagia();
            AtualizarDiagnosticoMagiaPreparada();
            return false;
        }

        if (bolaDeFogoPreparadaAtual != null)
        {
            statusPreparacaoMagia = "Ja existe uma Bola de Fogo preparada.";
            statusManaDaMagia = "Mana nao consumida: ja existe uma Bola de Fogo preparada.";
            ultimoConsumoManaSucesso = false;
            manaConsumidaNaUltimaPreparacao = 0;
            preparacaoBloqueadaPorFaltaDeMana = false;
            AtualizarDiagnosticoManaMagia();
            AtualizarDiagnosticoMagiaPreparada();
            return false;
        }

        preparandoBolaDeFogo = true;
        bool manaConsumida = false;
        int manaConsumidaNestaPreparacao = 0;

        try
        {
            EncontrarPontoMagiaPreparadaSeNecessario();

            if (pontoMagiaPreparada == null)
            {
                statusPreparacaoMagia = "PontoMagiaPreparada nao configurado.";
                statusManaDaMagia = "Mana nao consumida: PontoMagiaPreparada ausente.";
                manterRunaAposFalhaPreparacao = true;
                AtualizarDiagnosticoManaMagia();
                AtualizarDiagnosticoMagiaPreparada();
                return false;
            }

            if (prefabBolaDeFogo == null)
            {
                statusPreparacaoMagia = "Prefab Bola de Fogo nao configurado.";
                statusManaDaMagia = "Mana nao consumida: prefab Bola de Fogo ausente.";
                manterRunaAposFalhaPreparacao = true;
                AtualizarDiagnosticoManaMagia();
                AtualizarDiagnosticoMagiaPreparada();
                return false;
            }

            StatusPlayer statusDono = ObterStatusPlayerDonoAtual(true);
            if (statusDono == null && custoManaBolaDeFogo > 0)
            {
                statusPreparacaoMagia = "StatusPlayer do dono nao encontrado.";
                statusManaDaMagia = "Mana nao consumida: StatusPlayer do dono nao encontrado.";
                manterRunaAposFalhaPreparacao = true;
                AtualizarDiagnosticoManaMagia();
                AtualizarDiagnosticoMagiaPreparada();
                return false;
            }

            int custo = Mathf.Max(0, custoManaBolaDeFogo);
            manaNecessariaBolaDeFogo = custo;
            manaAtualDoDono = statusDono != null ? statusDono.GetManaAtual() : 0;
            preparacaoBloqueadaPorFaltaDeMana = false;
            ultimoConsumoManaSucesso = false;
            manaConsumidaNaUltimaPreparacao = 0;

            if (statusDono != null && !statusDono.TentarConsumirMana(custo))
            {
                manaAtualDoDono = statusDono.GetManaAtual();
                preparacaoBloqueadaPorFaltaDeMana = true;
                statusPreparacaoMagia = "Mana insuficiente para preparar Bola de Fogo.";
                statusManaDaMagia = $"Mana insuficiente. Atual: {manaAtualDoDono}, necessaria: {custo}.";
                AtualizarDiagnosticoMagiaPreparada();
                return false;
            }

            manaConsumida = custo > 0;
            manaConsumidaNestaPreparacao = custo;
            ultimoConsumoManaSucesso = manaConsumida;
            manaConsumidaNaUltimaPreparacao = manaConsumidaNestaPreparacao;
            manaAtualDoDono = statusDono != null ? statusDono.GetManaAtual() : manaAtualDoDono;

            BolaDeFogo instancia = Instantiate(prefabBolaDeFogo, pontoMagiaPreparada.position, pontoMagiaPreparada.rotation);

            if (instancia == null)
            {
                ReembolsarManaPreparacao(statusDono, manaConsumidaNestaPreparacao);
                statusPreparacaoMagia = "Falha ao instanciar Bola de Fogo.";
                manterRunaAposFalhaPreparacao = true;
                AtualizarDiagnosticoMagiaPreparada();
                return false;
            }

            bolaDeFogoPreparadaAtual = instancia;
            instancia.Preparar(pontoMagiaPreparada, ObterDonoDaMagia(), gameObject);

            if (instancia.EstadoAtual != BolaDeFogo.EstadoBolaDeFogo.Preparada)
            {
                ReembolsarManaPreparacao(statusDono, manaConsumidaNestaPreparacao);
                statusPreparacaoMagia = "Bola de Fogo instanciada, mas nao ficou em estado Preparada.";
                manterRunaAposFalhaPreparacao = true;
                bolaDeFogoPreparadaAtual = null;
                Destroy(instancia.gameObject);
                AtualizarDiagnosticoMagiaPreparada();
                return false;
            }

            manaConsumida = false;
            statusPreparacaoMagia = "Bola de Fogo preparada.";
            statusManaDaMagia = custo > 0
                ? $"{custo} de mana consumidos para preparar Bola de Fogo."
                : "Bola de Fogo preparada sem custo de mana.";
            AtualizarDiagnosticoMagiaPreparada();
            AtualizarMiraLaser();
            return true;
        }
        catch (System.Exception erro)
        {
            if (manaConsumida)
                ReembolsarManaPreparacao(statusPlayerDonoAtual, manaConsumidaNestaPreparacao);

            statusPreparacaoMagia = "Falha excepcional ao preparar Bola de Fogo.";
            statusManaDaMagia = $"Mana reembolsada apos falha: {erro.GetType().Name}.";
            manterRunaAposFalhaPreparacao = true;
            AtualizarDiagnosticoMagiaPreparada();
            return false;
        }
        finally
        {
            preparandoBolaDeFogo = false;
            AtualizarDiagnosticoManaMagia();
        }
    }

    public void NotificarBolaDeFogoEncerrada(BolaDeFogo bola)
    {
        if (bola == null || bola != bolaDeFogoPreparadaAtual)
            return;

        bolaDeFogoPreparadaAtual = null;
        statusPreparacaoMagia = "Bola de Fogo encerrada.";
        EsconderMiraLaser();
        AtualizarDiagnosticoMagiaPreparada();
    }

    private bool LancarBolaDeFogoPreparada()
    {
        AtualizarEstadoSelecao();

        BolaDeFogo bolaParaLancar = bolaDeFogoPreparadaAtual;

        if (bolaParaLancar == null)
        {
            statusLancamentoMagia = "Nenhuma Bola de Fogo preparada.";
            AtualizarDiagnosticoMagiaPreparada();
            return false;
        }

        if (bolaParaLancar.EstadoAtual != BolaDeFogo.EstadoBolaDeFogo.Preparada)
        {
            bolaDeFogoPreparadaAtual = null;
            EsconderMiraLaser();
            statusLancamentoMagia = "Referencia de Bola de Fogo preparada estava invalida.";
            AtualizarDiagnosticoMagiaPreparada();
            return false;
        }

        if (!cajadoSegurado)
        {
            statusLancamentoMagia = "Cajado nao esta segurado; lancamento ignorado.";
            AtualizarDiagnosticoMagiaPreparada();
            return false;
        }

        if (pontoLancamento == null)
        {
            EncontrarPontoLancamentoSeNecessario();

            if (pontoLancamento == null)
            {
                statusLancamentoMagia = "PontoLancamento nao configurado.";
                AtualizarDiagnosticoMagiaPreparada();
                return false;
            }
        }

        Vector3 direcao = ObterDirecaoLancamento();

        if (direcao.sqrMagnitude < 0.0001f)
        {
            statusLancamentoMagia = "Direcao de lancamento invalida.";
            statusDirecaoLancamento = pontoDirecaoMagia == null
                ? "PontoDirecaoMagia nao configurado."
                : "Eixo local de direcao gerou vetor invalido.";
            AtualizarDiagnosticoMagiaPreparada();
            return false;
        }

        ultimaDirecaoLancamento = direcao.normalized;
        EsconderMiraLaser();
        bolaDeFogoPreparadaAtual = null;
        bolaParaLancar.Lancar(ultimaDirecaoLancamento, ObterDonoDaMagia(), gameObject);

        ultimaMagiaLancada = "BolaDeFogo";
        statusLancamentoMagia = "Bola de Fogo lancada.";
        statusDirecaoLancamento = "Direcao obtida pelo PontoDirecaoMagia.";
        statusPreparacaoMagia = "Nenhuma Bola de Fogo preparada.";
        AtualizarDiagnosticoMagiaPreparada();
        AtualizarDiagnostico();
        return true;
    }

    private void LimparRunaCompleta()
    {
        for (int i = tracosDaRuna.Count - 1; i >= 0; i--)
        {
            LineRenderer traco = tracosDaRuna[i];

            if (traco != null)
                Destroy(traco.gameObject);
        }

        tracosDaRuna.Clear();
        pontosPorTraco.Clear();
        pontosTracoAtual = null;
        tracoAtual = null;
        estaDesenhando = false;
        quantidadeTracos = 0;
        quantidadePontosTracoAtual = 0;
        indiceTracoCriado = 0;
    }

    private void DestruirRunaVisual()
    {
        LimparRunaCompleta();

        if (raizDesenhoRunico != null)
        {
            Destroy(raizDesenhoRunico);
            raizDesenhoRunico = null;
        }
    }

    private void GarantirRaizDesenhoRunico()
    {
        if (raizDesenhoRunico != null)
            return;

        raizDesenhoRunico = new GameObject($"Desenho Runico - {name}");
    }

    private LineRenderer CriarLineRendererDoTraco()
    {
        indiceTracoCriado++;

        GameObject objetoTraco = new GameObject($"Traco {indiceTracoCriado}");
        objetoTraco.transform.SetParent(raizDesenhoRunico.transform, false);

        LineRenderer lineRenderer = objetoTraco.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.startWidth = larguraTraco;
        lineRenderer.endWidth = larguraTraco;
        lineRenderer.startColor = corTracoDesenhando;
        lineRenderer.endColor = corTracoDesenhando;

        Material materialLinha = materialTraco != null ? materialTraco : ObterMaterialTracoPadrao();

        if (materialLinha != null)
            lineRenderer.material = materialLinha;

        return lineRenderer;
    }

    private Material ObterMaterialTracoPadrao()
    {
        if (materialTracoRuntime != null)
            return materialTracoRuntime;

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            return null;

        materialTracoRuntime = new Material(shader);
        return materialTracoRuntime;
    }

    private void GarantirMiraLaser()
    {
        if (linhaMiraLaser == null)
        {
            Transform miraExistente = EncontrarFilhoPorNomeExato("MiraLaserMagia");
            if (miraExistente == null)
            {
                GameObject objetoMira = new GameObject("MiraLaserMagia");
                objetoMira.transform.SetParent(transform, false);
                objetoMira.transform.localPosition = Vector3.zero;
                objetoMira.transform.localRotation = Quaternion.identity;
                objetoMira.transform.localScale = Vector3.one;
                miraExistente = objetoMira.transform;
            }

            linhaMiraLaser = miraExistente.GetComponent<LineRenderer>();
            if (linhaMiraLaser == null)
                linhaMiraLaser = miraExistente.gameObject.AddComponent<LineRenderer>();
        }

        GarantirBufferImpactosMira();
        ConfigurarLinhaMiraLaserSeExistir();
    }

    private void ConfigurarLinhaMiraLaserSeExistir()
    {
        if (linhaMiraLaser == null)
            return;

        linhaMiraLaser.useWorldSpace = true;
        linhaMiraLaser.positionCount = 2;
        linhaMiraLaser.startWidth = larguraMiraLaser;
        linhaMiraLaser.endWidth = larguraMiraLaser;
        linhaMiraLaser.startColor = corMiraLaser;
        linhaMiraLaser.endColor = corMiraLaser;
        linhaMiraLaser.numCapVertices = 4;
        linhaMiraLaser.numCornerVertices = 2;
        linhaMiraLaser.loop = false;
        linhaMiraLaser.shadowCastingMode = ShadowCastingMode.Off;
        linhaMiraLaser.receiveShadows = false;

        Material materialLinha = materialMiraLaser != null ? materialMiraLaser : ObterMaterialMiraLaserPadrao();
        if (materialMiraLaser == null)
            AplicarCorNoMaterialMiraLaserRuntime();

        if (materialLinha != null)
            linhaMiraLaser.material = materialLinha;

        if (!miraLaserVisivel)
            linhaMiraLaser.enabled = false;
    }

    private Material ObterMaterialMiraLaserPadrao()
    {
        if (materialMiraLaserRuntime != null)
            return materialMiraLaserRuntime;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        materialMiraLaserRuntime = new Material(shader)
        {
            name = "Mira Laser Magia Runtime"
        };

        if (materialMiraLaserRuntime.HasProperty("_BaseColor"))
            materialMiraLaserRuntime.SetColor("_BaseColor", corMiraLaser);
        else if (materialMiraLaserRuntime.HasProperty("_Color"))
            materialMiraLaserRuntime.SetColor("_Color", corMiraLaser);

        return materialMiraLaserRuntime;
    }

    private void AplicarCorNoMaterialMiraLaserRuntime()
    {
        if (materialMiraLaserRuntime == null)
            return;

        if (materialMiraLaserRuntime.HasProperty("_BaseColor"))
            materialMiraLaserRuntime.SetColor("_BaseColor", corMiraLaser);
        else if (materialMiraLaserRuntime.HasProperty("_Color"))
            materialMiraLaserRuntime.SetColor("_Color", corMiraLaser);
    }

    private void GarantirBufferImpactosMira()
    {
        if (bufferImpactosMira == null || bufferImpactosMira.Length != capacidadeImpactosMira)
            bufferImpactosMira = new RaycastHit[capacidadeImpactosMira];
    }

    private void AtualizarMiraLaser()
    {
        if (!DeveMostrarMiraLaser())
        {
            EsconderMiraLaser();
            return;
        }

        GarantirBufferImpactosMira();
        Vector3 origem = ObterOrigemMiraLaser();
        Vector3 direcao = ObterDirecaoLancamento();

        if (direcao.sqrMagnitude < 0.0001f)
        {
            statusMiraLaser = "Direcao da mira invalida.";
            EsconderMiraLaser();
            return;
        }

        direcao.Normalize();
        origemAtualMira = origem;
        direcaoAtualMira = direcao;
        pontoFinalAtualMira = CalcularPontoFinalMira(origem, direcao, out float distanciaFinal, out GameObject objetoDetectado);
        distanciaAtualMira = distanciaFinal;
        ultimoObjetoDetectadoPelaMira = objetoDetectado;

        linhaMiraLaser.SetPosition(0, origemAtualMira);
        linhaMiraLaser.SetPosition(1, pontoFinalAtualMira);
        MostrarMiraLaser();
        statusMiraLaser = objetoDetectado != null
            ? $"Mira apontando para {objetoDetectado.name}."
            : "Mira sem impacto valido.";
    }

    private bool DeveMostrarMiraLaser()
    {
        if (!usarMiraLaser || !isActiveAndEnabled || linhaMiraLaser == null || estaDesenhando || !cajadoSegurado)
            return false;

        if (bolaDeFogoPreparadaAtual == null)
            return false;

        if (bolaDeFogoPreparadaAtual.EstadoAtual != BolaDeFogo.EstadoBolaDeFogo.Preparada)
            return false;

        EncontrarPontoDirecaoMagiaSeNecessario();
        return pontoDirecaoMagia != null;
    }

    private Vector3 ObterOrigemMiraLaser()
    {
        if (bolaDeFogoPreparadaAtual != null)
            return bolaDeFogoPreparadaAtual.transform.position;

        if (pontoMagiaPreparada != null)
            return pontoMagiaPreparada.position;

        return transform.position;
    }

    private Vector3 CalcularPontoFinalMira(Vector3 origem, Vector3 direcao, out float distanciaFinal, out GameObject objetoDetectado)
    {
        distanciaFinal = distanciaMaximaMiraLaser;
        objetoDetectado = null;

        if (!limitarMiraNoPrimeiroImpacto)
            return origem + direcao * distanciaFinal;

        GarantirBufferImpactosMira();

        float raioMira = ObterRaioMundialBolaDeFogoPreparada();
        int impactos = raioMira > 0.001f
            ? Physics.SphereCastNonAlloc(origem, raioMira, direcao, bufferImpactosMira, distanciaMaximaMiraLaser, camadasDetectadasPelaMira, detectarTriggersNaMira)
            : Physics.RaycastNonAlloc(origem, direcao, bufferImpactosMira, distanciaMaximaMiraLaser, camadasDetectadasPelaMira, detectarTriggersNaMira);

        float menorDistancia = distanciaMaximaMiraLaser;
        RaycastHit melhorHit = default;
        bool encontrouImpacto = false;

        for (int i = 0; i < impactos; i++)
        {
            RaycastHit hit = bufferImpactosMira[i];
            bufferImpactosMira[i] = default;

            if (!ImpactoMiraValido(hit))
                continue;

            if (hit.distance < menorDistancia)
            {
                menorDistancia = hit.distance;
                melhorHit = hit;
                encontrouImpacto = true;
            }
        }

        if (!encontrouImpacto)
            return origem + direcao * distanciaFinal;

        distanciaFinal = menorDistancia;
        objetoDetectado = melhorHit.collider != null ? melhorHit.collider.gameObject : null;
        return origem + direcao * distanciaFinal;
    }

    private float ObterRaioMundialBolaDeFogoPreparada()
    {
        if (bolaDeFogoPreparadaAtual == null)
            return 0f;

        SphereCollider sphere = bolaDeFogoPreparadaAtual.GetComponent<SphereCollider>();
        if (sphere == null)
            sphere = bolaDeFogoPreparadaAtual.GetComponentInChildren<SphereCollider>(true);

        if (sphere == null)
            return 0f;

        Vector3 escala = sphere.transform.lossyScale;
        float maiorEscala = Mathf.Max(Mathf.Abs(escala.x), Mathf.Abs(escala.y), Mathf.Abs(escala.z));
        return Mathf.Max(0f, sphere.radius * maiorEscala);
    }

    private bool ImpactoMiraValido(RaycastHit hit)
    {
        Collider col = hit.collider;
        if (col == null)
            return false;

        if (bolaDeFogoPreparadaAtual != null && bolaDeFogoPreparadaAtual.ColliderIgnoradoPelaMagia(col))
            return false;

        if (ColliderPertenceATransform(col, transform))
            return false;

        if (bolaDeFogoPreparadaAtual != null && ColliderPertenceATransform(col, bolaDeFogoPreparadaAtual.transform))
            return false;

        if (pontoMagiaPreparada != null && ColliderPertenceATransform(col, pontoMagiaPreparada))
            return false;

        if (pontoDirecaoMagia != null && ColliderPertenceATransform(col, pontoDirecaoMagia))
            return false;

        if (ultimoInteractor != null && ColliderPertenceATransform(col, ultimoInteractor))
            return false;

        GameObject dono = ObterDonoDaMagia();
        if (dono != null && ColliderPertenceATransform(col, dono.transform))
            return false;

        return true;
    }

    private static bool ColliderPertenceATransform(Collider col, Transform raiz)
    {
        if (col == null || raiz == null)
            return false;

        if (col.transform == raiz || col.transform.IsChildOf(raiz))
            return true;

        Rigidbody rbContato = col.attachedRigidbody;
        return rbContato != null && (rbContato.transform == raiz || rbContato.transform.IsChildOf(raiz));
    }

    private void MostrarMiraLaser()
    {
        if (linhaMiraLaser == null)
            return;

        linhaMiraLaser.enabled = true;
        miraLaserVisivel = true;
    }

    private void EsconderMiraLaser()
    {
        if (linhaMiraLaser != null)
        {
            linhaMiraLaser.enabled = false;
            linhaMiraLaser.positionCount = 2;
            linhaMiraLaser.SetPosition(0, Vector3.zero);
            linhaMiraLaser.SetPosition(1, Vector3.zero);
        }

        miraLaserVisivel = false;
        distanciaAtualMira = 0f;
        ultimoObjetoDetectadoPelaMira = null;
        statusMiraLaser = "Mira escondida.";
    }

    private void AtualizarEstadoSelecao()
    {
        if (interagivelXR == null)
        {
            cajadoSegurado = false;
            ultimoInteractor = null;
            return;
        }

        cajadoSegurado = interagivelXR.isSelected;

        if (!cajadoSegurado)
        {
            ultimoInteractor = null;
            return;
        }

        if (ultimoInteractor == null)
            ultimoInteractor = ObterPrimeiroInteractorSelecionando();
    }

    private void AtualizarDiagnostico()
    {
        quantidadeTracos = tracosDaRuna.Count;
        quantidadePontosTracoAtual = pontosTracoAtual != null ? pontosTracoAtual.Count : 0;
        AtualizarDiagnosticoMagiaPreparada();

        if (apagarRunaEm >= 0f && !estaDesenhando)
        {
            temporizadorAtivo = true;
            tempoRestanteParaLimpar = Mathf.Max(0f, apagarRunaEm - Time.time);
        }
        else
        {
            temporizadorAtivo = false;
            tempoRestanteParaLimpar = 0f;
        }

        if (!mostrarDiagnostico)
        {
            statusDiagnostico = string.Empty;
            return;
        }

        string nomeInteractor = ultimoInteractor != null ? ultimoInteractor.name : "nenhum";
        string nomePonto = pontoLancamento != null ? pontoLancamento.name : "nao configurado";
        string nomePontoMagia = pontoMagiaPreparada != null ? pontoMagiaPreparada.name : "nao configurado";
        string nomePontoDirecao = pontoDirecaoMagia != null ? pontoDirecaoMagia.name : "nao configurado";

        statusDiagnostico =
            $"Segurado: {cajadoSegurado} | Desenhando: {estaDesenhando} | Interactor: {nomeInteractor} | Ponto: {nomePonto} | Ponto Magia: {nomePontoMagia} | Ponto Direcao: {nomePontoDirecao} | Eixo Direcao: {eixoLocalDirecaoMagia} | Tracos: {quantidadeTracos} | Pontos Traco Atual: {quantidadePontosTracoAtual} | Limpar em: {tempoRestanteParaLimpar:0.00}s | Modo: {modoReconhecimentoRuna} | Runa: {ultimaRunaReconhecida} | Score: {pontuacaoUltimaAnalise:0.00} | Motivo: {motivoUltimaAnalise} | Magia Preparada: {ultimaMagiaPreparada} | Magia Lancada: {ultimaMagiaLancada} | Direcao Lancamento: {ultimaDirecaoLancamento} | Preparacao: {statusPreparacaoMagia} | Lancamento: {statusLancamentoMagia} | Direcao: {statusDirecaoLancamento}";
        statusDiagnostico += $" | Mana: {manaAtualDoDono}/{manaNecessariaBolaDeFogo} | Status Mana: {statusManaDaMagia}";
        statusDiagnostico += $" | Mira: {miraLaserVisivel} | Dist Mira: {distanciaAtualMira:0.00} | Status Mira: {statusMiraLaser}";
    }

    private void AtualizarDiagnosticoMagiaPreparada()
    {
        AtualizarDiagnosticoManaMagia();
        temBolaDeFogoPreparada = bolaDeFogoPreparadaAtual != null &&
                                 bolaDeFogoPreparadaAtual.EstadoAtual == BolaDeFogo.EstadoBolaDeFogo.Preparada;
    }

    private void EncontrarPontoLancamentoSeNecessario()
    {
        if (pontoLancamento != null)
            return;

        pontoLancamento = EncontrarFilhoPorNomeExato("PontoLancamento");
    }

    private void EncontrarPontoMagiaPreparadaSeNecessario()
    {
        if (pontoMagiaPreparada != null || !procurarPontoMagiaPreparadaAutomaticamente)
            return;

        pontoMagiaPreparada = EncontrarFilhoPorNomeExato("PontoMagiaPreparada");
    }

    private void EncontrarPontoDirecaoMagiaSeNecessario()
    {
        if (pontoDirecaoMagia != null || !procurarPontoDirecaoAutomaticamente)
            return;

        pontoDirecaoMagia = EncontrarFilhoPorNomeExato("PontoDirecaoMagia");
    }

    private Vector3 ObterDirecaoLancamento()
    {
        EncontrarPontoDirecaoMagiaSeNecessario();

        if (pontoDirecaoMagia == null)
        {
            statusDirecaoLancamento = "PontoDirecaoMagia nao configurado.";
            return Vector3.zero;
        }

        Vector3 eixoLocal = ObterEixoLocalDirecaoMagia();
        Vector3 direcaoMundo = pontoDirecaoMagia.TransformDirection(eixoLocal);

        if (direcaoMundo.sqrMagnitude < 0.0001f)
        {
            statusDirecaoLancamento = "Direcao local invalida.";
            return Vector3.zero;
        }

        statusDirecaoLancamento = "Direcao obtida pelo PontoDirecaoMagia.";
        return direcaoMundo.normalized;
    }

    private Vector3 ObterEixoLocalDirecaoMagia()
    {
        switch (eixoLocalDirecaoMagia)
        {
            case EixoLocalDirecaoMagia.TrasZ:
                return Vector3.back;
            case EixoLocalDirecaoMagia.DireitaX:
                return Vector3.right;
            case EixoLocalDirecaoMagia.EsquerdaX:
                return Vector3.left;
            case EixoLocalDirecaoMagia.CimaY:
                return Vector3.up;
            case EixoLocalDirecaoMagia.BaixoY:
                return Vector3.down;
            case EixoLocalDirecaoMagia.FrenteZ:
            default:
                return Vector3.forward;
        }
    }

    private Transform EncontrarFilhoPorNomeExato(string nomeFilho)
    {
        Transform[] filhos = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < filhos.Length; i++)
        {
            if (filhos[i].name == nomeFilho)
                return filhos[i];
        }

        return null;
    }

    private void EncontrarInteragivelXRSeNecessario()
    {
        if (interagivelXR != null)
            return;

        interagivelXR = GetComponent<XRBaseInteractable>();

        if (interagivelXR == null)
            interagivelXR = GetComponentInParent<XRBaseInteractable>();

        if (interagivelXR == null)
            interagivelXR = GetComponentInChildren<XRBaseInteractable>(true);
    }

    private Transform ObterPrimeiroInteractorSelecionando()
    {
        if (interagivelXR == null || interagivelXR.interactorsSelecting == null || interagivelXR.interactorsSelecting.Count == 0)
            return null;

        return ObterTransformInteractor(interagivelXR.interactorsSelecting[0]);
    }

    private static Transform ObterTransformInteractor(IXRInteractor interactor)
    {
        return interactor is Component componente ? componente.transform : null;
    }

    private GameObject ObterDonoDaMagia()
    {
        Transform interactor = ultimoInteractor != null ? ultimoInteractor : ObterPrimeiroInteractorSelecionando();

        if (interactor == null)
            return null;

        return interactor.root != null ? interactor.root.gameObject : interactor.gameObject;
    }

    private void AtualizarStatusPlayerDonoAtual()
    {
        statusPlayerDonoAtual = ResolverStatusPlayerDoDono();
        AtualizarDiagnosticoManaMagia();
    }

    private StatusPlayer ObterStatusPlayerDonoAtual(bool atualizarSeNecessario)
    {
        if (statusPlayerDonoAtual == null && atualizarSeNecessario)
            AtualizarStatusPlayerDonoAtual();

        return statusPlayerDonoAtual;
    }

    private StatusPlayer ResolverStatusPlayerDoDono()
    {
        GameObject dono = ObterDonoDaMagia();
        StatusPlayer status = ResolverStatusPlayerEmGameObject(dono);
        if (status != null)
            return status;

        Transform interactor = ultimoInteractor != null ? ultimoInteractor : ObterPrimeiroInteractorSelecionando();
        return ResolverStatusPlayerEmTransform(interactor);
    }

    private static StatusPlayer ResolverStatusPlayerEmGameObject(GameObject alvo)
    {
        return alvo != null ? ResolverStatusPlayerEmTransform(alvo.transform) : null;
    }

    private static StatusPlayer ResolverStatusPlayerEmTransform(Transform alvo)
    {
        if (alvo == null)
            return null;

        StatusPlayer status = alvo.GetComponent<StatusPlayer>();
        if (status != null)
            return status;

        status = alvo.GetComponentInParent<StatusPlayer>();
        if (status != null)
            return status;

        Transform raiz = alvo.root;
        return raiz != null ? raiz.GetComponentInChildren<StatusPlayer>(true) : null;
    }

    private void ReembolsarManaPreparacao(StatusPlayer statusDono, int quantidade)
    {
        if (statusDono == null || quantidade <= 0)
            return;

        statusDono.RecuperarMana(quantidade);
        ultimoConsumoManaSucesso = false;
        manaConsumidaNaUltimaPreparacao = 0;
        manaAtualDoDono = statusDono.GetManaAtual();
        statusManaDaMagia = $"Mana reembolsada: {quantidade}.";
    }

    private void AtualizarDiagnosticoManaMagia()
    {
        manaNecessariaBolaDeFogo = Mathf.Max(0, custoManaBolaDeFogo);
        manaAtualDoDono = statusPlayerDonoAtual != null ? statusPlayerDonoAtual.GetManaAtual() : 0;
    }

    private void AvisarUmaVez(string mensagem, ref bool avisoJaMostrado)
    {
        if (avisoJaMostrado)
            return;

        avisoJaMostrado = true;
        Debug.LogWarning(mensagem, this);
    }

    private void OnDrawGizmosSelected()
    {
        if (pontoDirecaoMagia == null)
            return;

        Vector3 direcao = pontoDirecaoMagia.TransformDirection(ObterEixoLocalDirecaoMagia());
        if (direcao.sqrMagnitude < 0.0001f)
            return;

        Vector3 origem = pontoDirecaoMagia.position;
        Vector3 destino = origem + direcao.normalized * 0.5f;

        Gizmos.color = new Color(1f, 0.35f, 0.02f, 1f);
        Gizmos.DrawLine(origem, destino);
        Gizmos.DrawSphere(destino, 0.035f);
    }
}
