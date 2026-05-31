using System.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class TartarugaIA : MonoBehaviour
{
    private enum EstadoTartaruga
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
    public class TagDanoPermitida
    {
        public string tag;
        public int danoFallback = 1;
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

    [Header("Patrulha")]
    [SerializeField] private Transform[] pontosPatrulha;
    [SerializeField] private float velocidadePatrulha = 1.1f;
    [SerializeField] private float distanciaChegadaPonto = 0.35f;
    [SerializeField] private float tempoObservando = 2.5f;

    [Header("Campo de visao")]
    [SerializeField] private string tagPlayer = "Player";
    [SerializeField] private float raioCampoVisao = 8f;
    [SerializeField, Range(1f, 360f)] private float anguloCampoVisao = 120f;
    [SerializeField] private LayerMask layersObstaculo;
    [SerializeField] private float distanciaIniciarPerseguicao = 5f;

    [Header("Debug visual")]
    [SerializeField] private bool desenharGizmos = true;
    [SerializeField] private bool desenharGizmosSempre = true;
    [SerializeField] private float alturaOlhos = 0.8f;
    [SerializeField] private Transform pontoOlhos;

    [Header("Perseguicao e ataque")]
    [SerializeField] private float velocidadePerseguicao = 2f;
    [SerializeField] private float distanciaAtaque = 1.3f;
    [SerializeField] private float cooldownAtaque = 1.5f;

    [Header("Vida")]
    [SerializeField] private int vidaMaxima = 5;
    [SerializeField] private int vidaAtual = 5;
    [SerializeField] private bool morto;
    [SerializeField] private float tempoAnimacaoDano = 0.35f;
    [SerializeField] private bool destruirDepoisDeMorrer;
    [SerializeField] private float tempoParaDestruirDepoisDaMorte = 3f;

    [Header("Dano recebido")]
    [SerializeField] private TagDanoPermitida[] tagsQueCausamDano;
    [SerializeField] private float cooldownReceberDano = 0.25f;

    [Header("Spawn normal ao morrer")]
    [SerializeField] private SpawnNormalConfig[] prefabsSpawnNormal;

    [Header("Spawn de missao ao morrer")]
    [SerializeField] private SpawnMissaoConfig[] prefabsSpawnMissao;

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

    [Header("Componentes")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private bool usarNavMeshAgent = true;

    private const float VelocidadeRotacao = 8f;
    private const float IntervaloBuscaPlayer = 0.35f;
    private const BindingFlags FlagsMembrosDano = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly int AndarHash = Animator.StringToHash("Andar");
    private static readonly int DanoHash = Animator.StringToHash("Dano");
    private static readonly int MorrerHash = Animator.StringToHash("Morrer");
    private static readonly int ObservaHash = Animator.StringToHash("Observa");
    private static readonly int AtaqueHash = Animator.StringToHash("Ataque");
    private static readonly int PerseguirHash = Animator.StringToHash("Perseguir");
    private static readonly int AlertaHash = Animator.StringToHash("Alerta");

    private EstadoTartaruga estadoAtual = EstadoTartaruga.Parado;
    private EstadoTartaruga estadoAntesDano = EstadoTartaruga.Parado;
    private Transform alvoPlayer;
    private int indicePatrulha = -1;
    private float tempoRestanteObservando;
    private float proximaBuscaPlayer;
    private float proximoAtaque;
    private Coroutine rotinaDano;
    private bool somAndarTocando;
    private bool somMorteTocado;
    private bool temAndar;
    private bool temDano;
    private bool temMorrer;
    private bool temObserva;
    private bool temAtaque;
    private bool temPerseguir;
    private bool temAlerta;
    private Transform ultimoPlayerResponsavel;
    private readonly Dictionary<int, float> proximoDanoPorOrigem = new();
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

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual <= 0 ? vidaMaxima : vidaAtual, 1, vidaMaxima);

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
            MudarEstado(EstadoTartaruga.Morto);
            return;
        }

        EscolherProximoPontoAleatorio();
        MudarEstado(QuantidadePontosValidos() > 0 ? EstadoTartaruga.Patrulhando : EstadoTartaruga.Parado);
    }

    private void OnDisable()
    {
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
        cooldownAtaque = Mathf.Max(0f, cooldownAtaque);
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        tempoAnimacaoDano = Mathf.Max(0f, tempoAnimacaoDano);
        tempoParaDestruirDepoisDaMorte = Mathf.Max(0f, tempoParaDestruirDepoisDaMorte);
        cooldownReceberDano = Mathf.Max(0f, cooldownReceberDano);
        NormalizarSpawnsNormais();
        NormalizarSpawnsMissao();
        volumeAndar = Mathf.Clamp01(volumeAndar);
        volumeAtacar = Mathf.Clamp01(volumeAtacar);
        volumeDano = Mathf.Clamp01(volumeDano);
        volumeMorrer = Mathf.Clamp01(volumeMorrer);
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
        ReceberDano(dano, null);
    }

    private void ReceberDano(int dano, Transform playerResponsavel)
    {
        if (morto)
            return;

        int danoFinal = Mathf.Max(0, dano);
        if (danoFinal == 0)
            return;

        if (playerResponsavel != null)
            ultimoPlayerResponsavel = playerResponsavel;

        vidaAtual -= danoFinal;
        TocarSomUmaVez(somDano, volumeDano);

        if (vidaAtual <= 0)
        {
            Morrer();
            return;
        }

        if (rotinaDano != null)
            StopCoroutine(rotinaDano);

        if (estadoAtual != EstadoTartaruga.TomandoDano)
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

        if (!TentarObterTagDanoPermitida(outroCollider, out TagDanoPermitida configTag, out Transform origemTag))
            return;

        int chaveOrigem = ObterChaveOrigemDano(outroCollider, origemTag);
        if (proximoDanoPorOrigem.TryGetValue(chaveOrigem, out float proximoPermitido) && Time.time < proximoPermitido)
            return;

        if (!TentarObterDanoDaArma(outroCollider, origemTag, out int dano))
            dano = configTag != null ? configTag.danoFallback : 0;

        dano = Mathf.Max(0, dano);
        if (dano == 0)
            return;

        proximoDanoPorOrigem[chaveOrigem] = Time.time + cooldownReceberDano;
        ReceberDano(dano, IdentificarPlayerResponsavel(outroCollider, origemTag));
    }

    private bool TentarObterTagDanoPermitida(Collider outroCollider, out TagDanoPermitida configTag, out Transform origemTag)
    {
        configTag = null;
        origemTag = null;

        if (tagsQueCausamDano == null || tagsQueCausamDano.Length == 0 || outroCollider == null)
            return false;

        Transform transformComTag = ProcurarTransformComTagPermitida(outroCollider.transform, out configTag);
        if (transformComTag != null)
        {
            origemTag = transformComTag;
            return true;
        }

        Rigidbody rbContato = outroCollider.attachedRigidbody;
        if (rbContato != null)
        {
            transformComTag = ProcurarTransformComTagPermitida(rbContato.transform, out configTag);
            if (transformComTag != null)
            {
                origemTag = transformComTag;
                return true;
            }
        }

        Transform root = outroCollider.transform.root;
        if (root != null && TentarObterConfigPorTag(root.tag, out configTag))
        {
            origemTag = root;
            return true;
        }

        return false;
    }

    private Transform ProcurarTransformComTagPermitida(Transform origem, out TagDanoPermitida configTag)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (TentarObterConfigPorTag(atual.tag, out configTag))
                return atual;

            atual = atual.parent;
        }

        configTag = null;
        return null;
    }

    private bool TentarObterConfigPorTag(string tagAtual, out TagDanoPermitida configTag)
    {
        configTag = null;

        if (string.IsNullOrWhiteSpace(tagAtual) || tagsQueCausamDano == null)
            return false;

        for (int i = 0; i < tagsQueCausamDano.Length; i++)
        {
            TagDanoPermitida config = tagsQueCausamDano[i];
            if (config == null || string.IsNullOrWhiteSpace(config.tag))
                continue;

            if (string.Equals(tagAtual, config.tag, StringComparison.Ordinal))
            {
                configTag = config;
                return true;
            }
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

        HashSet<Component> componentesVisitados = new();
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

    private int ObterChaveOrigemDano(Collider outroCollider, Transform origemTag)
    {
        if (outroCollider != null && outroCollider.attachedRigidbody != null)
            return outroCollider.attachedRigidbody.gameObject.GetInstanceID();

        if (origemTag != null)
            return origemTag.root.gameObject.GetInstanceID();

        return outroCollider != null ? outroCollider.transform.root.gameObject.GetInstanceID() : 0;
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
            case EstadoTartaruga.Patrulhando:
                AtualizarPatrulha();
                break;
            case EstadoTartaruga.Observando:
                AtualizarObservacao();
                break;
            case EstadoTartaruga.Alerta:
                AtualizarAlerta();
                break;
            case EstadoTartaruga.Perseguindo:
                AtualizarPerseguicao();
                break;
            case EstadoTartaruga.Atacando:
                AtualizarAtaque();
                break;
            case EstadoTartaruga.TomandoDano:
                PararMovimento();
                break;
            default:
                if (TemAlvoVisivel())
                {
                    MudarEstado(EstadoTartaruga.Alerta);
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
            MudarEstado(EstadoTartaruga.Alerta);
            return;
        }

        Transform ponto = ObterPontoAtual();
        if (ponto == null)
        {
            MudarEstado(EstadoTartaruga.Parado);
            return;
        }

        MoverPara(ponto.position, velocidadePatrulha);

        if (DistanciaXZ(transform.position, ponto.position) <= distanciaChegadaPonto)
        {
            tempoRestanteObservando = tempoObservando;
            MudarEstado(EstadoTartaruga.Observando);
        }
    }

    private void AtualizarObservacao()
    {
        if (TemAlvoVisivel())
        {
            MudarEstado(EstadoTartaruga.Alerta);
            return;
        }

        PararMovimento();
        tempoRestanteObservando -= Time.deltaTime;

        if (tempoRestanteObservando <= 0f)
        {
            EscolherProximoPontoAleatorio();
            MudarEstado(QuantidadePontosValidos() > 0 ? EstadoTartaruga.Patrulhando : EstadoTartaruga.Parado);
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
            MudarEstado(EstadoTartaruga.Perseguindo);
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
            MudarEstado(EstadoTartaruga.Atacando);
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
            MudarEstado(distancia <= distanciaIniciarPerseguicao ? EstadoTartaruga.Perseguindo : EstadoTartaruga.Alerta);
            return;
        }

        if (Time.time >= proximoAtaque)
        {
            proximoAtaque = Time.time + cooldownAtaque;
            TocarSomUmaVez(somAtacar, volumeAtacar);
            Debug.Log($"[TartarugaIA] {name} atacou {alvoPlayer.name}.", this);
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
            if (estadoAtual == EstadoTartaruga.Alerta || estadoAtual == EstadoTartaruga.Perseguindo || estadoAtual == EstadoTartaruga.Atacando)
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

    private void MudarEstado(EstadoTartaruga novoEstado)
    {
        if (morto && novoEstado != EstadoTartaruga.Morto)
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
            case EstadoTartaruga.Patrulhando:
                AtualizarAnimator(true, false, false, false, false, false, false);
                break;
            case EstadoTartaruga.Observando:
                AtualizarAnimator(false, true, false, false, false, false, false);
                break;
            case EstadoTartaruga.Alerta:
                AtualizarAnimator(false, false, true, false, false, false, false);
                break;
            case EstadoTartaruga.Perseguindo:
                AtualizarAnimator(true, false, false, true, false, false, false);
                break;
            case EstadoTartaruga.Atacando:
                AtualizarAnimator(false, false, false, true, true, false, false);
                break;
            case EstadoTartaruga.TomandoDano:
                AtualizarAnimator(false, false, false, false, false, true, false);
                break;
            case EstadoTartaruga.Morto:
                AtualizarAnimator(false, false, false, false, false, false, true);
                break;
            default:
                AtualizarAnimator(false, false, false, false, false, false, false);
                break;
        }
    }

    private void AtualizarAnimator(bool andar, bool observa, bool alerta, bool perseguir, bool ataque, bool dano, bool morrer)
    {
        SetBoolSeguro(AndarHash, temAndar, andar);
        SetBoolSeguro(ObservaHash, temObserva, observa);
        SetBoolSeguro(AlertaHash, temAlerta, alerta);
        SetBoolSeguro(PerseguirHash, temPerseguir, perseguir);
        SetBoolSeguro(AtaqueHash, temAtaque, ataque);
        SetBoolSeguro(DanoHash, temDano, dano);
        SetBoolSeguro(MorrerHash, temMorrer, morrer);
    }

    private void SetBoolSeguro(int hash, bool existeParametro, bool valor)
    {
        if (animator != null && existeParametro)
            animator.SetBool(hash, valor);
    }

    private void AtualizarAudioPorEstado()
    {
        if (estadoAtual == EstadoTartaruga.Patrulhando || estadoAtual == EstadoTartaruga.Perseguindo)
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
        MudarEstado(EstadoTartaruga.TomandoDano);

        yield return new WaitForSeconds(tempoAnimacaoDano);

        rotinaDano = null;
        if (!morto)
            MudarEstado(estadoAntesDano == EstadoTartaruga.Morto ? EstadoTartaruga.Parado : estadoAntesDano);
    }

    private void Morrer()
    {
        if (morto)
            return;

        morto = true;
        vidaAtual = 0;

        if (rotinaDano != null)
        {
            StopCoroutine(rotinaDano);
            rotinaDano = null;
        }

        PararMovimento();
        PararSomAndar();
        MudarEstado(EstadoTartaruga.Morto);
        TocarSomMorrer();
        SpawnarPrefabsNormais();
        SpawnarPrefabsMissao();

        if (destruirDepoisDeMorrer)
            Destroy(gameObject, tempoParaDestruirDepoisDaMorte);
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
        MudarEstado(QuantidadePontosValidos() > 0 ? EstadoTartaruga.Patrulhando : EstadoTartaruga.Parado);
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
            else if (parametro.nameHash == AtaqueHash && parametro.type == AnimatorControllerParameterType.Bool)
                temAtaque = true;
            else if (parametro.nameHash == PerseguirHash && parametro.type == AnimatorControllerParameterType.Bool)
                temPerseguir = true;
            else if (parametro.nameHash == AlertaHash && parametro.type == AnimatorControllerParameterType.Bool)
                temAlerta = true;
        }
    }
}
