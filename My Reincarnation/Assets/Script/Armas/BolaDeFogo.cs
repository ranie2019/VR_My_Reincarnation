using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BolaDeFogo : MonoBehaviour, IDano
{
    public enum EstadoBolaDeFogo
    {
        Preparada,
        Lancada,
        Explodida
    }

    [Header("Lancamento")]
    [SerializeField, Min(0.1f)] private float velocidade = 12f;
    [SerializeField, Min(0.1f)] private float tempoMaximoDeVida = 8f;
    [SerializeField] private bool explodirAoTerminarTempo = true;

    [Header("Colisao")]
    [SerializeField] private LayerMask camadasQueCausamImpacto = ~0;
    [SerializeField] private LayerMask camadasQueRecebemDano = ~0;
    [SerializeField] private string[] tagsQueRecebemDano = { "Enemy" };
    [SerializeField] private Collider[] collidersIgnorados;

    [Header("Explosao")]
    [SerializeField, Min(0.01f)] private float raioDaExplosao = 2.5f;
    [SerializeField, Min(0f)] private float dano = 20f;
    [SerializeField, Min(1)] private int capacidadeMaximaAlvos = 64;
    [SerializeField, Min(0f)] private float tempoParaDestruirAposExplosao = 1.5f;

    [Header("Area de Fogo")]
    [SerializeField] private EfeitoFogoArea prefabEfeitoFogoArea;
    [SerializeField] private bool criarAreaDeFogoAoExplodir = true;
    [SerializeField] private bool aplicarDanoInstantaneoNaExplosao;
    [SerializeField] private bool alinharAreaComSuperficie;
    [SerializeField] private bool projetarAreaDeFogoNoChao = true;
    [SerializeField] private LayerMask camadasChaoAreaDeFogo = ~0;
    [SerializeField, Min(0.1f)] private float alturaBuscaChaoArea = 3f;
    [SerializeField, Min(0.1f)] private float distanciaBuscaChaoArea = 80f;
    [SerializeField, Min(0f)] private float deslocamentoVerticalArea = 0.02f;

    [Header("Efeitos")]
    [SerializeField] private GameObject nucleo;
    [SerializeField] private ParticleSystem fogoPrincipal;
    [SerializeField] private ParticleSystem fagulhas;
    [SerializeField] private TrailRenderer rastro;
    [SerializeField] private ParticleSystem explosao;
    [SerializeField] private Material materialNucleo;
    [SerializeField] private Material materialParticulas;
    [SerializeField] private Material materialRastro;

    [Header("Audio")]
    [SerializeField] private AudioClip somFogoLoop;
    [SerializeField, Range(0f, 1f)] private float volumeSomFogoLoop = 1f;
    [SerializeField] private AudioClip somExplosao;
    [SerializeField, Range(0f, 1f)] private float volumeSomExplosao = 1f;
    [SerializeField] private AudioSource audioSource;

    [Header("Componentes")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider colliderImpacto;

    [Header("Diagnostico")]
    [SerializeField] private bool mostrarDiagnostico;
    [SerializeField] private EstadoBolaDeFogo estadoAtual = EstadoBolaDeFogo.Preparada;
    [SerializeField] private Transform pontoPreparacaoAtual;
    [SerializeField] private GameObject objetoCajadoIgnorado;
    [SerializeField] private GameObject donoDaMagia;
    [SerializeField] private bool estaPreparada = true;
    [SerializeField] private bool estaLancada;
    [SerializeField] private bool jaExplodiu;
    [SerializeField] private int quantidadeCollidersIgnorados;
    [SerializeField] private bool estaSeguindoPontoPreparacao;
    [SerializeField] private Collider ultimoColliderIgnorado;
    [SerializeField] private int quantidadeAlvosDetectados;
    [SerializeField] private int quantidadeAlvosDanificados;
    [SerializeField] private GameObject ultimoObjetoImpacto;
    [SerializeField] private Vector3 ultimoPontoImpacto;
    [SerializeField] private Vector3 ultimaNormalImpacto = Vector3.up;
    [SerializeField] private string motivoDaExplosao;

    private readonly HashSet<int> alvosDanificados = new();
    private readonly HashSet<int> idsCollidersIgnorados = new();
    private readonly List<Collider> collidersIgnoradosRuntime = new();
    private readonly RaycastHit[] bufferBuscaChaoArea = new RaycastHit[16];
    private Collider[] bufferAlvos;
    private Transform pontoPreparacao;
    private Transform raizDono;
    private float tempoLancamento;
    private bool tempoVidaAtivo;
    private Coroutine rotinaDestruirAposExplosao;
    private Vector3 escalaOriginalLocal = Vector3.one;
    private bool temPontoImpacto;
    private Collider ultimoColliderImpactoRegistrado;
    private Material materialNucleoRuntime;
    private Material materialParticulasRuntime;
    private Material materialRastroRuntime;
    private static Mesh meshEsferaRuntime;

    public EstadoBolaDeFogo EstadoAtual => estadoAtual;
    public bool EstaPreparada => estaPreparada;
    public bool EstaLancada => estaLancada;
    public bool JaExplodiu => jaExplodiu;
    public float RaioDaExplosao => raioDaExplosao;

    public float ObterDano()
    {
        return Mathf.Max(0f, dano);
    }

    public GameObject ObterDono()
    {
        return donoDaMagia;
    }

    public bool ColliderIgnoradoPelaMagia(Collider col)
    {
        return DeveIgnorarCollider(col);
    }

    private void Awake()
    {
        escalaOriginalLocal = transform.localScale;
        GarantirComponentes();
        GarantirBufferAlvos();
        ConfigurarEstadoPreparadoSemParent();
        IniciarSomFogoLoop();
    }

    private void Reset()
    {
        velocidade = 12f;
        tempoMaximoDeVida = 8f;
        explodirAoTerminarTempo = true;
        camadasQueCausamImpacto = ~0;
        camadasQueRecebemDano = ~0;
        tagsQueRecebemDano = new[] { "Enemy" };
        raioDaExplosao = 2.5f;
        dano = 20f;
        capacidadeMaximaAlvos = 64;
        tempoParaDestruirAposExplosao = 1.5f;
        criarAreaDeFogoAoExplodir = true;
        aplicarDanoInstantaneoNaExplosao = false;
        alinharAreaComSuperficie = false;
        projetarAreaDeFogoNoChao = true;
        camadasChaoAreaDeFogo = ~0;
        alturaBuscaChaoArea = 3f;
        distanciaBuscaChaoArea = 80f;
        deslocamentoVerticalArea = 0.02f;
        volumeSomFogoLoop = 1f;
        volumeSomExplosao = 1f;
        GarantirComponentes();
    }

    private void OnValidate()
    {
        velocidade = Mathf.Max(0.1f, velocidade);
        tempoMaximoDeVida = Mathf.Max(0.1f, tempoMaximoDeVida);
        raioDaExplosao = Mathf.Max(0.01f, raioDaExplosao);
        dano = Mathf.Max(0f, dano);
        capacidadeMaximaAlvos = Mathf.Max(1, capacidadeMaximaAlvos);
        tempoParaDestruirAposExplosao = Mathf.Max(0f, tempoParaDestruirAposExplosao);
        alturaBuscaChaoArea = Mathf.Max(0.1f, alturaBuscaChaoArea);
        distanciaBuscaChaoArea = Mathf.Max(0.1f, distanciaBuscaChaoArea);
        deslocamentoVerticalArea = Mathf.Max(0f, deslocamentoVerticalArea);
        volumeSomFogoLoop = Mathf.Clamp01(volumeSomFogoLoop);
        volumeSomExplosao = Mathf.Clamp01(volumeSomExplosao);
        GarantirComponentes();
    }

    private void OnDestroy()
    {
        DestruirMaterialRuntime(materialNucleoRuntime);
        DestruirMaterialRuntime(materialParticulasRuntime);
        DestruirMaterialRuntime(materialRastroRuntime);
    }

    private void Update()
    {
        if (!tempoVidaAtivo || estadoAtual != EstadoBolaDeFogo.Lancada)
            return;

        if (Time.time - tempoLancamento >= tempoMaximoDeVida)
        {
            if (explodirAoTerminarTempo)
                ExplodirInterno("Tempo maximo de vida", true, false);
            else
                Cancelar();
        }
    }

    public void Preparar(Transform pontoPreparacao, GameObject donoDaMagia)
    {
        Preparar(pontoPreparacao, donoDaMagia, null);
    }

    public void Preparar(Transform pontoPreparacao, GameObject donoDaMagia, GameObject objetoCajado)
    {
        if (pontoPreparacao == null)
            return;

        GarantirComponentes();
        GarantirBufferAlvos();

        this.pontoPreparacao = pontoPreparacao;
        pontoPreparacaoAtual = pontoPreparacao;
        ConfigurarObjetosIgnorados(donoDaMagia, objetoCajado);
        CancelarRotinaDestruicao();
        alvosDanificados.Clear();
        ultimoColliderIgnorado = null;

        transform.SetParent(pontoPreparacao, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = escalaOriginalLocal;

        ConfigurarRigidbodyPreparado();
        ConfigurarCollider(false);
        AplicarIgnoreCollisionComCollidersRegistrados(true);
        AtivarEfeitosDeProjetil(false);
        PararExplosao();
        IniciarSomFogoLoop();

        estadoAtual = EstadoBolaDeFogo.Preparada;
        tempoVidaAtivo = false;
        tempoLancamento = 0f;
        jaExplodiu = false;
        motivoDaExplosao = string.Empty;
        ultimoObjetoImpacto = null;
        temPontoImpacto = false;
        ultimoColliderImpactoRegistrado = null;
        ultimoPontoImpacto = transform.position;
        ultimaNormalImpacto = Vector3.up;
        AtualizarDiagnosticoEstado();
    }

    public void Lancar(Vector3 direcao, GameObject donoDaMagia)
    {
        Lancar(direcao, donoDaMagia, null);
    }

    public void Lancar(Vector3 direcao, GameObject donoDaMagia, GameObject objetoCajado)
    {
        if (estadoAtual != EstadoBolaDeFogo.Preparada)
            return;

        if (direcao.sqrMagnitude < 0.0001f)
            return;

        GarantirComponentes();
        ConfigurarObjetosIgnorados(donoDaMagia != null ? donoDaMagia : this.donoDaMagia, objetoCajado);

        Vector3 direcaoNormalizada = direcao.normalized;
        transform.SetParent(null, true);
        transform.rotation = Quaternion.LookRotation(direcaoNormalizada, Vector3.up);

        ConfigurarRigidbodyLancado();
        AplicarIgnoreCollisionComCollidersRegistrados(true);
        ConfigurarCollider(true);
        AtivarEfeitosDeProjetil(true);

        rb.linearVelocity = direcaoNormalizada * velocidade;
        rb.angularVelocity = Vector3.zero;

        estadoAtual = EstadoBolaDeFogo.Lancada;
        tempoVidaAtivo = true;
        tempoLancamento = Time.time;
        jaExplodiu = false;
        motivoDaExplosao = string.Empty;
        ultimoColliderImpactoRegistrado = null;
        AtualizarDiagnosticoEstado();
    }

    private void LateUpdate()
    {
        if (estadoAtual != EstadoBolaDeFogo.Preparada)
            return;

        AtualizarSeguimentoPreparacao();
    }

    public void Explodir()
    {
        ExplodirInterno("Explosao manual", true, temPontoImpacto);
    }

    public void Cancelar()
    {
        if (estadoAtual == EstadoBolaDeFogo.Explodida)
            return;

        estadoAtual = EstadoBolaDeFogo.Explodida;
        jaExplodiu = true;
        tempoVidaAtivo = false;
        motivoDaExplosao = "Cancelada";
        PararMovimento();
        ConfigurarCollider(false);
        DesativarEfeitosDeProjetil();
        PararExplosao();
        AtualizarDiagnosticoEstado();
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        RegistrarImpacto(collision);
        ProcessarImpacto(collision.collider, "Colisao");
    }

    private void OnTriggerEnter(Collider other)
    {
        RegistrarImpacto(other);
        ProcessarImpacto(other, "Trigger");
    }

    private void ProcessarImpacto(Collider outroCollider, string motivo)
    {
        if (estadoAtual != EstadoBolaDeFogo.Lancada || outroCollider == null)
            return;

        if (DeveIgnorarCollider(outroCollider))
            return;

        if (!CamadaEstaNaMascara(outroCollider.gameObject.layer, camadasQueCausamImpacto))
            return;

        ultimoObjetoImpacto = outroCollider.gameObject;
        ExplodirInterno(motivo, true, true);
    }

    private void ExplodirInterno(string motivo, bool aplicarDano, bool criarAreaPersistente)
    {
        if (estadoAtual == EstadoBolaDeFogo.Explodida)
            return;

        estadoAtual = EstadoBolaDeFogo.Explodida;
        jaExplodiu = true;
        tempoVidaAtivo = false;
        motivoDaExplosao = motivo;
        PararMovimento();
        ConfigurarCollider(false);
        DesativarEfeitosDeProjetil();

        if (criarAreaPersistente)
            CriarAreaDeFogoPersistente();

        if (aplicarDano && aplicarDanoInstantaneoNaExplosao)
            AplicarDanoEmArea();
        else
            LimparDiagnosticoDano();

        ReproduzirExplosao();
        AtualizarDiagnosticoEstado();

        CancelarRotinaDestruicao();
        rotinaDestruirAposExplosao = StartCoroutine(DestruirDepoisDaExplosao());
    }

    private void RegistrarImpacto(Collision collision)
    {
        ultimoColliderImpactoRegistrado = collision != null ? collision.collider : null;

        if (collision != null && collision.contactCount > 0)
        {
            ContactPoint contato = collision.GetContact(0);
            ultimoPontoImpacto = contato.point;
            ultimaNormalImpacto = contato.normal.sqrMagnitude > 0.0001f ? contato.normal.normalized : Vector3.up;
            temPontoImpacto = true;
            return;
        }

        ultimoPontoImpacto = transform.position;
        ultimaNormalImpacto = Vector3.up;
        temPontoImpacto = true;
    }

    private void RegistrarImpacto(Collider other)
    {
        ultimoColliderImpactoRegistrado = other;

        if (other != null)
        {
            ultimoPontoImpacto = other.ClosestPoint(transform.position);
            if ((ultimoPontoImpacto - transform.position).sqrMagnitude < 0.000001f)
                ultimoPontoImpacto = transform.position;
        }
        else
        {
            ultimoPontoImpacto = transform.position;
        }

        ultimaNormalImpacto = Vector3.up;
        temPontoImpacto = true;
    }

    private void CriarAreaDeFogoPersistente()
    {
        if (!criarAreaDeFogoAoExplodir || prefabEfeitoFogoArea == null || !temPontoImpacto)
            return;

        Vector3 normal = temPontoImpacto && ultimaNormalImpacto.sqrMagnitude > 0.0001f
            ? ultimaNormalImpacto.normalized
            : Vector3.up;
        Vector3 pontoArea = ultimoPontoImpacto;

        if (projetarAreaDeFogoNoChao && TentarProjetarAreaNoChao(ultimoPontoImpacto, out Vector3 pontoChao, out Vector3 normalChao))
        {
            pontoArea = pontoChao;
            normal = normalChao;
        }

        Vector3 posicao = pontoArea + Vector3.up * deslocamentoVerticalArea;
        Quaternion rotacao = alinharAreaComSuperficie
            ? Quaternion.FromToRotation(Vector3.up, normal)
            : Quaternion.identity;

        EfeitoFogoArea area = Instantiate(prefabEfeitoFogoArea, posicao, rotacao);
        if (area != null)
            area.Inicializar(donoDaMagia, gameObject);
    }

    private bool TentarProjetarAreaNoChao(Vector3 pontoOrigem, out Vector3 pontoChao, out Vector3 normalChao)
    {
        pontoChao = pontoOrigem;
        normalChao = Vector3.up;

        Vector3 origem = pontoOrigem + Vector3.up * alturaBuscaChaoArea;
        float distancia = alturaBuscaChaoArea + distanciaBuscaChaoArea;
        int hits = Physics.RaycastNonAlloc(
            origem,
            Vector3.down,
            bufferBuscaChaoArea,
            distancia,
            camadasChaoAreaDeFogo,
            QueryTriggerInteraction.Ignore);

        RaycastHit melhorHitTerrain = default;
        RaycastHit melhorHitGeral = default;
        bool encontrouTerrain = false;
        bool encontrouGeral = false;
        float melhorAlturaTerrain = float.NegativeInfinity;
        float menorAlturaGeral = float.PositiveInfinity;

        for (int i = 0; i < hits; i++)
        {
            RaycastHit hit = bufferBuscaChaoArea[i];
            Collider col = hit.collider;
            bufferBuscaChaoArea[i] = default;

            if (!ColliderValidoParaChaoArea(col))
                continue;

            bool ehTerrain = col is TerrainCollider;
            if (ehTerrain && hit.point.y > melhorAlturaTerrain)
            {
                melhorAlturaTerrain = hit.point.y;
                melhorHitTerrain = hit;
                encontrouTerrain = true;
            }

            if (!ehTerrain && hit.point.y < menorAlturaGeral)
            {
                menorAlturaGeral = hit.point.y;
                melhorHitGeral = hit;
                encontrouGeral = true;
            }
        }

        if (!encontrouTerrain && !encontrouGeral)
            return false;

        RaycastHit melhorHit = encontrouTerrain ? melhorHitTerrain : melhorHitGeral;
        pontoChao = melhorHit.point;
        normalChao = melhorHit.normal.sqrMagnitude > 0.0001f ? melhorHit.normal.normalized : Vector3.up;
        return true;
    }

    private bool ColliderValidoParaChaoArea(Collider col)
    {
        if (col == null)
            return false;

        if (DeveIgnorarCollider(col))
            return false;

        if (col == ultimoColliderImpactoRegistrado && !(col is TerrainCollider))
            return false;

        if (ResolverSlime(col) != null)
            return false;

        return CamadaEstaNaMascara(col.gameObject.layer, camadasChaoAreaDeFogo);
    }

    private void AplicarDanoEmArea()
    {
        GarantirBufferAlvos();
        alvosDanificados.Clear();
        quantidadeAlvosDetectados = Physics.OverlapSphereNonAlloc(
            transform.position,
            raioDaExplosao,
            bufferAlvos,
            camadasQueRecebemDano,
            QueryTriggerInteraction.Collide);

        quantidadeAlvosDanificados = 0;
        int danoFinal = Mathf.RoundToInt(ObterDano());

        if (danoFinal <= 0)
            return;

        for (int i = 0; i < quantidadeAlvosDetectados; i++)
        {
            Collider alvoCollider = bufferAlvos[i];
            bufferAlvos[i] = null;

            if (alvoCollider == null || DeveIgnorarCollider(alvoCollider))
                continue;

            if (!TagPodeReceberDano(alvoCollider))
                continue;

            SlimeIA slime = ResolverSlime(alvoCollider);
            if (slime == null || slime.Morto)
                continue;

            int idAlvo = slime.GetInstanceID();
            if (!alvosDanificados.Add(idAlvo))
                continue;

            slime.ReceberDano(danoFinal, gameObject);
            quantidadeAlvosDanificados++;
        }
    }

    private void LimparDiagnosticoDano()
    {
        quantidadeAlvosDetectados = 0;
        quantidadeAlvosDanificados = 0;
        alvosDanificados.Clear();
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

    private void DefinirDono(GameObject novoDono)
    {
        donoDaMagia = novoDono;
        raizDono = donoDaMagia != null ? donoDaMagia.transform.root : null;
    }

    public void ConfigurarObjetosIgnorados(GameObject novoDono, GameObject novoObjetoCajado)
    {
        DefinirDono(novoDono);

        if (novoObjetoCajado != null)
            objetoCajadoIgnorado = novoObjetoCajado;

        RecriarCollidersIgnorados();
    }

    private bool DeveIgnorarCollider(Collider outroCollider)
    {
        if (outroCollider == null)
            return true;

        if (colliderImpacto != null && outroCollider == colliderImpacto)
            return true;

        if (outroCollider.transform == transform || outroCollider.transform.IsChildOf(transform))
            return true;

        if (pontoPreparacao != null && (outroCollider.transform == pontoPreparacao || outroCollider.transform.IsChildOf(pontoPreparacao)))
            return true;

        if (ColliderPertenceAHierarquia(outroCollider, objetoCajadoIgnorado != null ? objetoCajadoIgnorado.transform : null))
        {
            ultimoColliderIgnorado = outroCollider;
            return true;
        }

        if (ColliderPertenceAHierarquia(outroCollider, donoDaMagia != null ? donoDaMagia.transform : null))
        {
            ultimoColliderIgnorado = outroCollider;
            return true;
        }

        if (raizDono != null && outroCollider.transform.root == raizDono)
        {
            ultimoColliderIgnorado = outroCollider;
            return true;
        }

        if (idsCollidersIgnorados.Contains(outroCollider.GetInstanceID()))
        {
            ultimoColliderIgnorado = outroCollider;
            return true;
        }

        if (collidersIgnorados != null)
        {
            for (int i = 0; i < collidersIgnorados.Length; i++)
            {
                if (collidersIgnorados[i] != null && collidersIgnorados[i] == outroCollider)
                {
                    ultimoColliderIgnorado = outroCollider;
                    return true;
                }
            }
        }

        return false;
    }

    private void RecriarCollidersIgnorados()
    {
        idsCollidersIgnorados.Clear();
        collidersIgnoradosRuntime.Clear();

        AdicionarCollidersSerializados();
        AdicionarCollidersDaHierarquia(donoDaMagia);

        if (raizDono != null && (donoDaMagia == null || raizDono != donoDaMagia.transform))
            AdicionarCollidersDaHierarquia(raizDono.gameObject);

        AdicionarCollidersDaHierarquia(objetoCajadoIgnorado);

        if (pontoPreparacao != null)
            AdicionarCollidersDaHierarquia(pontoPreparacao.gameObject);

        quantidadeCollidersIgnorados = collidersIgnoradosRuntime.Count;
    }

    private void AdicionarCollidersSerializados()
    {
        if (collidersIgnorados == null)
            return;

        for (int i = 0; i < collidersIgnorados.Length; i++)
            AdicionarColliderIgnorado(collidersIgnorados[i]);
    }

    private void AdicionarCollidersDaHierarquia(GameObject origem)
    {
        if (origem == null)
            return;

        Collider[] colliders = origem.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            AdicionarColliderIgnorado(colliders[i]);
    }

    private void AdicionarColliderIgnorado(Collider col)
    {
        if (col == null || col == colliderImpacto)
            return;

        int id = col.GetInstanceID();
        if (!idsCollidersIgnorados.Add(id))
            return;

        collidersIgnoradosRuntime.Add(col);
    }

    private void AplicarIgnoreCollisionComCollidersRegistrados(bool ignorar)
    {
        if (colliderImpacto == null)
            return;

        for (int i = 0; i < collidersIgnoradosRuntime.Count; i++)
        {
            Collider col = collidersIgnoradosRuntime[i];
            if (col != null && col != colliderImpacto)
                Physics.IgnoreCollision(colliderImpacto, col, ignorar);
        }

        quantidadeCollidersIgnorados = collidersIgnoradosRuntime.Count;
    }

    private static bool ColliderPertenceAHierarquia(Collider col, Transform raiz)
    {
        if (col == null || raiz == null)
            return false;

        if (col.transform == raiz || col.transform.IsChildOf(raiz))
            return true;

        Rigidbody rbContato = col.attachedRigidbody;
        return rbContato != null && (rbContato.transform == raiz || rbContato.transform.IsChildOf(raiz));
    }

    private bool TagPodeReceberDano(Collider alvoCollider)
    {
        if (tagsQueRecebemDano == null || tagsQueRecebemDano.Length == 0)
            return true;

        Transform atual = alvoCollider.transform;
        while (atual != null)
        {
            if (TagEstaPermitida(atual.tag))
                return true;

            atual = atual.parent;
        }

        if (alvoCollider.attachedRigidbody != null && TagEstaPermitida(alvoCollider.attachedRigidbody.tag))
            return true;

        Transform raiz = alvoCollider.transform.root;
        return raiz != null && TagEstaPermitida(raiz.tag);
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

            if (string.Equals(tagAtual, tagPermitida, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool CamadaEstaNaMascara(int layer, LayerMask mascara)
    {
        return (mascara.value & (1 << layer)) != 0;
    }

    private void GarantirComponentes()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        if (colliderImpacto == null)
            colliderImpacto = GetComponent<Collider>();

        if (colliderImpacto == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.16f;
            colliderImpacto = sphere;
        }

        GarantirAudio();
        GarantirReferenciasVisuais();
    }

    private void GarantirAudio()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 1f;
        audioSource.volume = volumeSomFogoLoop;
    }

    private void IniciarSomFogoLoop()
    {
        if (somFogoLoop == null)
            return;

        GarantirAudio();

        if (audioSource == null)
            return;

        if (audioSource.clip != somFogoLoop)
            audioSource.clip = somFogoLoop;

        audioSource.loop = true;
        audioSource.volume = volumeSomFogoLoop;

        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    private void TocarSomExplosao()
    {
        if (somExplosao == null)
            return;

        GarantirAudio();

        if (audioSource == null)
            return;

        audioSource.Stop();
        audioSource.loop = false;
        audioSource.clip = somExplosao;
        audioSource.volume = volumeSomExplosao;
        audioSource.Play();
    }

    private void GarantirReferenciasVisuais()
    {
        if (nucleo == null)
            nucleo = ObterOuCriarFilho("Nucleo");

        if (fogoPrincipal == null)
            fogoPrincipal = ObterOuCriarParticleSystem("Fogo Principal", true, 28f, 0.22f, 0.45f);

        if (fagulhas == null)
            fagulhas = ObterOuCriarParticleSystem("Fagulhas", true, 12f, 0.32f, 0.85f);

        if (rastro == null)
            rastro = ObterOuCriarTrailRenderer("Rastro");

        if (explosao == null)
            explosao = ObterOuCriarParticleSystem("Explosao", false, 0f, 0.45f, 2.2f);

        ConfigurarNucleo();
        AplicarMateriaisVisuais();
    }

    private GameObject ObterOuCriarFilho(string nome)
    {
        Transform encontrado = transform.Find(nome);
        if (encontrado != null)
            return encontrado.gameObject;

        GameObject filho = new GameObject(nome);
        filho.transform.SetParent(transform, false);
        filho.transform.localPosition = Vector3.zero;
        filho.transform.localRotation = Quaternion.identity;
        filho.transform.localScale = Vector3.one;
        return filho;
    }

    private ParticleSystem ObterOuCriarParticleSystem(string nome, bool loop, float emissao, float vida, float velocidadeParticula)
    {
        GameObject obj = ObterOuCriarFilho(nome);
        ParticleSystem ps = obj.GetComponent<ParticleSystem>();

        if (ps == null)
            ps = obj.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.loop = loop;
        main.startLifetime = vida;
        main.startSpeed = velocidadeParticula;
        main.startSize = loop ? 0.08f : 0.12f;
        main.maxParticles = loop ? 48 : 40;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.2f, 0.95f),
            new Color(1f, 0.18f, 0.02f, 0.65f));

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = emissao;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = loop ? 0.08f : 0.2f;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        if (!loop)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        return ps;
    }

    private TrailRenderer ObterOuCriarTrailRenderer(string nome)
    {
        GameObject obj = ObterOuCriarFilho(nome);
        TrailRenderer trail = obj.GetComponent<TrailRenderer>();

        if (trail == null)
            trail = obj.AddComponent<TrailRenderer>();

        trail.time = 0.22f;
        trail.minVertexDistance = 0.03f;
        trail.startWidth = 0.12f;
        trail.endWidth = 0f;
        trail.emitting = false;
        trail.autodestruct = false;
        return trail;
    }

    private void ConfigurarNucleo()
    {
        if (nucleo == null)
            return;

        nucleo.transform.localScale = Vector3.one * 0.22f;

        MeshFilter filtro = nucleo.GetComponent<MeshFilter>();
        MeshRenderer renderer = nucleo.GetComponent<MeshRenderer>();

        if (filtro == null)
            filtro = nucleo.AddComponent<MeshFilter>();

        if (renderer == null)
            renderer = nucleo.AddComponent<MeshRenderer>();

        if (filtro.sharedMesh == null)
            filtro.sharedMesh = ObterMeshEsfera();

        Collider col = nucleo.GetComponent<Collider>();
        if (col != null)
            DestruirComponenteSeguro(col);
    }

    private void AplicarMateriaisVisuais()
    {
        Material matNucleo = materialNucleo != null ? materialNucleo : ObterMaterialNucleoRuntime();
        Material matParticulas = materialParticulas != null ? materialParticulas : ObterMaterialParticulasRuntime();
        Material matTrail = materialRastro != null ? materialRastro : ObterMaterialRastroRuntime();

        if (nucleo != null && matNucleo != null)
        {
            Renderer renderer = nucleo.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = matNucleo;
        }

        AplicarMaterialParticleSystem(fogoPrincipal, matParticulas);
        AplicarMaterialParticleSystem(fagulhas, matParticulas);
        AplicarMaterialParticleSystem(explosao, matParticulas);

        if (rastro != null && matTrail != null)
            rastro.sharedMaterial = matTrail;
    }

    private static void AplicarMaterialParticleSystem(ParticleSystem ps, Material material)
    {
        if (ps == null || material == null)
            return;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    private Material ObterMaterialNucleoRuntime()
    {
        if (materialNucleoRuntime == null)
            materialNucleoRuntime = CriarMaterialRuntime("Bola de Fogo Nucleo Runtime", new Color(1f, 0.38f, 0.03f, 1f), true);

        return materialNucleoRuntime;
    }

    private Material ObterMaterialParticulasRuntime()
    {
        if (materialParticulasRuntime == null)
            materialParticulasRuntime = CriarMaterialRuntime("Bola de Fogo Particulas Runtime", new Color(1f, 0.45f, 0.08f, 0.75f), false);

        return materialParticulasRuntime;
    }

    private Material ObterMaterialRastroRuntime()
    {
        if (materialRastroRuntime == null)
            materialRastroRuntime = CriarMaterialRuntime("Bola de Fogo Rastro Runtime", new Color(1f, 0.22f, 0.02f, 0.65f), false);

        return materialRastroRuntime;
    }

    private static Mesh ObterMeshEsfera()
    {
        Mesh meshInterna = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        if (meshInterna != null)
            return meshInterna;

        if (meshEsferaRuntime == null)
            meshEsferaRuntime = CriarMeshEsferaRuntime();

        return meshEsferaRuntime;
    }

    private static Mesh CriarMeshEsferaRuntime()
    {
        const int segmentos = 16;
        const int aneis = 8;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normais = new List<Vector3>();
        List<int> triangulos = new List<int>();

        vertices.Add(Vector3.up * 0.5f);
        normais.Add(Vector3.up);

        for (int anel = 1; anel < aneis; anel++)
        {
            float v = anel / (float)aneis;
            float phi = Mathf.PI * v;
            float y = Mathf.Cos(phi) * 0.5f;
            float raio = Mathf.Sin(phi) * 0.5f;

            for (int segmento = 0; segmento < segmentos; segmento++)
            {
                float u = segmento / (float)segmentos;
                float theta = Mathf.PI * 2f * u;
                Vector3 normal = new Vector3(Mathf.Cos(theta) * raio, y, Mathf.Sin(theta) * raio).normalized;
                vertices.Add(normal * 0.5f);
                normais.Add(normal);
            }
        }

        int indiceInferior = vertices.Count;
        vertices.Add(Vector3.down * 0.5f);
        normais.Add(Vector3.down);

        for (int segmento = 0; segmento < segmentos; segmento++)
        {
            int atual = 1 + segmento;
            int proximo = 1 + (segmento + 1) % segmentos;
            triangulos.Add(0);
            triangulos.Add(proximo);
            triangulos.Add(atual);
        }

        for (int anel = 0; anel < aneis - 2; anel++)
        {
            int inicioAtual = 1 + anel * segmentos;
            int inicioProximo = inicioAtual + segmentos;

            for (int segmento = 0; segmento < segmentos; segmento++)
            {
                int atual = inicioAtual + segmento;
                int proximo = inicioAtual + (segmento + 1) % segmentos;
                int abaixo = inicioProximo + segmento;
                int abaixoProximo = inicioProximo + (segmento + 1) % segmentos;

                triangulos.Add(atual);
                triangulos.Add(proximo);
                triangulos.Add(abaixoProximo);
                triangulos.Add(atual);
                triangulos.Add(abaixoProximo);
                triangulos.Add(abaixo);
            }
        }

        int inicioUltimoAnel = 1 + (aneis - 2) * segmentos;
        for (int segmento = 0; segmento < segmentos; segmento++)
        {
            int atual = inicioUltimoAnel + segmento;
            int proximo = inicioUltimoAnel + (segmento + 1) % segmentos;
            triangulos.Add(indiceInferior);
            triangulos.Add(atual);
            triangulos.Add(proximo);
        }

        Mesh mesh = new Mesh
        {
            name = "Bola de Fogo Esfera Runtime",
            hideFlags = HideFlags.HideAndDontSave
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normais);
        mesh.SetTriangles(triangulos, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CriarMaterialRuntime(string nome, Color cor, bool emissivo)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = nome,
            color = cor
        };

        if (emissivo && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", cor * 1.5f);
        }

        return material;
    }

    private void ConfigurarEstadoPreparadoSemParent()
    {
        if (estadoAtual == EstadoBolaDeFogo.Lancada)
            return;

        ConfigurarRigidbodyPreparado();
        ConfigurarCollider(false);
        AtivarEfeitosDeProjetil(false);
        PararExplosao();
        estadoAtual = EstadoBolaDeFogo.Preparada;
        tempoVidaAtivo = false;
        pontoPreparacaoAtual = pontoPreparacao;
        AtualizarDiagnosticoEstado();
    }

    private void ConfigurarRigidbodyPreparado()
    {
        if (rb == null)
            return;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void ConfigurarRigidbodyLancado()
    {
        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.useGravity = false;
        rb.detectCollisions = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void ConfigurarCollider(bool ativo)
    {
        if (colliderImpacto != null)
            colliderImpacto.enabled = ativo;
    }

    private void PararMovimento()
    {
        if (rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = false;
    }

    private void AtualizarSeguimentoPreparacao()
    {
        pontoPreparacaoAtual = pontoPreparacao;
        estaSeguindoPontoPreparacao = pontoPreparacao != null;

        if (pontoPreparacao == null)
            return;

        if (transform.parent != pontoPreparacao)
            transform.SetParent(pontoPreparacao, false);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.SetPositionAndRotation(pontoPreparacao.position, pontoPreparacao.rotation);
        transform.localScale = escalaOriginalLocal;
    }

    private void AtivarEfeitosDeProjetil(bool lancada)
    {
        if (nucleo != null)
            nucleo.SetActive(true);

        TocarParticleSystem(fogoPrincipal, true);
        TocarParticleSystem(fagulhas, true);

        if (rastro != null)
        {
            rastro.Clear();
            rastro.emitting = lancada;
            rastro.gameObject.SetActive(lancada);
        }
    }

    private void DesativarEfeitosDeProjetil()
    {
        if (nucleo != null)
            nucleo.SetActive(false);

        TocarParticleSystem(fogoPrincipal, false);
        TocarParticleSystem(fagulhas, false);

        if (rastro != null)
        {
            rastro.emitting = false;
            rastro.gameObject.SetActive(false);
        }
    }

    private void ReproduzirExplosao()
    {
        TocarSomExplosao();

        if (explosao == null)
            return;

        explosao.gameObject.SetActive(true);
        explosao.Clear(true);
        explosao.Play(true);
    }

    private void PararExplosao()
    {
        if (explosao == null)
            return;

        explosao.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        explosao.gameObject.SetActive(false);
    }

    private static void TocarParticleSystem(ParticleSystem ps, bool tocar)
    {
        if (ps == null)
            return;

        ps.gameObject.SetActive(true);

        if (tocar)
            ps.Play(true);
        else
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private IEnumerator DestruirDepoisDaExplosao()
    {
        float tempo = tempoParaDestruirAposExplosao;

        if (explosao != null)
        {
            ParticleSystem.MainModule main = explosao.main;
            tempo = Mathf.Max(tempo, main.duration + main.startLifetime.constantMax);
        }

        if (somExplosao != null)
        {
            float pitch = audioSource != null ? Mathf.Abs(audioSource.pitch) : 1f;
            tempo = Mathf.Max(tempo, somExplosao.length / Mathf.Max(0.01f, pitch));
        }

        yield return new WaitForSeconds(tempo);
        Destroy(gameObject);
    }

    private void CancelarRotinaDestruicao()
    {
        if (rotinaDestruirAposExplosao == null)
            return;

        StopCoroutine(rotinaDestruirAposExplosao);
        rotinaDestruirAposExplosao = null;
    }

    private void GarantirBufferAlvos()
    {
        if (bufferAlvos == null || bufferAlvos.Length != capacidadeMaximaAlvos)
            bufferAlvos = new Collider[capacidadeMaximaAlvos];
    }

    private void AtualizarDiagnosticoEstado()
    {
        estaPreparada = estadoAtual == EstadoBolaDeFogo.Preparada;
        estaLancada = estadoAtual == EstadoBolaDeFogo.Lancada;
        jaExplodiu = estadoAtual == EstadoBolaDeFogo.Explodida;
        pontoPreparacaoAtual = pontoPreparacao;
        estaSeguindoPontoPreparacao = estaPreparada && pontoPreparacao != null && transform.parent == pontoPreparacao;
        quantidadeCollidersIgnorados = collidersIgnoradosRuntime.Count;

        if (mostrarDiagnostico)
        {
            // Diagnostico fica todo no Inspector; sem logs por frame.
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.02f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, raioDaExplosao));
    }

    private static void DestruirComponenteSeguro(Component componente)
    {
        if (componente == null)
            return;

        if (Application.isPlaying)
            Destroy(componente);
        else
            DestroyImmediate(componente);
    }

    private static void DestruirMaterialRuntime(Material material)
    {
        if (material == null)
            return;

        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);
    }
}
