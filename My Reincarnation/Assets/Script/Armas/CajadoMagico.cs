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

public enum OrigemAtivacaoMagia
{
    Runa,
    Voz
}

[DisallowMultipleComponent]
public class CajadoMagico : MonoBehaviour
{
    private enum EstadoDisponibilidadeDesenho
    {
        NoInventario,
        SaindoDoInventario,
        AguardandoEstabilidade,
        AguardandoSoltarActivate,
        Pronto,
        Desenhando
    }

    [Header("Ponto de Desenho")]
    [SerializeField] private Transform pontoLancamento;

    [Header("Desenho Runico")]
    [SerializeField] private bool usarDesenhoRunico = true;
    [SerializeField] private float distanciaMinimaEntrePontos = 0.025f;
    [SerializeField] private float distanciaMaximaSaltoEntrePontos = 0.45f;
    [SerializeField] private int maximoPontosPorTraco = 256;
    [SerializeField] private float larguraTraco = 0.015f;
    [SerializeField] private Material materialTraco;
    [SerializeField] private Color corTracoDesenhando = new Color(0.2f, 0.85f, 1f, 1f);
    [Min(0.1f)]
    [SerializeField] private float tempoParaApagarRuna = 3f;
    [SerializeField] private float atrasoDesenhoAposSairInventario = 0.15f;
    [SerializeField] private bool mostrarDiagnostico;

    [Header("Estabilidade Apos Inventario")]
    [SerializeField, Min(1)] private int framesConsecutivosPontoEstavel = 3;
    [SerializeField, Min(0.0001f)] private float distanciaMaximaPontoEstavelPorFrame = 0.12f;
    [SerializeField, Min(0.01f)] private float tempoMinimoAposSairInventario = 0.20f;

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
    [SerializeField] private OrigemAtivacaoMagia ultimaOrigemAtivacaoMagia;

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

    [Header("Diagnostico Inventario")]
    [SerializeField] private bool cajadoNoInventario;
    [SerializeField] private bool estadoLimpoAoEntrarNoInventario;
    [SerializeField] private bool listenersXRRegistrados;
    [SerializeField] private int quantidadeRegistrosActivated;
    [SerializeField] private int quantidadeRegistrosDeactivated;
    [SerializeField] private int quantidadeContainersDesenho;
    [SerializeField] private int pontosDoTracoAtual;
    [SerializeField] private bool rigidbodyPresente;
    [SerializeField] private bool rigidbodyCinematico;
    [SerializeField] private bool rigidbodyUsandoGravidade;
    [SerializeField] private bool rigidbodyDetectandoColisoes;
    [SerializeField] private string ultimoMotivoLimpeza;
    [SerializeField] private string statusCicloInventario;

    [Header("Diagnostico Estabilidade Desenho")]
    [SerializeField] private EstadoDisponibilidadeDesenho estadoDisponibilidadeDesenho = EstadoDisponibilidadeDesenho.Pronto;
    [SerializeField] private bool inventarioEstaProcessando;
    [SerializeField] private bool socketAindaSelecionando;
    [SerializeField] private bool maoEstaSelecionando;
    [SerializeField] private bool aguardandoSoltarActivate;
    [SerializeField] private int framesEstaveisAtuais;
    [SerializeField] private int framesEstaveisNecessarios;
    [SerializeField] private float distanciaMovidaPelaPontaNoUltimoFrame;
    [SerializeField] private bool parentMudouEstabilidade;
    [SerializeField] private bool escalaMudouEstabilidade;
    [SerializeField] private bool podeIniciarDesenho;
    [SerializeField] private int activatedRecebidosNesteCiclo;
    [SerializeField] private int deactivatedRecebidosNesteCiclo;
    [SerializeField] private string motivoBloqueioDesenho;

    private readonly List<LineRenderer> tracosDaRuna = new();
    private readonly List<List<Vector3>> pontosPorTraco = new();
    private readonly ReconhecedorRunas reconhecedorRunas = new();
    private List<Vector3> pontosTracoAtual;
    private XRBaseInteractable interagivelXR;
    private XRBaseInteractable interagivelRegistrado;
    private EstadoItemInventario estadoInventario;
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
    private bool eventoInventarioRegistrado;
    private float desenhoBloqueadoAte;
    private float horarioSaidaInventario;
    private Transform parentAnteriorEstabilidade;
    private Vector3 escalaAnteriorEstabilidade = Vector3.one;
    private Vector3 posicaoAnteriorPontoEstabilidade;
    private bool posicaoAnteriorPontoValida;
    private RaycastHit[] bufferImpactosMira;
    private Material materialMiraLaserRuntime;

    public Transform PontoLancamento => pontoLancamento;
    public Transform PontoMagiaPreparada => pontoMagiaPreparada;
    public Transform PontoDirecaoMagia => pontoDirecaoMagia;
    public BolaDeFogo BolaDeFogoPreparadaAtual => bolaDeFogoPreparadaAtual;
    public bool CajadoSegurado => cajadoSegurado;
    public bool EstaDesenhando => estaDesenhando;
    public bool PreparacaoBloqueadaPorFaltaDeMana => preparacaoBloqueadaPorFaltaDeMana;
    public OrigemAtivacaoMagia UltimaOrigemAtivacaoMagia => ultimaOrigemAtivacaoMagia;
    public string StatusPreparacaoMagia => statusPreparacaoMagia;
    public string StatusManaDaMagia => statusManaDaMagia;
    public IReadOnlyList<LineRenderer> TracosDaRuna => tracosDaRuna;

