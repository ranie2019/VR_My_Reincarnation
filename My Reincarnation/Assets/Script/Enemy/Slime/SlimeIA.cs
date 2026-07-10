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
        [Range(0f, 100f)] public float chance = 100f;
        public int quantidadeMinima = 1;
        public int quantidadeMaxima = 1;
        public Vector3 offsetSpawn;
    }

    [Serializable]
    public class SpawnMissaoConfig
    {
        public GameObject prefab;
        [Range(0f, 100f)] public float chance = 100f;
        public int quantidadeMinima = 1;
        public int quantidadeMaxima = 1;
        public Vector3 offsetSpawn;
        public string idMissao;
        public string idObjetivo;
        public bool exigirMissaoAtiva = true;
    }

    [Header("Vida")]
    [SerializeField] private int vidaMaxima = 5;
    [SerializeField] private int vidaAtual = 5;
    [SerializeField] private float tempoAnimacaoDano = 0.35f;
    [SerializeField] private float tempoParaMorrer = 3f;

    [Header("UI Vida")]
    [SerializeField] private Image imagemBarraVida;
    [SerializeField] private Canvas canvasVida;
    [SerializeField] private bool buscarBarraVidaAutomaticamente = true;
    [SerializeField] private bool esconderCanvasAoMorrer = true;

    [Header("Patrulha")]
    [SerializeField] private Transform[] pontosPatrulha;
    [SerializeField] private float velocidadePatrulha = 1.2f;
    [SerializeField] private float distanciaChegadaPonto = 0.35f;
    [SerializeField] private float tempoObservando = 2f;
    [SerializeField] private bool gerarPontosAutomaticamente = true;
    [SerializeField] private int quantidadePontosAuto = 4;
    [SerializeField] private float raioPatrulhaAuto = 4f;

    [Header("Campo de visao")]
    [SerializeField] private string tagPlayer = "Player";
    [SerializeField] private float raioCampoVisao = 8f;
    [SerializeField, Range(1f, 360f)] private float anguloCampoVisao = 120f;
    [SerializeField] private LayerMask layersObstaculo;
    [SerializeField] private float distanciaIniciarPerseguicao = 5f;

    [Header("Ataque")]
    [SerializeField] private float velocidadePerseguicao = 2.4f;
    [SerializeField] private float distanciaAtaque = 1.3f;

    [Header("Combate")]
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

    [Header("Investida")]
    [SerializeField] private float distanciaInvestida = 1.5f;
    [SerializeField] private float alturaInvestida = 0.6f;
    [SerializeField] private float duracaoInvestida = 0.35f;
    [SerializeField] private float cooldownAtaque = 1.2f;
    [SerializeField] private float raioVerificacaoColisaoInvestida = 0.35f;
    [SerializeField] private LayerMask layersBloqueioInvestida = ~0;
    [SerializeField] private float margemParedeInvestida = 0.15f;

    [Header("Dano recebido")]
    [SerializeField] private string[] tagsQueCausamDano;

    [Header("Respawn")]
    [SerializeField] private string idRespawnMonstro = "SlimeVerde";

    [Header("Debug")]
    [SerializeField] private bool desenharGizmos = true;
    [SerializeField] private bool desenharGizmosSempre = true;
    [SerializeField] private float alturaOlhos = 0.8f;
    [SerializeField] private Transform pontoOlhos;

    [Header("Spawn normal ao morrer")]
    [SerializeField] private SpawnNormalConfig[] prefabsSpawnNormal;

    [Header("Spawn de missao ao morrer")]
    [SerializeField] private SpawnMissaoConfig[] prefabsSpawnMissao;

    [Header("Recompensa REIN")]
    [SerializeField] private bool darReinAoMorrer = true;
    [SerializeField] private string reinMinimo = "0.01";
    [SerializeField] private string reinMaximo = "0.05";
    [SerializeField, Range(0, 5)] private int casasDecimaisSorteioRein = 5;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somAndar;
    [SerializeField] private AudioClip somAtacar;
    [SerializeField] private AudioClip somDano;
    [SerializeField] private AudioClip somMorrer;
    [SerializeField] private float volumeAndar = 1f;
    [SerializeField] private float volumeAtacar = 1f;
    [SerializeField] private float volumeDano = 1f;
    [SerializeField] private float volumeMorrer = 1f;

    [Header("Componentes opcionais")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private bool usarNavMeshAgent = true;
    private NavMeshAgent agent;
    private CharacterController characterController;

    private const float VelocidadeRotacao = 8f;
    private const float IntervaloBuscaPlayer = 0.35f;
    private const BindingFlags FlagsMembrosDano = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly int AndarHash = Animator.StringToHash("Andar");
    private static readonly int DanoHash = Animator.StringToHash("Dano");
    private static readonly int MorrerHash = Animator.StringToHash("Morrer");
    private static readonly int ObservaHash = Animator.StringToHash("Observa");
    private static readonly int AtacarHash = Animator.StringToHash("Atacar");
    private static readonly int PerseguirHash = Animator.StringToHash("Perseguir");
    private static readonly int AlertaHash = Animator.StringToHash("Alerta");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DieHash = Animator.StringToHash("Die");

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
    private bool temAndar;
    private bool temDano;
    private bool temMorrer;
    private bool temObserva;
    private bool temAtacar;
    private bool temPerseguir;
    private bool temAlerta;
    private bool temIsMoving;
    private bool temIsRunning;
    private bool temIsDead;
    private bool temAttack;
    private bool temHit;
    private bool temDie;
    private bool morto;
    private bool experienciaJaEntregue;
    private bool recompensaReinJaPaga;
    private Transform ultimoPlayerResponsavel;
    private ExperienciaInimigo experienciaInimigo;
    private static readonly string[] NomesMembrosDano =
    {
        "dano",
        "danoArma",
        "danoMachado",
        "danoPicareta",
        "danoDaArma",
        "damage",
        "Dano",
        "DanoArma",
        "Damage"
    };

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
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        experienciaInimigo = GetComponent<ExperienciaInimigo>();

        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual <= 0 ? vidaMaxima : vidaAtual, 1, vidaMaxima);
        ConfigurarUIVida();
        AtualizarBarraVida();

        if (agent != null)
        {
            agent.updateRotation = false;
            agent.speed = velocidadePatrulha;
        }

        AtualizarCacheParametrosAnimator();
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

            if (NavMesh.SamplePosition(posicaoDesejada, out NavMeshHit hit, raioPatrulhaAuto, NavMesh.AllAreas))
                posicaoFinal = hit.position;

            GameObject ponto = new GameObject($"PontoAuto_{gameObject.name}_{i}");
            ponto.transform.position = posicaoFinal;
            pontosValidos.Add(ponto.transform);
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

    private void OnDisable()
    {
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

    private void AtualizarBarraVida()
    {
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
        RegistrarPrimeiroAtacante(origemDano);
        AplicarDanoRecebido(dano, IdentificarPlayerResponsavel(null, origemDano != null ? origemDano.transform : null));
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

        SetTriggerSeguro(HitHash, temHit);

        if (rotinaDano != null)
            StopCoroutine(rotinaDano);

        if (estadoAtual != EstadoSlime.TomandoDano)
            estadoAntesDano = estadoAtual;

        rotinaDano = StartCoroutine(RotinaDano());
    }

    public void AcionarDano()
    {
        ReceberDano(1);
    }

    public void AcionarMorte()
    {
        Morrer();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        TentarReceberDanoPorContato(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        TentarReceberDanoPorContato(other);
    }

    private void TentarReceberDanoPorContato(Collider outroCollider)
    {
        if (morto || outroCollider == null)
            return;

        if (!TentarObterTagAutorizada(outroCollider, out Transform origemTag))
            return;

        if (!TentarObterDanoDaArma(outroCollider, origemTag, out int dano))
            return;

        dano = Mathf.Max(0, dano);
        if (dano == 0)
            return;

        RegistrarPrimeiroAtacante(outroCollider.gameObject);
        AplicarDanoRecebido(dano, IdentificarPlayerResponsavel(outroCollider, origemTag));
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

    private bool TentarObterTagAutorizada(Collider outroCollider, out Transform origemTag)
    {
        origemTag = null;

        if (tagsQueCausamDano == null || tagsQueCausamDano.Length == 0 || outroCollider == null)
            return false;

        Transform transformComTag = ProcurarTransformComTagPermitida(outroCollider.transform);
        if (transformComTag != null)
        {
            origemTag = transformComTag;
            return true;
        }

        Rigidbody rbContato = outroCollider.attachedRigidbody;
        if (rbContato != null)
        {
            transformComTag = ProcurarTransformComTagPermitida(rbContato.transform);
            if (transformComTag != null)
            {
                origemTag = transformComTag;
                return true;
            }
        }

        Transform root = outroCollider.transform.root;
        if (root != null && TagEstaPermitida(root.tag))
        {
            origemTag = root;
            return true;
        }

        return false;
    }

    private Transform ProcurarTransformComTagPermitida(Transform origem)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (TagEstaPermitida(atual.tag))
                return atual;

            atual = atual.parent;
        }

        return null;
    }

    private bool TagEstaPermitida(string tagAtual)
    {
        if (string.IsNullOrWhiteSpace(tagAtual) || tagsQueCausamDano == null)
            return false;

        for (int i = 0; i < tagsQueCausamDano.Length; i++)
        {
            string tagPermitida = tagsQueCausamDano[i];
            if (string.IsNullOrWhiteSpace(tagPermitida))
                continue;

            if (string.Equals(tagAtual, tagPermitida, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private bool TentarObterDanoDaArma(Collider outroCollider, Transform origemTag, out int dano)
    {
        dano = 0;

        if (TentarObterDanoEmTransform(outroCollider != null ? outroCollider.transform : null, out dano))
            return true;

        if (outroCollider != null && outroCollider.attachedRigidbody != null &&
            TentarObterDanoEmTransform(outroCollider.attachedRigidbody.transform, out dano))
            return true;

        if (TentarObterDanoEmTransform(origemTag, out dano))
            return true;

        Transform root = outroCollider != null ? outroCollider.transform.root : null;
        return TentarObterDanoEmTransform(root, out dano);
    }

    private bool TentarObterDanoEmTransform(Transform origem, out int dano)
    {
        dano = 0;
        if (origem == null)
            return false;

        HashSet<Component> componentesVisitados = new HashSet<Component>();
        Component[] componentesPais = origem.GetComponentsInParent<Component>(true);
        for (int i = 0; i < componentesPais.Length; i++)
        {
            if (TentarObterDanoEmComponente(componentesPais[i], componentesVisitados, out dano))
                return true;
        }

        Component[] componentesFilhos = origem.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < componentesFilhos.Length; i++)
        {
            if (TentarObterDanoEmComponente(componentesFilhos[i], componentesVisitados, out dano))
                return true;
        }

        return false;
    }

    private bool TentarObterDanoEmComponente(Component componente, HashSet<Component> componentesVisitados, out int dano)
    {
        dano = 0;
        if (componente == null || !componentesVisitados.Add(componente))
            return false;

        Type tipo = componente.GetType();
        for (int i = 0; i < NomesMembrosDano.Length; i++)
        {
            FieldInfo campo = tipo.GetField(NomesMembrosDano[i], FlagsMembrosDano);
            if (campo != null && TentarConverterDano(campo.GetValue(componente), out dano))
                return true;

            PropertyInfo propriedade = tipo.GetProperty(NomesMembrosDano[i], FlagsMembrosDano);
            if (propriedade != null &&
                propriedade.GetIndexParameters().Length == 0 &&
                TentarObterValorPropriedade(propriedade, componente, out object valor) &&
                TentarConverterDano(valor, out dano))
            {
                return true;
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

    private bool TentarConverterDano(object valor, out int dano)
    {
        dano = 0;
        if (valor == null)
            return false;

        switch (valor)
        {
            case int inteiro:
                dano = inteiro;
                return true;
            case float flutuante:
                dano = Mathf.RoundToInt(flutuante);
                return true;
            case double duplo:
                dano = Mathf.RoundToInt((float)duplo);
                return true;
            case long longo:
                dano = longo > int.MaxValue ? int.MaxValue : (int)longo;
                return true;
            case short curto:
                dano = curto;
                return true;
            case byte b:
                dano = b;
                return true;
            case string texto:
                return int.TryParse(texto, out dano);
            default:
                return false;
        }
    }

    private Transform IdentificarPlayerResponsavel(Collider outroCollider, Transform origemTag)
    {
        Transform player = ProcurarTransformComTagPlayer(origemTag);
        if (player != null)
            return player;

        if (outroCollider != null)
        {
            player = ProcurarTransformComTagPlayer(outroCollider.transform);
            if (player != null)
                return player;

            if (outroCollider.attachedRigidbody != null)
            {
                player = ProcurarTransformComTagPlayer(outroCollider.attachedRigidbody.transform);
                if (player != null)
                    return player;
            }
        }

        return alvoPlayer;
    }

    private Transform IdentificarPlayerResponsavel(Collider outroCollider)
    {
        return IdentificarPlayerResponsavel(outroCollider, outroCollider != null ? outroCollider.transform : null);
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

        MoverPara(ponto.position, velocidadePatrulha);

        if (DistanciaXZ(transform.position, ponto.position) <= distanciaChegadaPonto)
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
        OlharPara(alvoPlayer.position);

        if (DistanciaXZ(transform.position, alvoPlayer.position) <= distanciaIniciarPerseguicao)
            MudarEstado(EstadoSlime.Perseguindo);
    }

    private void AtualizarPerseguicao()
    {
        if (!TemAlvoVisivel())
        {
            PerderAlvoEVoltarPatrulha();
            return;
        }

        float distancia = DistanciaXZ(transform.position, alvoPlayer.position);
        if (distancia <= distanciaAtaque ||
            (ExisteBloqueadorEntreInimigoEPlayer(alvoPlayer, out RaycastHit hitBloqueio) && hitBloqueio.distance <= AlcanceEfetivoAtaque()))
        {
            MudarEstado(EstadoSlime.Atacando);
            return;
        }

        MoverPara(alvoPlayer.position, velocidadePerseguicao);
        OlharPara(alvoPlayer.position);
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
            OlharPara(alvoPlayer.position);
            return;
        }

        PararMovimento();
        OlharPara(alvoPlayer.position);

        float distancia = DistanciaXZ(transform.position, alvoPlayer.position);
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

        proximoAtaque = Time.time + cooldownAtaque;
        TocarSomUmaVez(somAtacar, volumeAtacar);
        SetTriggerSeguro(AttackHash, temAttack);

        if (rotinaInvestidaAtaque != null)
            StopCoroutine(rotinaInvestidaAtaque);

        rotinaInvestidaAtaque = StartCoroutine(InvestidaAtaque());
    }

    private IEnumerator InvestidaAtaque()
    {
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
        Vector3 direcao = alvoPlayer != null ? alvoPlayer.position - transform.position : transform.forward;
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
            if (col == null || PertenceAEsteInimigo(col))
                continue;

            hitBloqueio = hits[i];
            return true;
        }

        return false;
    }

    private Vector3 ObterOrigemCastInvestida(Vector3 posicaoBase)
    {
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
        if (col == null || alvoPlayer == null || impactoAtaqueResolvido)
            return;

        if (ColliderBloqueiaMesmoSendoDoPlayer(col) || !PertenceAoPlayerAlvo(col, alvoPlayer))
        {
            impactoAtaqueResolvido = true;
            RegistrarBloqueioSeForEscudo(col, alvoPlayer);
            return;
        }

        impactoAtaqueResolvido = true;
        TentarAplicarDanoNoPlayer(alvoPlayer, danoAtaque);
    }

    private void ResolverImpactoAtaque()
    {
        if (morto || alvoPlayer == null || estadoAtual != EstadoSlime.Atacando)
            return;

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
            if (col == null || PertenceAEsteInimigo(col))
                continue;

            primeiroHit = hits[i];
            return true;
        }

        return false;
    }

    private bool TentarAplicarDanoNoPlayer(Transform playerAlvo, int dano)
    {
        if (playerAlvo == null || dano <= 0)
            return false;

        float danoConfigurado = Mathf.Max(0f, ObterDano());
        if (danoConfigurado <= 0f)
            return false;

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

        Escudo escudo = col.GetComponentInParent<Escudo>();
        if (escudo != null && !escudo.Quebrado)
            return true;

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

    private void RegistrarBloqueioSeForEscudo(Collider col, Transform playerAlvo)
    {
        if (col == null)
            return;

        Escudo escudo = col.GetComponentInParent<Escudo>();
        if (escudo != null && escudo.EstaProtegendoPlayer(playerAlvo))
            escudo.RegistrarBloqueio(gameObject);
    }

    private Vector3 ObterOrigemAtaque()
    {
        if (pontoOrigemAtaque != null)
            return pontoOrigemAtaque.position;

        return transform.position + Vector3.up * alturaOrigemAtaque;
    }

    private Vector3 ObterDirecaoAtaque(Transform playerAlvo)
    {
        Vector3 origem = ObterOrigemAtaque();
        Vector3 direcao = playerAlvo != null
            ? playerAlvo.position + Vector3.up * alturaOrigemAtaque - origem
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
        return pontoOlhos != null ? pontoOlhos.position : transform.position + Vector3.up * alturaOlhos;
    }

    private void OnDrawGizmos()
    {
        if (desenharGizmos && desenharGizmosSempre)
            DesenharGizmosCampoVisao();
    }

    private void OnDrawGizmosSelected()
    {
        if (desenharGizmos && !desenharGizmosSempre)
            DesenharGizmosCampoVisao();
    }

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
        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    private void MudarEstado(EstadoSlime novoEstado)
    {
        if (morto && novoEstado != EstadoSlime.Morto)
            return;

        if (estadoAtual == novoEstado)
            return;

        estadoAtual = novoEstado;
        AtualizarAnimatorPorEstado();
        AtualizarAudioPorEstado();
    }

    private void AtualizarAnimatorPorEstado()
    {
        switch (estadoAtual)
        {
            case EstadoSlime.Patrulhando:
                AtualizarAnimator(true, false, false, false, false, false, false, true, false, false);
                break;
            case EstadoSlime.Observando:
                AtualizarAnimator(false, true, false, false, false, false, false, false, false, false);
                break;
            case EstadoSlime.Alerta:
                AtualizarAnimator(false, false, true, false, false, false, false, false, false, false);
                break;
            case EstadoSlime.Perseguindo:
                AtualizarAnimator(true, false, false, true, false, false, false, true, true, false);
                break;
            case EstadoSlime.Atacando:
                AtualizarAnimator(false, false, false, true, true, false, false, false, false, false);
                break;
            case EstadoSlime.TomandoDano:
                AtualizarAnimator(false, false, false, false, false, true, false, false, false, false);
                break;
            case EstadoSlime.Morto:
                AtualizarAnimator(false, false, false, false, false, false, true, false, false, true);
                SetTriggerSeguro(DieHash, temDie);
                break;
            default:
                AtualizarAnimator(false, false, false, false, false, false, false, false, false, false);
                break;
        }
    }

    private void AtualizarAnimator(
        bool andar,
        bool observa,
        bool alerta,
        bool perseguir,
        bool atacar,
        bool dano,
        bool morrer,
        bool isMoving,
        bool isRunning,
        bool isDead)
    {
        SetBoolSeguro(AndarHash, temAndar, andar);
        SetBoolSeguro(ObservaHash, temObserva, observa);
        SetBoolSeguro(AlertaHash, temAlerta, alerta);
        SetBoolSeguro(PerseguirHash, temPerseguir, perseguir);
        SetBoolSeguro(AtacarHash, temAtacar, atacar);
        SetBoolSeguro(DanoHash, temDano, dano);
        SetBoolSeguro(MorrerHash, temMorrer, morrer);
        SetBoolSeguro(IsMovingHash, temIsMoving, isMoving);
        SetBoolSeguro(IsRunningHash, temIsRunning, isRunning);
        SetBoolSeguro(IsDeadHash, temIsDead, isDead);
    }

    private void SetBoolSeguro(int hash, bool existeParametro, bool valor)
    {
        if (animator != null && existeParametro)
            animator.SetBool(hash, valor);
    }

    private void SetTriggerSeguro(int hash, bool existeParametro)
    {
        if (animator != null && existeParametro)
            animator.SetTrigger(hash);
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
        EntregarExperienciaAoMorrer();
        PagarRecompensaReinAoPrimeiroAtacante();
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
        else
        {
            { }
        }

        if (esconderCanvasAoMorrer && canvasVida != null)
            canvasVida.gameObject.SetActive(false);

        Destroy(gameObject, tempoParaMorrer);
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
        if (prefabsSpawnNormal == null)
            return;

        for (int i = 0; i < prefabsSpawnNormal.Length; i++)
        {
            SpawnNormalConfig config = prefabsSpawnNormal[i];
            if (config == null || config.prefab == null || !PassouNaChance(config.chance))
                continue;

            SpawnarPrefab(config.prefab, config.offsetSpawn, config.quantidadeMinima, config.quantidadeMaxima);
        }
    }

    private void SpawnarPrefabsMissao()
    {
        if (prefabsSpawnMissao == null)
            return;

        Transform player = ultimoPlayerResponsavel != null ? ultimoPlayerResponsavel : alvoPlayer;
        if (player == null)
            return;

        for (int i = 0; i < prefabsSpawnMissao.Length; i++)
        {
            SpawnMissaoConfig config = prefabsSpawnMissao[i];
            if (config == null || config.prefab == null)
                continue;

            if (!PlayerTemMissaoVinculada(player, config) || !PassouNaChance(config.chance))
                continue;

            SpawnarPrefab(config.prefab, config.offsetSpawn, config.quantidadeMinima, config.quantidadeMaxima);
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

    private bool PassouNaChance(float chance)
    {
        return UnityEngine.Random.Range(0f, 100f) <= Mathf.Clamp(chance, 0f, 100f);
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

            config.chance = Mathf.Clamp(config.chance, 0f, 100f);
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

    private void AtualizarCacheParametrosAnimator()
    {
        temAndar = false;
        temDano = false;
        temMorrer = false;
        temObserva = false;
        temAtacar = false;
        temPerseguir = false;
        temAlerta = false;
        temIsMoving = false;
        temIsRunning = false;
        temIsDead = false;
        temAttack = false;
        temHit = false;
        temDie = false;

        if (animator == null)
            return;

        AnimatorControllerParameter[] parametros = animator.parameters;
        for (int i = 0; i < parametros.Length; i++)
        {
            AnimatorControllerParameter parametro = parametros[i];

            if (parametro.nameHash == AndarHash && parametro.type == AnimatorControllerParameterType.Bool)
                temAndar = true;
            else if (parametro.nameHash == DanoHash && parametro.type == AnimatorControllerParameterType.Bool)
                temDano = true;
            else if (parametro.nameHash == MorrerHash && parametro.type == AnimatorControllerParameterType.Bool)
                temMorrer = true;
            else if (parametro.nameHash == ObservaHash && parametro.type == AnimatorControllerParameterType.Bool)
                temObserva = true;
            else if (parametro.nameHash == AtacarHash && parametro.type == AnimatorControllerParameterType.Bool)
                temAtacar = true;
            else if (parametro.nameHash == PerseguirHash && parametro.type == AnimatorControllerParameterType.Bool)
                temPerseguir = true;
            else if (parametro.nameHash == AlertaHash && parametro.type == AnimatorControllerParameterType.Bool)
                temAlerta = true;
            else if (parametro.nameHash == IsMovingHash && parametro.type == AnimatorControllerParameterType.Bool)
                temIsMoving = true;
            else if (parametro.nameHash == IsRunningHash && parametro.type == AnimatorControllerParameterType.Bool)
                temIsRunning = true;
            else if (parametro.nameHash == IsDeadHash && parametro.type == AnimatorControllerParameterType.Bool)
                temIsDead = true;
            else if (parametro.nameHash == AttackHash && parametro.type == AnimatorControllerParameterType.Trigger)
                temAttack = true;
            else if (parametro.nameHash == HitHash && parametro.type == AnimatorControllerParameterType.Trigger)
                temHit = true;
            else if (parametro.nameHash == DieHash && parametro.type == AnimatorControllerParameterType.Trigger)
                temDie = true;
        }
    }
    public void ReiniciarAposRespawn()
    {
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

        AtualizarCacheParametrosAnimator();

        if (QuantidadePontosValidos() == 0 && gerarPontosAutomaticamente)
            GerarPontosDePatrulhaAutomaticos();

        EscolherProximoPontoAleatorio();

        if (QuantidadePontosValidos() > 0)
            MudarEstado(EstadoSlime.Patrulhando);
        else
            MudarEstado(EstadoSlime.Parado);
    }
}
