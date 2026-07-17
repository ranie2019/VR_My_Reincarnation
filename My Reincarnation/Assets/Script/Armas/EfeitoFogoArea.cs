using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EfeitoFogoArea : MonoBehaviour
{
    [Header("Dano Continuo")]
    [SerializeField, Min(0)] private int danoPorPulso = 1;
    [SerializeField, Min(0.05f)] private float intervaloEntreDanos = 1f;
    [SerializeField] private bool aplicarPrimeiroDanoImediatamente = true;

    [Header("Area")]
    [SerializeField, Min(0.1f)] private float raioDaArea = 10f;
    [SerializeField, Min(0.1f)] private float tempoDeVidaDaArea = 10f;
    [SerializeField] private LayerMask camadasQueRecebemDano = ~0;
    [SerializeField] private string[] tagsQueRecebemDano = { "Enemy" };
    [SerializeField, Min(1)] private int capacidadeMaximaDeColliders = 128;
    [SerializeField, Min(0f)] private float tempoParaDestruirAposPararEfeito = 1.5f;

    [Header("Visual")]
    [SerializeField] private ParticleSystem efeitoFogoArea;
    [SerializeField] private Transform visualDaArea;
    [SerializeField] private bool ajustarVisualAoRaio = true;

    [Header("Audio")]
    [SerializeField] private AudioClip somAreaFogoLoop;
    [SerializeField, Range(0f, 1f)] private float volumeSomAreaFogoLoop = 1f;
    [SerializeField] private AudioSource audioSourceArea;

    [Header("Diagnostico")]
    [SerializeField] private bool mostrarDiagnostico;
    [SerializeField] private GameObject donoDaMagia;
    [SerializeField] private GameObject origemDaMagia;
    [SerializeField] private bool estaAtiva;
    [SerializeField] private float tempoRestante;
    [SerializeField] private float raioAtual;
    [SerializeField] private int quantidadeCollidersDetectadosNoUltimoPulso;
    [SerializeField] private int quantidadeAlvosDanificadosNoUltimoPulso;
    [SerializeField] private int totalDePulsosExecutados;
    [SerializeField] private GameObject ultimoAlvoDanificado;
    [SerializeField] private string statusArea;

    private readonly HashSet<int> alvosAtingidosNoPulso = new();
    private Collider[] bufferColliders;
    private Coroutine rotinaArea;
    private ParticleSystem brasasArea;
    private ParticleSystem fumacaArea;
    private Vector3 escalaOriginalVisual = Vector3.one;
    private float horarioFim;
    private bool inicializada;

    private void Awake()
    {
        GarantirBuffer();
        GarantirVisual();
        GarantirAudio();
        AplicarVisualAoRaio();
        AtualizarDiagnosticoArea("Aguardando inicializacao.");
    }

    private void OnEnable()
    {
        TocarSomAreaFogoLoop();
    }

    private void OnValidate()
    {
        danoPorPulso = Mathf.Max(0, danoPorPulso);
        intervaloEntreDanos = Mathf.Max(0.05f, intervaloEntreDanos);
        raioDaArea = Mathf.Max(0.1f, raioDaArea);
        tempoDeVidaDaArea = Mathf.Max(0.1f, tempoDeVidaDaArea);
        capacidadeMaximaDeColliders = Mathf.Max(1, capacidadeMaximaDeColliders);
        tempoParaDestruirAposPararEfeito = Mathf.Max(0f, tempoParaDestruirAposPararEfeito);
        volumeSomAreaFogoLoop = Mathf.Clamp01(volumeSomAreaFogoLoop);
        raioAtual = raioDaArea;

        if (audioSourceArea == null)
            audioSourceArea = GetComponent<AudioSource>();
    }

    private void OnDisable()
    {
        PararSomAreaFogoLoop();
        PararRotinaArea();
    }

    private void OnDestroy()
    {
        PararSomAreaFogoLoop();
        PararRotinaArea();
    }

    private void Reset()
    {
        GarantirAudio();
    }

    private void Update()
    {
        if (!estaAtiva)
            return;

        tempoRestante = Mathf.Max(0f, horarioFim - Time.time);
    }

    public void Inicializar(GameObject donoDaMagia, GameObject origemDaMagia)
    {
        if (inicializada)
            return;

        this.donoDaMagia = donoDaMagia;
        this.origemDaMagia = origemDaMagia;
        inicializada = true;
        estaAtiva = true;
        horarioFim = Time.time + tempoDeVidaDaArea;
        tempoRestante = tempoDeVidaDaArea;
        raioAtual = raioDaArea;

        GarantirBuffer();
        GarantirVisual();
        GarantirAudio();
        AplicarVisualAoRaio();
        TocarEfeitos(true);
        TocarSomAreaFogoLoop();

        PararRotinaArea();
        rotinaArea = StartCoroutine(RotinaArea());
        AtualizarDiagnosticoArea("Area de fogo ativa.");
    }

    private IEnumerator RotinaArea()
    {
        if (aplicarPrimeiroDanoImediatamente)
            AplicarPulsoDeDano();

        while (Time.time < horarioFim)
        {
            float espera = Mathf.Min(intervaloEntreDanos, Mathf.Max(0f, horarioFim - Time.time));
            if (espera > 0f)
                yield return new WaitForSeconds(espera);

            if (Time.time >= horarioFim)
                break;

            AplicarPulsoDeDano();
        }

        FinalizarEfeito();

        if (tempoParaDestruirAposPararEfeito > 0f)
            yield return new WaitForSeconds(tempoParaDestruirAposPararEfeito);

        Destroy(gameObject);
    }

    private void AplicarPulsoDeDano()
    {
        GarantirBuffer();
        alvosAtingidosNoPulso.Clear();
        quantidadeCollidersDetectadosNoUltimoPulso = Physics.OverlapSphereNonAlloc(
            transform.position,
            raioDaArea,
            bufferColliders,
            camadasQueRecebemDano,
            QueryTriggerInteraction.Collide);

        quantidadeAlvosDanificadosNoUltimoPulso = 0;
        totalDePulsosExecutados++;

        if (danoPorPulso <= 0)
        {
            LimparBufferUsado();
            AtualizarDiagnosticoArea("Pulso sem dano configurado.");
            return;
        }

        GameObject origemDano = ObterOrigemDano();

        for (int i = 0; i < quantidadeCollidersDetectadosNoUltimoPulso; i++)
        {
            Collider alvoCollider = bufferColliders[i];
            bufferColliders[i] = null;

            if (alvoCollider == null || DeveIgnorarCollider(alvoCollider))
                continue;

            SlimeIA slime = ResolverSlime(alvoCollider);
            if (slime == null || slime.Morto)
                continue;

            if (!TagPodeReceberDano(alvoCollider, slime))
                continue;

            int idAlvo = slime.GetInstanceID();
            if (!alvosAtingidosNoPulso.Add(idAlvo))
                continue;

            slime.ReceberDano(danoPorPulso, origemDano);
            quantidadeAlvosDanificadosNoUltimoPulso++;
            ultimoAlvoDanificado = slime.gameObject;
        }

        AtualizarDiagnosticoArea("Pulso de dano aplicado.");
    }

    private void FinalizarEfeito()
    {
        estaAtiva = false;
        tempoRestante = 0f;
        PararRotinaAreaSemLimparReferenciaAtual();
        TocarEfeitos(false);
        AtualizarDiagnosticoArea("Tempo da area finalizado.");
    }

    private void PararRotinaArea()
    {
        if (rotinaArea == null)
            return;

        StopCoroutine(rotinaArea);
        rotinaArea = null;
    }

    private void PararRotinaAreaSemLimparReferenciaAtual()
    {
        rotinaArea = null;
    }

    private void GarantirBuffer()
    {
        if (bufferColliders == null || bufferColliders.Length != capacidadeMaximaDeColliders)
            bufferColliders = new Collider[capacidadeMaximaDeColliders];
    }

    private void LimparBufferUsado()
    {
        for (int i = 0; i < quantidadeCollidersDetectadosNoUltimoPulso && i < bufferColliders.Length; i++)
            bufferColliders[i] = null;
    }

    private SlimeIA ResolverSlime(Collider alvoCollider)
    {
        if (alvoCollider == null)
            return null;

        SlimeIA slime = alvoCollider.GetComponentInParent<SlimeIA>();
        if (slime != null)
            return slime;

        if (alvoCollider.attachedRigidbody != null)
        {
            slime = alvoCollider.attachedRigidbody.GetComponentInParent<SlimeIA>();
            if (slime != null)
                return slime;
        }

        Transform raiz = alvoCollider.transform.root;
        return raiz != null ? raiz.GetComponentInChildren<SlimeIA>(true) : null;
    }

    private bool DeveIgnorarCollider(Collider col)
    {
        if (col == null)
            return true;

        if (col.transform == transform || col.transform.IsChildOf(transform))
            return true;

        if (PertenceAHierarquia(col, donoDaMagia != null ? donoDaMagia.transform : null))
            return true;

        if (PertenceAHierarquia(col, origemDaMagia != null ? origemDaMagia.transform : null))
            return true;

        return false;
    }

    private bool TagPodeReceberDano(Collider alvoCollider, SlimeIA slime)
    {
        if (tagsQueRecebemDano == null || tagsQueRecebemDano.Length == 0)
            return true;

        if (TagPermitidaEmHierarquia(alvoCollider != null ? alvoCollider.transform : null))
            return true;

        if (alvoCollider != null && alvoCollider.attachedRigidbody != null &&
            TagPermitidaEmHierarquia(alvoCollider.attachedRigidbody.transform))
            return true;

        return slime != null && TagPermitidaEmHierarquia(slime.transform);
    }

    private bool TagPermitidaEmHierarquia(Transform origem)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (TagEstaPermitida(atual.tag))
                return true;

            atual = atual.parent;
        }

        return false;
    }

    private bool TagEstaPermitida(string tagAtual)
    {
        if (string.IsNullOrWhiteSpace(tagAtual))
            return false;

        for (int i = 0; i < tagsQueRecebemDano.Length; i++)
        {
            string tagPermitida = tagsQueRecebemDano[i];
            if (string.IsNullOrWhiteSpace(tagPermitida))
                continue;

            if (tagAtual == tagPermitida)
                return true;
        }

        return false;
    }

    private GameObject ObterOrigemDano()
    {
        if (donoDaMagia != null)
            return donoDaMagia;

        if (origemDaMagia != null)
            return origemDaMagia;

        return gameObject;
    }

    private static bool PertenceAHierarquia(Collider col, Transform raiz)
    {
        if (col == null || raiz == null)
            return false;

        if (col.transform == raiz || col.transform.IsChildOf(raiz))
            return true;

        Rigidbody rbContato = col.attachedRigidbody;
        return rbContato != null && (rbContato.transform == raiz || rbContato.transform.IsChildOf(raiz));
    }

    private void GarantirVisual()
    {
        if (visualDaArea == null)
            visualDaArea = transform;

        escalaOriginalVisual = visualDaArea.localScale;

        if (efeitoFogoArea == null)
            efeitoFogoArea = ObterOuCriarParticleSystem("Fogo no Chao");

        if (brasasArea == null)
            brasasArea = ObterOuCriarParticleSystem("Brasas");

        if (fumacaArea == null)
            fumacaArea = ObterOuCriarParticleSystem("Fumaca Leve");

        ConfigurarFogoPrincipal();
        ConfigurarBrasas();
        ConfigurarFumaca();
    }

    private ParticleSystem ObterOuCriarParticleSystem(string nome)
    {
        Transform existente = transform.Find(nome);
        GameObject obj;

        if (existente != null)
        {
            obj = existente.gameObject;
        }
        else
        {
            obj = new GameObject(nome);
            obj.transform.SetParent(transform, false);
        }

        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        ParticleSystem ps = obj.GetComponent<ParticleSystem>();
        if (ps == null)
            ps = obj.AddComponent<ParticleSystem>();

        return ps;
    }

    private void ConfigurarFogoPrincipal()
    {
        if (efeitoFogoArea == null)
            return;

        ParticleSystem.MainModule main = efeitoFogoArea.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.75f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
        main.maxParticles = 180;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.12f, 0.85f),
            new Color(1f, 0.2f, 0.02f, 0.65f));

        ParticleSystem.EmissionModule emission = efeitoFogoArea.emission;
        emission.enabled = true;
        emission.rateOverTime = 45f;

        ConfigurarShapeCircular(efeitoFogoArea, raioDaArea);
    }

    private void ConfigurarBrasas()
    {
        if (brasasArea == null)
            return;

        ParticleSystem.MainModule main = brasasArea.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.55f, 0.06f, 0.8f),
            new Color(1f, 0.1f, 0.01f, 0.45f));

        ParticleSystem.EmissionModule emission = brasasArea.emission;
        emission.enabled = true;
        emission.rateOverTime = 18f;

        ConfigurarShapeCircular(brasasArea, raioDaArea * 0.85f);
    }

    private void ConfigurarFumaca()
    {
        if (fumacaArea == null)
            return;

        ParticleSystem.MainModule main = fumacaArea.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.maxParticles = 45;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.18f, 0.16f, 0.14f, 0.18f),
            new Color(0.05f, 0.04f, 0.04f, 0.08f));

        ParticleSystem.EmissionModule emission = fumacaArea.emission;
        emission.enabled = true;
        emission.rateOverTime = 8f;

        ConfigurarShapeCircular(fumacaArea, raioDaArea * 0.75f);
    }

    private static void ConfigurarShapeCircular(ParticleSystem ps, float raio)
    {
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0.1f, raio);
    }

    private void AplicarVisualAoRaio()
    {
        raioAtual = raioDaArea;

        if (!ajustarVisualAoRaio)
            return;

        if (visualDaArea != null)
            visualDaArea.localScale = escalaOriginalVisual;

        if (efeitoFogoArea != null)
            ConfigurarShapeCircular(efeitoFogoArea, raioDaArea);

        if (brasasArea != null)
            ConfigurarShapeCircular(brasasArea, raioDaArea * 0.85f);

        if (fumacaArea != null)
            ConfigurarShapeCircular(fumacaArea, raioDaArea * 0.75f);
    }

    private void TocarEfeitos(bool tocar)
    {
        TocarParticleSystem(efeitoFogoArea, tocar);
        TocarParticleSystem(brasasArea, tocar);
        TocarParticleSystem(fumacaArea, tocar);
    }

    private void GarantirAudio()
    {
        if (audioSourceArea == null)
            audioSourceArea = GetComponent<AudioSource>();

        if (audioSourceArea == null)
            audioSourceArea = gameObject.AddComponent<AudioSource>();

        audioSourceArea.playOnAwake = false;
        audioSourceArea.loop = true;
        audioSourceArea.spatialBlend = 1f;
        audioSourceArea.volume = volumeSomAreaFogoLoop;
    }

    private void TocarSomAreaFogoLoop()
    {
        GarantirAudio();

        AudioClip clip = somAreaFogoLoop != null ? somAreaFogoLoop : audioSourceArea.clip;
        if (clip == null)
            return;

        audioSourceArea.clip = clip;
        audioSourceArea.loop = true;
        audioSourceArea.volume = volumeSomAreaFogoLoop;

        if (!audioSourceArea.isPlaying)
            audioSourceArea.Play();
    }

    private void PararSomAreaFogoLoop()
    {
        if (audioSourceArea == null)
            return;

        audioSourceArea.Stop();
    }

    private static void TocarParticleSystem(ParticleSystem ps, bool tocar)
    {
        if (ps == null)
            return;

        if (tocar)
        {
            ps.gameObject.SetActive(true);
            ps.Play(true);
        }
        else
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void AtualizarDiagnosticoArea(string status)
    {
        statusArea = status;
        raioAtual = raioDaArea;

        if (estaAtiva)
            tempoRestante = Mathf.Max(0f, horarioFim - Time.time);

        if (mostrarDiagnostico)
        {
            // Diagnostico no Inspector; sem logs por pulso.
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, raioDaArea));
    }
}