    private void Awake()
    {
        EncontrarPontoLancamentoSeNecessario();
        EncontrarPontoMagiaPreparadaSeNecessario();
        EncontrarPontoDirecaoMagiaSeNecessario();
        EncontrarInteragivelXRSeNecessario();
        EncontrarEstadoInventarioSeNecessario(true);
        SincronizarEstadoInventarioAtual();
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
        RegistrarEventoInventario();
        SincronizarEstadoInventarioAtual();
        GarantirMiraLaser();
        RegistrarEventosXR();
        AtualizarEstadoSelecao();
        EsconderMiraLaser();
        AtualizarDiagnostico();
    }

    private void OnDisable()
    {
        CancelarDesenhoSemReconhecer("OnDisable");
        RemoverEventosXR();
        RemoverEventoInventario();
        cajadoSegurado = false;
        ultimoInteractor = null;
        statusPlayerDonoAtual = null;
        EsconderMiraLaser();
        AtualizarDiagnostico();
    }

    private void OnDestroy()
    {
        RemoverEventosXR();
        RemoverEventoInventario();
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
        distanciaMaximaSaltoEntrePontos = Mathf.Max(distanciaMinimaEntrePontos, distanciaMaximaSaltoEntrePontos);
        maximoPontosPorTraco = Mathf.Max(2, maximoPontosPorTraco);
        larguraTraco = Mathf.Max(0.001f, larguraTraco);
        tempoParaApagarRuna = Mathf.Max(0.1f, tempoParaApagarRuna);
        atrasoDesenhoAposSairInventario = Mathf.Max(0f, atrasoDesenhoAposSairInventario);
        framesConsecutivosPontoEstavel = Mathf.Max(1, framesConsecutivosPontoEstavel);
        distanciaMaximaPontoEstavelPorFrame = Mathf.Max(0.05f, distanciaMaximaPontoEstavelPorFrame);
        tempoMinimoAposSairInventario = Mathf.Max(0.01f, tempoMinimoAposSairInventario);
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
        AtualizarEstabilidadeDesenhoAposInventario();
        AtualizarMiraLaser();
    }

    private void RegistrarEventosXR()
    {
        EncontrarInteragivelXRSeNecessario();

        if (listenersXRRegistrados && interagivelRegistrado == interagivelXR)
            return;

        if (interagivelXR == null)
        {
            AvisarUmaVez(
                "CajadoMagico precisa de um XRBaseInteractable ou XRGrabInteractable no cajado ou na hierarquia.",
                ref avisoSemInteragivelMostrado);
            return;
        }

        RemoverEventosXR();

        interagivelXR.selectEntered.RemoveListener(AoSelecionarCajado);
        interagivelXR.selectExited.RemoveListener(AoSoltarCajado);
        interagivelXR.activated.RemoveListener(AoAtivarCajado);
        interagivelXR.deactivated.RemoveListener(AoDesativarCajado);
        interagivelXR.selectEntered.AddListener(AoSelecionarCajado);
        interagivelXR.selectExited.AddListener(AoSoltarCajado);
        interagivelXR.activated.AddListener(AoAtivarCajado);
        interagivelXR.deactivated.AddListener(AoDesativarCajado);
        interagivelRegistrado = interagivelXR;
        listenersXRRegistrados = true;
        quantidadeRegistrosActivated = 1;
        quantidadeRegistrosDeactivated = 1;
    }

    private void RemoverEventosXR()
    {
        if (interagivelRegistrado == null)
        {
            listenersXRRegistrados = false;
            quantidadeRegistrosActivated = 0;
            quantidadeRegistrosDeactivated = 0;
            return;
        }

        interagivelRegistrado.selectEntered.RemoveListener(AoSelecionarCajado);
        interagivelRegistrado.selectExited.RemoveListener(AoSoltarCajado);
        interagivelRegistrado.activated.RemoveListener(AoAtivarCajado);
        interagivelRegistrado.deactivated.RemoveListener(AoDesativarCajado);
        interagivelRegistrado = null;
        listenersXRRegistrados = false;
        quantidadeRegistrosActivated = 0;
        quantidadeRegistrosDeactivated = 0;
    }

    private void EncontrarEstadoInventarioSeNecessario(bool criarSeNecessario)
    {
        if (estadoInventario != null)
            return;

        estadoInventario = GetComponent<EstadoItemInventario>();

        if (estadoInventario == null && criarSeNecessario)
            estadoInventario = gameObject.AddComponent<EstadoItemInventario>();
    }

    private void RegistrarEventoInventario()
    {
        EncontrarEstadoInventarioSeNecessario(true);

        if (estadoInventario == null || eventoInventarioRegistrado)
            return;

        estadoInventario.EstadoInventarioAlterado -= AoEstadoInventarioAlterado;
        estadoInventario.EstadoInventarioAlterado += AoEstadoInventarioAlterado;
        eventoInventarioRegistrado = true;
    }

    private void RemoverEventoInventario()
    {
        if (estadoInventario != null && eventoInventarioRegistrado)
            estadoInventario.EstadoInventarioAlterado -= AoEstadoInventarioAlterado;

        eventoInventarioRegistrado = false;
    }

    private void SincronizarEstadoInventarioAtual()
    {
        EncontrarEstadoInventarioSeNecessario(false);
        bool estaNoInventarioAgora = estadoInventario != null && estadoInventario.estaNoInventario;

        if (estaNoInventarioAgora)
        {
            AoEntrarNoInventario();
        }
        else if (cajadoNoInventario || estadoDisponibilidadeDesenho == EstadoDisponibilidadeDesenho.NoInventario)
        {
            AoSairDoInventario();
        }
        else
        {
            cajadoNoInventario = false;
            if (!estaDesenhando)
                estadoDisponibilidadeDesenho = EstadoDisponibilidadeDesenho.Pronto;
        }
    }

