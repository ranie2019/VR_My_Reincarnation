using TMPro;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// IA do Goblin. Baseado no SlimeIA, reaproveitando: vida, UI de vida, patrulha,
/// campo de visao, estados, dano recebido, morte, spawn e recompensa.
///
/// Diferenca principal em relacao ao SlimeIA: o ataque e PARADO (sem
/// investida/pulo). O Goblin dispara a animacao de ataque, espera um pequeno
/// atraso (pra sincronizar com o golpe da animacao) e verifica se o player
/// esta no alcance pra aplicar dano.
///
/// A animacao usa o mesmo script SlimeAnimacao (ja generico o suficiente
/// pra qualquer inimigo, detecta os parametros do Animator sozinho).
/// </summary>
[DisallowMultipleComponent]
public class GoblinIA : MonoBehaviour, IDano
{
    private enum EstadoGoblin
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

    [SerializeField] private int vidaMaxima = 6;
    [SerializeField] private string nomeMonstro = "Goblin";
    [SerializeField] private TMP_Text textoNomeMonstro;
    [SerializeField, Min(1)] private int nivelMonstro = 1;
    [SerializeField] private TMP_Text textoNivel;
    [SerializeField] private int vidaAtual = 6;
    [SerializeField] private float tempoAnimacaoDano = 0.35f;
    [SerializeField] private float tempoParaMorrer = 3f;

    [SerializeField] private Image imagemBarraVida;
    [SerializeField] private Canvas canvasVida;
    [SerializeField] private bool buscarBarraVidaAutomaticamente = true;
    [SerializeField] private bool esconderCanvasAoMorrer = true;

    [SerializeField] private Transform[] pontosPatrulha;
    [SerializeField] private float velocidadePatrulha = 1.4f;
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

    [SerializeField] private float velocidadePerseguicao = 2.6f;
    [SerializeField] private float distanciaAtaque = 1.6f;

    [SerializeField, Min(0)] private int danoAtaque = 4;

    public int DanoAtaque
    {
        get => danoAtaque;
        set => danoAtaque = Mathf.Max(0, value);
    }

    [SerializeField] private Transform pontoOrigemAtaque;
    [SerializeField] private float alturaOrigemAtaque = 1.1f;
    [SerializeField] private float raioAtaque = 0.4f;
    [SerializeField] private float alcanceAtaque = 1.6f;
    [SerializeField] private LayerMask layersAtaque = ~0;

    [Tooltip("Tempo entre disparar a animacao de ataque e aplicar o dano, pra sincronizar com o golpe da animacao.")]
    [SerializeField] private float atrasoImpactoAtaque = 0.4f;
    [SerializeField] private float cooldownAtaque = 1.4f;


    [SerializeField] private string idRespawnMonstro = "Goblin";

    [SerializeField] private bool desenharGizmos = true;
    [SerializeField] private bool desenharGizmosSempre = true;
    [SerializeField] private float alturaOlhos = 1.4f;
    [SerializeField] private Transform pontoOlhos;

    [SerializeField] private SpawnNormalConfig[] prefabsSpawnNormal;

    [SerializeField] private SpawnMissaoConfig[] prefabsSpawnMissao;

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

    [SerializeField] private SlimeAnimacao animacao;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private bool usarNavMeshAgent = true;
    private NavMeshAgent agent;
    private CharacterController characterController;

    private const float VelocidadeRotacao = 8f;
    private const float IntervaloBuscaPlayer = 0.35f;

    private EstadoGoblin estadoAtual = EstadoGoblin.Parado;
    private EstadoGoblin estadoAntesDano = EstadoGoblin.Parado;
    private Transform alvoPlayer;
    private int indicePatrulha = -1;
    private float tempoRestanteObservando;
    private float proximaBuscaPlayer;
    private float proximoAtaque;
    private Coroutine rotinaDano;
    private Coroutine rotinaAtaque;
    private bool somAndarTocando;
    private bool somMorteTocado;
    private bool morto;
    private bool experienciaJaEntregue;
    private bool recompensaReinJaPaga;
    private Transform ultimoPlayerResponsavel;
    private RecompensaExperiencia experienciaInimigo = new RecompensaExperiencia();

