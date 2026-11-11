using TMPro;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SlimeIA : MonoBehaviour, IDano
{
    private enum EstadoSlime
    {
        Parado,
        Patrulhando,
        Observando,
        Alerta,
        Perseguindo,
        Atacando,
        TomandoDano,
        Morto
    }

    [Serializable]
    public class SpawnNormalConfig
    {
        public GameObject prefab;
        [HideInInspector] public float chance;
        public int quantidadeMinima = 1;
        public int quantidadeMaxima = 1;
        public Vector3 offsetSpawn;
    }

    [Serializable]
    public class SpawnMissaoConfig
    {
        public GameObject prefab;
        [HideInInspector] public float chance;
        public int quantidadeMinima = 1;
        public int quantidadeMaxima = 1;
        public Vector3 offsetSpawn;
        public string idMissao;
        public string idObjetivo;
        public bool exigirMissaoAtiva = true;
    }

    [SerializeField] private int vidaMaxima = 5;
    [SerializeField, Min(1)] private int nivelMonstro = 1;
    [SerializeField] private TMP_Text textoNivel;
    [SerializeField] private bool atributosPorNivel = true;
    [SerializeField] private string nomeMonstro;
    [SerializeField] private TMP_Text textoNomeMonstro;
    [SerializeField] private int vidaAtual = 5;
    [SerializeField] private float tempoAnimacaoDano = 0.35f;
    [Tooltip("Tempo em segundos entre a morte e a destruição do inimigo.")]
    [Min(0f)] public float tempoParaMorrer = 3f;

    [SerializeField] private Image imagemBarraVida;
    [SerializeField] private Canvas canvasVida;
    [SerializeField] private bool buscarBarraVidaAutomaticamente = true;
    [SerializeField] private bool esconderCanvasAoMorrer = true;

    [SerializeField] private Transform[] pontosPatrulha;
    [SerializeField] private float velocidadePatrulha = 1.2f;
    [SerializeField] private float distanciaChegadaPonto = 0.35f;
    [SerializeField] private float tempoObservando = 2f;
    [SerializeField] private bool gerarPontosAutomaticamente = true;
    [SerializeField] private int quantidadePontosAuto = 4;
    [SerializeField] private float raioPatrulhaAuto = 4f;

    [SerializeField] private string tagPlayer = "Player";
    [SerializeField] private float raioCampoVisao = 8f;
    [SerializeField, Range(1f, 360f)] private float anguloCampoVisao = 120f;
    [SerializeField] private LayerMask layersObstaculo;
    [SerializeField] private float distanciaIniciarPerseguicao = 5f;

    [SerializeField] private float velocidadePerseguicao = 2.4f;
    [SerializeField] private float distanciaAtaque = 1.3f;

    [SerializeField, Min(0)] private int danoAtaque = 3;

    public int DanoAtaque
    {
        get => danoAtaque;
        set => danoAtaque = Mathf.Max(0, value);
    }

    [SerializeField] private Transform pontoOrigemAtaque;
    [SerializeField] private float alturaOrigemAtaque = 0.7f;
    [SerializeField] private float raioAtaque = 0.35f;
    [SerializeField] private float alcanceAtaque = 1.2f;
    [SerializeField] private LayerMask layersAtaque = ~0;

    [SerializeField] private float distanciaInvestida = 1.5f;
    [SerializeField] private float alturaInvestida = 0.6f;
    [SerializeField] private float duracaoInvestida = 0.35f;
    [SerializeField] private float cooldownAtaque = 1.2f;
    [SerializeField] private float raioVerificacaoColisaoInvestida = 0.35f;
    [SerializeField] private LayerMask layersBloqueioInvestida = ~0;
    [SerializeField] private float margemParedeInvestida = 0.15f;


    [SerializeField] private string idRespawnMonstro = "SlimeVerde";

    [Tooltip("Exibe guias de visão, perseguição e ataque na janela Scene do Unity.")]
    [SerializeField] private bool desenharGizmos = true;
    [Tooltip("Exibe as guias mesmo quando o inimigo não está selecionado.")]
    [SerializeField] private bool desenharGizmosSempre = true;
    [SerializeField] private float alturaOlhos = 0.8f;
    [SerializeField] private Transform pontoOlhos;

    [SerializeField] private SpawnNormalConfig[] prefabsSpawnNormal;
    // Valor negativo identifica prefabs antigos, nos quais Nada era a chance restante.
    [SerializeField, HideInInspector] private float chanceNada = -1f;

    [SerializeField] private SpawnMissaoConfig[] prefabsSpawnMissao;
    [SerializeField, HideInInspector] private float chanceNadaMissao = -1f;

    [SerializeField] private bool darReinAoMorrer = true;
    [SerializeField] private string reinMinimo = "0.01";
    [SerializeField] private string reinMaximo = "0.05";
    [SerializeField, Range(0, 5)] private int casasDecimaisSorteioRein = 5;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somAndar;
    [SerializeField] private AudioClip somAtacar;
    [SerializeField] private AudioClip somDano;
    [SerializeField] private AudioClip somMorrer;
    [SerializeField] private float volumeAndar = 1f;
    [SerializeField] private float volumeAtacar = 1f;
    [SerializeField] private float volumeDano = 1f;
    [SerializeField] private float volumeMorrer = 1f;

    [SerializeField] private SlimeMovimento configuracaoMovimento = new SlimeMovimento();
    [SerializeField] private SlimeDeteccao configuracaoDeteccao = new SlimeDeteccao();
    [SerializeField] private SlimeAnimacao configuracaoAnimacao = new SlimeAnimacao();
    private SlimeAnimacao animacao;
    [SerializeField] private Rigidbody rb;
    [Tooltip("Usa a malha de navegação quando há um NavMeshAgent ativo sobre ela.")]
    [SerializeField] private bool usarNavMeshAgent = true;
    private NavMeshAgent agent;
    private CharacterController characterController;
    private SlimeMovimento movimentoVerde;
    private SlimeDeteccao deteccaoVerde;
    private RaycastHit[] hitsVerde;
    private Collider[] sobreposicoesVerde;
    private readonly List<GameObject> pontosAutoProprios = new List<GameObject>();
    internal bool UsaMovimentoVerde => movimentoVerde != null;
    private Vector3 PosicaoAlvo(Transform alvo) => deteccaoVerde != null ? deteccaoVerde.PosicaoJogador(alvo) : alvo.position;


    private const float VelocidadeRotacao = 8f;
    private const float IntervaloBuscaPlayer = 0.35f;

    private EstadoSlime estadoAtual = EstadoSlime.Parado;
    private EstadoSlime estadoAntesDano = EstadoSlime.Parado;
    private Transform alvoPlayer;
    private int indicePatrulha = -1;
    private float tempoRestanteObservando;
    private float proximaBuscaPlayer;
    private float proximoAtaque;
    private Coroutine rotinaDano;
    private Coroutine rotinaInvestidaAtaque;
    private bool impactoAtaqueResolvido;
    private bool investidaAtaqueEmAndamento;
    private bool agentPausadoPelaInvestida;
    private bool agentUpdatePositionAntesInvestida;
    private bool somAndarTocando;
    private bool somMorteTocado;

    private bool morto;
    private bool experienciaJaEntregue;
    private bool recompensaReinJaPaga;
    private Transform ultimoPlayerResponsavel;
    private ExperienciaInimigo experienciaInimigo;
    private GerenciadorMissoes gerenciadorMissoes;
    public int VidaAtual => vidaAtual;
    public int VidaMaxima => vidaMaxima;
    public bool Morto => morto;

    public float ObterDano()
    {
        return danoAtaque;
    }

    public GameObject ObterDono()
    {
        return gameObject;
    }

    public Transform[] ObterPontosPatrulha()
    {
        return pontosPatrulha;
    }

    public void DefinirPontosPatrulha(Transform[] novosPontos)
    {
        pontosPatrulha = novosPontos;
        indicePatrulha = -1;
        EscolherProximoPontoAleatorio();
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (configuracaoMovimento.Habilitado)
        {
            var capsula = GetComponent<CapsuleCollider>();
            if (rb != null && capsula != null)
            {
                movimentoVerde = configuracaoMovimento;
                movimentoVerde.Inicializar(rb, capsula, transform);
                hitsVerde = new RaycastHit[32];
                sobreposicoesVerde = new Collider[16];
            }
        }

        if (configuracaoDeteccao.Habilitado)
        {
            deteccaoVerde = configuracaoDeteccao;
            deteccaoVerde.Inicializar(transform);
        }

        if (configuracaoAnimacao.Habilitado)
        {
            animacao = configuracaoAnimacao;
            animacao.Inicializar(GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true));
        }

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        experienciaInimigo = GetComponent<ExperienciaInimigo>();
        gerenciadorMissoes = FindFirstObjectByType<GerenciadorMissoes>();
        if (atributosPorNivel)
        {
            vidaMaxima = CalcularAtributoPorNivel(nivelMonstro, 5, 3);
            danoAtaque = CalcularAtributoPorNivel(nivelMonstro, 5, 3);
            vidaAtual = vidaMaxima;
        }
        else
    
        vidaAtual = Mathf.Clamp(vidaAtual <= 0 ? vidaMaxima : vidaAtual, 1, vidaMaxima);
        ConfigurarUIVida();
        AtualizarBarraVida();

        if (agent != null)
        {
            agent.updateRotation = false;
            agent.speed = velocidadePatrulha;
        }
    }

    private void FixedUpdate()
    {
        movimentoVerde?.AtualizarFisica();
    }

    private void OnEnable()
    {
        if (morto)
        {
            MudarEstado(EstadoSlime.Morto);
            return;
        }

        EscolherProximoPontoAleatorio();
        MudarEstado(QuantidadePontosValidos() > 0 ? EstadoSlime.Patrulhando : EstadoSlime.Parado);
    }

    private void Start()
    {
        if (morto || !gerarPontosAutomaticamente)
            return;

        if (QuantidadePontosValidos() > 0)
            return;

        GerarPontosDePatrulhaAutomaticos();
    }

    private void GerarPontosDePatrulhaAutomaticos()
    {
        int quantidade = Mathf.Max(1, quantidadePontosAuto);
        List<Transform> pontosValidos = new List<Transform>(quantidade);

        for (int i = 0; i < quantidade; i++)
        {
            float angulo = i * (360f / quantidade) * Mathf.Deg2Rad;
            Vector3 direcao = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
            Vector3 posicaoDesejada = transform.position + direcao * raioPatrulhaAuto;
            Vector3 posicaoFinal = posicaoDesejada;

            if (movimentoVerde != null && !movimentoVerde.PreservarArcoInvestida)
            {
                if (!movimentoVerde.TentarProjetarPontoPatrulha(posicaoDesejada, out posicaoFinal))
                    continue;
            }
            else if (NavMesh.SamplePosition(posicaoDesejada, out NavMeshHit hit, raioPatrulhaAuto, NavMesh.AllAreas))
                posicaoFinal = hit.position;

            GameObject ponto = new GameObject($"PontoAuto_{gameObject.name}_{i}")
            {
                hideFlags = HideFlags.HideInHierarchy
            };
            ponto.transform.position = posicaoFinal;
            pontosValidos.Add(ponto.transform);
            if (movimentoVerde != null) pontosAutoProprios.Add(ponto);
        }

        if (pontosValidos.Count == 0)
            return;

        pontosPatrulha = pontosValidos.ToArray();
        indicePatrulha = -1;
        EscolherProximoPontoAleatorio();

        if (estadoAtual != EstadoSlime.Patrulhando &&
            estadoAtual != EstadoSlime.Alerta &&
            estadoAtual != EstadoSlime.Perseguindo &&
            estadoAtual != EstadoSlime.Atacando &&
            estadoAtual != EstadoSlime.TomandoDano)
        {
            MudarEstado(EstadoSlime.Patrulhando);
        }
    }

    private void OnDestroy()
    {
        foreach (var ponto in pontosAutoProprios) if (ponto != null) Destroy(ponto);
    }
    private void OnDisable()
    {
        if (movimentoVerde != null) movimentoVerde.Parar();
        CancelarImpactoAtaquePendente();
        PararSomAndar();
    }

    private void OnValidate()
    {
        velocidadePatrulha = Mathf.Max(0f, velocidadePatrulha);
        velocidadePerseguicao = Mathf.Max(0f, velocidadePerseguicao);
        distanciaChegadaPonto = Mathf.Max(0.01f, distanciaChegadaPonto);
        tempoObservando = Mathf.Max(0f, tempoObservando);
        raioCampoVisao = Mathf.Max(0f, raioCampoVisao);
        anguloCampoVisao = Mathf.Clamp(anguloCampoVisao, 1f, 360f);
        distanciaIniciarPerseguicao = Mathf.Max(0f, distanciaIniciarPerseguicao);
        alturaOlhos = Mathf.Max(0f, alturaOlhos);
        distanciaAtaque = Mathf.Max(0f, distanciaAtaque);
        danoAtaque = Mathf.Max(0, danoAtaque);
        cooldownAtaque = Mathf.Max(0f, cooldownAtaque);
        alturaOrigemAtaque = Mathf.Max(0f, alturaOrigemAtaque);
        raioAtaque = Mathf.Max(0.01f, raioAtaque);
        alcanceAtaque = Mathf.Max(0.01f, alcanceAtaque);
        distanciaInvestida = Mathf.Max(0f, distanciaInvestida);
        alturaInvestida = Mathf.Max(0f, alturaInvestida);
        duracaoInvestida = Mathf.Max(0.01f, duracaoInvestida);
        raioVerificacaoColisaoInvestida = Mathf.Max(0.01f, raioVerificacaoColisaoInvestida);
        margemParedeInvestida = Mathf.Max(0f, margemParedeInvestida);
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        tempoAnimacaoDano = Mathf.Max(0f, tempoAnimacaoDano);
        tempoParaMorrer = Mathf.Max(0f, tempoParaMorrer);
        quantidadePontosAuto = Mathf.Max(1, quantidadePontosAuto);
        raioPatrulhaAuto = Mathf.Max(0.1f, raioPatrulhaAuto);
        casasDecimaisSorteioRein = Mathf.Clamp(casasDecimaisSorteioRein, 0, 5);
        NormalizarSpawnsNormais();
        NormalizarSpawnsMissao();
        volumeAndar = Mathf.Clamp01(volumeAndar);
        volumeAtacar = Mathf.Clamp01(volumeAtacar);
        volumeDano = Mathf.Clamp01(volumeDano);
        volumeMorrer = Mathf.Clamp01(volumeMorrer);
        AtualizarBarraVida();
    }

    private void ConfigurarUIVida()
    {
        if (buscarBarraVidaAutomaticamente && imagemBarraVida == null)
            imagemBarraVida = ProcurarImagemBarraVida();

        if (canvasVida == null)
            canvasVida = GetComponentInChildren<Canvas>(true);
    }

    private Image ProcurarImagemBarraVida()
    {
        Image[] imagens = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < imagens.Length; i++)
        {
            Image imagem = imagens[i];
            if (imagem != null && string.Equals(imagem.name, "Vida Frente", StringComparison.Ordinal))
                return imagem;
        }

        return null;
    }

    public static int CalcularAtributoPorNivel(int nivel, int valorInicial, int aumentoPorNivel)
    {
        long valor = valorInicial + ((long)Mathf.Max(1, nivel) - 1) * aumentoPorNivel;
        return (int)System.Math.Max(0L, System.Math.Min(int.MaxValue, valor));
    }

    private void AtualizarBarraVida()
    {
        if (textoNomeMonstro != null)
            textoNomeMonstro.text = string.IsNullOrWhiteSpace(nomeMonstro) ? gameObject.name.Replace("(Clone)", "").Trim() : nomeMonstro;
        if (textoNivel != null)
            textoNivel.text = Mathf.Max(1, nivelMonstro).ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (imagemBarraVida == null)
            return;

        imagemBarraVida.fillAmount = vidaMaxima > 0 ? Mathf.Clamp01((float)vidaAtual / vidaMaxima) : 0f;
    }

    private void Update()
    {
        if (morto)
            return;

        AtualizarAlvoPlayer();
        AtualizarEstado();
        CorrigirInclinacao();
    }

    public void ReceberDano(int dano)
    {
        ReceberDano(dano, (GameObject)null);
    }

    public void ReceberDano(int dano, GameObject origemDano)
    {
        if (movimentoVerde != null && (morto || dano <= 0)) return;
        RegistrarPrimeiroAtacante(origemDano);
        AplicarDanoRecebido(dano, (ProcurarTransformComTagPlayer(origemDano != null ? origemDano.transform : null) ?? alvoPlayer));
    }

    public void ReceberDano(int dano, Transform origemDano)
    {
        ReceberDano(dano, origemDano != null ? origemDano.gameObject : null);
    }

    private void AplicarDanoRecebido(int dano, Transform playerResponsavel)
    {
        if (morto)
            return;

        int danoFinal = Mathf.Max(0, dano);
        if (danoFinal == 0)
            return;

        if (playerResponsavel != null)
            ultimoPlayerResponsavel = playerResponsavel;

        vidaAtual -= danoFinal;
        vidaAtual = Mathf.Max(0, vidaAtual);
        AtualizarBarraVida();
        TocarSomUmaVez(somDano, volumeDano);

        if (vidaAtual <= 0)
        {
            Morrer();
            return;
        }

        if (rotinaDano != null)
            StopCoroutine(rotinaDano);

        if (estadoAtual != EstadoSlime.TomandoDano)
            estadoAntesDano = movimentoVerde != null && estadoAtual == EstadoSlime.Atacando ? EstadoSlime.Perseguindo : estadoAtual;
        if (movimentoVerde != null) CancelarImpactoAtaquePendente();

        rotinaDano = StartCoroutine(RotinaDano());
        if (movimentoVerde != null && movimentoVerde.PreservarArcoInvestida) animacao?.ReiniciarAcaoConfigurada("Dano");
    }

    public void AcionarDano()
    {
        ReceberDano(1);
    }

    public void AcionarMorte()
    {
        Morrer();
    }

    private void RegistrarPrimeiroAtacante(GameObject origemDano)
    {
        if (origemDano == null)
            return;

        if (experienciaInimigo == null)
            experienciaInimigo = GetComponent<ExperienciaInimigo>();

        if (experienciaInimigo != null)
            experienciaInimigo.RegistrarPrimeiroAtacante(origemDano);
    }

    private Transform ProcurarTransformComTagPlayer(Transform origem)
    {
        if (origem == null || string.IsNullOrWhiteSpace(tagPlayer))
            return null;

        Transform atual = origem;
        while (atual != null)
        {
            if (string.Equals(atual.tag, tagPlayer, StringComparison.Ordinal))
                return atual;

            atual = atual.parent;
        }

        Transform root = origem.root;
        if (root != null && string.Equals(root.tag, tagPlayer, StringComparison.Ordinal))
            return root;

        return null;
    }

    private void AtualizarEstado()
    {
        switch (estadoAtual)
        {
            case EstadoSlime.Patrulhando:
                AtualizarPatrulha();
                break;
            case EstadoSlime.Observando:
                AtualizarObservacao();
                break;
            case EstadoSlime.Alerta:
                AtualizarAlerta();
                break;
            case EstadoSlime.Perseguindo:
                AtualizarPerseguicao();
                break;
            case EstadoSlime.Atacando:
                AtualizarAtaque();
                break;
            case EstadoSlime.TomandoDano:
                PararMovimento();
                break;
            default:
                if (TemAlvoVisivel())
                {
                    MudarEstado(EstadoSlime.Alerta);
                    break;
                }

                PararMovimento();
                break;
        }
    }

    private void AtualizarPatrulha()
    {
        if (TemAlvoVisivel())
        {
            MudarEstado(EstadoSlime.Alerta);
            return;
        }

        Transform ponto = ObterPontoAtual();
        if (ponto == null)
        {
            MudarEstado(EstadoSlime.Parado);
            return;
        }

        if (movimentoVerde != null && movimentoVerde.RotaBloqueada)
        {
            movimentoVerde.Parar(); EscolherProximoPontoAleatorio();
            tempoRestanteObservando = tempoObservando; MudarEstado(EstadoSlime.Observando); return;
        }
        MoverPara(ponto.position, velocidadePatrulha);

        if (DistanciaXZ(transform.position, ponto.position) <= distanciaChegadaPonto &&
            (movimentoVerde == null || movimentoVerde.AlturaDestinoCompativel(ponto.position)))
        {
            tempoRestanteObservando = tempoObservando;
            MudarEstado(EstadoSlime.Observando);
        }
    }

    private void AtualizarObservacao()
    {
        if (TemAlvoVisivel())
        {
            MudarEstado(EstadoSlime.Alerta);
            return;
        }

        PararMovimento();
        tempoRestanteObservando -= Time.deltaTime;

        if (tempoRestanteObservando <= 0f)
        {
            EscolherProximoPontoAleatorio();
            MudarEstado(QuantidadePontosValidos() > 0 ? EstadoSlime.Patrulhando : EstadoSlime.Parado);
        }
    }

    private void AtualizarAlerta()
    {
        if (!TemAlvoVisivel())
        {
            PerderAlvoEVoltarPatrulha();
            return;
        }

        PararMovimento();
        OlharPara(PosicaoAlvo(alvoPlayer));

        if (DistanciaXZ(transform.position, PosicaoAlvo(alvoPlayer)) <= distanciaIniciarPerseguicao)
            MudarEstado(EstadoSlime.Perseguindo);
    }

    private void AtualizarPerseguicao()
    {
        if (movimentoVerde != null && movimentoVerde.RotaBloqueada)
        {
            movimentoVerde.Parar(); alvoPlayer = null;
            proximaBuscaPlayer = Time.time + Mathf.Max(0.5f, tempoObservando);
            tempoRestanteObservando = Mathf.Max(0.5f, tempoObservando);
            MudarEstado(EstadoSlime.Observando); return;
        }
        if (!TemAlvoVisivel())
        {
            PerderAlvoEVoltarPatrulha();
            return;
        }

        float distancia = DistanciaXZ(transform.position, PosicaoAlvo(alvoPlayer));
        if (distancia <= distanciaAtaque ||
            (ExisteBloqueadorEntreInimigoEPlayer(alvoPlayer, out RaycastHit hitBloqueio) && hitBloqueio.distance <= AlcanceEfetivoAtaque()))
        {
            MudarEstado(EstadoSlime.Atacando);
            return;
        }

        MoverPara(PosicaoAlvo(alvoPlayer), velocidadePerseguicao);
        OlharPara(PosicaoAlvo(alvoPlayer));
    }

    private void AtualizarAtaque()
    {
        if (!TemAlvoVisivel())
        {
            PerderAlvoEVoltarPatrulha();
            return;
        }

        if (investidaAtaqueEmAndamento)
        {
            OlharPara(PosicaoAlvo(alvoPlayer));
            return;
        }

        PararMovimento();
        OlharPara(PosicaoAlvo(alvoPlayer));

        float distancia = DistanciaXZ(transform.position, PosicaoAlvo(alvoPlayer));
        bool bloqueadorEmAlcance = ExisteBloqueadorEntreInimigoEPlayer(alvoPlayer, out RaycastHit hitBloqueio) &&
                                   hitBloqueio.distance <= AlcanceEfetivoAtaque();

        if (distancia > distanciaAtaque && !bloqueadorEmAlcance)
        {
            MudarEstado(distancia <= distanciaIniciarPerseguicao ? EstadoSlime.Perseguindo : EstadoSlime.Alerta);
            return;
        }

        if (Time.time < proximoAtaque)
            return;

        IniciarInvestidaAtaque();
    }

    private bool ExisteBloqueadorEntreInimigoEPlayer(Transform playerAlvo, out RaycastHit hitBloqueio)
    {
        hitBloqueio = default;
        if (!TentarObterPrimeiroHitAtaque(playerAlvo, out RaycastHit primeiroHit))
            return false;

        if (ColliderBloqueiaMesmoSendoDoPlayer(primeiroHit.collider))
        {
            hitBloqueio = primeiroHit;
            return true;
        }

        if (PertenceAoPlayerAlvo(primeiroHit.collider, playerAlvo))
            return false;

        hitBloqueio = primeiroHit;
        return true;
    }

    private void IniciarInvestidaAtaque()
    {
        if (investidaAtaqueEmAndamento || alvoPlayer == null)
            return;

        Vector3 direcao = Vector3.zero;
        float distancia = distanciaInvestida;
        if (movimentoVerde != null)
        {
            Vector3 inicio = rb.position;
            direcao = ObterDirecaoHorizontalInvestida();
            if (!movimentoVerde.PreservarArcoInvestida)
                distancia = Mathf.Min(distancia, DistanciaXZ(inicio, PosicaoAlvo(alvoPlayer)));
            if (!movimentoVerde.PodeSaltarPara(inicio + direcao * distancia, alturaInvestida))
                return;
        }

        proximoAtaque = Time.time + cooldownAtaque;
        TocarSomUmaVez(somAtacar, volumeAtacar);

        if (rotinaInvestidaAtaque != null)
            StopCoroutine(rotinaInvestidaAtaque);

        if (movimentoVerde != null && movimentoVerde.PreservarArcoInvestida && animacao != null)
        {
            animacao.AnimarAtacando();
            animacao.ReiniciarAcaoConfigurada("Ataque");
        }
        rotinaInvestidaAtaque = StartCoroutine(InvestidaAtaque(direcao, distancia));
    }

    private IEnumerator InvestidaAtaque(Vector3 direcaoSalto, float distanciaSalto)
    {
        if (movimentoVerde != null)
        {
            if (movimentoVerde.PreservarArcoInvestida) yield return InvestidaArcoVermelho(direcaoSalto);
            else yield return InvestidaFisicaVerde(direcaoSalto, distanciaSalto);
            yield break;
        }
        investidaAtaqueEmAndamento = true;
        impactoAtaqueResolvido = false;

        PararMovimento();
        PausarAgentParaInvestida();

        Vector3 posicaoInicial = transform.position;
        Vector3 direcao = ObterDirecaoHorizontalInvestida();
        OlharNaDirecaoInstantaneo(direcao);

        Vector3 posicaoAlvo = posicaoInicial + direcao * distanciaInvestida;
        float duracao = Mathf.Max(0.01f, duracaoInvestida);
        float tempo = 0f;
        bool colidiuDuranteInvestida = false;

        while (tempo < duracao && !morto)
        {
            tempo += Time.deltaTime;
            float t = Mathf.Clamp01(tempo / duracao);

            Vector3 posicaoHorizontal = Vector3.Lerp(posicaoInicial, posicaoAlvo, t);
            float alturaPulo = Mathf.Sin(t * Mathf.PI) * alturaInvestida;
            Vector3 posicaoDesejada = new Vector3(posicaoHorizontal.x, posicaoInicial.y + alturaPulo, posicaoHorizontal.z);

            bool bloqueadoNoPasso = MoverInvestidaComColisao(posicaoDesejada, direcao, out RaycastHit hitBloqueioPasso);
            OlharNaDirecaoInstantaneo(direcao);

            if (bloqueadoNoPasso)
            {
                colidiuDuranteInvestida = true;
                ResolverImpactoCollider(hitBloqueioPasso.collider);
                AplicarPosicaoInvestida(posicaoInicial);

                break;
            }

            yield return null;
        }

        if (!morto && !colidiuDuranteInvestida)
        {
            Vector3 posicaoFinal = AjustarPosicaoFinalInvestida(transform.position, posicaoInicial.y);
            AplicarPosicaoInvestida(posicaoFinal);

            if (!impactoAtaqueResolvido)
                ResolverImpactoAtaque();
        }

        RetomarAgentDepoisInvestida();
        investidaAtaqueEmAndamento = false;
        rotinaInvestidaAtaque = null;
    }

    private IEnumerator InvestidaArcoVermelho(Vector3 direcao)
    {
        Vector3 inicio = rb.position;
        investidaAtaqueEmAndamento = true; impactoAtaqueResolvido = false;
        movimentoVerde.IniciarArco(direcao);
        float tempo = 0f;
        try
        {
            while (!morto && estadoAtual == EstadoSlime.Atacando && tempo < duracaoInvestida)
            {
                yield return new WaitForFixedUpdate();
                if (morto || estadoAtual != EstadoSlime.Atacando) break;
                tempo += Time.fixedDeltaTime;
                Vector3 alvo = SlimeMovimento.CalcularArco(inicio, direcao, distanciaInvestida, alturaInvestida, tempo / Mathf.Max(0.01f, duracaoInvestida));
                if (movimentoVerde.MoverArco(alvo, out var bloqueio, out bool aterrissou))
                {
                    if (!aterrissou)
                    {
                        ResolverImpactoCollider(bloqueio);
                        yield break;
                    }
                    break;
                }
            }
            if (!morto && estadoAtual == EstadoSlime.Atacando && !impactoAtaqueResolvido) ResolverImpactoAtaque();
        }
        finally
        {
            movimentoVerde.Suspender(false);
            investidaAtaqueEmAndamento = false; rotinaInvestidaAtaque = null;
        }
    }

    private IEnumerator InvestidaFisicaVerde(Vector3 direcao, float distancia)
    {
        investidaAtaqueEmAndamento = true; impactoAtaqueResolvido = false;
        movimentoVerde.Suspender(true);
        float gravidade = Mathf.Max(0.1f, -Physics.gravity.y);
        float subida = Mathf.Sqrt(2f * gravidade * Mathf.Max(0f, alturaInvestida));
        float duracao = Mathf.Max(duracaoInvestida, 2f * subida / gravidade);
        Vector3 velocidadeSalto = direcao * (distancia / Mathf.Max(0.01f, duracao));
        rb.MoveRotation(Quaternion.LookRotation(direcao, Vector3.up));
        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = velocidadeSalto + Vector3.up * subida;
        float tempo = 0f;
        while (!morto && estadoAtual == EstadoSlime.Atacando && tempo < duracao)
        {
            yield return new WaitForFixedUpdate();
            if (morto || estadoAtual != EstadoSlime.Atacando) break;
            tempo += Time.fixedDeltaTime;
            if (TentarObterBloqueioInvestida(rb.position, direcao, velocidadeSalto.magnitude * Time.fixedDeltaTime, out var hit))
            {
                ResolverImpactoCollider(hit.collider); break;
            }
            rb.linearVelocity = new Vector3(velocidadeSalto.x, rb.linearVelocity.y, velocidadeSalto.z);
        }
        if (!morto && estadoAtual == EstadoSlime.Atacando && !impactoAtaqueResolvido) ResolverImpactoAtaque();
        movimentoVerde.Suspender(false);
        investidaAtaqueEmAndamento = false; rotinaInvestidaAtaque = null;
    }

    private void PausarAgentParaInvestida()
    {
        if (!UsandoAgent())
            return;

        agentPausadoPelaInvestida = true;
        agentUpdatePositionAntesInvestida = agent.updatePosition;
        agent.isStopped = true;

        if (agent.hasPath)
            agent.ResetPath();

        agent.updatePosition = false;
    }

    private void RetomarAgentDepoisInvestida()
    {
        if (!agentPausadoPelaInvestida)
            return;

        agentPausadoPelaInvestida = false;

        if (agent == null || !agent.enabled)
            return;

        if (agent.isOnNavMesh)
            agent.Warp(transform.position);

        agent.updatePosition = agentUpdatePositionAntesInvestida;

        if (!morto)
            agent.isStopped = false;
    }

    private Vector3 ObterDirecaoHorizontalInvestida()
    {
        Vector3 direcao = alvoPlayer != null ? PosicaoAlvo(alvoPlayer) - transform.position : transform.forward;
        direcao.y = 0f;

        if (direcao.sqrMagnitude <= 0.0001f)
        {
            direcao = transform.forward;
            direcao.y = 0f;
        }

        if (direcao.sqrMagnitude <= 0.0001f)
            return Vector3.forward;

        return direcao.normalized;
    }

    private void OlharNaDirecaoInstantaneo(Vector3 direcao)
    {
        direcao.y = 0f;
        if (direcao.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(direcao.normalized, Vector3.up);
        CorrigirInclinacao();
    }

    private bool MoverInvestidaComColisao(Vector3 posicaoDesejada, Vector3 direcaoPadrao, out RaycastHit hitBloqueio)
    {
        hitBloqueio = default;

        Vector3 posicaoAtual = transform.position;
        Vector3 deltaHorizontal = new Vector3(
            posicaoDesejada.x - posicaoAtual.x,
            0f,
            posicaoDesejada.z - posicaoAtual.z);

        if (deltaHorizontal.sqrMagnitude > 0.0001f)
        {
            Vector3 direcao = deltaHorizontal.normalized;
            float distancia = deltaHorizontal.magnitude;

            if (TentarObterBloqueioInvestida(posicaoAtual, direcao, distancia, out hitBloqueio))
                return true;
        }
        else if (direcaoPadrao.sqrMagnitude > 0.0001f && TentarObterBloqueioInvestida(posicaoAtual, direcaoPadrao.normalized, 0.05f, out hitBloqueio))
        {
            return true;
        }

        AplicarPosicaoInvestida(posicaoDesejada);
        return false;
    }

    private bool TentarObterBloqueioInvestida(Vector3 origem, Vector3 direcao, float distancia, out RaycastHit hitBloqueio)
    {
        hitBloqueio = default;

        if (direcao.sqrMagnitude <= 0.0001f || distancia <= 0f)
            return false;

        if (movimentoVerde != null)
        {
            int n = Physics.SphereCastNonAlloc(ObterOrigemCastInvestida(origem), raioVerificacaoColisaoInvestida,
                direcao.normalized, hitsVerde, distancia + margemParedeInvestida, layersBloqueioInvestida, QueryTriggerInteraction.Ignore);
            if (n == hitsVerde.Length) return true; // Cancela o deslocamento; nao aplica impacto sem collider conhecido.
            float menor = float.PositiveInfinity;
            for (int i = 0; i < n; i++) if (hitsVerde[i].collider != null && !PertenceAEsteInimigo(hitsVerde[i].collider) && !Escudo.IgnorarNoAtaque(hitsVerde[i].collider, alvoPlayer) && hitsVerde[i].distance < menor)
            { hitBloqueio = hitsVerde[i]; menor = hitBloqueio.distance; }
            return hitBloqueio.collider != null;
        }
        RaycastHit[] hits = Physics.SphereCastAll(
            ObterOrigemCastInvestida(origem),
            raioVerificacaoColisaoInvestida,
            direcao.normalized,
            distancia + margemParedeInvestida,
            layersBloqueioInvestida,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null || PertenceAEsteInimigo(col) || Escudo.IgnorarNoAtaque(col, alvoPlayer))
                continue;

            hitBloqueio = hits[i];
            return true;
        }

        return false;
    }

    private Vector3 ObterOrigemCastInvestida(Vector3 posicaoBase)
    {
        if (movimentoVerde != null) return posicaoBase + movimentoVerde.CentroCorpo - transform.position;
        float alturaCast = Mathf.Max(alturaOrigemAtaque, raioVerificacaoColisaoInvestida + 0.05f);
        return posicaoBase + Vector3.up * alturaCast;
    }

    private Vector3 AjustarPosicaoFinalInvestida(Vector3 posicaoAtual, float yPadrao)
    {
        Vector3 posicaoFinal = new Vector3(posicaoAtual.x, yPadrao, posicaoAtual.z);
        float alturaBusca = Mathf.Max(alturaInvestida + raioVerificacaoColisaoInvestida + 1f, 1.5f);
        Vector3 origem = posicaoFinal + Vector3.up * alturaBusca;

        RaycastHit[] hits = Physics.RaycastAll(
            origem,
            Vector3.down,
            alturaBusca + 2f,
            layersBloqueioInvestida,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return posicaoFinal;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null || PertenceAEsteInimigo(col) || PertenceAoPlayerAlvo(col, alvoPlayer))
                continue;

            posicaoFinal.y = hits[i].point.y;
            return posicaoFinal;
        }

        return posicaoFinal;
    }

    private void AplicarPosicaoInvestida(Vector3 posicao)
    {
        if (rb != null)
        {
            if (!rb.isKinematic)
                rb.linearVelocity = Vector3.zero;

            rb.MovePosition(posicao);
            return;
        }

        if (characterController != null && characterController.enabled)
        {
            characterController.Move(posicao - transform.position);
            return;
        }

        transform.position = posicao;
    }

    private void ResolverImpactoCollider(Collider col)
    {
        if (movimentoVerde != null && (morto || estadoAtual != EstadoSlime.Atacando)) return;
        if (col == null || alvoPlayer == null || impactoAtaqueResolvido || Escudo.IgnorarNoAtaque(col, alvoPlayer))
            return;

        impactoAtaqueResolvido = true;
        float danoBruto = ObterDano();
        if (Escudo.TentarInterceptarGolpe(col, gameObject, alvoPlayer, danoBruto, out float restante))
        {
            TentarAplicarDanoNoPlayer(alvoPlayer, restante);
            return;
        }

        if (!ColliderBloqueiaMesmoSendoDoPlayer(col) && PertenceAoPlayerAlvo(col, alvoPlayer))
            TentarAplicarDanoNoPlayer(alvoPlayer, danoBruto);
    }

    private void ResolverImpactoAtaque()
    {
        if (morto || alvoPlayer == null || estadoAtual != EstadoSlime.Atacando)
            return;

        if (movimentoVerde != null)
        {
            int n = Physics.OverlapSphereNonAlloc(ObterOrigemAtaque(), raioAtaque, sobreposicoesVerde, layersAtaque, QueryTriggerInteraction.Ignore);
            if (n == sobreposicoesVerde.Length) return;
            Collider contato = null; float menor = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                var candidato = sobreposicoesVerde[i];
                if (candidato == null || PertenceAEsteInimigo(candidato) ||
                    (movimentoVerde.PreservarArcoInvestida && candidato.GetComponentInParent<SlimeIA>() != null) ||
                    Escudo.IgnorarNoAtaque(candidato, alvoPlayer)) continue;
                if (candidato.GetComponentInParent<Espada>() != null) { contato = candidato; break; }
                float d = (candidato.ClosestPoint(ObterOrigemAtaque()) - ObterOrigemAtaque()).sqrMagnitude;
                if (d < menor) { menor = d; contato = candidato; }
            }
            if (contato != null) { ResolverImpactoCollider(contato); return; }
        }
        if (!TentarObterPrimeiroHitAtaque(alvoPlayer, out RaycastHit hit))
            return;

        Collider col = hit.collider;
        if (col == null)
            return;

        ResolverImpactoCollider(col);
    }

    private bool TentarObterPrimeiroHitAtaque(Transform playerAlvo, out RaycastHit primeiroHit)
    {
        primeiroHit = default;

        Vector3 origem = ObterOrigemAtaque();
        Vector3 direcao = ObterDirecaoAtaque(playerAlvo);
        if (movimentoVerde != null)
        {
            int n = Physics.SphereCastNonAlloc(origem, raioAtaque, direcao, hitsVerde, AlcanceEfetivoAtaque(), layersAtaque, QueryTriggerInteraction.Ignore);
            if (n == hitsVerde.Length) return false;
            float menor = float.PositiveInfinity;
            for (int i = 0; i < n; i++) if (hitsVerde[i].collider != null && !PertenceAEsteInimigo(hitsVerde[i].collider) &&
                (!movimentoVerde.PreservarArcoInvestida || hitsVerde[i].collider.GetComponentInParent<SlimeIA>() == null) &&
                !Escudo.IgnorarNoAtaque(hitsVerde[i].collider, playerAlvo) && hitsVerde[i].distance < menor)
            { primeiroHit = hitsVerde[i]; menor = primeiroHit.distance; }
            return primeiroHit.collider != null;
        }
        float alcance = AlcanceEfetivoAtaque();

        if (direcao.sqrMagnitude <= 0.0001f || alcance <= 0f)
            return false;

        RaycastHit[] hits = Physics.SphereCastAll(
            origem,
            raioAtaque,
            direcao.normalized,
            alcance,
            layersAtaque,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null || PertenceAEsteInimigo(col) || Escudo.IgnorarNoAtaque(col, playerAlvo))
                continue;

            primeiroHit = hits[i];
            return true;
        }

        return false;
    }

    private bool TentarAplicarDanoNoPlayer(Transform playerAlvo, float danoConfigurado)
    {
        if (playerAlvo == null || !float.IsFinite(danoConfigurado) || danoConfigurado <= 0f)
            return false;

        if (movimentoVerde != null)
        {
            var status = ObterStatusPlayerEmTransform(playerAlvo);
            if (status == null) return false;
            status.ReceberDano(danoConfigurado, gameObject); return true;
        }
        Component[] componentes = playerAlvo.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < componentes.Length; i++)
        {
            Component componente = componentes[i];
            if (componente == null || componente == this)
                continue;

            MethodInfo metodo = componente.GetType().GetMethod(
                "ReceberDano",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(float), typeof(GameObject) },
                null);

            if (metodo != null)
            {
                metodo.Invoke(componente, new object[] { danoConfigurado, gameObject });
                return true;
            }

            metodo = componente.GetType().GetMethod(
                "ReceberDano",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(int) },
                null);

            if (metodo == null)
                continue;

            metodo.Invoke(componente, new object[] { Mathf.RoundToInt(danoConfigurado) });
            return true;
        }

        return false;
    }

    private bool ColliderBloqueiaMesmoSendoDoPlayer(Collider col)
    {
        if (col == null)
            return false;

        var escudo = col.GetComponentInParent<Escudo>();
        if (escudo != null) return escudo.EstaProtegendoPlayer(alvoPlayer);

        return col.GetComponentInParent<Espada>() != null;
    }

    private bool PertenceAoPlayerAlvo(Collider col, Transform playerAlvo)
    {
        if (col == null || playerAlvo == null)
            return false;

        Transform t = col.transform;
        if (t == playerAlvo || t.IsChildOf(playerAlvo))
            return true;

        if (col.attachedRigidbody != null)
        {
            Transform rbTransform = col.attachedRigidbody.transform;
            if (rbTransform == playerAlvo || rbTransform.IsChildOf(playerAlvo))
                return true;
        }

        Transform root = t.root;
        return root != null && (root == playerAlvo || root.IsChildOf(playerAlvo));
    }

    private bool PertenceAEsteInimigo(Collider col)
    {
        if (col == null)
            return false;

        Transform t = col.transform;
        if (t == transform || t.IsChildOf(transform))
            return true;

        if (col.attachedRigidbody != null)
        {
            Transform rbTransform = col.attachedRigidbody.transform;
            if (rbTransform == transform || rbTransform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    private Vector3 ObterOrigemAtaque()
    {
        if (movimentoVerde != null) return movimentoVerde.CentroCorpo;
        if (pontoOrigemAtaque != null)
            return pontoOrigemAtaque.position;

        return transform.position + Vector3.up * alturaOrigemAtaque;
    }

    private Vector3 ObterDirecaoAtaque(Transform playerAlvo)
    {
        Vector3 origem = ObterOrigemAtaque();
        Vector3 direcao = playerAlvo != null
            ? (deteccaoVerde != null ? PosicaoAlvo(playerAlvo) : playerAlvo.position + Vector3.up * alturaOrigemAtaque) - origem
            : ObterFrenteAtaque();

        if (direcao.sqrMagnitude <= 0.0001f)
            direcao = ObterFrenteAtaque();

        return direcao.normalized;
    }

    private Vector3 ObterFrenteAtaque()
    {
        if (pontoOrigemAtaque != null)
            return pontoOrigemAtaque.forward;

        return transform.forward;
    }

    private float AlcanceEfetivoAtaque()
    {
        return Mathf.Max(0.01f, alcanceAtaque);
    }

    private void CancelarImpactoAtaquePendente()
    {
        if (rotinaInvestidaAtaque != null)
        {
            StopCoroutine(rotinaInvestidaAtaque);
            rotinaInvestidaAtaque = null;
        }

        investidaAtaqueEmAndamento = false;
        impactoAtaqueResolvido = false;
        if (movimentoVerde != null) movimentoVerde.Suspender(false);
        RetomarAgentDepoisInvestida();
    }

    private void AtualizarAlvoPlayer()
    {
        if (Time.time < proximaBuscaPlayer)
            return;

        proximaBuscaPlayer = Time.time + IntervaloBuscaPlayer;

        if (alvoPlayer != null)
        {
            if (EstaNoCampoDeVisao(alvoPlayer))
                return;

            alvoPlayer = null;
            if (estadoAtual == EstadoSlime.Alerta || estadoAtual == EstadoSlime.Perseguindo || estadoAtual == EstadoSlime.Atacando)
                VoltarParaPatrulha();
        }

        alvoPlayer = ProcurarPrimeiroPlayerVisivel();
    }

    private Transform ProcurarPrimeiroPlayerVisivel()
    {
        if (deteccaoVerde != null) return deteccaoVerde.ProcurarJogador(tagPlayer, ObterOrigemVisao(), ObterFrentePlana(), raioCampoVisao, anguloCampoVisao, layersObstaculo);
        if (string.IsNullOrWhiteSpace(tagPlayer))
            return null;

        GameObject[] players;
        try
        {
            players = GameObject.FindGameObjectsWithTag(tagPlayer);
        }
        catch (UnityException)
        {
            return null;
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && EstaNoCampoDeVisao(players[i].transform))
                return players[i].transform;
        }

        return null;
    }

    private bool TemAlvoVisivel()
    {
        return alvoPlayer != null && EstaNoCampoDeVisao(alvoPlayer);
    }

    private bool EstaNoCampoDeVisao(Transform alvo)
    {
        if (deteccaoVerde != null) return deteccaoVerde.Visivel(alvo, ObterOrigemVisao(), ObterFrentePlana(), raioCampoVisao, anguloCampoVisao, layersObstaculo);
        if (!EstaDentroDoRaioEAngulo(alvo, out float distancia))
            return false;

        Vector3 origem = ObterOrigemVisao();
        Vector3 destino = alvo.position;
        return !LinhaDeVisaoBloqueada(origem, destino, distancia, out _);
    }

    private bool EstaDentroDoRaioEAngulo(Transform alvo, out float distancia)
    {
        distancia = 0f;
        if (alvo == null)
            return false;

        Vector3 origem = ObterOrigemVisao();
        Vector3 destino = alvo.position;
        Vector3 direcao = destino - origem;
        distancia = direcao.magnitude;

        if (distancia > raioCampoVisao)
            return false;

        Vector3 direcaoPlana = direcao;
        direcaoPlana.y = 0f;
        if (direcaoPlana.sqrMagnitude <= 0.0001f)
            return true;

        Vector3 frente = ObterFrentePlana();
        float angulo = Vector3.Angle(frente, direcaoPlana.normalized);
        return angulo <= anguloCampoVisao * 0.5f;
    }

    private bool LinhaDeVisaoBloqueada(Vector3 origem, Vector3 destino, float distancia, out RaycastHit hit)
    {
        hit = default;
        if (layersObstaculo.value == 0 || distancia <= 0.0001f)
            return false;

        Vector3 direcaoRaycast = (destino - origem).normalized;
        return Physics.Raycast(origem, direcaoRaycast, out hit, distancia, layersObstaculo, QueryTriggerInteraction.Ignore);
    }

    private Vector3 ObterFrentePlana()
    {
        Vector3 frente = transform.forward;
        frente.y = 0f;
        return frente.sqrMagnitude > 0.0001f ? frente.normalized : Vector3.forward;
    }

    private Vector3 ObterOrigemVisao()
    {
        if (movimentoVerde != null && movimentoVerde.PreservarArcoInvestida)
            return pontoOlhos != null && pontoOlhos != transform ? pontoOlhos.position : transform.position + Vector3.up * alturaOlhos;
        if (movimentoVerde != null) return movimentoVerde.CentroCorpo;
        return pontoOlhos != null ? pontoOlhos.position : transform.position + Vector3.up * alturaOlhos;
    }

    private void OnDrawGizmos()
    {
        if (desenharGizmos && desenharGizmosSempre && GizmosPodemDesenhar())
            DesenharGizmosCampoVisao();
    }

    private void OnDrawGizmosSelected()
    {
        if (desenharGizmos && !desenharGizmosSempre && GizmosPodemDesenhar())
            DesenharGizmosCampoVisao();
    }

    // Fora do Play (ou antes do Awake) o movimento pode existir sem ter sido inicializado.
    private bool GizmosPodemDesenhar() => movimentoVerde == null || movimentoVerde.Inicializado;

    private void DesenharGizmosCampoVisao()
    {
        Vector3 origem = ObterOrigemVisao();

        DesenharArcoVisao(origem, raioCampoVisao, anguloCampoVisao, new Color(0f, 0.8f, 1f, 1f));
        DesenharArcoVisao(origem, distanciaIniciarPerseguicao, anguloCampoVisao, new Color(1f, 0.35f, 0.1f, 1f));
        DesenharGizmosAtaque();

        Transform alvoGizmo = ObterAlvoParaGizmo();
        if (alvoGizmo == null || !EstaDentroDoRaioEAngulo(alvoGizmo, out float distancia))
            return;

        Vector3 destino = alvoGizmo.position;
        if (LinhaDeVisaoBloqueada(origem, destino, distancia, out RaycastHit hit))
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(origem, hit.point);
            Gizmos.DrawWireSphere(hit.point, 0.08f);
            return;
        }

        bool dentroPerseguicao = DistanciaXZ(transform.position, destino) <= distanciaIniciarPerseguicao;
        Gizmos.color = dentroPerseguicao ? Color.red : Color.yellow;
        Gizmos.DrawLine(origem, destino);
        Gizmos.DrawWireSphere(destino, 0.12f);
    }

    private void DesenharArcoVisao(Vector3 origem, float raio, float angulo, Color cor)
    {
        if (raio <= 0f)
            return;

        Vector3 frente = ObterFrentePlana();
        float meioAngulo = angulo * 0.5f;
        Vector3 limiteEsquerdo = Quaternion.Euler(0f, -meioAngulo, 0f) * frente;
        Vector3 limiteDireito = Quaternion.Euler(0f, meioAngulo, 0f) * frente;

        Gizmos.color = cor;
        Gizmos.DrawLine(origem, origem + limiteEsquerdo * raio);
        Gizmos.DrawLine(origem, origem + limiteDireito * raio);

        const int segmentos = 32;
        Vector3 anterior = origem + limiteEsquerdo * raio;
        for (int i = 1; i <= segmentos; i++)
        {
            float t = i / (float)segmentos;
            float anguloAtual = Mathf.Lerp(-meioAngulo, meioAngulo, t);
            Vector3 direcao = Quaternion.Euler(0f, anguloAtual, 0f) * frente;
            Vector3 atual = origem + direcao * raio;
            Gizmos.DrawLine(anterior, atual);
            anterior = atual;
        }
    }

    private void DesenharGizmosAtaque()
    {
        Vector3 origem = ObterOrigemAtaque();
        Vector3 direcao = Application.isPlaying && alvoPlayer != null ? ObterDirecaoAtaque(alvoPlayer) : ObterFrentePlana();
        Vector3 destino = origem + direcao * AlcanceEfetivoAtaque();

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(origem, raioAtaque);
        Gizmos.DrawLine(origem, destino);
        Gizmos.DrawWireSphere(destino, raioAtaque);
    }

    private Transform ObterAlvoParaGizmo()
    {
        if (alvoPlayer != null)
            return alvoPlayer;

        if (string.IsNullOrWhiteSpace(tagPlayer))
            return null;

        GameObject[] players;
        try
        {
            players = GameObject.FindGameObjectsWithTag(tagPlayer);
        }
        catch (UnityException)
        {
            return null;
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && EstaDentroDoRaioEAngulo(players[i].transform, out _))
                return players[i].transform;
        }

        return null;
    }

    private void MoverPara(Vector3 destino, float velocidade)
    {
        if (movimentoVerde != null) { movimentoVerde.SolicitarMovimento(destino, velocidade); movimentoVerde.Olhar(destino); return; }
        if (UsandoAgent())
        {
            agent.isStopped = false;
            agent.speed = velocidade;
            agent.SetDestination(destino);
            OlharPara(destino);
            return;
        }

        Vector3 posicaoAtual = transform.position;
        Vector3 destinoPlano = new Vector3(destino.x, posicaoAtual.y, destino.z);
        Vector3 proximaPosicao = Vector3.MoveTowards(posicaoAtual, destinoPlano, velocidade * Time.deltaTime);

        if (rb != null && !rb.isKinematic)
            rb.MovePosition(proximaPosicao);
        else
            transform.position = proximaPosicao;

        OlharPara(destinoPlano);
    }

    private void PararMovimento()
    {
        if (movimentoVerde != null) { movimentoVerde.Parar(); return; }
        if (UsandoAgent())
        {
            agent.isStopped = true;
            if (agent.hasPath)
                agent.ResetPath();
        }

        if (rb != null && !rb.isKinematic)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    private void OlharPara(Vector3 alvo)
    {
        if (movimentoVerde != null) { movimentoVerde.Olhar(alvo); return; }
        Vector3 direcao = alvo - transform.position;
        direcao.y = 0f;

        if (direcao.sqrMagnitude <= 0.0001f)
            return;

        Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, VelocidadeRotacao * Time.deltaTime);
        CorrigirInclinacao();
    }

    private void CorrigirInclinacao()
    {
        if (movimentoVerde != null) return;
        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    private void MudarEstado(EstadoSlime novoEstado)
    {
        if (morto && novoEstado != EstadoSlime.Morto)
            return;

        if (estadoAtual == novoEstado)
            return;

        if (movimentoVerde != null && estadoAtual == EstadoSlime.Atacando && novoEstado != EstadoSlime.Atacando) CancelarImpactoAtaquePendente();
        estadoAtual = novoEstado;
        AtualizarAnimatorPorEstado();
        AtualizarAudioPorEstado();
    }

    // ---------------------------------------------------------------
    // Animacao: cada estado chama exatamente 1 metodo do SlimeAnimacao.
    // Esse script (SlimeIA) nao mexe mais no Animator diretamente.
    // ---------------------------------------------------------------
    private void AtualizarAnimatorPorEstado()
    {
        if (animacao == null)
            return;

        switch (estadoAtual)
        {
            case EstadoSlime.Patrulhando:
                animacao.AnimarPatrulhando();
                break;
            case EstadoSlime.Observando:
                animacao.AnimarObservando();
                break;
            case EstadoSlime.Alerta:
                animacao.AnimarAlerta();
                break;
            case EstadoSlime.Perseguindo:
                animacao.AnimarPerseguindo();
                break;
            case EstadoSlime.Atacando:
                if (movimentoVerde != null && movimentoVerde.PreservarArcoInvestida)
                    animacao.AnimarParado();
                else
                    animacao.AnimarAtacando();
                break;
            case EstadoSlime.TomandoDano:
                animacao.AnimarTomandoDano();
                break;
            case EstadoSlime.Morto:
                animacao.AnimarMorto();
                break;
            default:
                animacao.AnimarParado();
                break;
        }
    }

    private void AtualizarAudioPorEstado()
    {
        if (estadoAtual == EstadoSlime.Patrulhando || estadoAtual == EstadoSlime.Perseguindo)
            TocarSomAndar();
        else
            PararSomAndar();
    }

    private void TocarSomAndar()
    {
        if (audioSource == null || somAndar == null || somAndarTocando)
            return;

        audioSource.clip = somAndar;
        audioSource.volume = volumeAndar;
        audioSource.loop = true;
        audioSource.Play();
        somAndarTocando = true;
    }

    private void PararSomAndar()
    {
        if (audioSource == null || !somAndarTocando)
            return;

        audioSource.Stop();
        audioSource.clip = null;
        audioSource.loop = false;
        somAndarTocando = false;
    }

    private void TocarSomUmaVez(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip, volume);
    }

    private void TocarSomMorrer()
    {
        if (somMorteTocado)
            return;

        somMorteTocado = true;
        if (somMorrer != null)
            AudioSource.PlayClipAtPoint(somMorrer, transform.position, volumeMorrer);
    }

    private IEnumerator RotinaDano()
    {
        MudarEstado(EstadoSlime.TomandoDano);

        yield return new WaitForSeconds(tempoAnimacaoDano);

        rotinaDano = null;
        if (!morto)
            MudarEstado(estadoAntesDano == EstadoSlime.Morto ? EstadoSlime.Parado : estadoAntesDano);
    }

    private void Morrer()
    {
        if (morto)
            return;

        morto = true;
        vidaAtual = 0;
        AtualizarBarraVida();

        if (rotinaDano != null)
        {
            StopCoroutine(rotinaDano);
            rotinaDano = null;
        }

        CancelarImpactoAtaquePendente();
        PararMovimento();
        PararSomAndar();
        MudarEstado(EstadoSlime.Morto);
        TocarSomMorrer();
        if (movimentoVerde != null)
        {
            movimentoVerde.Morrer();
            FinalizarMorteVerde(); return;
        }
        EntregarExperienciaAoMorrer();
        PagarRecompensaReinAoPrimeiroAtacante();
        NotificarMissaoDeMorte();
        SpawnarPrefabsNormais();
        SpawnarPrefabsMissao();
        if (RespawnMonstro.Instancia != null)
        {
            RespawnMonstro.Instancia.AgendarRespawn(
                idRespawnMonstro,
                transform.position,
                transform.rotation,
                pontosPatrulha);
        }

        if (esconderCanvasAoMorrer && canvasVida != null)
            canvasVida.gameObject.SetActive(false);

        Destroy(gameObject, tempoParaMorrer);
    }

    private void ExecutarEtapaMorteVerde(Action etapa)
    {
        try { etapa(); } catch (Exception erro) { Debug.LogException(erro, this); }
    }
    private void FinalizarMorteVerde()
    {
        try
        {
            ExecutarEtapaMorteVerde(EntregarExperienciaAoMorrer);
            ExecutarEtapaMorteVerde(PagarRecompensaReinAoPrimeiroAtacante);
            ExecutarEtapaMorteVerde(NotificarMissaoDeMorte);
            ExecutarEtapaMorteVerde(SpawnarPrefabsNormais);
            ExecutarEtapaMorteVerde(SpawnarPrefabsMissao);
            ExecutarEtapaMorteVerde(() =>
            {
                if (RespawnMonstro.Instancia == null) return;
                // Pontos manuais continuam externos; pontos automaticos serao recriados pela nova IA.
                Transform[] pontos = pontosAutoProprios.Count > 0 ? null : pontosPatrulha;
                if (movimentoVerde.PreservarArcoInvestida)
                    RespawnMonstro.Instancia.AgendarRespawnVermelho(idRespawnMonstro, transform.position, transform.rotation, pontos, GetInstanceID(),
                        movimentoVerde.RaioMundial, movimentoVerde.AlturaMundial, movimentoVerde.CentroRelativo);
                else RespawnMonstro.Instancia.AgendarRespawnVerde(idRespawnMonstro, transform.position, transform.rotation, pontos, GetInstanceID());
            });
        }
        finally
        {
            if (esconderCanvasAoMorrer && canvasVida != null) canvasVida.gameObject.SetActive(false);
            float esperaMorte = tempoParaMorrer;
            if (movimentoVerde.PreservarArcoInvestida && animacao != null) esperaMorte = Mathf.Max(esperaMorte, animacao.ObterTempoMorteConfigurado());
            Destroy(gameObject, esperaMorte);
        }
    }

    private void EntregarExperienciaAoMorrer()
    {
        if (experienciaJaEntregue)
            return;

        experienciaJaEntregue = true;

        if (experienciaInimigo == null)
            experienciaInimigo = GetComponent<ExperienciaInimigo>();

        if (experienciaInimigo != null)
            experienciaInimigo.EntregarExperiencia();
    }

    /// <summary>
    /// Avisa o GerenciadorMissoes que este inimigo morreu, para o progresso
    /// de uma missao do tipo "MatarInimigos" avancar (ex: 5/10 -> 6/10).
    /// Usa o mesmo idRespawnMonstro ja usado pelo sistema de respawn.
    /// </summary>
    private void NotificarMissaoDeMorte()
    {
        if (gerenciadorMissoes == null)
            gerenciadorMissoes = FindFirstObjectByType<GerenciadorMissoes>();

        if (gerenciadorMissoes != null)
            gerenciadorMissoes.NotificarInimigoMorto(idRespawnMonstro);
    }

    private void PagarRecompensaReinAoPrimeiroAtacante()
    {
        if (!darReinAoMorrer || recompensaReinJaPaga)
            return;

        recompensaReinJaPaga = true;

        CarteiraReinPlayer carteira = EncontrarCarteiraReinDoPrimeiroAtacante();
        if (carteira == null)
            return;

        long unidades = SortearReinUnidades();
        if (unidades <= 0)
            return;

        carteira.AdicionarReinUnidades(unidades);
    }

    private CarteiraReinPlayer EncontrarCarteiraReinDoPrimeiroAtacante()
    {
        if (experienciaInimigo == null)
            experienciaInimigo = GetComponent<ExperienciaInimigo>();

        StatusPlayer status = experienciaInimigo != null ? experienciaInimigo.PrimeiroAtacante : null;
        if (status == null)
            status = ObterStatusPlayerEmTransform(ultimoPlayerResponsavel);
        if (status == null)
            status = ObterStatusPlayerEmTransform(alvoPlayer);
        if (status == null)
            status = EncontrarStatusPlayerFallbackRein();

        return status != null ? status.ObterCarteiraRein() : null;
    }

    private long SortearReinUnidades()
    {
        TentarConverterReinParaUnidades(reinMinimo, out long minimo);
        TentarConverterReinParaUnidades(reinMaximo, out long maximo);

        minimo = Math.Max(0L, minimo);
        maximo = Math.Max(0L, maximo);

        if (maximo < minimo)
        {
            long temporario = minimo;
            minimo = maximo;
            maximo = temporario;
        }

        int casas = Mathf.Clamp(casasDecimaisSorteioRein, 0, 5);
        long passo = CalcularPassoUnidadesRein(casas);
        minimo = ArredondarParaPasso(minimo, passo, true);
        maximo = ArredondarParaPasso(maximo, passo, false);

        if (maximo < minimo)
            maximo = minimo;

        return RandomLongInclusivo(minimo, maximo, passo);
    }

    private static bool TentarConverterReinParaUnidades(string texto, out long unidades)
    {
        unidades = 0L;
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        string normalizado = texto.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal valor))
            return false;

        if (valor <= 0m)
            return true;

        unidades = CarteiraReinPlayer.ConverterDecimalParaUnidades(valor);
        return true;
    }

    private static long CalcularPassoUnidadesRein(int casasDecimais)
    {
        int casas = Mathf.Clamp(casasDecimais, 0, 5);
        long passo = 1L;
        for (int i = casas; i < 5; i++)
            passo *= 10L;

        return Math.Max(1L, passo);
    }

    private static long ArredondarParaPasso(long valor, long passo, bool paraCima)
    {
        passo = Math.Max(1L, passo);
        long resto = valor % passo;
        if (resto == 0L)
            return valor;

        return paraCima ? valor + (passo - resto) : valor - resto;
    }

    private static long RandomLongInclusivo(long minimo, long maximo, long passo)
    {
        if (maximo <= minimo)
            return minimo;

        passo = Math.Max(1L, passo);
        long quantidadePassos = ((maximo - minimo) / passo) + 1L;
        double sorteio = UnityEngine.Random.value * quantidadePassos;
        long indice = Math.Min(quantidadePassos - 1L, (long)Math.Floor(sorteio));
        return minimo + indice * passo;
    }

    private static StatusPlayer ObterStatusPlayerEmTransform(Transform alvo)
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

    private StatusPlayer EncontrarStatusPlayerFallbackRein()
    {
        GameObject player = GameObject.FindGameObjectWithTag(tagPlayer);
        StatusPlayer status = player != null ? ObterStatusPlayerEmTransform(player.transform) : null;
        return status != null ? status : FindFirstObjectByType<StatusPlayer>();
    }

    private void SpawnarPrefabsNormais()
    {
        if (prefabsSpawnNormal == null || prefabsSpawnNormal.Length == 0)
            return;

        var chances = new float[prefabsSpawnNormal.Length + 1];
        for (int i = 0; i < prefabsSpawnNormal.Length; i++)
        {
            var item = prefabsSpawnNormal[i];
            chances[i] = item != null && item.prefab != null ? SanitizarChanceDrop(item.chance) : float.NaN;
        }
        chances[prefabsSpawnNormal.Length] = chanceNada;

        int indice = SortearIndiceDrop(chances);
        if (indice >= 0)
        {
            var item = prefabsSpawnNormal[indice];
            SpawnarPrefab(item.prefab, item.offsetSpawn, item.quantidadeMinima, item.quantidadeMaxima);
        }
    }

    private static int SortearIndiceDrop(float[] chances)
    {
        DistribuirChancesDrop(chances);
        double sorteio = UnityEngine.Random.Range(0, 16777216) * (100d / 16777216d);
        double acumulado = 0d;
        for (int i = 0; i < chances.Length - 1; i++)
        {
            acumulado += chances[i];
            if (sorteio < acumulado)
                return i;
        }
        return -1;
    }

    public static float SanitizarChanceDrop(float chance)
    {
        return float.IsNaN(chance) || float.IsInfinity(chance) ? 0f : Mathf.Clamp(chance, 0f, 100f);
    }

    // O último elemento representa Nada. NaN identifica uma entrada sem prefab.
    public static void DistribuirChancesDrop(float[] chances, int indiceEditado = -1)
    {
        int indiceNada = chances.Length - 1;
        if (chances[indiceNada] < 0f)
        {
            double somaItens = 0;
            for (int i = 0; i < indiceNada; i++)
                somaItens += SanitizarChanceDrop(chances[i]);
            chances[indiceNada] = (float)Math.Max(0d, 100d - somaItens);
        }

        chances[indiceNada] = SanitizarChanceDrop(chances[indiceNada]);

        double total = 0;
        int destinoSaldo = -1;
        for (int i = 0; i < chances.Length; i++)
        {
            if (i == indiceEditado || float.IsNaN(chances[i]))
                continue;

            chances[i] = SanitizarChanceDrop(chances[i]);
            total += chances[i];
            if (destinoSaldo < 0 || chances[i] > chances[destinoSaldo] ||
                (i == indiceNada && chances[i] == chances[destinoSaldo]))
                destinoSaldo = i;
        }

        if (destinoSaldo < 0)
        {
            chances[indiceNada] = 100f;
        }
        else
        {
            double restante = 100d - (indiceEditado >= 0 ? chances[indiceEditado] : 0f);
            double saldo = restante;
            for (int i = 0; i < chances.Length; i++)
            {
                if (i == indiceEditado || i == destinoSaldo || float.IsNaN(chances[i]))
                    continue;
                chances[i] = total > 0d ? (float)(restante * chances[i] / total) : 0f;
                saldo -= chances[i];
            }
            chances[destinoSaldo] = (float)Math.Max(0d, saldo);
        }

        for (int i = 0; i < indiceNada; i++)
            if (float.IsNaN(chances[i]))
                chances[i] = 0f;
    }

    private void SpawnarPrefabsMissao()
    {
        if (prefabsSpawnMissao == null || prefabsSpawnMissao.Length == 0)
            return;

        Transform player = ultimoPlayerResponsavel != null ? ultimoPlayerResponsavel : alvoPlayer;
        if (player == null)
            return;

        var chances = new float[prefabsSpawnMissao.Length + 1];
        for (int i = 0; i < prefabsSpawnMissao.Length; i++)
        {
            var item = prefabsSpawnMissao[i];
            chances[i] = item != null && item.prefab != null ? SanitizarChanceDrop(item.chance) : float.NaN;
        }
        chances[prefabsSpawnMissao.Length] = chanceNadaMissao;

        int indice = SortearIndiceDrop(chances);
        if (indice >= 0)
        {
            var item = prefabsSpawnMissao[indice];
            if (PlayerTemMissaoVinculada(player, item))
                SpawnarPrefab(item.prefab, item.offsetSpawn, item.quantidadeMinima, item.quantidadeMaxima);
        }
    }

    private bool PlayerTemMissaoVinculada(Transform player, SpawnMissaoConfig config)
    {
        // TODO: integrar com o sistema de missao do Player futuramente.
        // Exemplo futuro:
        // var missoes = player.GetComponent<SistemaMissoesPlayer>();
        // return missoes != null && missoes.TemMissaoAtiva(config.idMissao, config.idObjetivo);
        return config != null && !config.exigirMissaoAtiva;
    }

    private void SpawnarPrefab(GameObject prefab, Vector3 offset, int quantidadeMinima, int quantidadeMaxima)
    {
        if (prefab == null)
            return;

        int minimo = Mathf.Max(0, quantidadeMinima);
        int maximo = Mathf.Max(minimo, quantidadeMaxima);
        int quantidade = UnityEngine.Random.Range(minimo, maximo + 1);
        Vector3 posicao = transform.position + offset;

        for (int i = 0; i < quantidade; i++)
            Instantiate(prefab, posicao, Quaternion.identity);
    }

    private void NormalizarSpawnsNormais()
    {
        if (prefabsSpawnNormal == null)
            return;

        for (int i = 0; i < prefabsSpawnNormal.Length; i++)
        {
            SpawnNormalConfig config = prefabsSpawnNormal[i];
            if (config == null)
                continue;

            config.quantidadeMinima = Mathf.Max(0, config.quantidadeMinima);
            config.quantidadeMaxima = Mathf.Max(config.quantidadeMinima, config.quantidadeMaxima);
        }
    }

    private void NormalizarSpawnsMissao()
    {
        if (prefabsSpawnMissao == null)
            return;

        for (int i = 0; i < prefabsSpawnMissao.Length; i++)
        {
            SpawnMissaoConfig config = prefabsSpawnMissao[i];
            if (config == null)
                continue;

            config.chance = Mathf.Clamp(config.chance, 0f, 100f);
            config.quantidadeMinima = Mathf.Max(0, config.quantidadeMinima);
            config.quantidadeMaxima = Mathf.Max(config.quantidadeMinima, config.quantidadeMaxima);
        }
    }

    private void VoltarParaPatrulha()
    {
        MudarEstado(QuantidadePontosValidos() > 0 ? EstadoSlime.Patrulhando : EstadoSlime.Parado);
    }

    private void PerderAlvoEVoltarPatrulha()
    {
        alvoPlayer = null;
        VoltarParaPatrulha();
    }

    private void EscolherProximoPontoAleatorio()
    {
        int quantidade = QuantidadePontosValidos();
        if (quantidade == 0)
        {
            indicePatrulha = -1;
            return;
        }

        if (pontosPatrulha == null || pontosPatrulha.Length == 0)
            return;

        int novoIndice = indicePatrulha;
        for (int tentativas = 0; tentativas < 16; tentativas++)
        {
            int candidato = UnityEngine.Random.Range(0, pontosPatrulha.Length);
            if (pontosPatrulha[candidato] == null)
                continue;

            if (quantidade == 1 || candidato != indicePatrulha)
            {
                novoIndice = candidato;
                break;
            }
        }

        if (novoIndice < 0 || novoIndice >= pontosPatrulha.Length || pontosPatrulha[novoIndice] == null)
            novoIndice = PrimeiroIndicePontoValido();

        indicePatrulha = novoIndice;
    }

    private Transform ObterPontoAtual()
    {
        if (pontosPatrulha == null || pontosPatrulha.Length == 0)
            return null;

        if (indicePatrulha < 0 || indicePatrulha >= pontosPatrulha.Length || pontosPatrulha[indicePatrulha] == null)
            EscolherProximoPontoAleatorio();

        return indicePatrulha >= 0 && indicePatrulha < pontosPatrulha.Length
            ? pontosPatrulha[indicePatrulha]
            : null;
    }

    private int QuantidadePontosValidos()
    {
        if (pontosPatrulha == null)
            return 0;

        int quantidade = 0;
        for (int i = 0; i < pontosPatrulha.Length; i++)
        {
            if (pontosPatrulha[i] != null)
                quantidade++;
        }

        return quantidade;
    }

    private int PrimeiroIndicePontoValido()
    {
        if (pontosPatrulha == null)
            return -1;

        for (int i = 0; i < pontosPatrulha.Length; i++)
        {
            if (pontosPatrulha[i] != null)
                return i;
        }

        return -1;
    }

    private bool UsandoAgent()
    {
        return usarNavMeshAgent &&
               agent != null &&
               agent.enabled &&
               agent.isOnNavMesh;
    }

    private float DistanciaXZ(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public void ReiniciarAposRespawn()
    {
        if (movimentoVerde != null) movimentoVerde.Reiniciar();
        morto = false;
        experienciaJaEntregue = false;
        recompensaReinJaPaga = false;
        somMorteTocado = false;
        impactoAtaqueResolvido = false;
        investidaAtaqueEmAndamento = false;
        agentPausadoPelaInvestida = false;
        agentUpdatePositionAntesInvestida = true;
        alvoPlayer = null;
        ultimoPlayerResponsavel = null;
        estadoAntesDano = EstadoSlime.Parado;
        proximaBuscaPlayer = 0f;
        proximoAtaque = 0f;
        tempoRestanteObservando = 0f;
        estadoAtual = EstadoSlime.Parado;

        vidaAtual = vidaMaxima;
        AtualizarBarraVida();

        if (canvasVida != null)
            canvasVida.gameObject.SetActive(true);

        if (rotinaDano != null)
        {
            StopCoroutine(rotinaDano);
            rotinaDano = null;
        }

        if (rotinaInvestidaAtaque != null)
        {
            StopCoroutine(rotinaInvestidaAtaque);
            rotinaInvestidaAtaque = null;
        }

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (agent == null)
            agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.Warp(transform.position);
                agent.isStopped = false;
                agent.updatePosition = true;
                agent.speed = velocidadePatrulha;
                agent.ResetPath();
            }
        }

        if (animacao != null)
            animacao.AtualizarCacheParametros();

        if (QuantidadePontosValidos() == 0 && gerarPontosAutomaticamente)
            GerarPontosDePatrulhaAutomaticos();

        EscolherProximoPontoAleatorio();

        if (QuantidadePontosValidos() > 0)
            MudarEstado(EstadoSlime.Patrulhando);
        else
            MudarEstado(EstadoSlime.Parado);
    }

    #region Movimento
    [Serializable]
    public sealed class SlimeMovimento
    {
        [SerializeField] private bool habilitado;
        public bool Habilitado => habilitado;

        [Header("Solo (metros de mundo)")]
        [SerializeField] private LayerMask camadasSolo = ~0;
        [SerializeField, Range(1f, 60f)] private float inclinacaoMaxima = 45f;
        [SerializeField, Min(0.01f)] private float toleranciaSolo = 0.12f;
        [SerializeField, Min(0.01f)] private float toleranciaDesnivel = 0.18f;
        [SerializeField, Min(0.05f)] private float quedaMaxima = 0.6f;
        [SerializeField, Min(0.1f)] private float tempoRotaBloqueada = 2f;
        [SerializeField] private Collider colliderModelo;
        [Header("Investida propria do Vermelho")]
        [Tooltip("Habilita o perfil do Vermelho: arco de duracao fixa, animacoes e volume de respawn proprios.")]
        [SerializeField] private bool perfilVermelho;
        private bool controlandoArco, gravidadeAntesArco;
        public bool PreservarArcoInvestida => perfilVermelho;
        public float RaioMundial => Mathf.Min(capsula.bounds.extents.x, capsula.bounds.extents.z);
        public float AlturaMundial => capsula.bounds.size.y;
        public Vector3 CentroRelativo => capsula.bounds.center - transform.position;
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private Rigidbody corpo;
        private CapsuleCollider capsula;
        private Vector3 destino;
        private float velocidade;
        private bool mover, suspenso, morto;
        private Vector3 normalSolo = Vector3.up;
        private float tempoSemProgresso;
        private float melhorDistancia = float.PositiveInfinity;
        private Vector3 ultimaPosicaoComProgresso;
        private float yawAlvo;
        public bool Inicializado => transform != null && capsula != null;
        public Vector3 CentroCorpo => capsula != null ? capsula.bounds.center : (transform != null ? transform.position + Vector3.up * 0.3f : Vector3.zero);
        public bool RotaBloqueada => tempoSemProgresso >= tempoRotaBloqueada;

        private Transform transform;

        internal void Inicializar(Rigidbody rigidbody, CapsuleCollider collider, Transform raiz)
        {
            corpo = rigidbody;
            capsula = collider;
            transform = raiz;
            // Mantem o fundo do modelo na mesma altura, substituindo apenas a forma fisica.
            if (colliderModelo != null && colliderModelo.enabled)
            {
                Bounds b = colliderModelo.bounds;
                float escalaRaio = Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)));
                float escalaAltura = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
                float raio = Mathf.Max(0.05f, Mathf.Min(b.extents.x, b.extents.z) * 0.9f);
                float altura = Mathf.Max(raio * 2f, b.size.y);
                capsula.radius = raio / escalaRaio; capsula.height = altura / escalaAltura;
                capsula.center = transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + altura * 0.5f, b.center.z));
                colliderModelo.enabled = false;
            }
            capsula.direction = 1; capsula.enabled = true;
            corpo.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            corpo.interpolation = RigidbodyInterpolation.Interpolate;
            yawAlvo = transform.eulerAngles.y;
        }

        public bool AlturaDestinoCompativel(Vector3 alvo)
            => Mathf.Abs(alvo.y - transform.position.y) <= Mathf.Max(quedaMaxima, capsula.bounds.size.y + toleranciaDesnivel);
        public bool PodeSaltarPara(Vector3 alvo, float alturaSalto)
        {
            Bounds b = capsula.bounds;
            float alturaSondagem = toleranciaDesnivel + Mathf.Max(0f, alturaSalto);
            Vector3 origem = new Vector3(alvo.x, b.min.y + alturaSondagem, alvo.z);
            return ProcurarSolo(origem, alturaSondagem + quedaMaxima, out _);
        }
        public bool TentarProjetarPontoPatrulha(Vector3 alvo, out Vector3 ponto)
        {
            ponto = default;
            Bounds b = capsula.bounds;
            Vector3 origem = new Vector3(alvo.x, b.min.y + toleranciaDesnivel, alvo.z);
            if (!ProcurarSolo(origem, toleranciaDesnivel + quedaMaxima, out var solo))
                return false;

            ponto = solo.point + Vector3.up * (transform.position.y - b.min.y);
            return true;
        }

        public void SolicitarMovimento(Vector3 alvo, float rapidez)
        {
            if (!mover || (perfilVermelho && (alvo - destino).sqrMagnitude > 0.25f))
            {
                ultimaPosicaoComProgresso = corpo.position;
                melhorDistancia = float.PositiveInfinity;
                tempoSemProgresso = 0f;
            }
            destino = alvo; velocidade = Mathf.Max(0f, rapidez); mover = velocidade > 0f;
        }
        public void Parar() { mover = false; tempoSemProgresso = 0f; }
        public void Olhar(Vector3 alvo)
        {
            Vector3 dir = alvo - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) yawAlvo = Quaternion.LookRotation(dir).eulerAngles.y;
        }
        public void Suspender(bool valor)
        {
            if (!valor && controlandoArco)
            {
                corpo.useGravity = gravidadeAntesArco;
                controlandoArco = false;
                corpo.linearVelocity = new Vector3(0f, Mathf.Min(0f, corpo.linearVelocity.y), 0f);
            }
            suspenso = valor; mover = false; tempoSemProgresso = 0f;
            if (corpo != null && !corpo.isKinematic)
                corpo.linearVelocity = new Vector3(0f, corpo.linearVelocity.y, 0f);
        }
        public void IniciarArco(Vector3 direcao)
        {
            Suspender(true);
            gravidadeAntesArco = corpo.useGravity; controlandoArco = true;
            corpo.useGravity = false; corpo.linearVelocity = Vector3.zero; corpo.angularVelocity = Vector3.zero;
            corpo.MoveRotation(Quaternion.LookRotation(direcao, Vector3.up));
        }
        internal static Vector3 CalcularArco(Vector3 inicio, Vector3 direcao, float distancia, float altura, float progresso)
        {
            float t = Mathf.Clamp01(progresso);
            return inicio + direcao * (distancia * t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * altura);
        }
        public bool MoverArco(Vector3 alvo, out Collider bloqueio, out bool aterrissou)
        {
            bloqueio = null;
            aterrissou = false;
            Vector3 delta = alvo - corpo.position;
            float distancia = delta.magnitude;
            if (distancia <= 0.0001f) return false;
            Bounds b = capsula.bounds;
            float raio = Mathf.Min(b.extents.x, b.extents.z) * 0.95f;
            Vector3 baixo = b.center; baixo.y = b.min.y + raio;
            Vector3 alto = b.center; alto.y = Mathf.Max(baixo.y, b.max.y - raio);
            int n = Physics.CapsuleCastNonAlloc(baixo, alto, raio, delta.normalized, hits, distancia + 0.02f, ~0, QueryTriggerInteraction.Ignore);
            if (n == hits.Length) return true;
            float menor = distancia + 0.02f;
            for (int i = 0; i < n; i++)
            {
                var hit = hits[i];
                Collider col = hit.collider;
                if (Ignorar(col) || col.GetComponentInParent<SlimeIA>() != null ||
                    (hit.distance <= 0.001f && Vector3.Dot(delta, hit.normal) >= 0f) ||
                    hit.distance >= menor) continue;

                menor = hit.distance;
                bloqueio = col;
                aterrissou = delta.y <= 0f && (camadasSolo.value & (1 << col.gameObject.layer)) != 0 &&
                    SoloTransitavel(hit.normal, inclinacaoMaxima) &&
                    col.GetComponentInParent<StatusPlayer>() == null &&
                    col.GetComponentInParent<Escudo>() == null &&
                    col.GetComponentInParent<Espada>() == null;
            }
            corpo.MovePosition(bloqueio != null ? corpo.position + delta.normalized * Mathf.Max(0f, menor - 0.02f) : alvo);
            return bloqueio != null;
        }
        public void Reiniciar() { morto = false; Suspender(false); melhorDistancia = float.PositiveInfinity; }
        public void Morrer() { morto = true; Suspender(true); }

        private bool Ignorar(Collider col)
        {
            return col == null || col.transform.IsChildOf(transform) || col.transform == transform;
        }
        private bool ProcurarSolo(Vector3 centro, float profundidade, out RaycastHit solo)
        {
            solo = default;
            int n = Physics.RaycastNonAlloc(centro, Vector3.down, hits, profundidade, camadasSolo, QueryTriggerInteraction.Ignore);
            if (n == hits.Length) return false; // Sem resultado completo, nao arriscar um salto/queda.
            float menor = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (Ignorar(h.collider) || h.collider.GetComponentInParent<StatusPlayer>() != null ||
                    h.collider.GetComponentInParent<SlimeIA>() != null || h.distance >= menor) continue;
                menor = h.distance; solo = h;
            }
            return solo.collider != null && SoloTransitavel(solo.normal, inclinacaoMaxima);
        }
        internal static bool SoloTransitavel(Vector3 normal, float limite)
            => normal.y >= Mathf.Cos(Mathf.Clamp(limite, 1f, 60f) * Mathf.Deg2Rad);
        internal static Vector3 VelocidadeNoSolo(Vector3 direcao, Vector3 normal, float rapidez)
        {
            Vector3 tangente = Vector3.ProjectOnPlane(direcao, normal);
            return tangente.sqrMagnitude > 0.0001f ? tangente.normalized * Mathf.Max(0f, rapidez) : Vector3.zero;
        }

        private bool PassoSeguro(Vector3 dir, float distancia)
        {
            Bounds b = capsula.bounds;
            Vector3 origem = new Vector3(b.center.x, b.min.y + toleranciaDesnivel, b.center.z) + dir * distancia;
            if (!ProcurarSolo(origem, toleranciaDesnivel + quedaMaxima, out var frente)) return false;
            // Um cast acima do pequeno desnivel nao confunde a ondulacao com uma parede.
            float raio = Mathf.Max(0.03f, Mathf.Min(b.extents.x, b.extents.z) * 0.8f);
            Vector3 baixo = new Vector3(b.center.x, b.min.y + raio + toleranciaDesnivel, b.center.z);
            Vector3 alto = new Vector3(b.center.x, Mathf.Max(baixo.y, b.max.y - raio), b.center.z);
            int n = Physics.CapsuleCastNonAlloc(baixo, alto, raio, dir, hits, distancia, ~0, QueryTriggerInteraction.Ignore);
            if (n == hits.Length) return false;
            for (int i = 0; i < n; i++)
                if (!Ignorar(hits[i].collider) && !SoloTransitavel(hits[i].normal, inclinacaoMaxima)) return false;
            return true;
        }

        internal void AtualizarFisica()
        {
            if (corpo == null || corpo.isKinematic || suspenso || morto) return;
            corpo.MoveRotation(Quaternion.Slerp(corpo.rotation, Quaternion.Euler(0f, yawAlvo, 0f), 8f * Time.fixedDeltaTime));
            Bounds b = capsula.bounds;
            bool noSolo = ProcurarSolo(new Vector3(b.center.x, b.min.y + toleranciaDesnivel, b.center.z),
                toleranciaDesnivel + toleranciaSolo, out var solo);
            normalSolo = noSolo ? solo.normal : Vector3.up;
            Vector3 dir = destino - corpo.position; dir.y = 0f;
            Vector3 horizontal = Vector3.zero;
            if (mover && dir.sqrMagnitude > 0.0025f && noSolo)
            {
                dir.Normalize(); float passo = Mathf.Max(0.15f, velocidade * Time.fixedDeltaTime + 0.1f);
                if (!PassoSeguro(dir, passo))
                {
                    Vector3 esquerda = Quaternion.Euler(0f, -55f, 0f) * dir;
                    Vector3 direita = Quaternion.Euler(0f, 55f, 0f) * dir;
                    if (PassoSeguro(esquerda, passo)) dir = esquerda;
                    else if (PassoSeguro(direita, passo)) dir = direita;
                    else dir = Vector3.zero;
                }
                horizontal = VelocidadeNoSolo(dir, normalSolo, velocidade);

            }
            if (mover && !perfilVermelho)
            {
                Vector3 deslocamento = corpo.position - ultimaPosicaoComProgresso;
                deslocamento.y = 0f;
                if (deslocamento.sqrMagnitude >= 0.02f * 0.02f)
                {
                    ultimaPosicaoComProgresso = corpo.position;
                    tempoSemProgresso = 0f;
                }
                else
                {
                    Vector3 ateDestino = destino - corpo.position;
                    ateDestino.y = 0f;
                    tempoSemProgresso = ateDestino.sqrMagnitude <= 0.0025f ? 0f : tempoSemProgresso + Time.fixedDeltaTime;
                }
            }
            else if (mover)
            {
                Vector3 ateDestino = destino - corpo.position; ateDestino.y = 0f;
                float distancia = ateDestino.magnitude;
                if (distancia < melhorDistancia - 0.02f) { melhorDistancia = distancia; tempoSemProgresso = 0f; }
                else tempoSemProgresso += Time.fixedDeltaTime;
            }
            else tempoSemProgresso = 0f;
            // Sem contato, conserva a velocidade vertical: a gravidade continua sendo a unica aceleracao.
            bool apoiado = noSolo && solo.distance - toleranciaDesnivel <= 0.025f && corpo.linearVelocity.y <= 0.5f;
            float vertical = apoiado ? horizontal.y : corpo.linearVelocity.y;
            corpo.linearVelocity = new Vector3(horizontal.x, vertical, horizontal.z);
        }

    }
    #endregion

    #region Deteccao
    [Serializable]
    private sealed class SlimeDeteccao
    {
        [SerializeField] private bool habilitado;
        public bool Habilitado => habilitado;

        private Transform transform;
        internal void Inicializar(Transform raiz) => transform = raiz;

        [SerializeField, Min(0.05f)] private float intervaloVisao = 0.15f;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private Transform jogador, corpoVR;
        private float proximaConsulta;
        private bool ultimaVisao;
        private Transform ultimoAlvo;
        private Vector3 ultimoDestino;

        private sealed class CacheJogadores { public GameObject[] itens; public float consulta; }
        private static readonly Dictionary<string, CacheJogadores> jogadoresPorTag = new Dictionary<string, CacheJogadores>();
        public Transform ProcurarJogador(string tagJogador, Vector3 origem, Vector3 frente, float raio, float angulo, LayerMask obstaculos)
        {
            if (string.IsNullOrWhiteSpace(tagJogador)) return null;
            if (!jogadoresPorTag.TryGetValue(tagJogador, out var cache))
            { cache = new CacheJogadores(); jogadoresPorTag.Add(tagJogador, cache); }
            if (cache.itens == null || Time.time >= cache.consulta || Time.time < cache.consulta - 0.75f)
            {
                try { cache.itens = GameObject.FindGameObjectsWithTag(tagJogador); }
                catch (UnityException) { cache.itens = System.Array.Empty<GameObject>(); }
                cache.consulta = Time.time + 0.75f;
            }
            Transform melhor = null; float distancia = float.PositiveInfinity;
            foreach (var candidato in cache.itens)
            {
                if (candidato == null || !candidato.activeInHierarchy) continue;
                float d = (PosicaoJogador(candidato.transform) - origem).sqrMagnitude;
                if (d < distancia && Visivel(candidato.transform, origem, frente, raio, angulo, obstaculos))
                { melhor = candidato.transform; distancia = d; }
            }
            return melhor;
        }
        public Vector3 PosicaoJogador(Transform raiz)
        {
            if (raiz == null) return transform.position;
            if (jogador != raiz || corpoVR == null)
            {
                jogador = raiz;
                var avatar = raiz.GetComponent<AvatarBody>();
                corpoVR = avatar != null ? avatar.corpoAvatar : null;
                if (corpoVR == null)
                {
                    var cam = raiz.GetComponentInChildren<Camera>();
                    corpoVR = cam != null ? cam.transform : raiz;
                }
            }
            return corpoVR.position;
        }
        public bool Visivel(Transform raiz, Vector3 origem, Vector3 frente, float raio, float angulo, LayerMask obstaculos)
        {
            if (raiz == null) return false;
            Vector3 destino = PosicaoJogador(raiz);
            if (ultimoAlvo == raiz && Time.time < proximaConsulta && (destino - ultimoDestino).sqrMagnitude < 0.04f) return ultimaVisao;
            ultimoAlvo = raiz; ultimoDestino = destino; proximaConsulta = Time.time + intervaloVisao;
            Vector3 dir = destino - origem;
            Vector3 plano = dir; plano.y = 0f;
            ultimaVisao = dir.sqrMagnitude <= raio * raio &&
                (plano.sqrMagnitude < 0.0001f || Vector3.Angle(frente, plano) <= angulo * 0.5f);
            if (!ultimaVisao || dir.sqrMagnitude < 0.0001f) return ultimaVisao;
            int n = Physics.RaycastNonAlloc(origem, dir.normalized, hits, dir.magnitude, obstaculos, QueryTriggerInteraction.Ignore);
            if (n == hits.Length) return ultimaVisao = false;
            for (int i = 0; i < n; i++)
            {
                var col = hits[i].collider;
                if (col == null || col.transform.IsChildOf(transform) || col.transform == transform ||
                    col.transform.IsChildOf(raiz) || col.transform == raiz) continue;
                ultimaVisao = false; break;
            }
            return ultimaVisao;
        }
    }
    #endregion

    #region Animacao
    [Serializable]
    public sealed class SlimeAnimacao
    {
        [SerializeField] private bool habilitado;
        public bool Habilitado => habilitado;

        [Tooltip("Se vazio, tenta encontrar automaticamente (no proprio objeto ou nos filhos).")]
        [SerializeField] private Animator animator;

        // Hashes dos parametros. Os nomes precisam bater EXATAMENTE com os
        // parametros criados no Animator Controller (aba Parameters).
        private static readonly int AndarHash = Animator.StringToHash("Andar");
        private static readonly int PatrulhaHash = Animator.StringToHash("Patrulha");
        private static readonly int AlertaHash = Animator.StringToHash("Alerta");
        private static readonly int AtaqueHash = Animator.StringToHash("Ataque");
        private static readonly int DanoHash = Animator.StringToHash("Dano");
        private static readonly int MorrerHash = Animator.StringToHash("Morrer");
        private static readonly int MortoHash = Animator.StringToHash("Morto");
        private static readonly int CorrerHash = Animator.StringToHash("Correr");

        // Indica se cada parametro realmente existe no Animator Controller atual.
        private bool temAndar;
        private bool temPatrulha;
        private bool temAlerta;
        private bool temAtaque;
        private bool temDano;
        private bool temMorrer;
        private bool temMorto;
        private bool temCorrer;

        [Header("Acoes repetidas e morte (opt-in do Vermelho)")]
        [SerializeField] private bool reiniciarAcoesPorGolpe;
        [SerializeField] private AnimationClip clipMorte;
        [SerializeField, Min(0f)] private float margemTransicaoMorte = 0.25f;
        public void ReiniciarAcaoConfigurada(string estado)
        {
            if (!reiniciarAcoesPorGolpe || animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;
            int hash = Animator.StringToHash(animator.GetLayerName(0) + "." + estado);
            if (animator.HasState(0, hash)) animator.Play(hash, 0, 0f);
        }
        public float ObterTempoMorteConfigurado()
        {
            if (!reiniciarAcoesPorGolpe || animator == null || clipMorte == null) return 0f;
            return clipMorte.length / Mathf.Max(0.01f, Mathf.Abs(animator.speed)) + margemTransicaoMorte;
        }

        internal void Inicializar(Animator fallback)
        {
            if (animator == null)
                animator = fallback;
            AtualizarCacheParametros();
        }

        internal void Configurar(Animator alvo, bool reiniciar, AnimationClip morte, float margem)
        {
            animator = alvo;
            reiniciarAcoesPorGolpe = reiniciar;
            clipMorte = morte;
            margemTransicaoMorte = margem;
            AtualizarCacheParametros();
        }

        /// <summary>
        /// Re-escaneia os parametros do Animator atual. Chame isso se o Animator
        /// Controller for trocado em tempo de execucao, ou apos um respawn.
        /// </summary>
        public void AtualizarCacheParametros()
        {
            temAndar = false;
            temPatrulha = false;
            temAlerta = false;
            temAtaque = false;
            temDano = false;
            temMorrer = false;
            temMorto = false;
            temCorrer = false;

            if (animator == null)
                return;

            AnimatorControllerParameter[] parametros = animator.parameters;
            for (int i = 0; i < parametros.Length; i++)
            {
                AnimatorControllerParameter parametro = parametros[i];
                if (parametro.type != AnimatorControllerParameterType.Bool)
                    continue;

                if (parametro.nameHash == AndarHash)
                    temAndar = true;
                else if (parametro.nameHash == PatrulhaHash)
                    temPatrulha = true;
                else if (parametro.nameHash == AlertaHash)
                    temAlerta = true;
                else if (parametro.nameHash == AtaqueHash)
                    temAtaque = true;
                else if (parametro.nameHash == DanoHash)
                    temDano = true;
                else if (parametro.nameHash == MorrerHash)
                    temMorrer = true;
                else if (parametro.nameHash == MortoHash)
                    temMorto = true;
                else if (parametro.nameHash == CorrerHash)
                    temCorrer = true;
            }
        }

        // ---------------------------------------------------------------
        // API publica: um metodo por acao. O SlimeIA (ou qualquer outro
        // script de IA) so precisa chamar o metodo certo pro estado atual.
        // ---------------------------------------------------------------

        /// <summary>Parado/idle. Nenhuma animacao de acao ligada.</summary>
        public void AnimarParado()
        {
            AplicarUnico(nenhum: true);
        }

        /// <summary>Indo ate o ponto de patrulha.</summary>
        public void AnimarPatrulhando()
        {
            AplicarUnico(andar: true);
        }

        /// <summary>Chegou no ponto de patrulha, observando ao redor.</summary>
        public void AnimarObservando()
        {
            AplicarUnico(patrulha: true);
        }

        /// <summary>Avistou o player, ainda fora de alcance de perseguicao.</summary>
        public void AnimarAlerta()
        {
            AplicarUnico(alerta: true);
        }

        /// <summary>
        /// Perseguindo o player. Usa "Correr" se o Animator tiver esse parametro
        /// (ex: Tartaruga), senao usa "Andar" (ex: Slime).
        /// </summary>
        public void AnimarPerseguindo()
        {
            if (temCorrer)
                AplicarUnico(correr: true);
            else
                AplicarUnico(andar: true);
        }

        /// <summary>Atacando o player.</summary>
        public void AnimarAtacando()
        {
            AplicarUnico(ataque: true);
        }

        /// <summary>Acabou de tomar dano.</summary>
        public void AnimarTomandoDano()
        {
            AplicarUnico(dano: true);
        }

        /// <summary>Morreu.</summary>
        public void AnimarMorto()
        {
            AplicarUnico(morrer: true);
        }

        /// <summary>
        /// Liga exatamente UM dos parametros passados como true e desliga todos
        /// os outros. Garante que nunca fica mais de uma animacao de acao ativa
        /// ao mesmo tempo.
        /// </summary>
        private void AplicarUnico(
            bool nenhum = false,
            bool andar = false,
            bool patrulha = false,
            bool alerta = false,
            bool ataque = false,
            bool dano = false,
            bool morrer = false,
            bool correr = false)
        {
            SetBoolSeguro(AndarHash, temAndar, andar);
            SetBoolSeguro(PatrulhaHash, temPatrulha, patrulha);
            SetBoolSeguro(AlertaHash, temAlerta, alerta);
            SetBoolSeguro(AtaqueHash, temAtaque, ataque);
            SetBoolSeguro(DanoHash, temDano, dano);
            SetBoolSeguro(MorrerHash, temMorrer, morrer);
            SetBoolSeguro(MortoHash, temMorto, morrer);
            SetBoolSeguro(CorrerHash, temCorrer, correr);
            if (reiniciarAcoesPorGolpe)
            {
                string estado = morrer ? "Morto" : dano ? "Dano" : ataque ? "Ataque" : alerta ? "Alerta" :
                    patrulha ? "Patrulha" : (andar || correr) ? "Andar" : "Parado";
                ReiniciarAcaoConfigurada(estado);
            }
        }

        private void SetBoolSeguro(int hash, bool existeParametro, bool valor)
        {
            if (animator != null && existeParametro)
                animator.SetBool(hash, valor);
        }
    }
    #endregion
}