    private void AoEstadoInventarioAlterado(bool estaNoInventarioAgora)
    {
        if (estaNoInventarioAgora)
            AoEntrarNoInventario();
        else
            AoSairDoInventario();
    }

    private void AoEntrarNoInventario()
    {
        cajadoNoInventario = true;
        estadoLimpoAoEntrarNoInventario = false;
        desenhoBloqueadoAte = float.PositiveInfinity;
        estadoDisponibilidadeDesenho = EstadoDisponibilidadeDesenho.NoInventario;
        aguardandoSoltarActivate = false;
        ResetarAmostraEstabilidade("Entrou no inventario.");

        CancelarDesenhoSemReconhecer("Entrou no inventario.");
        activateFoiUsadoParaLancar = false;
        cajadoSegurado = false;
        ultimoInteractor = null;
        statusPlayerDonoAtual = null;
        EsconderMiraLaser();

        estadoLimpoAoEntrarNoInventario = tracosDaRuna.Count == 0 &&
                                          pontosPorTraco.Count == 0 &&
                                          tracoAtual == null &&
                                          pontosTracoAtual == null &&
                                          !estaDesenhando;
        statusCicloInventario = estadoLimpoAoEntrarNoInventario
            ? "Cajado entrou no inventario com runa limpa."
            : "Cajado entrou no inventario, mas ainda ha estado de runa.";
        AtualizarDiagnostico();
    }

    private void AoSairDoInventario()
    {
        cajadoNoInventario = false;
        estadoLimpoAoEntrarNoInventario = false;
        desenhoBloqueadoAte = Time.time + Mathf.Max(atrasoDesenhoAposSairInventario, tempoMinimoAposSairInventario);

        CancelarDesenhoSemReconhecer("Saiu do inventario.");
        EncontrarPontoLancamentoSeNecessario();
        EncontrarPontoMagiaPreparadaSeNecessario();
        EncontrarPontoDirecaoMagiaSeNecessario();
        EncontrarInteragivelXRSeNecessario();
        RegistrarEventosXR();
        AtualizarEstadoSelecao();
        EsconderMiraLaser();
        IniciarValidacaoEstabilidadeAposInventario("Saiu do inventario.");

        statusCicloInventario = "Cajado saiu do inventario e aguarda estabilidade.";
        AtualizarDiagnostico();
    }

    private void CancelarDesenhoSemReconhecer(string motivo)
    {
        ultimoMotivoLimpeza = motivo;
        suprimirAnaliseAoFinalizar = false;
        manterRunaAposFalhaPreparacao = false;
        estaDesenhando = false;
        tracoAtual = null;
        pontosTracoAtual = null;
        CancelarTemporizadorLimpeza();
        DestruirRunaVisual();
    }

    private void RemoverTracoAtualInvalido(string motivo)
    {
        ultimoMotivoLimpeza = motivo;

        LineRenderer tracoInvalido = tracoAtual;
        int indice = tracoInvalido != null ? tracosDaRuna.IndexOf(tracoInvalido) : -1;

        if (indice >= 0)
        {
            tracosDaRuna.RemoveAt(indice);
            if (indice < pontosPorTraco.Count)
                pontosPorTraco.RemoveAt(indice);
        }

        if (tracoInvalido != null)
            Destroy(tracoInvalido.gameObject);

        tracoAtual = null;
        pontosTracoAtual = null;
        estaDesenhando = false;
        estadoDisponibilidadeDesenho = cajadoNoInventario
            ? EstadoDisponibilidadeDesenho.NoInventario
            : EstadoDisponibilidadeDesenho.Pronto;

        if (pontosPorTraco.Count > 0)
            IniciarTemporizadorLimpeza();
        else
        {
            CancelarTemporizadorLimpeza();
            DestruirRaizDesenhoSeVazia();
        }
    }

    private void CancelarTracoPorInstabilidade(string motivo)
    {
        CancelarDesenhoSemReconhecer(motivo);
        aguardandoSoltarActivate = false;
        IniciarValidacaoEstabilidadeAposInventario(motivo);
        estadoDisponibilidadeDesenho = EstadoDisponibilidadeDesenho.AguardandoEstabilidade;
        AtualizarDiagnostico();
    }

    private void DestruirRaizDesenhoSeVazia()
    {
        if (raizDesenhoRunico == null || raizDesenhoRunico.transform.childCount > 0)
            return;

        Destroy(raizDesenhoRunico);
        raizDesenhoRunico = null;
    }

    private void AoSelecionarCajado(SelectEnterEventArgs args)
    {
        if (cajadoNoInventario || !InteractorEhMaoValida(args != null ? args.interactorObject : null))
        {
            AtualizarEstadoSelecao();
            AtualizarDiagnostico();
            return;
        }

        cajadoSegurado = true;
        ultimoInteractor = ObterTransformInteractor(args != null ? args.interactorObject : null);
        AtualizarStatusPlayerDonoAtual();
        AtualizarDiagnostico();
    }

    private void AoSoltarCajado(SelectExitEventArgs args)
    {
        if (cajadoNoInventario)
        {
            CancelarDesenhoSemReconhecer("Solto pelo inventario.");
            return;
        }

        if (estaDesenhando && EventoVeioDeMaoOuSemInteractor(args != null ? args.interactorObject : null))
            FinalizarTracoAtual();

        AtualizarEstadoSelecao();
        statusPlayerDonoAtual = null;
        AtualizarDiagnostico();
    }