    public int ExperienciaAoMorrer => RecompensaExperiencia.Calcular(nivelMonstro);
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
        if (animacao == null)
            animacao = GetComponent<SlimeAnimacao>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        gerenciadorMissoes = FindFirstObjectByType<GerenciadorMissoes>();

        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual <= 0 ? vidaMaxima : vidaAtual, 1, vidaMaxima);
        ConfigurarUIVida();
        AtualizarBarraVida();

        if (agent != null)
        {
            agent.updateRotation = false;
            agent.speed = velocidadePatrulha;
        }
    }

    private void OnEnable()
    {
        if (morto)
        {
            MudarEstado(EstadoGoblin.Morto);
            return;
        }

        EscolherProximoPontoAleatorio();
        MudarEstado(QuantidadePontosValidos() > 0 ? EstadoGoblin.Patrulhando : EstadoGoblin.Parado);
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

            GameObject ponto = new GameObject($"PontoAuto_{gameObject.name}_{i}")
            {
                hideFlags = HideFlags.HideInHierarchy
            };
            ponto.transform.position = posicaoFinal;
            pontosValidos.Add(ponto.transform);
        }

        if (pontosValidos.Count == 0)
            return;

        pontosPatrulha = pontosValidos.ToArray();
        indicePatrulha = -1;
        EscolherProximoPontoAleatorio();

        if (estadoAtual != EstadoGoblin.Patrulhando &&
            estadoAtual != EstadoGoblin.Alerta &&
            estadoAtual != EstadoGoblin.Perseguindo &&
            estadoAtual != EstadoGoblin.Atacando &&
            estadoAtual != EstadoGoblin.TomandoDano)
        {
            MudarEstado(EstadoGoblin.Patrulhando);
        }
    }

    private void OnDisable()
    {
        CancelarAtaquePendente();
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
        atrasoImpactoAtaque = Mathf.Max(0f, atrasoImpactoAtaque);
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
            if (imagem != null && (imagem.name == "Barra Vida" || imagem.name == "Vida Frente"))
                return imagem;
        }

        return null;
    }

    private void AtualizarBarraVida()
    {
        if (textoNomeMonstro != null)
            textoNomeMonstro.text = nomeMonstro;

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

        if (estadoAtual != EstadoGoblin.TomandoDano)
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

    private void RegistrarPrimeiroAtacante(GameObject origemDano)
    {
        if (origemDano == null)
            return;


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
            case EstadoGoblin.Patrulhando:
                AtualizarPatrulha();
                break;
            case EstadoGoblin.Observando:
                AtualizarObservacao();
                break;
            case EstadoGoblin.Alerta:
                AtualizarAlerta();
                break;
            case EstadoGoblin.Perseguindo:
                AtualizarPerseguicao();
                break;
            case EstadoGoblin.Atacando:
                AtualizarAtaque();
                break;
            case EstadoGoblin.TomandoDano:
                PararMovimento();
                break;
            default:
                if (TemAlvoVisivel())
                {
                    MudarEstado(EstadoGoblin.Alerta);
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
            MudarEstado(EstadoGoblin.Alerta);
            return;
        }

        Transform ponto = ObterPontoAtual();
        if (ponto == null)
        {
            MudarEstado(EstadoGoblin.Parado);
            return;
        }

        MoverPara(ponto.position, velocidadePatrulha);

        if (DistanciaXZ(transform.position, ponto.position) <= distanciaChegadaPonto)
        {
            tempoRestanteObservando = tempoObservando;
            MudarEstado(EstadoGoblin.Observando);
        }
    }

    private void AtualizarObservacao()
    {
        if (TemAlvoVisivel())
        {
            MudarEstado(EstadoGoblin.Alerta);
            return;
        }

        PararMovimento();
        tempoRestanteObservando -= Time.deltaTime;

        if (tempoRestanteObservando <= 0f)
        {
            EscolherProximoPontoAleatorio();
            MudarEstado(QuantidadePontosValidos() > 0 ? EstadoGoblin.Patrulhando : EstadoGoblin.Parado);
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
            MudarEstado(EstadoGoblin.Perseguindo);
    }

    private void AtualizarPerseguicao()
    {
        if (!TemAlvoVisivel())
        {
            PerderAlvoEVoltarPatrulha();
            return;
        }

        float distancia = DistanciaXZ(transform.position, alvoPlayer.position);
        if (distancia <= distanciaAtaque)
        {
            MudarEstado(EstadoGoblin.Atacando);
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

        PararMovimento();
        OlharPara(alvoPlayer.position);

        float distancia = DistanciaXZ(transform.position, alvoPlayer.position);
        if (distancia > distanciaAtaque)
        {
            MudarEstado(distancia <= distanciaIniciarPerseguicao ? EstadoGoblin.Perseguindo : EstadoGoblin.Alerta);
            return;
        }

        if (Time.time < proximoAtaque || rotinaAtaque != null)
            return;

        IniciarAtaqueParado();
    }

    private void IniciarAtaqueParado()
    {
        if (alvoPlayer == null)
            return;

        proximoAtaque = Time.time + cooldownAtaque;
        TocarSomUmaVez(somAtacar, volumeAtacar);

        if (rotinaAtaque != null)
            StopCoroutine(rotinaAtaque);

        rotinaAtaque = StartCoroutine(RotinaAtaqueParado());
    }

    /// <summary>
    /// Ataque parado: so espera o atraso configurado (pra sincronizar com o
    /// golpe da animacao) e aplica dano se o player ainda estiver no alcance.
    /// Nao move o Transform, diferente da investida do Slime.
    /// </summary>
    private IEnumerator RotinaAtaqueParado()
    {
        yield return new WaitForSeconds(atrasoImpactoAtaque);

        rotinaAtaque = null;

        if (morto || estadoAtual != EstadoGoblin.Atacando)
            yield break;

        ResolverImpactoAtaque();
    }

    private void ResolverImpactoAtaque()
    {
        if (morto || alvoPlayer == null)
            return;

        if (!TentarObterPrimeiroHitAtaque(alvoPlayer, out RaycastHit hit))
            return;

        Collider col = hit.collider;
        if (col == null)
            return;

        float danoBruto = ObterDano();
        if (Escudo.TentarInterceptarGolpe(col, gameObject, alvoPlayer, danoBruto, out float restante))
        {
            TentarAplicarDanoNoPlayer(alvoPlayer, restante);
            return;
        }

        if (!ColliderBloqueiaMesmoSendoDoPlayer(col) && PertenceAoPlayerAlvo(col, alvoPlayer))
            TentarAplicarDanoNoPlayer(alvoPlayer, danoBruto);
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

    private void CancelarAtaquePendente()
    {
        if (rotinaAtaque != null)
        {
            StopCoroutine(rotinaAtaque);
            rotinaAtaque = null;
        }
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
            if (estadoAtual == EstadoGoblin.Alerta || estadoAtual == EstadoGoblin.Perseguindo || estadoAtual == EstadoGoblin.Atacando)
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

    private void MudarEstado(EstadoGoblin novoEstado)
    {
        if (morto && novoEstado != EstadoGoblin.Morto)
            return;

        if (estadoAtual == novoEstado)
            return;

        estadoAtual = novoEstado;
        AtualizarAnimacaoPorEstado();
        AtualizarAudioPorEstado();
    }

    private void AtualizarAnimacaoPorEstado()
    {
        if (animacao == null)
            return;

        switch (estadoAtual)
        {
            case EstadoGoblin.Patrulhando:
                animacao.AnimarPatrulhando();
                break;
            case EstadoGoblin.Observando:
                animacao.AnimarObservando();
                break;
            case EstadoGoblin.Alerta:
                animacao.AnimarAlerta();
                break;
            case EstadoGoblin.Perseguindo:
                animacao.AnimarPerseguindo();
                break;
            case EstadoGoblin.Atacando:
                animacao.AnimarAtacando();
                break;
            case EstadoGoblin.TomandoDano:
                animacao.AnimarTomandoDano();
                break;
            case EstadoGoblin.Morto:
                animacao.AnimarMorto();
                break;
            default:
                animacao.AnimarParado();
                break;
        }
    }

    private void AtualizarAudioPorEstado()
    {
        if (estadoAtual == EstadoGoblin.Patrulhando || estadoAtual == EstadoGoblin.Perseguindo)
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
        MudarEstado(EstadoGoblin.TomandoDano);

        yield return new WaitForSeconds(tempoAnimacaoDano);

        rotinaDano = null;
        if (!morto)
            MudarEstado(estadoAntesDano == EstadoGoblin.Morto ? EstadoGoblin.Parado : estadoAntesDano);
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

        CancelarAtaquePendente();
        PararMovimento();
        PararSomAndar();
        MudarEstado(EstadoGoblin.Morto);
        TocarSomMorrer();
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

    private void EntregarExperienciaAoMorrer()
    {
        if (experienciaJaEntregue)
            return;

        experienciaJaEntregue = true;


        experienciaInimigo.EntregarExperiencia(ExperienciaAoMorrer);
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

        StatusPlayer status = experienciaInimigo.PrimeiroAtacante;
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
        MudarEstado(QuantidadePontosValidos() > 0 ? EstadoGoblin.Patrulhando : EstadoGoblin.Parado);
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
        experienciaInimigo = new RecompensaExperiencia();
        morto = false;
        experienciaJaEntregue = false;
        recompensaReinJaPaga = false;
        somMorteTocado = false;
        alvoPlayer = null;
        ultimoPlayerResponsavel = null;
        estadoAntesDano = EstadoGoblin.Parado;
        proximaBuscaPlayer = 0f;
        proximoAtaque = 0f;
        tempoRestanteObservando = 0f;
        estadoAtual = EstadoGoblin.Parado;

        vidaAtual = vidaMaxima;
        AtualizarBarraVida();

        if (canvasVida != null)
            canvasVida.gameObject.SetActive(true);

        if (rotinaDano != null)
        {
            StopCoroutine(rotinaDano);
            rotinaDano = null;
        }

        CancelarAtaquePendente();

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
            MudarEstado(EstadoGoblin.Patrulhando);
        else
            MudarEstado(EstadoGoblin.Parado);
    }

    private sealed class RecompensaExperiencia
    {
        public StatusPlayer PrimeiroAtacante { get; private set; }
        private bool entregue;

        public static int Calcular(int nivel)
        {
            return (int)System.Math.Min(int.MaxValue, 3L + (Mathf.Max(1, nivel) - 1L) * 2L);
        }

        public void RegistrarPrimeiroAtacante(GameObject origem)
        {
            if (PrimeiroAtacante != null || origem == null) return;

            PrimeiroAtacante = origem.GetComponentInParent<StatusPlayer>()
                ?? origem.transform.root.GetComponentInChildren<StatusPlayer>();
            if (PrimeiroAtacante != null) return;

            foreach (var componente in origem.GetComponentsInParent<MonoBehaviour>(true))
            {
                PrimeiroAtacante = EncontrarDono(componente);
                if (PrimeiroAtacante != null) return;
            }

            foreach (var componente in origem.GetComponentsInChildren<MonoBehaviour>(true))
            {
                PrimeiroAtacante = EncontrarDono(componente);
                if (PrimeiroAtacante != null) return;
            }

            var agarravel = origem.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (agarravel == null) return;
            foreach (var mao in agarravel.interactorsSelecting)
            {
                PrimeiroAtacante = mao.transform.GetComponentInParent<StatusPlayer>();
                if (PrimeiroAtacante != null) return;
            }
        }

        private static readonly string[] MetodosDono =
        {
            "GetStatusPlayerDonoAtual", "GetStatusPlayerDono", "GetStatusPlayerOwner",
            "GetDonoAtual", "GetDono", "GetOwner", "ObterDonoAtual"
        };

        private static readonly string[] PropriedadesDono =
        {
            "DonoAtual", "Dono", "Owner", "StatusPlayerDonoAtual"
        };

        private static StatusPlayer EncontrarDono(MonoBehaviour componente)
        {
            if (componente == null) return null;
            var tipo = componente.GetType();

            foreach (string nome in MetodosDono)
            {
                var metodo = tipo.GetMethod(nome, System.Type.EmptyTypes);
                if (metodo == null) continue;
                var dono = ResolverStatus(metodo.Invoke(componente, null));
                if (dono != null) return dono;
            }

            foreach (string nome in PropriedadesDono)
            {
                var propriedade = tipo.GetProperty(nome);
                if (propriedade == null || !propriedade.CanRead || propriedade.GetIndexParameters().Length != 0) continue;
                var dono = ResolverStatus(propriedade.GetValue(componente));
                if (dono != null) return dono;
            }

            return null;
        }

        private static StatusPlayer ResolverStatus(object dono)
        {
            if (dono is Component componente && componente != null)
                return componente.GetComponentInParent<StatusPlayer>();
            if (dono is GameObject objeto && objeto != null)
                return objeto.GetComponentInParent<StatusPlayer>();
            return null;
        }

        public void EntregarExperiencia(int xp)
        {
            if (entregue || xp <= 0) return;
            var jogador = PrimeiroAtacante;
            if (jogador == null)
                jogador = GameObject.FindWithTag("Player")?.GetComponentInChildren<StatusPlayer>()
                    ?? UnityEngine.Object.FindFirstObjectByType<StatusPlayer>();
            if (jogador == null) return;

            entregue = true;
            var grupo = jogador.GetComponentInParent<GrupoPlayer>();
            if (grupo == null || !grupo.TemGrupoValido())
            {
                jogador.ReceberExperiencia(xp);
                return;
            }

            var membros = grupo.GetMembrosValidos();
            if (!membros.Contains(jogador)) membros.Add(jogador);
            int parcela = xp / membros.Count;
            int resto = xp % membros.Count;
            foreach (var membro in membros)
            {
                int ganho = parcela + (membro == jogador ? resto : 0);
                if (ganho > 0) membro.ReceberExperiencia(ganho);
            }
        }
    }
}