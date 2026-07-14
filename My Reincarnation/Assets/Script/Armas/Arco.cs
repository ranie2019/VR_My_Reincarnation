using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class Arco : MonoBehaviour
{
    private enum TipoMiraArco
    {
        Nenhuma,
        PontoImpacto,
        TrajetoriaCurva
    }

    [Serializable]
    private class FlechaPrefabConfigurada
    {
        public string idTipoFlecha;
        public GameObject prefabFlecha;
    }

    [Header("Attach Duas Maos")]
    [SerializeField] private ArmaAttachDuasMao attachDuasMao;

    [Header("Modo Inventario")]
    [SerializeField] private bool estaNoInventario;
    [SerializeField] private bool detectarModoInventarioPorParent = true;

    [Header("Visual Seguro de Inventario")]
    [SerializeField] private bool usarVisualDedicadoNoInventario = true;
    [SerializeField] private GameObject visualDedicadoInventario;
    [SerializeField] private bool esconderVisualFuncionalNoInventario = false;

    [Header("Visual Principal do Arco - Nunca Ocultar")]
    [SerializeField] private Renderer[] renderersPrincipaisDoArco;
    [SerializeField] private GameObject[] objetosPrincipaisDoArco;

    [Header("Efeitos que Podem Sumir no Inventario")]
    [SerializeField] private LineRenderer[] linhasParaOcultarNoInventario;
    [SerializeField] private GameObject[] objetosEfeitoParaOcultarNoInventario;
    [SerializeField] private Renderer[] renderersEfeitoParaOcultarNoInventario;

    [Header("Corda no Inventario")]
    [SerializeField] private bool ocultarCordaRealNoInventario = false;
    [SerializeField] private Renderer[] renderersCordaReal;
    [SerializeField] private Transform[] pontosOuBonesCorda;
    [SerializeField] private bool resetarCordaAoEntrarNoInventario = true;

    [Header("Diagnostico Inventario Arco")]
    [SerializeField] private bool diagnosticoEstaNoInventario;
    [SerializeField] private int diagnosticoQuantidadeRenderersPrincipais;
    [SerializeField] private bool diagnosticoRenderPrincipalVisivel;
    [SerializeField] private bool diagnosticoTodosRenderersPrincipaisVisiveis;
    [SerializeField] private bool diagnosticoCordaRealVisivel;
    [SerializeField] private bool diagnosticoLinhaAzulVisivel;
    [SerializeField] private bool diagnosticoVisualDedicadoInventarioAtivo;
    [SerializeField] private bool diagnosticoMiraVisivel;
    [SerializeField] private bool diagnosticoFlechaPreparadaExiste;
    [SerializeField] private bool diagnosticoAreaCordaAtiva;

    [Header("Pontos da Corda")]
    [SerializeField] private Transform pontoCordaTopo;
    [SerializeField] private Transform pontoCordaRepouso;
    [SerializeField] private Transform pontoCordaAtual;
    [SerializeField] private Transform pontoCordaBaixo;

    [Header("Corda Real com Bones")]
    [SerializeField] private bool usarCordaRealComBones = true;
    [SerializeField] private Transform boneCordaTopo;
    [SerializeField] private Transform boneCordaMeio;
    [SerializeField] private Transform boneCordaBaixo;
    [SerializeField] private Transform pontoRepousoCordaMeio;
    [SerializeField] private bool esconderLineRendererCorda = true;
    [SerializeField] private bool mostrarLinhaDebugCorda = false;

    [Header("Flecha")]
    [SerializeField] private GameObject prefabFlecha;
    [SerializeField, HideInInspector] private Transform pontoFlecha;
    [SerializeField] private Transform pontoDirecaoDisparo;
    [SerializeField] private bool criarFlechaAutomaticamente = true;

    [Header("Municao / Flechas")]
    [SerializeField] private InventarioFlechas inventarioFlechas;
    [SerializeField] private string idTipoFlechaEquipada;
    [SerializeField] private GameObject prefabFlechaEquipada;
    [SerializeField] private bool consumirFlechaDoInventario = true;
    [SerializeField] private bool permitirDisparoSemFlechaParaTeste;
    [SerializeField] private List<FlechaPrefabConfigurada> prefabsFlechasDisponiveis = new List<FlechaPrefabConfigurada>();

    [Header("UI Flechas")]
    [SerializeField] private SelecionadorFlechasUI selecionadorFlechasUI;
    [SerializeField] private InputActionReference acaoAbrirSeletorFlechas;
    [SerializeField] private bool abrirSeletorSomenteSegurandoArco = true;

    [Header("Diagnostico Flechas")]
    [SerializeField] private string diagnosticoFlechaEquipada;
    [SerializeField] private int diagnosticoQuantidadeFlechaEquipada;
    [SerializeField] private bool diagnosticoTemFlechaEquipada;
    [SerializeField] private int diagnosticoRenderersFlechaDisparo;
    [SerializeField] private bool diagnosticoMalhaFlechaDisparoVisivel;

    [Header("Pose da Flecha no Arco")]
    [SerializeField] private bool usarOffsetFlechaNoArco = true;
    [SerializeField] private Transform pontoVisualFlechaNoArco;
    [SerializeField] private Vector3 offsetLocalPosicaoFlechaNoArco = Vector3.zero;
    [SerializeField] private Vector3 offsetLocalRotacaoFlechaNoArco = new Vector3(90f, 0f, 0f);

    [Header("Pontos de Disparo por Mao")]
    [SerializeField] private bool usarPontosDisparoPorMao = true;
    [SerializeField] private Transform pontoFlechaDireita;
    [SerializeField] private Transform pontoDirecaoDisparoDireita;
    [SerializeField] private Transform pontoFlechaEsquerda;
    [SerializeField] private Transform pontoDirecaoDisparoEsquerda;

    [Header("Mira do Arco")]
    [SerializeField] private bool usarMiraArco = true;
    [SerializeField] private TipoMiraArco tipoMiraArco = TipoMiraArco.TrajetoriaCurva;
    [SerializeField] private LineRenderer linhaMiraTrajetoria;
    [SerializeField] private Transform pontoVisualImpacto;
    [SerializeField] private int quantidadePontosMira = 24;
    [SerializeField] private float intervaloTempoMira = 0.05f;
    [SerializeField] private float distanciaMaximaMira = 15f;
    [SerializeField] private float larguraLinhaMira = 0.01f;
    [SerializeField] private Color corLinhaMira = new Color(0f, 1f, 1f, 0.9f);
    [SerializeField] private LayerMask camadasColisaoMira = ~0;
    [SerializeField] private bool mostrarMiraSomenteComPuxadaMinima = true;
    [SerializeField] private float percentualMinimoParaMostrarMira = 0.1f;
    [SerializeField] private bool ocultarMiraAoSoltar = true;
    [SerializeField] private bool criarMiraAutomaticamente = true;
    [SerializeField] private float multiplicadorVelocidadeMira = 1f;

    [Header("Diagnostico Mira do Arco")]
    [SerializeField] private bool diagnosticoMiraAtiva;
    [SerializeField] private int diagnosticoQuantidadePontosMira;
    [SerializeField] private float diagnosticoPercentualPuxadaMira;
    [SerializeField] private bool diagnosticoLinhaMiraCriada;
    [SerializeField] private bool diagnosticoDirecaoMiraValida;
    [SerializeField] private bool diagnosticoDeveMostrarMira;
    [SerializeField] private Transform diagnosticoPontoFlechaAtual;
    [SerializeField] private Transform diagnosticoPontoDirecaoAtual;
    [SerializeField] private bool diagnosticoSeguradoPelaDireita;
    [SerializeField] private bool diagnosticoSeguradoPelaEsquerda;

    [Header("Puxada e Tensao")]
    [SerializeField] private float distanciaMaximaPuxada = 0.45f;
    [SerializeField] private float distanciaMinimaParaDisparo = 0.08f;
    [SerializeField] private float percentualParaAcumularEnergia = 0.95f;
    [SerializeField] private float multiplicadorDistanciaSeguranca = 2f;

    [Header("Energia do Disparo")]
    [SerializeField] private float forcaMinimaDisparo = 5f;
    [SerializeField] private float forcaMaximaDisparo = 35f;
    [SerializeField] private float energiaExtraMaxima = 25f;
    [SerializeField] private float velocidadeAcumuloEnergia = 10f;
    [SerializeField] private float multiplicadorDanoEnergia = 1f;

    [Header("Curvatura Visual do Arco")]
    [SerializeField] private bool usarCurvaturaVisual = true;

    [SerializeField] private Transform arcoCima1;
    [SerializeField] private Transform arcoCima2;
    [SerializeField] private Transform arcoCima3;

    [SerializeField] private Transform arcoBaixo1;
    [SerializeField] private Transform arcoBaixo2;
    [SerializeField] private Transform arcoBaixo3;

    [SerializeField] private Vector3 eixoLocalCurvatura = Vector3.forward;

    [SerializeField] private float anguloCima1 = 3f;
    [SerializeField] private float anguloCima2 = 6f;
    [SerializeField] private float anguloCima3 = 9f;

    [SerializeField] private float anguloBaixo1 = -3f;
    [SerializeField] private float anguloBaixo2 = -6f;
    [SerializeField] private float anguloBaixo3 = -9f;

    [SerializeField] private bool inverterCurvatura = false;
    [SerializeField] private float suavidadeCurvatura = 20f;

    [Header("Corda Visual")]
    [SerializeField] private LineRenderer linhaCorda;
    [SerializeField] private bool usarLinhaCordaProcedural = true;
    [SerializeField] private float larguraLinhaCorda = 0.012f;
    [SerializeField] private bool ocultarMalhaCordaOriginalQuandoUsarLinha = true;

    [Header("Corda Azul / Corda Visual")]
    [SerializeField] private LineRenderer linhaCordaVisual;
    [SerializeField] private bool controlarLinhaCordaVisual = true;
    [SerializeField] private bool ocultarLinhaCordaVisualNoInventario = false;
    [SerializeField] private bool manterLinhaCordaVisualSempreVisivel = true;

    [Header("Diagnostico Corda Visual")]
    [SerializeField] private bool diagnosticoLinhaCordaVisualExiste;
    [SerializeField] private bool diagnosticoLinhaCordaVisualVisivel;
    [SerializeField] private bool diagnosticoLinhaCordaVisualOcultaPorInventario;

    [Header("Deteccao")]
    [SerializeField] private Collider areaPuxarCorda;

    [Header("Entrada VR da Corda")]
    [SerializeField] private bool exigirBotaoParaPuxarCorda = true;
    [SerializeField] private InputActionReference acaoGripMaoEsquerda;
    [SerializeField] private InputActionReference acaoGripMaoDireita;
    [SerializeField] private float valorMinimoGripParaSegurar = 0.5f;
    [SerializeField] private bool permitirFallbackTriggerSemInputConfigurado = false;

    [Header("Durabilidade do Arco")]
    [SerializeField] private float durabilidadeMaxima = 100f;
    [SerializeField] private float durabilidadeAtual = 100f;
    [SerializeField] private TMP_Text textoValorAtual;
    [SerializeField] private TMP_Text textoValorTotal;
    [SerializeField] private bool destruirAoQuebrar = false;
    [SerializeField] private bool desativarAoQuebrar = true;
    [SerializeField] private float desgastePorDisparo = 1f;

    [Header("Texto Durabilidade")]
    [SerializeField] private Color corTextoA = Color.white;
    [SerializeField] private Color corTextoB = Color.cyan;
    [SerializeField] private float velocidadePiscarTexto = 2f;
    [SerializeField] private bool usarEfeitoLedTexto = true;

    [Header("Dano Recebido pelo Arco")]
    [SerializeField] private string[] tagsQueDanificamArco;

    [Header("Audio")]
    [SerializeField] private AudioClip somPuxarCorda;
    [SerializeField] private AudioClip somSoltarFlecha;
    [SerializeField] private AudioSource audioSource;

    private bool cordaSendoPuxada;
    private bool disparoJaProcessado;
    private bool gripPuxarPressionado;
    private bool maoCandidataDentroDaArea;
    private bool cordaSeguradaPeloGrip;
    private Transform maoCandidataPuxar;
    private Transform maoPuxando;
    private GameObject flechaPreparada;

    private float distanciaPuxadaAtual;
    private float percentualPuxada;
    private float energiaAcumulada;
    private bool arcoQuebrado;
    private Transform raizTextoDurabilidade;
    private static readonly CultureInfo CulturaDurabilidade = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] NomesMembrosDano =
    {
        "dano",
        "danoArma",
        "danoEspada",
        "danoMachado",
        "danoPicareta",
        "danoAtaque",
        "danoDaArma",
        "damage",
        "Dano",
        "DanoArma",
        "DanoAtaque",
        "Damage"
    };

    private Quaternion rotacaoBaseCima1;
    private Quaternion rotacaoBaseCima2;
    private Quaternion rotacaoBaseCima3;

    private Quaternion rotacaoBaseBaixo1;
    private Quaternion rotacaoBaseBaixo2;
    private Quaternion rotacaoBaseBaixo3;

    private float tensaoVisualAtual;
    private Vector3 posicaoLocalBaseBoneCordaTopo;
    private Vector3 posicaoLocalBaseBoneCordaBaixo;
    private Quaternion rotacaoLocalBaseBoneCordaTopo;
    private Quaternion rotacaoLocalBaseBoneCordaMeio;
    private Quaternion rotacaoLocalBaseBoneCordaBaixo;
    private bool basesCordaRealCapturadas;
    private bool visualModoInventarioAplicado;
    private bool visualPrincipalProtegidoCapturado;
    private readonly List<Renderer> renderersPrincipaisProtegidos = new List<Renderer>();
    private readonly List<GameObject> objetosPrincipaisProtegidos = new List<GameObject>();
    private readonly Vector3[] pontosCalculadosMira = new Vector3[64];
    private readonly RaycastHit[] resultadosRaycastMira = new RaycastHit[16];

    public float DurabilidadeAtual => durabilidadeAtual;
    public float DurabilidadeMaxima => durabilidadeMaxima;
    public bool Quebrado => arcoQuebrado || durabilidadeAtual <= 0f;

    private void Awake()
    {
        InvalidarCapturaVisualPrincipalProtegido();

        if (attachDuasMao == null)
            attachDuasMao = GetComponent<ArmaAttachDuasMao>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        EncontrarInventarioFlechasSeNecessario();
        EncontrarSelecionadorFlechasSeNecessario();

        if (areaPuxarCorda == null)
            areaPuxarCorda = EncontrarAreaPuxarCorda();

        EncontrarPontosFlechaPorMaoSeNecessario();

        if (areaPuxarCorda != null)
            areaPuxarCorda.isTrigger = true;

        NormalizarValores();
        EncontrarTextosDurabilidadeSeNecessario(true);
        AtualizarTextoDurabilidade(true);
        AplicarCorTexto(corTextoA);
        ConfigurarRepassadorTriggerCorda();
        ConfigurarCordaRealComBonesSeNecessario();
        ConfigurarVisualizacaoLinhaCorda();
        GarantirMiraCriada();
        GarantirConfiguracaoVisualPrincipal();
        CapturarRotacoesBaseCurvatura();
        CapturarBaseCordaReal();
        ResetarCorda();
        AtualizarLinhaCorda();

        if (estaNoInventario)
            DefinirModoInventario(true);
        else
            SincronizarModoInventarioPorParent();

        AtualizarDiagnosticoFlechaEquipada();
    }

    private void OnValidate()
    {
        InvalidarCapturaVisualPrincipalProtegido();
        NormalizarValores();
        GarantirConfiguracaoVisualPrincipal();
        AtualizarTextoDurabilidade(false);
    }

    private void Update()
    {
        SincronizarModoInventarioPorParent();
        AtualizarEntradaSeletorFlechas();
        AtualizarDiagnosticoFlechaEquipada();

        if (estaNoInventario)
        {
            GarantirEstadoVisualInventario();
            return;
        }

        AtualizarEntradaPuxada();

        if (!cordaSendoPuxada)
            return;

        AtualizarPuxada();
    }

    private void LateUpdate()
    {
        if (estaNoInventario)
        {
            GarantirEstadoVisualInventario();
            return;
        }

        if (!cordaSendoPuxada)
        {
            GarantirCordaNoRepousoSeNaoPuxando();
            EsconderMiraArco();
        }
        else
        {
            AtualizarMiraArco();
        }

        AtualizarLinhaCorda();
        RotacionarTextoParaCamera();
        AtualizarEfeitoLedTexto();
    }

    private void OnDisable()
    {
        maoCandidataPuxar = null;
        maoCandidataDentroDaArea = false;
        cordaSeguradaPeloGrip = false;
        CancelarPuxada();
    }

    public void DefinirModoInventario(bool noInventario)
    {
        estaNoInventario = noInventario;
        diagnosticoEstaNoInventario = estaNoInventario;

        if (estaNoInventario)
        {
            CancelarPuxadaSemDisparar();
            AplicarVisualModoInventario(true);

            if (areaPuxarCorda != null)
                areaPuxarCorda.enabled = false;

            AtualizarDiagnosticoInventarioArco();
            return;
        }

        visualModoInventarioAplicado = false;
        CancelarPuxadaSemDisparar();
        AplicarVisualModoInventario(false);

        if (areaPuxarCorda != null)
        {
            areaPuxarCorda.enabled = !Quebrado;
            areaPuxarCorda.isTrigger = true;
        }

        EsconderMiraArco();
        AtualizarDiagnosticoInventarioArco();
    }

    private void LimparEstadoInteracaoArco()
    {
        CancelarPuxadaSemDisparar();
    }

    private void CancelarPuxadaSemDisparar()
    {
        LimparFlechaPreparada(true);
        EsconderMiraArco();

        cordaSendoPuxada = false;
        cordaSeguradaPeloGrip = false;
        maoPuxando = null;
        maoCandidataPuxar = null;
        maoCandidataDentroDaArea = false;
        gripPuxarPressionado = false;
        percentualPuxada = 0f;
        distanciaPuxadaAtual = 0f;
        energiaAcumulada = 0f;
        disparoJaProcessado = false;

        ResetarCordaParaRepousoImediato();
        EsconderMiraArco();
    }

    private void GarantirEstadoVisualInventario()
    {
        if (flechaPreparada != null || cordaSendoPuxada || cordaSeguradaPeloGrip || percentualPuxada > 0f || energiaAcumulada > 0f)
            CancelarPuxadaSemDisparar();
        else
            EsconderMiraArco();

        if (!visualModoInventarioAplicado || !diagnosticoTodosRenderersPrincipaisVisiveis)
            AplicarVisualModoInventario(true);
        else
        {
            AplicarLinhaCordaVisualModoInventario(true);
            ForcarVisualPrincipalDoArcoVisivel();
        }

        if (areaPuxarCorda != null && areaPuxarCorda.enabled)
            areaPuxarCorda.enabled = false;

        AtualizarDiagnosticoInventarioArco();
    }

    private void AplicarVisualModoInventario(bool noInventario)
    {
        GarantirConfiguracaoVisualPrincipal();

        if (resetarCordaAoEntrarNoInventario || !noInventario)
            ResetarCordaParaRepousoImediato();

        AplicarEfeitosInventario(false);
        AplicarLinhasInventario(false);
        AplicarRenderersCordaReal(!noInventario || !ocultarCordaRealNoInventario);
        AplicarLinhaCordaVisualModoInventario(noInventario);

        if (!noInventario)
            EsconderMiraArco();

        ForcarVisualPrincipalDoArcoVisivel();
        visualModoInventarioAplicado = noInventario;
        AtualizarDiagnosticoInventarioArco();
    }

    private void GarantirVisualPrincipalDoArcoVisivel()
    {
        ForcarVisualPrincipalDoArcoVisivel();
    }

    private void ForcarVisualPrincipalDoArcoVisivel()
    {
        CapturarVisualPrincipalProtegidoSeNecessario();
        AplicarVisualDedicadoInventarioSeNecessario();

        bool podeMostrarVisualFuncional = !estaNoInventario ||
                                          visualDedicadoInventario == null ||
                                          !usarVisualDedicadoNoInventario ||
                                          !esconderVisualFuncionalNoInventario;

        if (!podeMostrarVisualFuncional)
        {
            AplicarRenderersPrincipaisFuncionais(false);
            return;
        }

        if (objetosPrincipaisProtegidos != null)
        {
            for (int i = 0; i < objetosPrincipaisProtegidos.Count; i++)
            {
                if (objetosPrincipaisProtegidos[i] != null)
                    objetosPrincipaisProtegidos[i].SetActive(true);
            }
        }

        AplicarRenderersPrincipaisFuncionais(true);
    }

    private void AplicarRenderersPrincipaisFuncionais(bool visivel)
    {
        for (int i = 0; i < renderersPrincipaisProtegidos.Count; i++)
        {
            Renderer renderer = renderersPrincipaisProtegidos[i];
            if (renderer == null)
                continue;

            AtivarHierarquiaVisualPrincipal(renderer.transform);
            renderer.enabled = visivel;
        }
    }

    private void AplicarVisualDedicadoInventarioSeNecessario()
    {
        diagnosticoVisualDedicadoInventarioAtivo = false;

        if (!usarVisualDedicadoNoInventario || visualDedicadoInventario == null)
            return;

        if (estaNoInventario && !visualDedicadoInventario.activeSelf)
            visualDedicadoInventario.SetActive(true);

        if (!estaNoInventario)
            return;

        Renderer[] renderers = visualDedicadoInventario.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = true;
        }

        diagnosticoVisualDedicadoInventarioAtivo = visualDedicadoInventario.activeInHierarchy;
    }

    private void AtivarHierarquiaVisualPrincipal(Transform alvo)
    {
        Transform atual = alvo;
        while (atual != null)
        {
            atual.gameObject.SetActive(true);

            if (atual == transform)
                break;

            atual = atual.parent;
        }
    }

    private void GarantirConfiguracaoVisualPrincipal()
    {
        if (linhaCordaVisual == null && linhaCorda != null)
            linhaCordaVisual = linhaCorda;

        CapturarVisualPrincipalProtegidoSeNecessario();
    }

    private void PreencherRenderersPrincipaisAutomaticamenteSeNecessario()
    {
        CapturarVisualPrincipalProtegidoSeNecessario();
    }

    private void CapturarVisualPrincipalProtegidoSeNecessario()
    {
        if (visualPrincipalProtegidoCapturado)
            return;

        renderersPrincipaisProtegidos.Clear();
        objetosPrincipaisProtegidos.Clear();

        AdicionarObjetosPrincipaisProtegidos(objetosPrincipaisDoArco);
        AdicionarRenderersPrincipaisProtegidos(renderersPrincipaisDoArco);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (RendererEhVisualPrincipalPorEstadoInicial(renderer))
                AdicionarRendererPrincipalProtegido(renderer);
        }

        if (renderersPrincipaisProtegidos.Count > 0)
            renderersPrincipaisDoArco = renderersPrincipaisProtegidos.ToArray();

        if (objetosPrincipaisProtegidos.Count > 0)
            objetosPrincipaisDoArco = objetosPrincipaisProtegidos.ToArray();

        visualPrincipalProtegidoCapturado = true;
    }

    private void InvalidarCapturaVisualPrincipalProtegido()
    {
        visualPrincipalProtegidoCapturado = false;
        renderersPrincipaisProtegidos.Clear();
        objetosPrincipaisProtegidos.Clear();
    }

    private void AdicionarRenderersPrincipaisProtegidos(Renderer[] renderers)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
            AdicionarRendererPrincipalProtegido(renderers[i]);
    }

    private void AdicionarRendererPrincipalProtegido(Renderer renderer)
    {
        if (renderer == null || renderersPrincipaisProtegidos.Contains(renderer))
            return;

        renderersPrincipaisProtegidos.Add(renderer);
        AdicionarObjetoPrincipalProtegido(renderer.gameObject);
    }

    private void AdicionarObjetosPrincipaisProtegidos(GameObject[] objetos)
    {
        if (objetos == null)
            return;

        for (int i = 0; i < objetos.Length; i++)
            AdicionarObjetoPrincipalProtegido(objetos[i]);
    }

    private void AdicionarObjetoPrincipalProtegido(GameObject objeto)
    {
        if (objeto == null || objetosPrincipaisProtegidos.Contains(objeto))
            return;

        objetosPrincipaisProtegidos.Add(objeto);
    }

    private bool RendererEhVisualPrincipalPorEstadoInicial(Renderer renderer)
    {
        if (renderer == null || renderer is LineRenderer || !renderer.transform.IsChildOf(transform))
            return false;

        if (!renderer.enabled || !renderer.gameObject.activeSelf)
            return false;

        if (renderer == linhaMiraTrajetoria || renderer == linhaCorda || renderer == linhaCordaVisual)
            return false;

        if (visualDedicadoInventario != null && renderer.transform.IsChildOf(visualDedicadoInventario.transform))
            return false;

        if (RendererEstaEmLista(renderer, renderersEfeitoParaOcultarNoInventario))
            return false;

        if (ocultarCordaRealNoInventario && RendererEstaEmLista(renderer, renderersCordaReal))
            return false;

        if (pontoVisualImpacto != null && renderer.transform.IsChildOf(pontoVisualImpacto))
            return false;

        if (flechaPreparada != null && renderer.transform.IsChildOf(flechaPreparada.transform))
            return false;

        return true;
    }

    private static bool RendererEstaEmLista(Renderer renderer, Renderer[] lista)
    {
        if (renderer == null || lista == null)
            return false;

        for (int i = 0; i < lista.Length; i++)
        {
            if (lista[i] == renderer)
                return true;
        }

        return false;
    }

    private bool RendererPertenceACordaReal(Renderer renderer)
    {
        if (renderer == null)
            return false;

        if (renderersCordaReal != null)
        {
            for (int i = 0; i < renderersCordaReal.Length; i++)
            {
                if (renderersCordaReal[i] == renderer)
                    return true;
            }
        }

        Transform rendererTransform = renderer.transform;

        if (pontosOuBonesCorda != null)
        {
            for (int i = 0; i < pontosOuBonesCorda.Length; i++)
            {
                Transform ponto = pontosOuBonesCorda[i];
                if (ponto != null && (rendererTransform == ponto || rendererTransform.IsChildOf(ponto)))
                    return true;
            }
        }

        if (RendererEstaEmTransform(renderer, boneCordaTopo) ||
            RendererEstaEmTransform(renderer, boneCordaMeio) ||
            RendererEstaEmTransform(renderer, boneCordaBaixo) ||
            RendererEstaEmTransform(renderer, pontoCordaTopo) ||
            RendererEstaEmTransform(renderer, pontoCordaAtual) ||
            RendererEstaEmTransform(renderer, pontoCordaBaixo))
        {
            return true;
        }

        return NomeEhCordaReal(renderer.name) || NomeEhCordaReal(rendererTransform.name);
    }

    private static bool RendererEstaEmTransform(Renderer renderer, Transform alvo)
    {
        if (renderer == null || alvo == null)
            return false;

        Transform rendererTransform = renderer.transform;
        return rendererTransform == alvo || rendererTransform.IsChildOf(alvo);
    }

    private static bool NomeEhCordaReal(string nome)
    {
        string normalizado = NormalizarNomeCompleto(nome);

        if (string.IsNullOrWhiteSpace(normalizado))
            return false;

        return normalizado == "corda" ||
               normalizado == "cordabaixo" ||
               normalizado == "cordacima" ||
               normalizado == "cordameio" ||
               normalizado.StartsWith("cordatrava") ||
               normalizado.StartsWith("cordareal");
    }

    private void AplicarLinhasInventario(bool visivel)
    {
        AplicarLineRenderer(linhaMiraTrajetoria, visivel);

        if (linhasParaOcultarNoInventario == null)
            return;

        LineRenderer cordaVisual = ObterLinhaCordaVisual();
        for (int i = 0; i < linhasParaOcultarNoInventario.Length; i++)
        {
            LineRenderer linha = linhasParaOcultarNoInventario[i];
            if (linha == null || linha == cordaVisual || linha == linhaCorda)
                continue;

            AplicarLineRenderer(linha, visivel);
        }
    }

    private void AplicarLinhaCordaVisualModoInventario(bool noInventario)
    {
        if (!controlarLinhaCordaVisual)
            return;

        LineRenderer cordaVisual = ObterLinhaCordaVisual();
        diagnosticoLinhaCordaVisualExiste = cordaVisual != null;

        if (cordaVisual == null)
            return;

        bool deveFicarVisivel = manterLinhaCordaVisualSempreVisivel ||
                                !noInventario ||
                                !ocultarLinhaCordaVisualNoInventario;

        cordaVisual.enabled = deveFicarVisivel;
        diagnosticoLinhaCordaVisualOcultaPorInventario = noInventario && !deveFicarVisivel;

        if (deveFicarVisivel)
        {
            GarantirLinhaCordaVisualComPontos(cordaVisual);
            AtualizarLinhaCorda();
        }
        else
        {
            cordaVisual.positionCount = 0;
        }
    }

    private LineRenderer ObterLinhaCordaVisual()
    {
        if (linhaCordaVisual != null)
            return linhaCordaVisual;

        linhaCordaVisual = linhaCorda;
        return linhaCordaVisual;
    }

    private void GarantirLinhaCordaVisualComPontos(LineRenderer linha)
    {
        if (linha == null)
            return;

        linha.widthMultiplier = larguraLinhaCorda;
        DefinirPontosLocaisLinhaCorda(linha);
    }

    private static void AplicarLineRenderer(LineRenderer linha, bool visivel)
    {
        if (linha == null)
            return;

        linha.enabled = visivel;
        if (!visivel)
            linha.positionCount = 0;
    }

    private void AplicarEfeitosInventario(bool visivel)
    {
        // IMPORTANTE: nao esconder renderers principais do arco aqui.
        // Modo inventario deve esconder apenas efeitos, mira e flecha.
        if (pontoVisualImpacto != null && !GameObjectEhVisualPrincipalProtegido(pontoVisualImpacto.gameObject))
            pontoVisualImpacto.gameObject.SetActive(visivel);

        if (objetosEfeitoParaOcultarNoInventario != null)
        {
            for (int i = 0; i < objetosEfeitoParaOcultarNoInventario.Length; i++)
            {
                GameObject objeto = objetosEfeitoParaOcultarNoInventario[i];
                if (objeto != null && !GameObjectEhVisualPrincipalProtegido(objeto))
                    objeto.SetActive(visivel);
            }
        }

        if (renderersEfeitoParaOcultarNoInventario == null)
            return;

        for (int i = 0; i < renderersEfeitoParaOcultarNoInventario.Length; i++)
        {
            Renderer renderer = renderersEfeitoParaOcultarNoInventario[i];
            if (renderer != null && !RendererEhVisualPrincipalProtegido(renderer))
                renderer.enabled = visivel;
        }
    }

    private void AplicarRenderersCordaReal(bool visivel)
    {
        if (renderersCordaReal == null)
            return;

        for (int i = 0; i < renderersCordaReal.Length; i++)
        {
            Renderer renderer = renderersCordaReal[i];
            if (renderer != null && !RendererEhVisualPrincipalProtegido(renderer))
                renderer.enabled = visivel;
        }
    }

    private bool RendererEhVisualPrincipalProtegido(Renderer renderer)
    {
        if (renderer == null)
            return false;

        CapturarVisualPrincipalProtegidoSeNecessario();

        for (int i = 0; i < renderersPrincipaisProtegidos.Count; i++)
        {
            Renderer principal = renderersPrincipaisProtegidos[i];
            if (principal == null)
                continue;

            if (renderer == principal)
                return true;

            if (renderer.transform == principal.transform ||
                renderer.transform.IsChildOf(principal.transform) ||
                principal.transform.IsChildOf(renderer.transform))
                return true;
        }

        for (int i = 0; i < objetosPrincipaisProtegidos.Count; i++)
        {
            GameObject objeto = objetosPrincipaisProtegidos[i];
            if (objeto == null)
                continue;

            Transform alvo = objeto.transform;
            if (renderer.transform == alvo || renderer.transform.IsChildOf(alvo) || alvo.IsChildOf(renderer.transform))
                return true;
        }

        return false;
    }

    private bool GameObjectEhVisualPrincipalProtegido(GameObject objeto)
    {
        if (objeto == null)
            return false;

        CapturarVisualPrincipalProtegidoSeNecessario();
        Transform transformObjeto = objeto.transform;

        for (int i = 0; i < objetosPrincipaisProtegidos.Count; i++)
        {
            GameObject principal = objetosPrincipaisProtegidos[i];
            if (principal == null)
                continue;

            Transform transformPrincipal = principal.transform;
            if (transformObjeto == transformPrincipal ||
                transformObjeto.IsChildOf(transformPrincipal) ||
                transformPrincipal.IsChildOf(transformObjeto))
                return true;
        }

        for (int i = 0; i < renderersPrincipaisProtegidos.Count; i++)
        {
            Renderer renderer = renderersPrincipaisProtegidos[i];
            if (renderer == null)
                continue;

            Transform transformRenderer = renderer.transform;
            if (transformRenderer == transformObjeto ||
                transformRenderer.IsChildOf(transformObjeto) ||
                transformObjeto.IsChildOf(transformRenderer))
            {
                return true;
            }
        }

        return false;
    }

    private void ResetarCordaParaRepousoImediato()
    {
        if (pontoCordaAtual != null && pontoCordaRepouso != null)
        {
            pontoCordaAtual.position = pontoCordaRepouso.position;
            pontoCordaAtual.rotation = pontoCordaRepouso.rotation;
        }

        if (usarCordaRealComBones)
        {
            if (boneCordaMeio != null)
            {
                Transform repouso = pontoRepousoCordaMeio != null ? pontoRepousoCordaMeio : pontoCordaRepouso;
                if (repouso != null && !TransformPareceContainerArmacaoCorda(boneCordaMeio))
                {
                    boneCordaMeio.position = repouso.position;
                    boneCordaMeio.rotation = repouso.rotation;
                }

                if (basesCordaRealCapturadas)
                    boneCordaMeio.localRotation = rotacaoLocalBaseBoneCordaMeio;
            }

            FixarPontasCordaReal();
        }

        AplicarCurvaturaVisualImediata(0f);

        if (linhaCorda != null && linhaCorda.enabled)
            AtualizarLinhaCorda();
    }

    private static bool TransformPareceContainerArmacaoCorda(Transform alvo)
    {
        if (alvo == null)
            return false;

        string nome = NormalizarNomeCompleto(alvo.name);
        return nome.Contains("armacao") && nome.Contains("corda");
    }

    private void AtualizarDiagnosticoInventarioArco()
    {
        diagnosticoEstaNoInventario = estaNoInventario;
        diagnosticoQuantidadeRenderersPrincipais = ContarRenderersPrincipaisValidos();
        diagnosticoRenderPrincipalVisivel = ExisteRendererPrincipalVisivel();
        diagnosticoTodosRenderersPrincipaisVisiveis = TodosRenderersPrincipaisVisiveis();
        diagnosticoCordaRealVisivel = ExisteCordaRealVisivel();
        LineRenderer cordaVisual = ObterLinhaCordaVisual();
        diagnosticoLinhaAzulVisivel = cordaVisual != null && cordaVisual.enabled;
        diagnosticoMiraVisivel = linhaMiraTrajetoria != null && linhaMiraTrajetoria.enabled;
        diagnosticoFlechaPreparadaExiste = flechaPreparada != null;
        diagnosticoAreaCordaAtiva = areaPuxarCorda != null && areaPuxarCorda.enabled;

        diagnosticoLinhaCordaVisualExiste = cordaVisual != null;
        diagnosticoLinhaCordaVisualVisivel = cordaVisual != null && cordaVisual.enabled;
    }

    private bool ExisteRendererPrincipalVisivel()
    {
        PreencherRenderersPrincipaisAutomaticamenteSeNecessario();

        if (renderersPrincipaisDoArco != null)
        {
            for (int i = 0; i < renderersPrincipaisDoArco.Length; i++)
            {
                Renderer renderer = renderersPrincipaisDoArco[i];
                if (renderer != null && renderer.enabled)
                    return true;
            }
        }

        return false;
    }

    private int ContarRenderersPrincipaisValidos()
    {
        PreencherRenderersPrincipaisAutomaticamenteSeNecessario();

        int quantidade = 0;
        if (renderersPrincipaisDoArco == null)
            return quantidade;

        for (int i = 0; i < renderersPrincipaisDoArco.Length; i++)
        {
            if (renderersPrincipaisDoArco[i] != null)
                quantidade++;
        }

        return quantidade;
    }

    private bool TodosRenderersPrincipaisVisiveis()
    {
        PreencherRenderersPrincipaisAutomaticamenteSeNecessario();

        bool encontrouRenderer = false;
        if (renderersPrincipaisDoArco == null)
            return false;

        for (int i = 0; i < renderersPrincipaisDoArco.Length; i++)
        {
            Renderer renderer = renderersPrincipaisDoArco[i];
            if (renderer == null)
                continue;

            encontrouRenderer = true;
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                return false;
        }

        return encontrouRenderer;
    }

    private bool ExisteCordaRealVisivel()
    {
        if (renderersCordaReal != null)
        {
            for (int i = 0; i < renderersCordaReal.Length; i++)
            {
                Renderer renderer = renderersCordaReal[i];
                if (renderer != null && renderer.enabled)
                    return true;
            }
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && !(renderer is LineRenderer) && RendererPertenceACordaReal(renderer) && renderer.enabled)
                return true;
        }

        return false;
    }

    private void SincronizarModoInventarioPorParent()
    {
        if (!detectarModoInventarioPorParent || estaNoInventario)
            return;

        if (attachDuasMao != null && attachDuasMao.ArcoEstaSegurado())
            return;

        if (TransformEstaDentroDeInventario(transform))
            DefinirModoInventario(true);
    }

    private void CapturarRotacoesBaseCurvatura()
    {
        rotacaoBaseCima1 = arcoCima1 != null ? arcoCima1.localRotation : Quaternion.identity;
        rotacaoBaseCima2 = arcoCima2 != null ? arcoCima2.localRotation : Quaternion.identity;
        rotacaoBaseCima3 = arcoCima3 != null ? arcoCima3.localRotation : Quaternion.identity;

        rotacaoBaseBaixo1 = arcoBaixo1 != null ? arcoBaixo1.localRotation : Quaternion.identity;
        rotacaoBaseBaixo2 = arcoBaixo2 != null ? arcoBaixo2.localRotation : Quaternion.identity;
        rotacaoBaseBaixo3 = arcoBaixo3 != null ? arcoBaixo3.localRotation : Quaternion.identity;
    }

    private void IniciarPuxada(Transform mao)
    {
        if (estaNoInventario)
            return;

        if (Quebrado || cordaSendoPuxada || mao == null)
            return;

        if (!MaoValidaParaPuxarCorda(mao))
            return;

        Transform maoQueSegura = attachDuasMao.ObterMaoQueSegura();
        if (MaoPertenceAoTransform(mao, maoQueSegura))
            return;

        cordaSendoPuxada = true;
        cordaSeguradaPeloGrip = true;
        disparoJaProcessado = false;
        maoPuxando = mao;

        if (criarFlechaAutomaticamente)
            PrepararFlecha();

        TocarSom(somPuxarCorda);
    }

    private void AtualizarEntradaPuxada()
    {
        if (estaNoInventario)
            return;

        if (Quebrado)
        {
            if (cordaSendoPuxada)
                CancelarPuxada();

            gripPuxarPressionado = false;
            maoCandidataPuxar = null;
            maoCandidataDentroDaArea = false;
            return;
        }

        Transform maoReferencia = cordaSendoPuxada ? maoPuxando : maoCandidataPuxar;
        if (maoReferencia == null)
        {
            gripPuxarPressionado = false;
            return;
        }

        if (!MaoValidaParaPuxarCorda(maoReferencia))
        {
            gripPuxarPressionado = false;

            if (cordaSendoPuxada)
                CancelarPuxada();

            maoCandidataPuxar = null;
            maoCandidataDentroDaArea = false;
            return;
        }

        gripPuxarPressionado = GripDaMaoPuxandoPressionado();

        if (gripPuxarPressionado)
        {
            if (!cordaSendoPuxada)
                IniciarPuxada(maoReferencia);

            return;
        }

        if (cordaSendoPuxada && cordaSeguradaPeloGrip)
            SoltarCorda();
    }

    private void AtualizarPuxada()
    {
        if (estaNoInventario)
            return;

        if (maoPuxando == null || pontoCordaRepouso == null || pontoCordaAtual == null)
        {
            CancelarPuxada();
            return;
        }

        if (!MaoValidaParaPuxarCorda(maoPuxando))
        {
            CancelarPuxada();
            return;
        }

        Vector3 origemCorda = pontoCordaRepouso.position;
        Vector3 vetorPuxada = maoPuxando.position - origemCorda;

        if (vetorPuxada.magnitude > distanciaMaximaPuxada * multiplicadorDistanciaSeguranca)
        {
            CancelarPuxada();
            return;
        }

        Vector3 vetorLimitado = Vector3.ClampMagnitude(vetorPuxada, distanciaMaximaPuxada);

        DefinirPosicaoCordaAtual(origemCorda + vetorLimitado);
        FixarPontasCordaReal();

        distanciaPuxadaAtual = Vector3.Distance(pontoCordaRepouso.position, pontoCordaAtual.position);
        percentualPuxada = distanciaMaximaPuxada > 0f
            ? Mathf.Clamp01(distanciaPuxadaAtual / distanciaMaximaPuxada)
            : 0f;

        AplicarCurvaturaVisual(percentualPuxada);
        AtualizarLinhaCorda();
        ManterFlechaPreparadaNoPonto();
        AtualizarAcumuloEnergia();
        AtualizarMiraArco();
    }

    private void AtualizarAcumuloEnergia()
    {
        if (percentualPuxada < percentualParaAcumularEnergia)
            return;

        energiaAcumulada += velocidadeAcumuloEnergia * Time.deltaTime;
        energiaAcumulada = Mathf.Clamp(energiaAcumulada, 0f, energiaExtraMaxima);
    }

    private void SoltarCorda()
    {
        if (estaNoInventario)
            return;

        if (disparoJaProcessado)
            return;

        disparoJaProcessado = true;

        if (Quebrado)
        {
            LimparFlechaPreparada(true);
            ResetarCorda();
            return;
        }

        if (distanciaPuxadaAtual < distanciaMinimaParaDisparo)
        {
            LimparFlechaPreparada(true);
            ResetarCorda();
            return;
        }

        if (DispararFlecha())
            ReduzirDurabilidade(desgastePorDisparo);

        ResetarCorda();
    }

    private bool DispararFlecha()
    {
        if (estaNoInventario)
            return false;

        if (Quebrado)
            return false;

        Transform pontoFlechaAtual = ObterPontoFlechaAtual();
        if (!TentarPrepararPrefabFlechaParaDisparo(out GameObject prefabParaDisparo))
        {
            flechaPreparada = null;
            AtualizarDiagnosticoFlechaEquipada();
            return false;
        }

        GameObject flecha = flechaPreparada;

        if (flecha == null && prefabParaDisparo != null && pontoFlechaAtual != null)
            flecha = Instantiate(prefabParaDisparo, pontoFlechaAtual.position, pontoFlechaAtual.rotation);

        if (flecha == null)
        {
            flechaPreparada = null;
            return false;
        }

        GarantirMalhaFlechaVisivel(flecha);

        Transform flechaTransform = flecha.transform;
        Vector3 direcao = CalcularDirecaoDisparo();
        flechaTransform.SetParent(null, true);
        flechaTransform.position = pontoFlechaAtual != null ? pontoFlechaAtual.position : flechaTransform.position;
        AplicarRotacaoFlechaLancada(flechaTransform, direcao, pontoFlechaAtual);

        GarantirComponenteFlecha(flecha);
        ConfigurarDonoFlecha(flecha);

        float forcaBase = Mathf.Lerp(forcaMinimaDisparo, forcaMaximaDisparo, percentualPuxada);
        float forcaFinal = forcaBase + energiaAcumulada;
        float multiplicadorDano = CalcularMultiplicadorDano();

        TentarAplicarMultiplicadorDano(flecha, multiplicadorDano);
        ReativarCollidersDaFlecha(flecha);

        bool disparou = TentarChamarDispararFlecha(flecha, direcao, forcaFinal);
        if (!disparou)
            disparou = DispararPorRigidbody(flecha, direcao, forcaFinal);

        if (!disparou)
        {
            flechaPreparada = null;
            return false;
        }

        if (!ConsumirFlechaEquipadaAposDisparo())
        {
            Destroy(flecha);
            flechaPreparada = null;
            return false;
        }

        MarcarFlechaComoLancada(flecha);
        TocarSom(somSoltarFlecha);
        flechaPreparada = null;
        AtualizarDiagnosticoFlechaEquipada();
        return true;
    }

    private void ResetarCorda()
    {
        if (pontoCordaAtual != null && pontoCordaRepouso != null)
        {
            DefinirPosicaoCordaAtual(pontoCordaRepouso.position);
            pontoCordaAtual.rotation = pontoCordaRepouso.rotation;
        }

        FixarPontasCordaReal();

        cordaSendoPuxada = false;
        cordaSeguradaPeloGrip = false;
        maoPuxando = null;
        gripPuxarPressionado = false;
        percentualPuxada = 0f;
        distanciaPuxadaAtual = 0f;
        energiaAcumulada = 0f;
        disparoJaProcessado = false;

        AplicarCurvaturaVisualImediata(0f);
        AtualizarLinhaCorda();

        if (ocultarMiraAoSoltar)
            EsconderMiraArco();
    }

    private void GarantirMiraCriada()
    {
        diagnosticoLinhaMiraCriada = linhaMiraTrajetoria != null;

        if (!usarMiraArco)
            return;

        if (!criarMiraAutomaticamente && linhaMiraTrajetoria == null)
            return;

        if (linhaMiraTrajetoria == null)
        {
            Transform miraExistente = transform.Find("Mira_Trajetoria_Arco");
            GameObject objetoMira = miraExistente != null
                ? miraExistente.gameObject
                : new GameObject("Mira_Trajetoria_Arco");

            objetoMira.transform.SetParent(transform, false);
            linhaMiraTrajetoria = objetoMira.GetComponent<LineRenderer>();

            if (linhaMiraTrajetoria == null)
                linhaMiraTrajetoria = objetoMira.AddComponent<LineRenderer>();
        }

        diagnosticoLinhaMiraCriada = linhaMiraTrajetoria != null;
        if (linhaMiraTrajetoria == null)
            return;

        linhaMiraTrajetoria.useWorldSpace = true;
        linhaMiraTrajetoria.startWidth = larguraLinhaMira;
        linhaMiraTrajetoria.endWidth = larguraLinhaMira;
        linhaMiraTrajetoria.startColor = corLinhaMira;
        linhaMiraTrajetoria.endColor = corLinhaMira;
        linhaMiraTrajetoria.numCornerVertices = 2;
        linhaMiraTrajetoria.numCapVertices = 2;

        if (linhaMiraTrajetoria.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader != null)
                linhaMiraTrajetoria.sharedMaterial = new Material(shader);
        }

        if (linhaMiraTrajetoria.sharedMaterial != null)
        {
            if (linhaMiraTrajetoria.sharedMaterial.HasProperty("_BaseColor"))
                linhaMiraTrajetoria.sharedMaterial.SetColor("_BaseColor", corLinhaMira);

            if (linhaMiraTrajetoria.sharedMaterial.HasProperty("_Color"))
                linhaMiraTrajetoria.sharedMaterial.SetColor("_Color", corLinhaMira);
        }

        if (!cordaSendoPuxada)
        {
            linhaMiraTrajetoria.positionCount = 0;
            linhaMiraTrajetoria.enabled = false;

            if (pontoVisualImpacto != null)
                pontoVisualImpacto.gameObject.SetActive(false);
        }
    }

    private void AtualizarMiraArco()
    {
        if (estaNoInventario)
        {
            EsconderMiraArco();
            return;
        }

        diagnosticoPercentualPuxadaMira = percentualPuxada;
        GarantirMiraCriada();

        if (!DeveMostrarMira())
        {
            EsconderMiraArco();
            return;
        }

        if (!ObterOrigemEDirecaoDisparo(out Vector3 origem, out Vector3 direcao))
        {
            diagnosticoDirecaoMiraValida = false;
            EsconderMiraArco();
            return;
        }

        diagnosticoDirecaoMiraValida = true;

        float forcaBase = Mathf.Lerp(forcaMinimaDisparo, forcaMaximaDisparo, percentualPuxada);
        float forcaFinal = forcaBase + energiaAcumulada;
        origem += direcao * 0.08f;
        Vector3 velocidadeInicial = direcao * forcaFinal * multiplicadorVelocidadeMira;

        int quantidadePontos = CalcularTrajetoriaMira(origem, velocidadeInicial, out bool encontrouImpacto, out RaycastHit impacto);
        diagnosticoQuantidadePontosMira = quantidadePontos;

        if (tipoMiraArco == TipoMiraArco.TrajetoriaCurva && linhaMiraTrajetoria != null && quantidadePontos > 1)
        {
            linhaMiraTrajetoria.startWidth = larguraLinhaMira;
            linhaMiraTrajetoria.endWidth = larguraLinhaMira;
            linhaMiraTrajetoria.startColor = corLinhaMira;
            linhaMiraTrajetoria.endColor = corLinhaMira;
            linhaMiraTrajetoria.positionCount = quantidadePontos;

            for (int i = 0; i < quantidadePontos; i++)
                linhaMiraTrajetoria.SetPosition(i, pontosCalculadosMira[i]);

            linhaMiraTrajetoria.enabled = true;
            diagnosticoMiraAtiva = true;
        }
        else if (linhaMiraTrajetoria != null)
        {
            linhaMiraTrajetoria.positionCount = 0;
            linhaMiraTrajetoria.enabled = false;
            diagnosticoMiraAtiva = tipoMiraArco == TipoMiraArco.PontoImpacto && encontrouImpacto;
        }

        AtualizarPontoVisualImpacto(encontrouImpacto, impacto);
    }

    private bool DeveMostrarMira()
    {
        diagnosticoPercentualPuxadaMira = percentualPuxada;

        bool podeMostrar =
            usarMiraArco &&
            tipoMiraArco != TipoMiraArco.Nenhuma &&
            cordaSendoPuxada &&
            !Quebrado &&
            (!mostrarMiraSomenteComPuxadaMinima || percentualPuxada >= percentualMinimoParaMostrarMira);

        diagnosticoDeveMostrarMira = podeMostrar;
        return podeMostrar;
    }

    private int CalcularTrajetoriaMira(Vector3 origem, Vector3 velocidadeInicial, out bool encontrouImpacto, out RaycastHit impacto)
    {
        encontrouImpacto = false;
        impacto = default;

        int limitePontos = Mathf.Clamp(quantidadePontosMira, 4, pontosCalculadosMira.Length);
        pontosCalculadosMira[0] = origem;

        Vector3 pontoAnterior = origem;
        float distanciaAcumulada = 0f;
        int quantidadeReal = 1;

        for (int i = 1; i < limitePontos; i++)
        {
            float tempo = i * intervaloTempoMira;
            Vector3 pontoAtual = origem + velocidadeInicial * tempo + 0.5f * Physics.gravity * tempo * tempo;
            if (!VetorValido(pontoAtual))
                break;

            Vector3 segmento = pontoAtual - pontoAnterior;
            float distanciaSegmento = segmento.magnitude;

            if (distanciaSegmento <= 0.0001f)
                continue;

            if (distanciaAcumulada + distanciaSegmento > distanciaMaximaMira)
            {
                float distanciaRestante = Mathf.Max(0f, distanciaMaximaMira - distanciaAcumulada);
                pontoAtual = pontoAnterior + segmento.normalized * distanciaRestante;
                distanciaSegmento = distanciaRestante;
            }

            if (TentarEncontrarImpactoMira(pontoAnterior, pontoAtual, out impacto))
            {
                pontosCalculadosMira[quantidadeReal] = impacto.point;
                quantidadeReal++;
                encontrouImpacto = true;
                return quantidadeReal;
            }

            pontosCalculadosMira[quantidadeReal] = pontoAtual;
            quantidadeReal++;
            distanciaAcumulada += distanciaSegmento;
            pontoAnterior = pontoAtual;

            if (distanciaAcumulada >= distanciaMaximaMira)
                break;
        }

        return quantidadeReal;
    }

    private bool TentarEncontrarImpactoMira(Vector3 pontoAnterior, Vector3 pontoAtual, out RaycastHit impacto)
    {
        impacto = default;

        Vector3 direcaoSegmento = pontoAtual - pontoAnterior;
        float distanciaSegmento = direcaoSegmento.magnitude;
        if (distanciaSegmento <= 0.0001f)
            return false;

        int quantidadeHits = Physics.RaycastNonAlloc(
            pontoAnterior,
            direcaoSegmento.normalized,
            resultadosRaycastMira,
            distanciaSegmento,
            camadasColisaoMira,
            QueryTriggerInteraction.Ignore);

        bool encontrou = false;
        float menorDistancia = float.MaxValue;

        for (int i = 0; i < quantidadeHits; i++)
        {
            RaycastHit hit = resultadosRaycastMira[i];
            if (hit.collider == null || ColliderDeveSerIgnoradoPelaMira(hit.collider))
                continue;

            if (hit.distance >= menorDistancia)
                continue;

            menorDistancia = hit.distance;
            impacto = hit;
            encontrou = true;
        }

        return encontrou;
    }

    private bool ColliderDeveSerIgnoradoPelaMira(Collider col)
    {
        if (col == null)
            return true;

        if (ColliderPertenceAoProprioArco(col))
            return true;

        if (flechaPreparada != null && TransformPertenceAoObjeto(col.transform, flechaPreparada.transform))
            return true;

        if (col.attachedRigidbody != null &&
            flechaPreparada != null &&
            TransformPertenceAoObjeto(col.attachedRigidbody.transform, flechaPreparada.transform))
        {
            return true;
        }

        GameObject donoFlecha = ObterDonoFlecha();
        if (donoFlecha == null)
            return false;

        if (TransformPertenceAoObjeto(col.transform, donoFlecha.transform))
            return true;

        return col.attachedRigidbody != null &&
               TransformPertenceAoObjeto(col.attachedRigidbody.transform, donoFlecha.transform);
    }

    private static bool TransformPertenceAoObjeto(Transform origem, Transform alvo)
    {
        if (origem == null || alvo == null)
            return false;

        return origem == alvo || origem.IsChildOf(alvo);
    }

    private static bool TransformEstaDentroDeInventario(Transform origem)
    {
        Transform atual = origem != null ? origem.parent : null;

        while (atual != null)
        {
            string nome = NormalizarNomeCompleto(atual.name);

            if (nome.Contains("slot") ||
                nome.Contains("socket") ||
                nome.Contains("inventario") ||
                nome.Contains("inventário") ||
                nome.Contains("inventory") ||
                nome.Contains("pontoencaixe"))
            {
                return true;
            }

            atual = atual.parent;
        }

        return false;
    }

    private static bool VetorValido(Vector3 valor)
    {
        return !float.IsNaN(valor.x) &&
               !float.IsNaN(valor.y) &&
               !float.IsNaN(valor.z) &&
               !float.IsInfinity(valor.x) &&
               !float.IsInfinity(valor.y) &&
               !float.IsInfinity(valor.z);
    }

    private void AtualizarPontoVisualImpacto(bool encontrouImpacto, RaycastHit impacto)
    {
        if (pontoVisualImpacto == null)
            return;

        pontoVisualImpacto.gameObject.SetActive(encontrouImpacto);
        if (!encontrouImpacto)
            return;

        pontoVisualImpacto.position = impacto.point;
        if (impacto.normal.sqrMagnitude > 0.0001f)
            pontoVisualImpacto.rotation = Quaternion.LookRotation(impacto.normal.normalized);
    }

    private void EsconderMiraArco()
    {
        diagnosticoMiraAtiva = false;
        diagnosticoQuantidadePontosMira = 0;

        if (linhaMiraTrajetoria != null)
        {
            linhaMiraTrajetoria.positionCount = 0;
            linhaMiraTrajetoria.enabled = false;
        }

        if (pontoVisualImpacto != null)
            pontoVisualImpacto.gameObject.SetActive(false);
    }

    private void AplicarCurvaturaVisual(float tensao)
    {
        if (!usarCurvaturaVisual)
            return;

        float velocidade = Mathf.Max(0f, suavidadeCurvatura);
        float fator = velocidade <= 0f ? 1f : Mathf.Clamp01(Time.deltaTime * velocidade);
        tensaoVisualAtual = Mathf.Lerp(tensaoVisualAtual, Mathf.Clamp01(tensao), fator);
        AplicarRotacoesCurvatura(tensaoVisualAtual);
    }

    private void AplicarCurvaturaVisualImediata(float tensao)
    {
        tensaoVisualAtual = Mathf.Clamp01(tensao);
        AplicarRotacoesCurvatura(tensaoVisualAtual);
    }

    private void AplicarRotacoesCurvatura(float tensao)
    {
        Vector3 eixoNormalizado = eixoLocalCurvatura.sqrMagnitude > 0.0001f
            ? eixoLocalCurvatura.normalized
            : Vector3.forward;

        float sinal = inverterCurvatura ? -1f : 1f;

        AplicarRotacaoLocal(arcoCima1, rotacaoBaseCima1, eixoNormalizado, anguloCima1 * sinal, tensao);
        AplicarRotacaoLocal(arcoCima2, rotacaoBaseCima2, eixoNormalizado, anguloCima2 * sinal, tensao);
        AplicarRotacaoLocal(arcoCima3, rotacaoBaseCima3, eixoNormalizado, anguloCima3 * sinal, tensao);

        AplicarRotacaoLocal(arcoBaixo1, rotacaoBaseBaixo1, eixoNormalizado, anguloBaixo1 * sinal, tensao);
        AplicarRotacaoLocal(arcoBaixo2, rotacaoBaseBaixo2, eixoNormalizado, anguloBaixo2 * sinal, tensao);
        AplicarRotacaoLocal(arcoBaixo3, rotacaoBaseBaixo3, eixoNormalizado, anguloBaixo3 * sinal, tensao);
    }

    private static void AplicarRotacaoLocal(Transform alvo, Quaternion rotacaoBase, Vector3 eixo, float angulo, float tensao)
    {
        if (alvo == null)
            return;

        alvo.localRotation = rotacaoBase * Quaternion.AngleAxis(angulo * tensao, eixo);
    }

    private void AtualizarLinhaCorda()
    {
        if (linhaCorda == null || pontoCordaTopo == null || pontoCordaAtual == null || pontoCordaBaixo == null)
            return;

        if (!linhaCorda.enabled)
            return;

        DefinirPontosLocaisLinhaCorda(linhaCorda);
    }

    private void DefinirPontosLocaisLinhaCorda(LineRenderer linha)
    {
        if (linha == null || pontoCordaTopo == null || pontoCordaAtual == null || pontoCordaBaixo == null)
            return;

        Transform linhaTransform = linha.transform;
        linha.useWorldSpace = false;
        linha.positionCount = 3;
        linha.SetPosition(0, linhaTransform.InverseTransformPoint(pontoCordaTopo.position));
        linha.SetPosition(1, linhaTransform.InverseTransformPoint(pontoCordaAtual.position));
        linha.SetPosition(2, linhaTransform.InverseTransformPoint(pontoCordaBaixo.position));
    }

    private void DefinirPosicaoCordaAtual(Vector3 posicaoMundo)
    {
        GarantirPontoCordaAtualMovelSeguro();

        if (pontoCordaAtual == null)
            return;

        pontoCordaAtual.position = posicaoMundo;
    }

    private void ConfigurarCordaRealComBonesSeNecessario()
    {
        if (!usarCordaRealComBones)
        {
            ConfigurarPontosCordaSegurosSeNecessario();
            return;
        }

        if (boneCordaTopo == null)
            boneCordaTopo = pontoCordaTopo;

        if (boneCordaMeio == null)
            boneCordaMeio = pontoCordaAtual;

        if (boneCordaBaixo == null)
            boneCordaBaixo = pontoCordaBaixo;

        if (pontoRepousoCordaMeio == null)
            pontoRepousoCordaMeio = CriarPontoRepousoCordaMeio();

        if (boneCordaTopo == null || boneCordaMeio == null || boneCordaBaixo == null || pontoRepousoCordaMeio == null)
        {
            ConfigurarPontosCordaSegurosSeNecessario();
            return;
        }

        pontoCordaTopo = boneCordaTopo;
        pontoCordaAtual = boneCordaMeio;
        pontoCordaBaixo = boneCordaBaixo;
        pontoCordaRepouso = pontoRepousoCordaMeio;

        GarantirPontoCordaRepousoSeguro();
        GarantirPontoCordaAtualMovelSeguro();
    }

    private Transform CriarPontoRepousoCordaMeio()
    {
        Vector3 posicaoRepouso = ObterPosicaoRepousoCordaSegura();

        Transform container = ObterOuCriarFilho(transform, "Pontos_Corda_Real");
        return ObterOuCriarPonto(container, "Ponto_Repouso_Corda_Meio", posicaoRepouso);
    }

    private void GarantirPontoCordaRepousoSeguro()
    {
        Transform repousoReferencia = pontoCordaRepouso != null ? pontoCordaRepouso : pontoRepousoCordaMeio;

        if (!PontoRepousoCordaInvalido(repousoReferencia))
        {
            pontoCordaRepouso = repousoReferencia;
            pontoRepousoCordaMeio = repousoReferencia;
            return;
        }

        Vector3 posicaoRepouso = ObterPosicaoRepousoCordaSegura();
        Transform container = ObterOuCriarFilho(transform, "Pontos_Corda_Real");
        Transform repousoSeguro = ObterOuCriarPonto(container, "Ponto_Corda_Repouso", posicaoRepouso);

        pontoCordaRepouso = repousoSeguro;
        pontoRepousoCordaMeio = repousoSeguro;
    }

    private void GarantirPontoCordaAtualMovelSeguro()
    {
        if (!PontoMovelCordaInvalido(pontoCordaAtual))
            return;

        Vector3 posicaoAtualSegura = ObterPosicaoRepousoCordaSegura();
        Transform container = ObterOuCriarFilho(transform, "Pontos_Corda_Real");
        pontoCordaAtual = ObterOuCriarPonto(container, "Ponto_Corda_Atual", posicaoAtualSegura);
        boneCordaMeio = pontoCordaAtual;
    }

    private Vector3 ObterPosicaoRepousoCordaSegura()
    {
        if (pontoRepousoCordaMeio != null && !PontoRepousoCordaInvalido(pontoRepousoCordaMeio))
            return pontoRepousoCordaMeio.position;

        if (pontoCordaRepouso != null && !PontoRepousoCordaInvalido(pontoCordaRepouso))
            return pontoCordaRepouso.position;

        if (pontoCordaTopo != null && pontoCordaBaixo != null)
            return (pontoCordaTopo.position + pontoCordaBaixo.position) * 0.5f;

        if (boneCordaMeio != null && !PontoMovelCordaInvalido(boneCordaMeio))
            return boneCordaMeio.position;

        if (pontoCordaAtual != null)
            return pontoCordaAtual.position;

        return transform.position;
    }

    private bool PontoRepousoCordaInvalido(Transform ponto)
    {
        if (ponto == null || ponto == transform)
            return true;

        if (ponto == pontoCordaTopo || ponto == pontoCordaAtual || ponto == pontoCordaBaixo)
            return true;

        if (PontoCordaTemComponentesInvalidos(ponto))
            return true;

        return TransformEhPaiDe(ponto, pontoCordaTopo) ||
               TransformEhPaiDe(ponto, pontoCordaAtual) ||
               TransformEhPaiDe(ponto, pontoCordaBaixo);
    }

    private bool PontoMovelCordaInvalido(Transform ponto)
    {
        if (ponto == null || ponto == transform)
            return true;

        if (ponto == pontoCordaTopo || ponto == pontoCordaRepouso || ponto == pontoCordaBaixo)
            return true;

        if (PontoCordaTemComponentesInvalidos(ponto))
            return true;

        return PontoMovelControlaOutroPontoDaCorda(ponto);
    }

    private bool PontoMovelControlaOutroPontoDaCorda(Transform ponto)
    {
        return TransformEhPaiDe(ponto, pontoCordaTopo) ||
               TransformEhPaiDe(ponto, pontoCordaRepouso) ||
               TransformEhPaiDe(ponto, pontoCordaBaixo);
    }

    private static bool TransformEhPaiDe(Transform possivelPai, Transform possivelFilho)
    {
        return possivelPai != null &&
               possivelFilho != null &&
               possivelPai != possivelFilho &&
               possivelFilho.IsChildOf(possivelPai);
    }

    private void ConfigurarVisualizacaoLinhaCorda()
    {
        bool linhaProceduralAtiva = usarLinhaCordaProcedural ||
                                    manterLinhaCordaVisualSempreVisivel ||
                                    !usarCordaRealComBones ||
                                    (mostrarLinhaDebugCorda && !esconderLineRendererCorda);

        if (linhaProceduralAtiva)
        {
            GarantirLineRendererPresoAoArco();

            if (linhaCorda != null)
                linhaCorda.enabled = true;

            AtualizarVisibilidadeCordaOriginal(true);
            return;
        }

        if (linhaCorda != null)
            linhaCorda.enabled = false;
    }

    private void GarantirCordaNoRepousoSeNaoPuxando()
    {
        GarantirPontoCordaRepousoSeguro();
        GarantirPontoCordaAtualMovelSeguro();

        if (pontoCordaAtual != null && pontoCordaRepouso != null)
            pontoCordaAtual.position = pontoCordaRepouso.position;
    }

    private void CapturarBaseCordaReal()
    {
        if (!usarCordaRealComBones || boneCordaMeio == null)
            return;

        if (boneCordaTopo != null)
        {
            posicaoLocalBaseBoneCordaTopo = transform.InverseTransformPoint(boneCordaTopo.position);
            rotacaoLocalBaseBoneCordaTopo = boneCordaTopo.localRotation;
        }

        if (boneCordaBaixo != null)
        {
            posicaoLocalBaseBoneCordaBaixo = transform.InverseTransformPoint(boneCordaBaixo.position);
            rotacaoLocalBaseBoneCordaBaixo = boneCordaBaixo.localRotation;
        }

        rotacaoLocalBaseBoneCordaMeio = boneCordaMeio.localRotation;
        basesCordaRealCapturadas = true;
    }

    private void FixarPontasCordaReal()
    {
        if (!usarCordaRealComBones || !basesCordaRealCapturadas)
            return;

        if (boneCordaTopo != null)
        {
            boneCordaTopo.position = transform.TransformPoint(posicaoLocalBaseBoneCordaTopo);
            boneCordaTopo.localRotation = rotacaoLocalBaseBoneCordaTopo;
        }

        if (boneCordaBaixo != null)
        {
            boneCordaBaixo.position = transform.TransformPoint(posicaoLocalBaseBoneCordaBaixo);
            boneCordaBaixo.localRotation = rotacaoLocalBaseBoneCordaBaixo;
        }

        if (!cordaSendoPuxada && boneCordaMeio != null)
            boneCordaMeio.localRotation = rotacaoLocalBaseBoneCordaMeio;
    }

    private void ConfigurarPontosCordaSegurosSeNecessario()
    {
        if (!PontosCordaPrecisamSerSeparados())
            return;

        Transform container = ObterOuCriarFilho(transform, "Pontos_Corda_Automaticos");
        Vector3 posicaoTopo;
        Vector3 posicaoRepouso;
        Vector3 posicaoAtual;
        Vector3 posicaoBaixo;
        Transform referencia = ObterReferenciaVisualCorda();

        if (TentarObterPosicoesAtuaisDaCorda(out posicaoTopo, out posicaoRepouso, out posicaoAtual, out posicaoBaixo))
        {
            posicaoAtual = posicaoRepouso;
        }
        else
        {
            Vector3 centro = ObterCentroCorda(referencia);
            Vector3 eixo = ObterEixoPrincipalCorda(referencia);
            float comprimento = ObterComprimentoAproximadoCorda(referencia, eixo);

            if (comprimento <= 0.001f)
                comprimento = Mathf.Max(distanciaMaximaPuxada, 0.25f);

            float metadeComprimento = comprimento * 0.5f;
            posicaoTopo = centro + eixo * metadeComprimento;
            posicaoRepouso = centro;
            posicaoAtual = centro;
            posicaoBaixo = centro - eixo * metadeComprimento;
        }

        pontoCordaTopo = ObterOuCriarPonto(container, "Ponto_Corda_Topo", posicaoTopo);
        pontoCordaRepouso = ObterOuCriarPonto(container, "Ponto_Corda_Repouso", posicaoRepouso);
        pontoCordaAtual = ObterOuCriarPonto(container, "Ponto_Corda_Atual", posicaoAtual);
        pontoCordaBaixo = ObterOuCriarPonto(container, "Ponto_Corda_Baixo", posicaoBaixo);

        if (ocultarMalhaCordaOriginalQuandoUsarLinha)
            OcultarRendererDaCordaOriginal(referencia);
    }

    private bool PontosCordaPrecisamSerSeparados()
    {
        if (pontoCordaTopo == null || pontoCordaRepouso == null || pontoCordaAtual == null || pontoCordaBaixo == null)
            return true;

        return pontoCordaAtual == pontoCordaTopo ||
               pontoCordaAtual == pontoCordaRepouso ||
               pontoCordaAtual == pontoCordaBaixo ||
               PontoCordaTemComponentesInvalidos(pontoCordaAtual) ||
               PontosCordaEstaoEmHierarquiaInvalida();
    }

    private bool TentarObterPosicoesAtuaisDaCorda(
        out Vector3 posicaoTopo,
        out Vector3 posicaoRepouso,
        out Vector3 posicaoAtual,
        out Vector3 posicaoBaixo)
    {
        posicaoTopo = Vector3.zero;
        posicaoRepouso = Vector3.zero;
        posicaoAtual = Vector3.zero;
        posicaoBaixo = Vector3.zero;

        if (pontoCordaTopo == null || pontoCordaRepouso == null || pontoCordaAtual == null || pontoCordaBaixo == null)
            return false;

        if (PontoCordaTemComponentesInvalidos(pontoCordaTopo) ||
            PontoCordaTemComponentesInvalidos(pontoCordaRepouso) ||
            PontoCordaTemComponentesInvalidos(pontoCordaAtual) ||
            PontoCordaTemComponentesInvalidos(pontoCordaBaixo))
            return false;

        posicaoTopo = pontoCordaTopo.position;
        posicaoRepouso = pontoCordaRepouso.position;
        posicaoAtual = pontoCordaAtual.position;
        posicaoBaixo = pontoCordaBaixo.position;
        return true;
    }

    private bool PontosCordaEstaoEmHierarquiaInvalida()
    {
        return PontoEhDescendenteDeOutroPonto(pontoCordaTopo) ||
               PontoEhDescendenteDeOutroPonto(pontoCordaRepouso) ||
               PontoEhDescendenteDeOutroPonto(pontoCordaAtual) ||
               PontoEhDescendenteDeOutroPonto(pontoCordaBaixo);
    }

    private bool PontoEhDescendenteDeOutroPonto(Transform ponto)
    {
        if (ponto == null)
            return false;

        return PontoEhDescendenteDe(ponto, pontoCordaTopo) ||
               PontoEhDescendenteDe(ponto, pontoCordaRepouso) ||
               PontoEhDescendenteDe(ponto, pontoCordaAtual) ||
               PontoEhDescendenteDe(ponto, pontoCordaBaixo);
    }

    private static bool PontoEhDescendenteDe(Transform ponto, Transform possivelPai)
    {
        return ponto != null && possivelPai != null && ponto != possivelPai && ponto.IsChildOf(possivelPai);
    }

    private Transform ObterReferenciaVisualCorda()
    {
        if (pontoCordaAtual != null)
            return pontoCordaAtual;

        if (pontoCordaRepouso != null)
            return pontoCordaRepouso;

        if (pontoCordaTopo != null)
            return pontoCordaTopo;

        if (pontoCordaBaixo != null)
            return pontoCordaBaixo;

        return transform;
    }

    private static bool PontoCordaTemComponentesInvalidos(Transform alvo)
    {
        return alvo != null &&
               (alvo.GetComponent<Renderer>() != null ||
                alvo.GetComponent<MeshFilter>() != null ||
                alvo.GetComponent<Collider>() != null ||
                alvo.GetComponent<Rigidbody>() != null ||
                alvo.GetComponent<XRBaseInteractable>() != null);
    }

    private static Vector3 ObterCentroCorda(Transform referencia)
    {
        if (referencia == null)
            return Vector3.zero;

        Renderer renderer = referencia.GetComponent<Renderer>();
        return renderer != null ? renderer.bounds.center : referencia.position;
    }

    private static Vector3 ObterEixoPrincipalCorda(Transform referencia)
    {
        if (referencia == null)
            return Vector3.up;

        MeshFilter meshFilter = referencia.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Vector3 tamanho = meshFilter.sharedMesh.bounds.size;

            if (tamanho.x >= tamanho.y && tamanho.x >= tamanho.z)
                return referencia.right.normalized;

            if (tamanho.y >= tamanho.x && tamanho.y >= tamanho.z)
                return referencia.up.normalized;

            return referencia.forward.normalized;
        }

        return referencia.up.normalized;
    }

    private static float ObterComprimentoAproximadoCorda(Transform referencia, Vector3 eixo)
    {
        if (referencia == null)
            return 0f;

        Renderer renderer = referencia.GetComponent<Renderer>();
        if (renderer == null)
            return 0f;

        Vector3 eixoNormalizado = eixo.sqrMagnitude > 0.0001f ? eixo.normalized : Vector3.up;
        Vector3 extents = renderer.bounds.extents;
        return 2f * (
            Mathf.Abs(eixoNormalizado.x) * extents.x +
            Mathf.Abs(eixoNormalizado.y) * extents.y +
            Mathf.Abs(eixoNormalizado.z) * extents.z
        );
    }

    private static Transform ObterOuCriarFilho(Transform pai, string nome)
    {
        Transform existente = pai != null ? pai.Find(nome) : null;
        if (existente != null)
            return existente;

        GameObject novo = new GameObject(nome);
        Transform novoTransform = novo.transform;

        if (pai != null)
            novoTransform.SetParent(pai, true);

        return novoTransform;
    }

    private static Transform ObterOuCriarPonto(Transform container, string nome, Vector3 posicaoMundo)
    {
        Transform existente = container != null ? container.Find(nome) : null;
        Transform ponto = existente;

        if (ponto == null)
        {
            GameObject novo = new GameObject(nome);
            ponto = novo.transform;

            if (container != null)
                ponto.SetParent(container, true);
        }

        ponto.position = posicaoMundo;
        return ponto;
    }

    private void ConfigurarLinhaCordaSeNecessario()
    {
        GarantirLineRendererPresoAoArco();
    }

    private void GarantirLineRendererPresoAoArco()
    {
        if (linhaCorda != null)
        {
            Transform linhaTransform = linhaCorda.transform;
            if (linhaTransform != transform && !linhaTransform.IsChildOf(transform))
                linhaTransform.SetParent(transform, true);
        }
        else
        {
            Transform container = ObterOuCriarFilho(transform, "CordaVisual_LineRenderer");
            linhaCorda = container.GetComponent<LineRenderer>();

            if (linhaCorda == null)
                linhaCorda = container.gameObject.AddComponent<LineRenderer>();
        }

        if (linhaCorda == null)
            return;

        if (linhaCordaVisual == null)
            linhaCordaVisual = linhaCorda;

        linhaCorda.useWorldSpace = false;
        linhaCorda.positionCount = 3;
        linhaCorda.widthMultiplier = larguraLinhaCorda;
        linhaCorda.numCapVertices = 2;
        linhaCorda.numCornerVertices = 2;
        linhaCorda.startColor = Color.black;
        linhaCorda.endColor = Color.black;

        if (linhaCorda.material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                linhaCorda.material = new Material(shader);
        }
    }

    private void OcultarRendererDaCordaOriginal(Transform referencia)
    {
        if (referencia == null || referencia == transform)
            return;

        Renderer renderer = referencia.GetComponent<Renderer>();
        if (renderer != null && !(renderer is LineRenderer) && RendererPertenceACordaReal(renderer))
            renderer.enabled = false;
    }

    private void AtualizarVisibilidadeCordaOriginal(bool linhaProceduralAtiva)
    {
        if (!linhaProceduralAtiva || !ocultarMalhaCordaOriginalQuandoUsarLinha)
            return;

        Renderer rendererCorda = EncontrarRendererCordaOriginal();
        if (rendererCorda != null)
            rendererCorda.enabled = false;
    }

    private Renderer EncontrarRendererCordaOriginal()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererAtual = renderers[i];
            if (rendererAtual == null || rendererAtual is LineRenderer)
                continue;

            if (RendererPertenceACordaReal(rendererAtual))
                return rendererAtual;
        }

        return null;
    }

    private Collider EncontrarAreaPuxarCorda()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider colliderEncontrado = colliders[i];
            if (colliderEncontrado == null)
                continue;

            string nome = NormalizarNome(colliderEncontrado.name);
            if (nome.Contains("area_puxar_corda") || (nome.Contains("puxar") && nome.Contains("corda")))
                return colliderEncontrado;
        }

        return null;
    }

    private void ConfigurarRepassadorTriggerCorda()
    {
        if (areaPuxarCorda == null)
            return;

        areaPuxarCorda.isTrigger = true;

        ArcoCordaTrigger triggerCorda = areaPuxarCorda.GetComponent<ArcoCordaTrigger>();
        if (triggerCorda == null)
            triggerCorda = areaPuxarCorda.gameObject.AddComponent<ArcoCordaTrigger>();

        triggerCorda.DefinirArco(this);
    }

    public void AoMaoEntrouNaAreaDaCorda(Collider other)
    {
        if (estaNoInventario)
            return;

        if (ColliderPertenceAoProprioArco(other))
            return;

        Transform mao = ObterMaoDoCollider(other);
        if (mao == null || attachDuasMao == null || !attachDuasMao.MaoPodePuxarCorda(mao))
            return;

        maoCandidataPuxar = mao;
        maoCandidataDentroDaArea = true;
    }

    public void AoMaoSaiuDaAreaDaCorda(Collider other)
    {
        if (estaNoInventario)
            return;

        if (ColliderPertenceAoProprioArco(other))
            return;

        Transform mao = ObterMaoDoCollider(other);
        if (mao == null)
            return;

        if (MaoPertenceAoTransform(mao, maoCandidataPuxar) || MaoPertenceAoTransform(maoCandidataPuxar, mao))
        {
            maoCandidataDentroDaArea = false;

            if (!cordaSendoPuxada)
                maoCandidataPuxar = null;
        }

        if (!cordaSendoPuxada)
            return;

        if (MaoPertenceAoTransform(mao, maoPuxando) || MaoPertenceAoTransform(maoPuxando, mao))
        {
            // Sair do trigger nao solta nem dispara. O grip continua sendo a fonte de verdade.
            maoCandidataDentroDaArea = false;
        }
    }

    private bool GripDaMaoPuxandoPressionado()
    {
        Transform maoReferencia = cordaSendoPuxada ? maoPuxando : maoCandidataPuxar;

        if (!exigirBotaoParaPuxarCorda)
            return maoReferencia != null && maoCandidataDentroDaArea;

        if (maoReferencia == null || !MaoValidaParaPuxarCorda(maoReferencia))
            return false;

        return GripPressionadoParaMao(maoReferencia);
    }

    private bool GripPressionadoParaMao(Transform mao)
    {
        InputActionReference acaoGrip = ObterAcaoGripParaMao(mao);
        if (acaoGrip == null || acaoGrip.action == null)
            return permitirFallbackTriggerSemInputConfigurado && maoCandidataDentroDaArea;

        return LerValorAcaoGrip(acaoGrip) >= valorMinimoGripParaSegurar;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (estaNoInventario)
            return;

        ProcessarPossivelDanoRecebido(other);
        AoMaoEntrouNaAreaDaCorda(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (estaNoInventario)
            return;

        AoMaoSaiuDaAreaDaCorda(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        if (estaNoInventario)
            return;

        ProcessarPossivelDanoRecebido(collision.collider);
    }

    public void AtualizarDurabilidadeVisual()
    {
        NormalizarValores();
        AtualizarTextoDurabilidade(true);
    }

    private void ProcessarPossivelDanoRecebido(Collider outroCollider)
    {
        if (outroCollider == null || Quebrado || ColliderPertenceAoProprioArco(outroCollider))
            return;

        if (!TagDanificaArco(outroCollider, out GameObject origemDano))
            return;

        if (origemDano == null || TransformPertenceAoProprioArco(origemDano.transform))
            return;

        if (!TentarObterDanoDaOrigem(outroCollider, origemDano, out float danoRecebido))
            return;

        ReduzirDurabilidade(danoRecebido);
    }

    private void ReduzirDurabilidade(float valor)
    {
        if (valor <= 0f || Quebrado)
            return;

        durabilidadeAtual = Mathf.Max(0f, durabilidadeAtual - valor);
        AtualizarTextoDurabilidade(true);

        if (durabilidadeAtual <= 0f)
            QuebrarArco();
    }

    private void QuebrarArco()
    {
        if (arcoQuebrado)
            return;

        arcoQuebrado = true;
        durabilidadeAtual = 0f;
        LimparFlechaPreparada(true);
        ResetarCorda();
        AtualizarTextoDurabilidade(true);

        if (destruirAoQuebrar)
        {
            Destroy(gameObject);
            return;
        }

        if (desativarAoQuebrar)
            gameObject.SetActive(false);
    }

    private bool TentarObterDanoDaOrigem(Collider outroCollider, GameObject origemDano, out float dano)
    {
        dano = 0f;

        if (outroCollider != null && TentarObterDanoEmTransform(outroCollider.transform, out dano))
            return dano > 0f;

        if (outroCollider != null &&
            outroCollider.attachedRigidbody != null &&
            TentarObterDanoEmTransform(outroCollider.attachedRigidbody.transform, out dano))
        {
            return dano > 0f;
        }

        if (origemDano != null && TentarObterDanoEmTransform(origemDano.transform, out dano))
            return dano > 0f;

        Transform root = outroCollider != null ? outroCollider.transform.root : null;
        return TentarObterDanoEmTransform(root, out dano) && dano > 0f;
    }

    private bool TentarObterDanoEmTransform(Transform origem, out float dano)
    {
        dano = 0f;
        if (origem == null)
            return false;

        Component[] componentesPais = origem.GetComponentsInParent<Component>(true);
        for (int i = 0; i < componentesPais.Length; i++)
        {
            if (TentarObterDanoEmComponente(componentesPais[i], out dano))
                return true;
        }

        Component[] componentesFilhos = origem.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < componentesFilhos.Length; i++)
        {
            if (TentarObterDanoEmComponente(componentesFilhos[i], out dano))
                return true;
        }

        return false;
    }

    private bool TentarObterDanoEmComponente(Component componente, out float dano)
    {
        dano = 0f;
        if (componente == null || componente == this)
            return false;

        if (componente is IDano fonteDano)
        {
            dano = Mathf.Max(0f, fonteDano.ObterDano());
            return dano > 0f;
        }

        Type tipo = componente.GetType();
        for (int i = 0; i < NomesMembrosDano.Length; i++)
        {
            FieldInfo campo = tipo.GetField(NomesMembrosDano[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (campo != null && TentarConverterDano(campo.GetValue(componente), out dano))
                return dano > 0f;

            PropertyInfo propriedade = tipo.GetProperty(NomesMembrosDano[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (propriedade != null &&
                propriedade.GetIndexParameters().Length == 0 &&
                TentarObterValorPropriedade(propriedade, componente, out object valor) &&
                TentarConverterDano(valor, out dano))
            {
                return dano > 0f;
            }
        }

        return false;
    }

    private bool TentarObterValorPropriedade(PropertyInfo propriedade, Component componente, out object valor)
    {
        valor = null;
        try
        {
            valor = propriedade.GetValue(componente);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TentarConverterDano(object valor, out float dano)
    {
        dano = 0f;
        if (valor == null)
            return false;

        switch (valor)
        {
            case int inteiro:
                dano = inteiro;
                return true;
            case float flutuante:
                dano = flutuante;
                return true;
            case double duplo:
                dano = (float)duplo;
                return true;
            case long longo:
                dano = longo;
                return true;
            case short curto:
                dano = curto;
                return true;
            case byte b:
                dano = b;
                return true;
            case string texto:
                return float.TryParse(texto, out dano);
            default:
                return false;
        }
    }

    private bool TagDanificaArco(Collider outroCollider, out GameObject origemResolvida)
    {
        origemResolvida = null;

        if (outroCollider == null || tagsQueDanificamArco == null || tagsQueDanificamArco.Length == 0)
            return false;

        Transform origemTag = ProcurarTransformComTagPermitida(outroCollider.transform, tagsQueDanificamArco);
        if (origemTag == null && outroCollider.attachedRigidbody != null)
            origemTag = ProcurarTransformComTagPermitida(outroCollider.attachedRigidbody.transform, tagsQueDanificamArco);

        if (origemTag == null && outroCollider.transform.root != null &&
            TagEstaPermitida(outroCollider.transform.root.tag, tagsQueDanificamArco))
        {
            origemTag = outroCollider.transform.root;
        }

        origemResolvida = origemTag != null ? origemTag.gameObject : null;
        return origemResolvida != null;
    }

    private void EncontrarTextosDurabilidadeSeNecessario(bool criarSeNecessario = false)
    {
        if (textoValorAtual != null && textoValorTotal != null)
            return;

        TMP_Text[] textos = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textos.Length; i++)
        {
            TMP_Text texto = textos[i];
            if (texto == null)
                continue;

            string nome = NormalizarNomeCompleto(texto.gameObject.name);

            if (textoValorAtual == null && (nome == "valoratual" || nome == "atual"))
                textoValorAtual = texto;

            if (textoValorTotal == null && (nome == "valortotal" || nome == "total"))
                textoValorTotal = texto;
        }

        if (criarSeNecessario && Application.isPlaying && (textoValorAtual == null || textoValorTotal == null))
            CriarTextosDurabilidadeSeNecessario();
    }

    private void AtualizarTextoDurabilidade(bool criarSeNecessario = false)
    {
        EncontrarTextosDurabilidadeSeNecessario(criarSeNecessario);

        string atual = FormatarValorDurabilidade(durabilidadeAtual);
        string total = FormatarValorDurabilidade(durabilidadeMaxima);

        if (textoValorAtual != null)
            textoValorAtual.text = atual;

        if (textoValorTotal != null)
            textoValorTotal.text = total;
    }

    private string FormatarValorDurabilidade(float valor)
    {
        return Mathf.Max(0f, valor).ToString("N0", CulturaDurabilidade);
    }

    private void AtualizarEfeitoLedTexto()
    {
        EncontrarTextosDurabilidadeSeNecessario(true);

        Color cor = corTextoA;
        if (usarEfeitoLedTexto && velocidadePiscarTexto > 0f)
        {
            float t = Mathf.PingPong(Time.time * velocidadePiscarTexto, 1f);
            cor = Color.Lerp(corTextoA, corTextoB, t);
        }

        AplicarCorTexto(cor);
    }

    private void AplicarCorTexto(Color cor)
    {
        if (textoValorAtual != null)
            textoValorAtual.color = cor;

        if (textoValorTotal != null)
            textoValorTotal.color = cor;
    }

    private void CriarTextosDurabilidadeSeNecessario()
    {
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            canvas = CriarCanvasDurabilidade();

        Transform raiz = canvas.transform;

        if (textoValorAtual == null)
            textoValorAtual = CriarTextoDurabilidade(raiz, "Valor Atual", new Vector2(-45f, 0f), TextAlignmentOptions.Right);

        if (!ExisteSeparadorDurabilidade(raiz))
            CriarTextoDurabilidade(raiz, "Separador", Vector2.zero, TextAlignmentOptions.Center).text = "/";

        if (textoValorTotal == null)
            textoValorTotal = CriarTextoDurabilidade(raiz, "Valor Total", new Vector2(45f, 0f), TextAlignmentOptions.Left);

        raizTextoDurabilidade = raiz;
    }

    private Canvas CriarCanvasDurabilidade()
    {
        GameObject canvasObj = new GameObject("Canvas Durabilidade", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObj.transform.SetParent(transform, false);

        RectTransform rect = canvasObj.GetComponent<RectTransform>();
        rect.localPosition = new Vector3(0f, 0.55f, -0.35f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.01f;
        rect.sizeDelta = new Vector2(160f, 40f);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = ObterCameraTextoDurabilidade();
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        scaler.referencePixelsPerUnit = 100f;

        return canvas;
    }

    private TMP_Text CriarTextoDurabilidade(Transform parent, string nome, Vector2 posicao, TextAlignmentOptions alinhamento)
    {
        GameObject textoObj = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textoObj.transform.SetParent(parent, false);

        RectTransform rect = textoObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = new Vector2(140f, 30f);

        TextMeshProUGUI texto = textoObj.GetComponent<TextMeshProUGUI>();
        texto.text = "0/0";
        texto.fontSize = 24f;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 10f;
        texto.fontSizeMax = 26f;
        texto.alignment = alinhamento;
        texto.raycastTarget = false;
        texto.color = corTextoA;

        return texto;
    }

    private bool ExisteSeparadorDurabilidade(Transform raiz)
    {
        if (raiz == null)
            return false;

        TMP_Text[] textos = raiz.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textos.Length; i++)
        {
            TMP_Text texto = textos[i];
            if (texto == null)
                continue;

            string nome = NormalizarNomeCompleto(texto.gameObject.name);
            if (nome == "separador" || texto.text.Trim() == "/")
                return true;
        }

        return false;
    }

    private void RotacionarTextoParaCamera()
    {
        Transform raizTexto = ObterRaizTextoDurabilidade();
        if (raizTexto == null)
            return;

        Camera cameraAlvo = ObterCameraTextoDurabilidade();
        if (cameraAlvo == null)
            return;

        Transform cameraTransform = cameraAlvo.transform;
        raizTexto.LookAt(
            raizTexto.position + cameraTransform.rotation * Vector3.forward,
            cameraTransform.rotation * Vector3.up);
    }

    private Transform ObterRaizTextoDurabilidade()
    {
        if (raizTextoDurabilidade != null)
            return raizTextoDurabilidade;

        EncontrarTextosDurabilidadeSeNecessario(true);

        if (textoValorAtual != null && textoValorAtual.transform.parent != null)
            raizTextoDurabilidade = textoValorAtual.transform.parent;
        else if (textoValorTotal != null && textoValorTotal.transform.parent != null)
            raizTextoDurabilidade = textoValorTotal.transform.parent;

        return raizTextoDurabilidade;
    }

    private Camera ObterCameraTextoDurabilidade()
    {
        if (Camera.main != null)
            return Camera.main;

        return FindFirstObjectByType<Camera>();
    }

    private Transform ObterMaoDoCollider(Collider other)
    {
        if (other == null || ColliderPertenceAoProprioArco(other))
            return null;

        XRBaseInteractor interactor = other.GetComponentInParent<XRBaseInteractor>();
        if (interactor != null && !TransformPertenceAoProprioArco(interactor.transform))
            return interactor.transform;

        Transform atual = other.transform;
        while (atual != null)
        {
            if (TransformPertenceAoProprioArco(atual))
                return null;

            string nome = NormalizarNome(atual.name);
            bool pareceMao =
                nome.Contains("left") ||
                nome.Contains("right") ||
                nome.Contains("esquerda") ||
                nome.Contains("direita") ||
                nome.Contains("hand") ||
                nome.Contains("controller") ||
                nome.Contains("mao");

            if (pareceMao)
                return atual;

            atual = atual.parent;
        }

        return null;
    }

    private bool ColliderPertenceAoProprioArco(Collider other)
    {
        return other != null && TransformPertenceAoProprioArco(other.transform);
    }

    private bool TransformPertenceAoProprioArco(Transform alvo)
    {
        return alvo != null && (alvo == transform || alvo.IsChildOf(transform));
    }

    private bool MaoValidaParaPuxarCorda(Transform mao)
    {
        return mao != null &&
               attachDuasMao != null &&
               attachDuasMao.ArcoEstaSegurado() &&
               attachDuasMao.MaoPodePuxarCorda(mao);
    }

    private InputActionReference ObterAcaoGripParaMao(Transform mao)
    {
        if (mao == null || attachDuasMao == null)
            return null;

        Transform atual = mao;

        while (atual != null)
        {
            string nome = NormalizarNome(atual.name);

            if (nome.Contains("left") || nome.Contains("esquerda"))
                return acaoGripMaoEsquerda;

            if (nome.Contains("right") || nome.Contains("direita"))
                return acaoGripMaoDireita;

            atual = atual.parent;
        }

        if (attachDuasMao.EstaSeguradoPelaDireita())
            return acaoGripMaoEsquerda;

        if (attachDuasMao.EstaSeguradoPelaEsquerda())
            return acaoGripMaoDireita;

        return null;
    }

    private static float LerValorAcaoGrip(InputActionReference acaoGrip)
    {
        if (acaoGrip == null || acaoGrip.action == null)
            return 0f;

        if (!acaoGrip.action.enabled)
            acaoGrip.action.Enable();

        try
        {
            return acaoGrip.action.ReadValue<float>();
        }
        catch
        {
            return acaoGrip.action.IsPressed() ? 1f : 0f;
        }
    }

    private void PrepararFlecha()
    {
        if (estaNoInventario)
            return;

        Transform pontoFlechaAtual = ObterPontoFlechaAtual();
        if (flechaPreparada != null || pontoFlechaAtual == null)
            return;

        if (!TentarPrepararPrefabFlechaParaDisparo(out GameObject prefabParaPreparar) || prefabParaPreparar == null)
            return;

        flechaPreparada = Instantiate(prefabParaPreparar, pontoFlechaAtual.position, pontoFlechaAtual.rotation, pontoFlechaAtual);
        GarantirMalhaFlechaVisivel(flechaPreparada);
        AplicarPoseFlechaNoArco(flechaPreparada, pontoFlechaAtual);

        GarantirComponenteFlecha(flechaPreparada);
        ConfigurarDonoFlecha(flechaPreparada);

        Rigidbody rb = flechaPreparada.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Collider[] colliders = flechaPreparada.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private void CancelarPuxada()
    {
        if (!cordaSendoPuxada && flechaPreparada == null)
            return;

        LimparFlechaPreparada(true);
        ResetarCorda();
    }

    private void ManterFlechaPreparadaNoPonto()
    {
        if (estaNoInventario)
            return;

        Transform pontoFlechaAtual = ObterPontoFlechaAtual();
        if (flechaPreparada == null || pontoFlechaAtual == null)
            return;

        AplicarPoseFlechaNoArco(flechaPreparada, pontoFlechaAtual);
    }

    private void AplicarPoseFlechaNoArco(GameObject flecha, Transform pontoFlechaAtual)
    {
        if (flecha == null || pontoFlechaAtual == null)
            return;

        Transform pontoPose = pontoVisualFlechaNoArco != null ? pontoVisualFlechaNoArco : pontoFlechaAtual;
        Transform flechaTransform = flecha.transform;

        flechaTransform.SetParent(pontoPose, false);
        flechaTransform.localPosition = usarOffsetFlechaNoArco ? offsetLocalPosicaoFlechaNoArco : Vector3.zero;
        flechaTransform.localRotation = usarOffsetFlechaNoArco
            ? Quaternion.Euler(offsetLocalRotacaoFlechaNoArco)
            : Quaternion.identity;
    }

    private void AplicarRotacaoFlechaLancada(Transform flechaTransform, Vector3 direcao, Transform pontoFlechaAtual)
    {
        if (flechaTransform == null)
            return;

        Quaternion offsetRotacao = usarOffsetFlechaNoArco
            ? Quaternion.Euler(offsetLocalRotacaoFlechaNoArco)
            : Quaternion.identity;

        if (VetorValido(direcao) && direcao.sqrMagnitude > 0.0001f)
        {
            Vector3 eixoCima = pontoFlechaAtual != null ? pontoFlechaAtual.up : transform.up;
            if (!VetorValido(eixoCima) || eixoCima.sqrMagnitude <= 0.0001f)
                eixoCima = Vector3.up;

            flechaTransform.rotation = Quaternion.LookRotation(direcao.normalized, eixoCima.normalized) * offsetRotacao;
            return;
        }

        if (pontoFlechaAtual != null)
            flechaTransform.rotation = pontoFlechaAtual.rotation * offsetRotacao;
    }

    public void EquiparTipoFlecha(string idTipoFlecha)
    {
        string id = NormalizarIdFlecha(idTipoFlecha);
        if (string.IsNullOrWhiteSpace(id))
        {
            idTipoFlechaEquipada = string.Empty;
            prefabFlechaEquipada = null;
            AtualizarDiagnosticoFlechaEquipada();
            return;
        }

        EncontrarInventarioFlechasSeNecessario();

        GameObject prefabEncontrado = ObterPrefabConfiguradoPorId(id);
        if (prefabEncontrado == null && inventarioFlechas != null)
            inventarioFlechas.TentarObterPrefabFlecha(id, out prefabEncontrado);

        if (prefabEncontrado == null)
            return;

        idTipoFlechaEquipada = id;
        prefabFlechaEquipada = prefabEncontrado;
        AtualizarDiagnosticoFlechaEquipada();
    }

    public string ObterIdTipoFlechaEquipada()
    {
        return idTipoFlechaEquipada;
    }

    public int ObterQuantidadeFlechaEquipada()
    {
        EncontrarInventarioFlechasSeNecessario(false);
        return inventarioFlechas != null ? inventarioFlechas.ObterQuantidadeTotal(idTipoFlechaEquipada) : 0;
    }

    public bool TemFlechaParaDisparar()
    {
        return TentarPrepararPrefabFlechaParaDisparo(out _);
    }

    private bool TentarPrepararPrefabFlechaParaDisparo(out GameObject prefabParaDisparo)
    {
        prefabParaDisparo = null;

        if (permitirDisparoSemFlechaParaTeste)
        {
            prefabParaDisparo = prefabFlechaEquipada != null ? prefabFlechaEquipada : prefabFlecha;
            return prefabParaDisparo != null;
        }

        EncontrarInventarioFlechasSeNecessario();

        if (inventarioFlechas == null)
            return false;

        string id = NormalizarIdFlecha(idTipoFlechaEquipada);
        bool precisaEscolherOutra = string.IsNullOrWhiteSpace(id) || !inventarioFlechas.TemFlecha(id);
        if (precisaEscolherOutra)
        {
            if (!inventarioFlechas.TentarEquiparPrimeiraFlechaDisponivel(out id, out GameObject prefabEncontrado))
                return false;

            idTipoFlechaEquipada = id;
            GameObject prefabConfigurado = ObterPrefabConfiguradoPorId(id);
            prefabFlechaEquipada = prefabConfigurado != null ? prefabConfigurado : prefabEncontrado;
        }

        if (!inventarioFlechas.TemFlecha(idTipoFlechaEquipada))
            return false;

        prefabParaDisparo = ObterPrefabConfiguradoPorId(idTipoFlechaEquipada);
        if (prefabParaDisparo == null)
            prefabParaDisparo = prefabFlechaEquipada;

        if (prefabParaDisparo == null)
            inventarioFlechas.TentarObterPrefabFlecha(idTipoFlechaEquipada, out prefabParaDisparo);

        if (prefabParaDisparo == null)
            prefabParaDisparo = ObterPrefabConfiguradoPorId(idTipoFlechaEquipada);

        if (prefabParaDisparo != null)
            prefabFlechaEquipada = prefabParaDisparo;

        AtualizarDiagnosticoFlechaEquipada();
        return prefabParaDisparo != null;
    }

    private bool ConsumirFlechaEquipadaAposDisparo()
    {
        if (permitirDisparoSemFlechaParaTeste || !consumirFlechaDoInventario)
            return true;

        EncontrarInventarioFlechasSeNecessario();
        if (inventarioFlechas == null)
            return false;

        bool consumiu = inventarioFlechas.ConsumirUmaFlecha(idTipoFlechaEquipada);
        AtualizarDiagnosticoFlechaEquipada();
        return consumiu;
    }

    private void AtualizarEntradaSeletorFlechas()
    {
        if (!BotaoAbrirSeletorPressionado() || !PodeAbrirSeletorFlechas())
            return;

        EncontrarSelecionadorFlechasSeNecessario();
        if (selecionadorFlechasUI != null)
            selecionadorFlechasUI.Alternar(this, inventarioFlechas);
    }

    private bool PodeAbrirSeletorFlechas()
    {
        if (estaNoInventario || Quebrado)
            return false;

        if (!abrirSeletorSomenteSegurandoArco)
            return true;

        return attachDuasMao != null && attachDuasMao.ArcoEstaSegurado();
    }

    private bool BotaoAbrirSeletorPressionado()
    {
        if (acaoAbrirSeletorFlechas == null || acaoAbrirSeletorFlechas.action == null)
            return false;

        try
        {
            return acaoAbrirSeletorFlechas.action.WasPressedThisFrame();
        }
        catch
        {
            return false;
        }
    }

    private void AtualizarDiagnosticoFlechaEquipada()
    {
        diagnosticoFlechaEquipada = string.IsNullOrWhiteSpace(idTipoFlechaEquipada)
            ? "Nenhuma"
            : idTipoFlechaEquipada;

        EncontrarInventarioFlechasSeNecessario(false);
        diagnosticoQuantidadeFlechaEquipada = inventarioFlechas != null
            ? inventarioFlechas.ObterQuantidadeTotal(idTipoFlechaEquipada)
            : 0;

        diagnosticoTemFlechaEquipada = permitirDisparoSemFlechaParaTeste
            ? (prefabFlechaEquipada != null || prefabFlecha != null)
            : prefabFlechaEquipada != null && diagnosticoQuantidadeFlechaEquipada > 0;
    }

    private void EncontrarInventarioFlechasSeNecessario(bool criarSeFaltar = true)
    {
        if (inventarioFlechas != null)
            return;

        inventarioFlechas = FindFirstObjectByType<InventarioFlechas>();
        if (inventarioFlechas != null)
            return;

        InventarioVR inventario = FindFirstObjectByType<InventarioVR>();
        if (inventario == null)
            return;

        inventarioFlechas = inventario.GetComponent<InventarioFlechas>();
        if (inventarioFlechas == null && criarSeFaltar && Application.isPlaying)
            inventarioFlechas = inventario.gameObject.AddComponent<InventarioFlechas>();
    }

    private void EncontrarSelecionadorFlechasSeNecessario()
    {
        if (selecionadorFlechasUI != null)
            return;

        SelecionadorFlechasUI[] seletores = FindObjectsByType<SelecionadorFlechasUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (seletores != null && seletores.Length > 0)
            selecionadorFlechasUI = seletores[0];
    }

    private GameObject ObterPrefabConfiguradoPorId(string idTipoFlecha)
    {
        string id = NormalizarIdFlecha(idTipoFlecha);
        if (string.IsNullOrWhiteSpace(id) || prefabsFlechasDisponiveis == null)
            return null;

        for (int i = 0; i < prefabsFlechasDisponiveis.Count; i++)
        {
            FlechaPrefabConfigurada configuracao = prefabsFlechasDisponiveis[i];
            if (configuracao == null || configuracao.prefabFlecha == null)
                continue;

            if (string.Equals(NormalizarIdFlecha(configuracao.idTipoFlecha), id, StringComparison.Ordinal))
                return configuracao.prefabFlecha;
        }

        return null;
    }

    private static string NormalizarIdFlecha(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }

    private void GarantirMalhaFlechaVisivel(GameObject flecha)
    {
        diagnosticoRenderersFlechaDisparo = 0;
        diagnosticoMalhaFlechaDisparoVisivel = false;

        if (flecha == null)
            return;

        flecha.SetActive(true);

        Renderer[] renderers = flecha.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !RendererEhMalhaFlecha(renderer))
                continue;

            AtivarHierarquiaAte(flecha.transform, renderer.transform);
            renderer.enabled = true;
            diagnosticoRenderersFlechaDisparo++;
        }

        diagnosticoMalhaFlechaDisparoVisivel = diagnosticoRenderersFlechaDisparo > 0;
    }

    private static bool RendererEhMalhaFlecha(Renderer renderer)
    {
        return renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
    }

    private static void AtivarHierarquiaAte(Transform raiz, Transform alvo)
    {
        Transform atual = alvo;
        while (atual != null)
        {
            atual.gameObject.SetActive(true);

            if (atual == raiz)
                return;

            atual = atual.parent;
        }
    }

    private void LimparFlechaPreparada(bool destruir)
    {
        if (flechaPreparada == null)
            return;

        if (destruir)
            Destroy(flechaPreparada);

        flechaPreparada = null;
    }

    private void ReativarCollidersDaFlecha(GameObject flecha)
    {
        Collider[] colliders = flecha.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = true;
    }

    private Transform ObterPontoFlechaAtual()
    {
        AtualizarDiagnosticoMaoSegurando();

        if (usarPontosDisparoPorMao && attachDuasMao != null)
        {
            if (attachDuasMao.EstaSeguradoPelaDireita() && pontoFlechaDireita != null)
            {
                diagnosticoPontoFlechaAtual = pontoFlechaDireita;
                return pontoFlechaDireita;
            }

            if (attachDuasMao.EstaSeguradoPelaEsquerda() && pontoFlechaEsquerda != null)
            {
                diagnosticoPontoFlechaAtual = pontoFlechaEsquerda;
                return pontoFlechaEsquerda;
            }
        }

        if (pontoFlecha != null)
        {
            diagnosticoPontoFlechaAtual = pontoFlecha;
            return pontoFlecha;
        }

        if (pontoFlechaEsquerda != null)
        {
            diagnosticoPontoFlechaAtual = pontoFlechaEsquerda;
            return pontoFlechaEsquerda;
        }

        diagnosticoPontoFlechaAtual = pontoFlechaDireita;
        return pontoFlechaDireita;
    }

    private Transform ObterPontoDirecaoDisparoAtual()
    {
        AtualizarDiagnosticoMaoSegurando();

        if (usarPontosDisparoPorMao && attachDuasMao != null)
        {
            if (attachDuasMao.EstaSeguradoPelaDireita() && pontoDirecaoDisparoDireita != null)
            {
                diagnosticoPontoDirecaoAtual = pontoDirecaoDisparoDireita;
                return pontoDirecaoDisparoDireita;
            }

            if (attachDuasMao.EstaSeguradoPelaEsquerda() && pontoDirecaoDisparoEsquerda != null)
            {
                diagnosticoPontoDirecaoAtual = pontoDirecaoDisparoEsquerda;
                return pontoDirecaoDisparoEsquerda;
            }
        }

        if (pontoDirecaoDisparo != null)
        {
            diagnosticoPontoDirecaoAtual = pontoDirecaoDisparo;
            return pontoDirecaoDisparo;
        }

        if (pontoDirecaoDisparoEsquerda != null)
        {
            diagnosticoPontoDirecaoAtual = pontoDirecaoDisparoEsquerda;
            return pontoDirecaoDisparoEsquerda;
        }

        diagnosticoPontoDirecaoAtual = pontoDirecaoDisparoDireita;
        return pontoDirecaoDisparoDireita;
    }

    private bool ObterOrigemEDirecaoDisparo(out Vector3 origem, out Vector3 direcao)
    {
        Transform pontoFlechaAtual = ObterPontoFlechaAtual();
        Transform pontoDirecaoAtual = ObterPontoDirecaoDisparoAtual();

        origem = pontoFlechaAtual != null ? pontoFlechaAtual.position : transform.position;
        direcao = Vector3.zero;

        if (pontoDirecaoAtual != null)
        {
            Vector3 direcaoEntrePontos = pontoDirecaoAtual.position - origem;
            if (direcaoEntrePontos.sqrMagnitude > 0.0001f)
                direcao = direcaoEntrePontos.normalized;
            else if (VetorValido(pontoDirecaoAtual.forward) && pontoDirecaoAtual.forward.sqrMagnitude > 0.0001f)
                direcao = pontoDirecaoAtual.forward.normalized;
        }

        if (direcao.sqrMagnitude <= 0.0001f &&
            pontoFlechaAtual != null &&
            VetorValido(pontoFlechaAtual.forward) &&
            pontoFlechaAtual.forward.sqrMagnitude > 0.0001f)
        {
            direcao = pontoFlechaAtual.forward.normalized;
        }

        if (direcao.sqrMagnitude <= 0.0001f &&
            VetorValido(transform.forward) &&
            transform.forward.sqrMagnitude > 0.0001f)
        {
            direcao = transform.forward.normalized;
        }

        return VetorValido(origem) && VetorValido(direcao) && direcao.sqrMagnitude > 0.0001f;
    }

    private void AtualizarDiagnosticoMaoSegurando()
    {
        diagnosticoSeguradoPelaDireita = attachDuasMao != null && attachDuasMao.EstaSeguradoPelaDireita();
        diagnosticoSeguradoPelaEsquerda = attachDuasMao != null && attachDuasMao.EstaSeguradoPelaEsquerda();
    }

    private void EncontrarPontosFlechaPorMaoSeNecessario()
    {
        if (pontoFlechaEsquerda == null)
            pontoFlechaEsquerda = EncontrarTransformPorNomes("ponto flecha e", "pontoflechae", "ponto flecha esquerda", "pontoflechaesquerda");

        if (pontoFlechaDireita == null)
            pontoFlechaDireita = EncontrarTransformPorNomes("ponto flecha d", "pontoflechad", "ponto flecha direita", "pontoflechadireita");

        if (pontoDirecaoDisparoEsquerda == null)
            pontoDirecaoDisparoEsquerda = EncontrarTransformPorNomes("me dis", "medis", "ponto direcao esquerda", "pontodirecaoesquerda", "ponto direcao disparo esquerda", "pontodirecaodisparoesquerda");

        if (pontoDirecaoDisparoDireita == null)
            pontoDirecaoDisparoDireita = EncontrarTransformPorNomes("md dis", "mddis", "ponto direcao direita", "pontodirecaodireita", "ponto direcao disparo direita", "pontodirecaodisparodireita");
    }

    private Transform EncontrarTransformPorNomes(params string[] nomes)
    {
        Transform[] filhos = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < filhos.Length; i++)
        {
            Transform filho = filhos[i];
            if (filho == null)
                continue;

            string nomeNormalizado = NormalizarNomeCompleto(filho.name);
            for (int j = 0; j < nomes.Length; j++)
            {
                if (string.Equals(nomeNormalizado, NormalizarNomeCompleto(nomes[j]), StringComparison.Ordinal))
                    return filho;
            }
        }

        return null;
    }

    private Vector3 CalcularDirecaoDisparo()
    {
        if (ObterOrigemEDirecaoDisparo(out _, out Vector3 direcao))
            return direcao;

        return transform.forward;
    }

    private float CalcularMultiplicadorDano()
    {
        if (energiaExtraMaxima <= 0f)
            return 1f;

        return 1f + (energiaAcumulada / energiaExtraMaxima) * multiplicadorDanoEnergia;
    }

    private bool TentarChamarDispararFlecha(GameObject flecha, Vector3 direcao, float forcaFinal)
    {
        MonoBehaviour[] componentes = flecha.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < componentes.Length; i++)
        {
            MonoBehaviour componente = componentes[i];
            if (componente == null)
                continue;

            MethodInfo metodo = componente.GetType().GetMethod(
                "Disparar",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Vector3), typeof(float) },
                null
            );

            if (metodo == null)
                continue;

            try
            {
                object resultado = metodo.Invoke(componente, new object[] { direcao, forcaFinal });
                return resultado is bool disparou ? disparou : true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private void TentarAplicarMultiplicadorDano(GameObject flecha, float multiplicadorDano)
    {
        string[] nomesMetodos =
        {
            "DefinirMultiplicadorDano",
            "DefinirMultiplicadorDeDano",
            "SetMultiplicadorDano",
            "AplicarMultiplicadorDano",
            "ConfigurarMultiplicadorDano"
        };

        MonoBehaviour[] componentes = flecha.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < componentes.Length; i++)
        {
            MonoBehaviour componente = componentes[i];
            if (componente == null)
                continue;

            Type tipo = componente.GetType();

            for (int j = 0; j < nomesMetodos.Length; j++)
            {
                MethodInfo metodo = tipo.GetMethod(
                    nomesMetodos[j],
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(float) },
                    null
                );

                if (metodo == null)
                    continue;

                try
                {
                    metodo.Invoke(componente, new object[] { multiplicadorDano });
                    return;
                }
                catch
                {
                    continue;
                }
            }
        }
    }

    private static void GarantirComponenteFlecha(GameObject flecha)
    {
        if (flecha == null)
            return;

        if (flecha.GetComponentInChildren<Flecha>(true) != null)
            return;

        flecha.AddComponent<Flecha>();
    }

    private void ConfigurarDonoFlecha(GameObject flecha)
    {
        if (flecha == null)
            return;

        GameObject donoFlecha = ObterDonoFlecha();
        Flecha[] componentesFlecha = flecha.GetComponentsInChildren<Flecha>(true);

        for (int i = 0; i < componentesFlecha.Length; i++)
        {
            if (componentesFlecha[i] != null)
                componentesFlecha[i].DefinirDono(donoFlecha);
        }
    }

    private void MarcarFlechaComoLancada(GameObject flecha)
    {
        if (flecha == null)
            return;

        GameObject donoFlecha = ObterDonoFlecha();
        if (donoFlecha == null)
            donoFlecha = gameObject;

        Flecha[] componentesFlecha = flecha.GetComponentsInChildren<Flecha>(true);
        for (int i = 0; i < componentesFlecha.Length; i++)
        {
            if (componentesFlecha[i] != null)
                componentesFlecha[i].MarcarComoLancada(donoFlecha);
        }
    }

    private GameObject ObterDonoFlecha()
    {
        Transform maoQueSegura = attachDuasMao != null ? attachDuasMao.ObterMaoQueSegura() : null;
        Transform player = EncontrarPlayerDonoAPartirDoTransform(maoQueSegura);

        if (player != null)
            return player.gameObject;

        return maoQueSegura != null && maoQueSegura.root != null
            ? maoQueSegura.root.gameObject
            : null;
    }

    private Transform EncontrarPlayerDonoAPartirDoTransform(Transform origem)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (string.Equals(atual.tag, "Player", StringComparison.Ordinal))
                return atual;

            atual = atual.parent;
        }

        return null;
    }

    private static bool DispararPorRigidbody(GameObject flecha, Vector3 direcao, float forcaFinal)
    {
        Rigidbody rb = flecha.GetComponent<Rigidbody>();
        if (rb == null)
            rb = flecha.GetComponentInChildren<Rigidbody>();

        if (rb == null)
            return false;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(direcao * forcaFinal, ForceMode.Impulse);
        return true;
    }

    private void TocarSom(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip);
    }

    private static bool MaoPertenceAoTransform(Transform mao, Transform alvo)
    {
        if (mao == null || alvo == null)
            return false;

        if (mao == alvo)
            return true;

        return mao.IsChildOf(alvo) || alvo.IsChildOf(mao);
    }

    private void NormalizarValores()
    {
        distanciaMaximaPuxada = Mathf.Max(0.001f, distanciaMaximaPuxada);
        distanciaMinimaParaDisparo = Mathf.Clamp(distanciaMinimaParaDisparo, 0f, distanciaMaximaPuxada);
        percentualParaAcumularEnergia = Mathf.Clamp01(percentualParaAcumularEnergia);

        forcaMinimaDisparo = Mathf.Max(0f, forcaMinimaDisparo);
        forcaMaximaDisparo = Mathf.Max(forcaMinimaDisparo, forcaMaximaDisparo);
        energiaExtraMaxima = Mathf.Max(0f, energiaExtraMaxima);
        velocidadeAcumuloEnergia = Mathf.Max(0f, velocidadeAcumuloEnergia);
        multiplicadorDanoEnergia = Mathf.Max(0f, multiplicadorDanoEnergia);
        suavidadeCurvatura = Mathf.Max(0f, suavidadeCurvatura);
        multiplicadorDistanciaSeguranca = Mathf.Max(1.01f, multiplicadorDistanciaSeguranca);
        valorMinimoGripParaSegurar = Mathf.Clamp01(valorMinimoGripParaSegurar);
        larguraLinhaCorda = Mathf.Max(0.001f, larguraLinhaCorda);
        quantidadePontosMira = Mathf.Clamp(quantidadePontosMira, 4, 64);
        intervaloTempoMira = Mathf.Max(0.01f, intervaloTempoMira);
        distanciaMaximaMira = Mathf.Max(1f, distanciaMaximaMira);
        larguraLinhaMira = Mathf.Max(0.001f, larguraLinhaMira);
        percentualMinimoParaMostrarMira = Mathf.Clamp01(percentualMinimoParaMostrarMira);
        multiplicadorVelocidadeMira = Mathf.Max(0.01f, multiplicadorVelocidadeMira);

        durabilidadeMaxima = Mathf.Max(1f, durabilidadeMaxima);
        durabilidadeAtual = Mathf.Clamp(durabilidadeAtual, 0f, durabilidadeMaxima);
        desgastePorDisparo = Mathf.Max(0f, desgastePorDisparo);
        velocidadePiscarTexto = Mathf.Max(0f, velocidadePiscarTexto);
        arcoQuebrado = durabilidadeAtual <= 0f;
    }

    private static string NormalizarNome(string nome)
    {
        return string.IsNullOrWhiteSpace(nome)
            ? string.Empty
            : nome.Trim().ToLowerInvariant();
    }

    private static string NormalizarNomeCompleto(string texto)
    {
        return string.IsNullOrWhiteSpace(texto)
            ? string.Empty
            : texto.Trim().ToLowerInvariant()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);
    }

    private static Transform ProcurarTransformComTagPermitida(Transform origem, string[] tagsPermitidas)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (TagEstaPermitida(atual.tag, tagsPermitidas))
                return atual;

            atual = atual.parent;
        }

        return null;
    }

    private static bool TagEstaPermitida(string tagAtual, string[] tagsPermitidas)
    {
        if (string.IsNullOrWhiteSpace(tagAtual) || tagsPermitidas == null)
            return false;

        for (int i = 0; i < tagsPermitidas.Length; i++)
        {
            string tagPermitida = tagsPermitidas[i];
            if (string.IsNullOrWhiteSpace(tagPermitida))
                continue;

            if (string.Equals(tagAtual, tagPermitida.Trim(), StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