    private void AoAtivarCajado(ActivateEventArgs args)
    {
        if (cajadoNoInventario || !EventoVeioDeMaoOuSemInteractor(args != null ? args.interactorObject : null))
            return;

        activatedRecebidosNesteCiclo++;

        if (activateFoiUsadoParaLancar)
            return;

        Transform interactorAtivacao = ObterTransformInteractor(args != null ? args.interactorObject : null);

        if (interactorAtivacao != null)
            ultimoInteractor = interactorAtivacao;

        AtualizarStatusPlayerDonoAtual();

        if (bolaDeFogoPreparadaAtual != null)
        {
            if (!PodeUsarActivateAgora(out string motivoBloqueioMagia))
            {
                aguardandoSoltarActivate = false;
                motivoBloqueioDesenho = motivoBloqueioMagia;
                AtualizarDiagnostico();
                return;
            }

            activateFoiUsadoParaLancar = LancarBolaDeFogoPreparada();
            return;
        }

        if (!PodeIniciarDesenhoAgora(out string motivoBloqueio))
        {
            aguardandoSoltarActivate = false;
            motivoBloqueioDesenho = motivoBloqueio;
            AtualizarDiagnostico();
            return;
        }

        IniciarNovoTraco();
    }

    private void AoDesativarCajado(DeactivateEventArgs args)
    {
        if (cajadoNoInventario || !EventoVeioDeMaoOuSemInteractor(args != null ? args.interactorObject : null))
            return;

        deactivatedRecebidosNesteCiclo++;

        if (aguardandoSoltarActivate)
        {
            aguardandoSoltarActivate = false;
            estadoDisponibilidadeDesenho = EstadoDisponibilidadeDesenho.AguardandoEstabilidade;
            motivoBloqueioDesenho = "Activate residual liberado; aguardando estabilidade.";
            AtualizarDiagnostico();
            return;
        }

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

        if (!PodeIniciarDesenhoAgora(out string motivoBloqueio))
        {
            motivoBloqueioDesenho = motivoBloqueio;
            return;
        }

        if (pontoLancamento == null)
        {
            AvisarUmaVez(
                "CajadoMagico nao pode iniciar desenho porque PontoLancamento nao foi configurado.",
                ref avisoSemPontoLancamentoMostrado);
            return;
        }

        Vector3 primeiroPonto = pontoLancamento.position;
        if (!VetorFinito(primeiroPonto))
        {
            motivoBloqueioDesenho = "PontoLancamento invalido.";
            return;
        }

        CancelarTemporizadorLimpeza();
        GarantirRaizDesenhoRunico();

        tracoAtual = CriarLineRendererDoTraco();
        pontosTracoAtual = new List<Vector3>();
        tracosDaRuna.Add(tracoAtual);
        pontosPorTraco.Add(pontosTracoAtual);
        estaDesenhando = true;
        estadoDisponibilidadeDesenho = EstadoDisponibilidadeDesenho.Desenhando;
        motivoBloqueioDesenho = "Desenhando.";

        RegistrarPontoNoTracoAtual(primeiroPonto, true);
        AtualizarDiagnostico();
    }

    public void FinalizarTracoAtual()
    {
        if (!estaDesenhando)
            return;

        if (pontosTracoAtual == null || pontosTracoAtual.Count < 2)
        {
            RemoverTracoAtualInvalido("Traco com menos de dois pontos.");
            AtualizarDiagnostico();
            return;
        }

        estaDesenhando = false;
        tracoAtual = null;
        pontosTracoAtual = null;
        estadoDisponibilidadeDesenho = cajadoNoInventario
            ? EstadoDisponibilidadeDesenho.NoInventario
            : EstadoDisponibilidadeDesenho.Pronto;
        IniciarTemporizadorLimpeza();

        if (!suprimirAnaliseAoFinalizar)
            AnalisarRunaAposTracoFinalizado();

        AtualizarDiagnostico();
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
            return;

        ultimaRunaReconhecida = resultado.idRuna;

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
        if (cajadoNoInventario)
        {
            CancelarDesenhoSemReconhecer("Atualizacao bloqueada porque o cajado esta no inventario.");
            return;
        }

        AtualizarFlagsSelecaoEInventario();

        if (!cajadoSegurado)
        {
            if (inventarioEstaProcessando || socketAindaSelecionando)
                CancelarTracoPorInstabilidade("Desenho cancelado por transicao de inventario.");
            else
                FinalizarTracoAtual();
            return;
        }

        if (pontoLancamento == null || tracoAtual == null || pontosTracoAtual == null)
        {
            CancelarDesenhoSemReconhecer("Referencia de desenho invalida.");
            return;
        }

        if (!PodeContinuarDesenhoAtual(out string motivoBloqueio))
        {
            CancelarTracoPorInstabilidade(motivoBloqueio);
            return;
        }

        RegistrarPontoNoTracoAtual(pontoLancamento.position, false);
    }

    private void RegistrarPontoNoTracoAtual(Vector3 posicao, bool forcar)
    {
        if (tracoAtual == null || pontosTracoAtual == null)
            return;

        if (!VetorFinito(posicao))
            return;

        if (pontosTracoAtual.Count >= maximoPontosPorTraco)
            return;

        if (!forcar && pontosTracoAtual.Count > 0)
        {
            float distancia = Vector3.Distance(pontosTracoAtual[pontosTracoAtual.Count - 1], posicao);

            if (distancia < distanciaMinimaEntrePontos)
                return;

            if (distancia > distanciaMaximaSaltoEntrePontos)
            {
                CancelarTracoPorInstabilidade("Salto grande entre pontos; traco descartado.");
                return;
            }
        }

        int indice = pontosTracoAtual.Count;
        pontosTracoAtual.Add(posicao);
        tracoAtual.positionCount = indice + 1;
        tracoAtual.SetPosition(indice, posicao);
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
            bool preparou = TentarPrepararBolaDeFogo(OrigemAtivacaoMagia.Runa);

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

    public bool TentarPrepararBolaDeFogoPorVoz()
    {
        return TentarPrepararBolaDeFogo(OrigemAtivacaoMagia.Voz);
    }

    public bool TentarPrepararBolaDeFogo(OrigemAtivacaoMagia origem)
    {
        ultimaOrigemAtivacaoMagia = origem;
        AtualizarEstadoSelecao();

        if (!isActiveAndEnabled)
        {
            statusPreparacaoMagia = "CajadoMagico esta desativado.";
            AtualizarDiagnosticoMagiaPreparada();
            AtualizarDiagnostico();
            return false;
        }

        if (cajadoNoInventario)
        {
            statusPreparacaoMagia = "Preparacao ignorada: cajado esta no inventario.";
            AtualizarDiagnosticoMagiaPreparada();
            AtualizarDiagnostico();
            return false;
        }

        if (!cajadoSegurado)
        {
            statusPreparacaoMagia = origem == OrigemAtivacaoMagia.Voz
                ? "Comando ignorado: cajado nao esta segurado."
                : "Cajado nao esta segurado; preparacao ignorada.";
            AtualizarDiagnosticoMagiaPreparada();
            AtualizarDiagnostico();
            return false;
        }

        if (origem == OrigemAtivacaoMagia.Voz && estaDesenhando)
        {
            statusPreparacaoMagia = "Comando ignorado: jogador esta desenhando uma runa.";
            AtualizarDiagnosticoMagiaPreparada();
            AtualizarDiagnostico();
            return false;
        }

        return PrepararBolaDeFogo(origem);
    }

    private bool PrepararBolaDeFogo(OrigemAtivacaoMagia origem)
    {
        manterRunaAposFalhaPreparacao = false;
        ultimaOrigemAtivacaoMagia = origem;

        if (preparandoBolaDeFogo)
        {
            statusPreparacaoMagia = "Preparacao de Bola de Fogo ja esta em andamento.";
            AtualizarDiagnosticoManaMagia();
            AtualizarDiagnosticoMagiaPreparada();
            return false;
        }

        if (bolaDeFogoPreparadaAtual != null)
        {
            statusPreparacaoMagia = origem == OrigemAtivacaoMagia.Voz
                ? "Comando reconhecido, mas ja existe uma magia preparada."
                : "Ja existe uma Bola de Fogo preparada.";
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
                statusPreparacaoMagia = origem == OrigemAtivacaoMagia.Voz
                    ? "Comando reconhecido, mas nao ha mana suficiente."
                    : "Mana insuficiente para preparar Bola de Fogo.";
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
            statusPreparacaoMagia = origem == OrigemAtivacaoMagia.Voz
                ? "Bola de Fogo preparada por voz."
                : "Bola de Fogo preparada por runa.";
            ultimaMagiaPreparada = "Bola de Fogo";
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
            {
                traco.positionCount = 0;
                traco.enabled = false;
                traco.gameObject.SetActive(false);
                Destroy(traco.gameObject);
            }
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
            raizDesenhoRunico.SetActive(false);
            Destroy(raizDesenhoRunico);
            raizDesenhoRunico = null;
        }
    }

    private void GarantirRaizDesenhoRunico()
    {
        if (raizDesenhoRunico != null)
            return;

        raizDesenhoRunico = new GameObject($"Desenho Runico - {name} - {GetInstanceID()}");
        raizDesenhoRunico.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        raizDesenhoRunico.transform.localScale = Vector3.one;
    }

    private LineRenderer CriarLineRendererDoTraco()
    {
        indiceTracoCriado++;

        GameObject objetoTraco = new GameObject($"Traco {indiceTracoCriado}");
        objetoTraco.transform.SetParent(raizDesenhoRunico.transform, false);
        objetoTraco.transform.localPosition = Vector3.zero;
        objetoTraco.transform.localRotation = Quaternion.identity;
        objetoTraco.transform.localScale = Vector3.one;

        LineRenderer lineRenderer = objetoTraco.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;
        lineRenderer.widthMultiplier = 1f;
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

    private void IniciarValidacaoEstabilidadeAposInventario(string motivo)
    {
        horarioSaidaInventario = Time.time;
        estadoDisponibilidadeDesenho = EstadoDisponibilidadeDesenho.SaindoDoInventario;
        activatedRecebidosNesteCiclo = 0;
        deactivatedRecebidosNesteCiclo = 0;
        ResetarAmostraEstabilidade(motivo);
    }

    private void ResetarAmostraEstabilidade(string motivo)
    {
        framesEstaveisAtuais = 0;
        framesEstaveisNecessarios = Mathf.Max(1, framesConsecutivosPontoEstavel);
        distanciaMovidaPelaPontaNoUltimoFrame = 0f;
        parentMudouEstabilidade = false;
        escalaMudouEstabilidade = false;
        parentAnteriorEstabilidade = transform.parent;
        escalaAnteriorEstabilidade = transform.localScale;
        posicaoAnteriorPontoValida = pontoLancamento != null && VetorFinito(pontoLancamento.position);
        posicaoAnteriorPontoEstabilidade = posicaoAnteriorPontoValida ? pontoLancamento.position : Vector3.zero;
        motivoBloqueioDesenho = motivo;
    }

    private void AtualizarEstabilidadeDesenhoAposInventario()
    {
        CorrigirEstadoInventarioLocalSeNecessario();
        AtualizarFlagsSelecaoEInventario();

        if (cajadoNoInventario)
        {
            estadoDisponibilidadeDesenho = EstadoDisponibilidadeDesenho.NoInventario;
            podeIniciarDesenho = false;
            motivoBloqueioDesenho = "Cajado no inventario.";
            return;
        }

        if (estaDesenhando)
        {
            estadoDisponibilidadeDesenho = EstadoDisponibilidadeDesenho.Desenhando;
            podeIniciarDesenho = false;
            motivoBloqueioDesenho = "Desenhando.";
            return;
        }

        if (estadoDisponibilidadeDesenho == EstadoDisponibilidadeDesenho.Pronto &&
            !inventarioEstaProcessando &&
            !socketAindaSelecionando)
        {
            PodeIniciarDesenhoAgora(out _);
            return;
        }

        if (aguardandoSoltarActivate)
        {
            aguardandoSoltarActivate = false;
            motivoBloqueioDesenho = "Activate residual limpo automaticamente.";
        }

        estadoDisponibilidadeDesenho = EstadoDisponibilidadeDesenho.AguardandoEstabilidade;

        if (!CondicoesBasicasParaEstabilidade(out string motivoBasico))
        {
            ResetarAmostraEstabilidade(motivoBasico);
            podeIniciarDesenho = false;
            return;
        }

        bool estabilizouNesteFrame = AtualizarAmostraEstabilidadeAtual();

        if (!estabilizouNesteFrame)
        {
            podeIniciarDesenho = false;
            return;
        }

        bool tempoMinimoCumprido = Time.time - horarioSaidaInventario >= tempoMinimoAposSairInventario &&
                                   Time.time >= desenhoBloqueadoAte;

        if (!tempoMinimoCumprido)
        {
            podeIniciarDesenho = false;
            motivoBloqueioDesenho = "Aguardando tempo minimo apos inventario.";
            return;
        }

        if (framesEstaveisAtuais < framesConsecutivosPontoEstavel)
        {
            podeIniciarDesenho = false;
            motivoBloqueioDesenho = "Aguardando frames estaveis.";
            return;
        }

        estadoDisponibilidadeDesenho = EstadoDisponibilidadeDesenho.Pronto;
        podeIniciarDesenho = true;
        motivoBloqueioDesenho = "Pronto.";
    }

    private void CorrigirEstadoInventarioLocalSeNecessario()
    {
        if (!cajadoNoInventario)
            return;

        bool maoSelecionandoAgora = ObterPrimeiroInteractorMaoSelecionando() != null;
        bool socketSelecionandoAgora = ExisteSocketSelecionando();
        bool estadoExternoAindaNoInventario = estadoInventario != null &&
                                              (estadoInventario.estaNoInventario || estadoInventario.estaSendoProcessado);

        if ((!estadoExternoAindaNoInventario || maoSelecionandoAgora) && maoSelecionandoAgora && !socketSelecionandoAgora)
        {
            cajadoNoInventario = false;
            IniciarValidacaoEstabilidadeAposInventario("Estado local de inventario corrigido pela mao.");
        }
    }

    private bool AtualizarAmostraEstabilidadeAtual()
    {
        if (pontoLancamento == null || !VetorFinito(pontoLancamento.position))
        {
            ResetarAmostraEstabilidade("PontoLancamento invalido.");
            return false;
        }

        Transform parentAtual = transform.parent;
        Vector3 escalaAtual = transform.localScale;
        Vector3 posicaoAtual = pontoLancamento.position;

        parentMudouEstabilidade = parentAtual != parentAnteriorEstabilidade;
        escalaMudouEstabilidade = !VetoresQuaseIguais(escalaAtual, escalaAnteriorEstabilidade, 0.0001f);
        distanciaMovidaPelaPontaNoUltimoFrame = posicaoAnteriorPontoValida
            ? Vector3.Distance(posicaoAnteriorPontoEstabilidade, posicaoAtual)
            : 0f;

        float limiteMovimentoNormal = Mathf.Max(
            distanciaMaximaPontoEstavelPorFrame,
            distanciaMaximaSaltoEntrePontos * 0.5f);
        bool pontoMoveuMuito = !posicaoAnteriorPontoValida ||
                               distanciaMovidaPelaPontaNoUltimoFrame > limiteMovimentoNormal;

        parentAnteriorEstabilidade = parentAtual;
        escalaAnteriorEstabilidade = escalaAtual;
        posicaoAnteriorPontoEstabilidade = posicaoAtual;
        posicaoAnteriorPontoValida = true;

        if (parentMudouEstabilidade || escalaMudouEstabilidade || pontoMoveuMuito)
        {
            framesEstaveisAtuais = 0;
            if (parentMudouEstabilidade)
                motivoBloqueioDesenho = "Parent do cajado mudou.";
            else if (escalaMudouEstabilidade)
                motivoBloqueioDesenho = "Escala do cajado mudou.";
            else
                motivoBloqueioDesenho = "PontoLancamento ainda esta movendo.";
            return false;
        }

        framesEstaveisAtuais++;
        return true;
    }

    private bool CondicoesBasicasParaEstabilidade(out string motivo)
    {
        AtualizarFlagsSelecaoEInventario();

        if (inventarioEstaProcessando)
        {
            motivo = "Inventario ainda esta processando o item.";
            return false;
        }

        if (socketAindaSelecionando)
        {
            motivo = "Socket do inventario ainda seleciona o cajado.";
            return false;
        }

        if (!maoEstaSelecionando)
        {
            motivo = "Nenhuma mao valida selecionando o cajado.";
            return false;
        }

        if (pontoLancamento == null)
        {
            motivo = "PontoLancamento nao configurado.";
            return false;
        }

        motivo = string.Empty;
        return true;
    }

    private bool PodeContinuarDesenhoAtual(out string motivo)
    {
        if (cajadoNoInventario)
        {
            motivo = "Cajado voltou ao inventario durante o desenho.";
            return false;
        }

        if (!CondicoesBasicasParaEstabilidade(out motivo))
            return false;

        motivo = string.Empty;
        return true;
    }

    private bool PodeIniciarDesenhoAgora(out string motivo)
    {
        if (!usarDesenhoRunico)
        {
            motivo = "Desenho runico desativado.";
            podeIniciarDesenho = false;
            return false;
        }

        return PodeUsarActivateAgora(out motivo);
    }

    private bool PodeUsarActivateAgora(out string motivo)
    {
        if (aguardandoSoltarActivate)
        {
            aguardandoSoltarActivate = false;
        }

        if (estaDesenhando)
        {
            motivo = "Ja esta desenhando.";
            podeIniciarDesenho = false;
            return false;
        }

        if (estadoDisponibilidadeDesenho != EstadoDisponibilidadeDesenho.Pronto)
        {
            motivo = $"Estado atual: {estadoDisponibilidadeDesenho}.";
            podeIniciarDesenho = false;
            return false;
        }

        if (!CondicoesBasicasParaEstabilidade(out motivo))
        {
            podeIniciarDesenho = false;
            return false;
        }

        if (Time.time < desenhoBloqueadoAte)
        {
            motivo = "Aguardando tempo minimo apos inventario.";
            podeIniciarDesenho = false;
            return false;
        }

        motivo = "Pronto.";
        podeIniciarDesenho = true;
        return true;
    }

    private void AtualizarFlagsSelecaoEInventario()
    {
        inventarioEstaProcessando = InventarioAindaEstaProcessando();
        socketAindaSelecionando = ExisteSocketSelecionando();
        maoEstaSelecionando = ExisteMaoSelecionando();
        framesEstaveisNecessarios = Mathf.Max(1, framesConsecutivosPontoEstavel);
    }

    private bool InventarioAindaEstaProcessando()
    {
        if (estadoInventario == null)
            return false;

        bool maoSelecionandoAgora = ObterPrimeiroInteractorMaoSelecionando() != null;
        bool socketSelecionandoAgora = ExisteSocketSelecionando();

        if (estadoInventario.estaNoInventario && !maoSelecionandoAgora)
            return true;

        if (estadoInventario.estaSendoProcessado && (socketSelecionandoAgora || !maoSelecionandoAgora))
            return true;

        return false;
    }

    private bool ExisteMaoSelecionando()
    {
        return ObterPrimeiroInteractorMaoSelecionando() != null;
    }

    private bool ExisteSocketSelecionando()
    {
        if (interagivelXR == null || interagivelXR.interactorsSelecting == null)
            return false;

        for (int i = 0; i < interagivelXR.interactorsSelecting.Count; i++)
        {
            if (InteractorEhSocketOuInventario(interagivelXR.interactorsSelecting[i]))
                return true;
        }

        return false;
    }

    private static bool VetoresQuaseIguais(Vector3 a, Vector3 b, float tolerancia)
    {
        return (a - b).sqrMagnitude <= tolerancia * tolerancia;
    }

    private void AtualizarEstadoSelecao()
    {
        if (interagivelXR == null || cajadoNoInventario)
        {
            cajadoSegurado = false;
            ultimoInteractor = null;
            return;
        }

        AtualizarFlagsSelecaoEInventario();
        if (inventarioEstaProcessando || socketAindaSelecionando)
        {
            cajadoSegurado = false;
            ultimoInteractor = null;
            return;
        }

        Transform interactorMao = ObterPrimeiroInteractorMaoSelecionando();
        cajadoSegurado = interactorMao != null;

        if (!cajadoSegurado)
        {
            ultimoInteractor = null;
            return;
        }

        if (ultimoInteractor == null)
            ultimoInteractor = interactorMao;
    }

    private void AtualizarDiagnostico()
    {
        quantidadeTracos = tracosDaRuna.Count;
        quantidadePontosTracoAtual = pontosTracoAtual != null ? pontosTracoAtual.Count : 0;
        pontosDoTracoAtual = quantidadePontosTracoAtual;
        quantidadeContainersDesenho = raizDesenhoRunico != null ? 1 : 0;
        AtualizarFlagsSelecaoEInventario();
        AtualizarDiagnosticoRigidbody();
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
            $"Segurado: {cajadoSegurado} | Desenhando: {estaDesenhando} | Interactor: {nomeInteractor} | Ponto: {nomePonto} | Ponto Magia: {nomePontoMagia} | Ponto Direcao: {nomePontoDirecao} | Eixo Direcao: {eixoLocalDirecaoMagia} | Tracos: {quantidadeTracos} | Pontos Traco Atual: {quantidadePontosTracoAtual} | Limpar em: {tempoRestanteParaLimpar:0.00}s | Modo: {modoReconhecimentoRuna} | Runa: {ultimaRunaReconhecida} | Score: {pontuacaoUltimaAnalise:0.00} | Motivo: {motivoUltimaAnalise} | Magia Preparada: {ultimaMagiaPreparada} | Magia Lancada: {ultimaMagiaLancada} | Origem: {ultimaOrigemAtivacaoMagia} | Direcao Lancamento: {ultimaDirecaoLancamento} | Preparacao: {statusPreparacaoMagia} | Lancamento: {statusLancamentoMagia} | Direcao: {statusDirecaoLancamento}";
        statusDiagnostico += $" | Mana: {manaAtualDoDono}/{manaNecessariaBolaDeFogo} | Status Mana: {statusManaDaMagia}";
        statusDiagnostico += $" | Mira: {miraLaserVisivel} | Dist Mira: {distanciaAtualMira:0.00} | Status Mira: {statusMiraLaser}";
        statusDiagnostico += $" | Inventario: {cajadoNoInventario} | Limpo Inventario: {estadoLimpoAoEntrarNoInventario} | Listeners XR: {listenersXRRegistrados} | Activated Reg: {quantidadeRegistrosActivated} | Deactivated Reg: {quantidadeRegistrosDeactivated} | Containers: {quantidadeContainersDesenho} | Ultima Limpeza: {ultimoMotivoLimpeza} | Ciclo Inventario: {statusCicloInventario} | Rigidbody: {rigidbodyPresente} | Kinematic: {rigidbodyCinematico} | Gravity: {rigidbodyUsandoGravidade} | Collisions: {rigidbodyDetectandoColisoes}";
        statusDiagnostico += $" | Estado Desenho: {estadoDisponibilidadeDesenho} | Processando: {inventarioEstaProcessando} | Socket: {socketAindaSelecionando} | Mao: {maoEstaSelecionando} | Aguardando Soltar: {aguardandoSoltarActivate} | Frames Estaveis: {framesEstaveisAtuais}/{framesEstaveisNecessarios} | Dist Ponta: {distanciaMovidaPelaPontaNoUltimoFrame:0.000} | Parent Mudou: {parentMudouEstabilidade} | Escala Mudou: {escalaMudouEstabilidade} | Pode Desenhar: {podeIniciarDesenho} | Activated Ciclo: {activatedRecebidosNesteCiclo} | Deactivated Ciclo: {deactivatedRecebidosNesteCiclo} | Bloqueio: {motivoBloqueioDesenho}";
    }

    private void AtualizarDiagnosticoRigidbody()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rigidbodyPresente = rb != null;
        rigidbodyCinematico = rb != null && rb.isKinematic;
        rigidbodyUsandoGravidade = rb != null && rb.useGravity;
        rigidbodyDetectandoColisoes = rb != null && rb.detectCollisions;
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

    private bool InteractorEhMaoValida(IXRInteractor interactor)
    {
        if (interactor == null || InteractorEhSocketOuInventario(interactor))
            return false;

        if (interactor is XRDirectInteractor || interactor is XRRayInteractor)
            return true;

        Transform interactorTransform = ObterTransformInteractor(interactor);
        while (interactorTransform != null)
        {
            string nome = NormalizarNome(interactorTransform.name);
            if (nome.Contains("left") ||
                nome.Contains("right") ||
                nome.Contains("esquerda") ||
                nome.Contains("direita") ||
                nome.Contains("hand") ||
                nome.Contains("mao") ||
                nome.Contains("controller") ||
                nome.Contains("controlador"))
                return true;

            interactorTransform = interactorTransform.parent;
        }

        return false;
    }

    private bool EventoVeioDeMaoOuSemInteractor(IXRInteractor interactor)
    {
        return interactor == null || InteractorEhMaoValida(interactor);
    }

    private bool InteractorEhSocketOuInventario(IXRInteractor interactor)
    {
        if (interactor == null)
            return false;

        if (interactor is XRSocketInteractor)
            return true;

        Transform atual = ObterTransformInteractor(interactor);
        while (atual != null)
        {
            string nome = NormalizarNome(atual.name);
            if (nome.Contains("socket") ||
                nome.Contains("slot") ||
                nome.Contains("inventario") ||
                nome.Contains("inventory") ||
                nome.Contains("pontoencaixe") ||
                nome.Contains("ponto_encaixe"))
                return true;

            atual = atual.parent;
        }

        return false;
    }

    private Transform ObterPrimeiroInteractorMaoSelecionando()
    {
        if (interagivelXR == null || interagivelXR.interactorsSelecting == null || interagivelXR.interactorsSelecting.Count == 0)
            return null;

        for (int i = 0; i < interagivelXR.interactorsSelecting.Count; i++)
        {
            IXRInteractor interactor = interagivelXR.interactorsSelecting[i];
            if (InteractorEhMaoValida(interactor))
                return ObterTransformInteractor(interactor);
        }

        return null;
    }

    private static Transform ObterTransformInteractor(IXRInteractor interactor)
    {
        return interactor is Component componente ? componente.transform : null;
    }

    private GameObject ObterDonoDaMagia()
    {
        Transform interactor = ultimoInteractor != null ? ultimoInteractor : ObterPrimeiroInteractorMaoSelecionando();

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

        Transform interactor = ultimoInteractor != null ? ultimoInteractor : ObterPrimeiroInteractorMaoSelecionando();
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

    private static bool VetorFinito(Vector3 valor)
    {
        return !float.IsNaN(valor.x) && !float.IsInfinity(valor.x) &&
               !float.IsNaN(valor.y) && !float.IsInfinity(valor.y) &&
               !float.IsNaN(valor.z) && !float.IsInfinity(valor.z);
    }

    private static string NormalizarNome(string nome)
    {
        return string.IsNullOrWhiteSpace(nome)
            ? string.Empty
            : nome.Trim().ToLowerInvariant();
    }

    private void AvisarUmaVez(string mensagem, ref bool avisoJaMostrado)
    {
        if (avisoJaMostrado)
            return;

        avisoJaMostrado = true;
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
