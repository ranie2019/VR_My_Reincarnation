using UnityEngine;

[DisallowMultipleComponent]
public class FonteAgua : MonoBehaviour
{
    private const string NomeJato = "FX_JatoAgua";
    private const string NomeQueda = "FX_QuedaAgua";
    private const string NomeRespingo = "FX_RespingoAgua";
    private const string NomeAguaSuperior = "Agua_Prato_Superior";
    private const string NomeAguaMeio = "Agua_Prato_Meio";
    private const string NomeAguaInferior = "Agua_Prato_Inferior";

    [Header("Referencias")]
    [SerializeField] private Transform origemJato;
    [SerializeField] private Transform alvoJato;
    [SerializeField] private Transform pratoSuperior;
    [SerializeField] private Transform pratoMeio;
    [SerializeField] private Transform pratoInferior;
    [SerializeField] private Material materialAgua;

    [Header("Pratos")]
    [SerializeField] private float alturaAguaPrato = 0.03f;
    [SerializeField] private float escalaAguaPratoSuperior = 1.1f;
    [SerializeField] private float escalaAguaPratoMeio = 1.45f;
    [SerializeField] private float escalaAguaPratoInferior = 1.8f;
    [SerializeField] private float deslocamentoVerticalPratoSuperior = 0.02f;
    [SerializeField] private float deslocamentoVerticalPratoMeio = 0.02f;
    [SerializeField] private float deslocamentoVerticalPratoInferior = 0.02f;

    [Header("Rotacao dos Pratos")]
    [SerializeField] private bool usarRotacaoDoPrato = true;
    [SerializeField] private Vector3 rotacaoAguaPratoSuperior = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 rotacaoAguaPratoMeio = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 rotacaoAguaPratoInferior = new Vector3(0f, 0f, 0f);

    [Header("Jato Inicial Pressurizado")]
    [SerializeField] private float alturaJatoInicial = 1.2f;
    [SerializeField] private float quantidadeParticulasJatoInicial = 220f;
    [SerializeField] private float tamanhoParticulaJatoInicial = 0.045f;
    [SerializeField] private float velocidadeJatoInicial = 5.5f;
    [SerializeField] private float larguraJatoInicial = 0.025f;
    [SerializeField] private float gravidadeJatoInicial = 0.35f;
    [SerializeField] private float vidaParticulaJatoInicial = 0.45f;
    [SerializeField] private float inclinacaoJatoInicial = 0f;

    [Header("Queda Espalhada")]
    [SerializeField] private float quantidadeParticulasQueda = 260f;
    [SerializeField] private float tamanhoParticulaQueda = 0.06f;
    [SerializeField] private float velocidadeQueda = 1.8f;
    [SerializeField] private float larguraQueda = 1.2f;
    [SerializeField] private float gravidadeQueda = 1.4f;
    [SerializeField] private float vidaParticulaQueda = 1.1f;
    [SerializeField] private float alturaInicioQueda = 0.9f;
    [SerializeField] private float raioAreaQueda = 1.4f;
    [SerializeField] private bool espalharQuedaEmCirculo = true;

    [Header("Direcao do Jato")]
    [SerializeField] private bool jatoSempreParaCima = true;
    [SerializeField] private bool usarAlvoParaQueda = true;

    [Header("Animacao")]
    [SerializeField] private float velocidadeAnimacaoUV = 0.05f;
    [SerializeField] private float intensidadeOndulacao = 0.015f;
    [SerializeField] private float velocidadeOndulacao = 1.6f;
    [SerializeField] private Color corAgua = new Color(0.45f, 0.85f, 1f, 0.5f);

    [Header("Sistema")]
    [SerializeField] private bool criarAutomaticamente = true;
    [SerializeField] private bool tocarAoIniciar = true;

    private ParticleSystem particleJato;
    private ParticleSystem particleQueda;
    private ParticleSystem particleRespingo;
    private Transform aguaSuperior;
    private Transform aguaMeio;
    private Transform aguaInferior;
    private Mesh meshDiscoAgua;
    private Material materialAguaInstancia;
    private Material materialParticulaInstancia;
    private Vector2 offsetUV;

    private void Start()
    {
        if (criarAutomaticamente)
            CriarOuAtualizarSistema();
        else
            CapturarObjetosExistentes();

        if (tocarAoIniciar)
            IniciarAgua();
        else
            PararAgua();
    }

    private void Update()
    {
        AtualizarPosicoesFX();
        AnimarAguaDosPratos();
        AnimarMaterialAgua();
    }

    private void OnDisable()
    {
        PararAgua();
    }

    private void OnValidate()
    {
        alturaAguaPrato = Mathf.Max(0f, alturaAguaPrato);
        escalaAguaPratoSuperior = Mathf.Max(0.01f, escalaAguaPratoSuperior);
        escalaAguaPratoMeio = Mathf.Max(0.01f, escalaAguaPratoMeio);
        escalaAguaPratoInferior = Mathf.Max(0.01f, escalaAguaPratoInferior);
        alturaJatoInicial = Mathf.Max(0f, alturaJatoInicial);
        quantidadeParticulasJatoInicial = Mathf.Max(1f, quantidadeParticulasJatoInicial);
        tamanhoParticulaJatoInicial = Mathf.Max(0.001f, tamanhoParticulaJatoInicial);
        velocidadeJatoInicial = Mathf.Max(0.01f, velocidadeJatoInicial);
        larguraJatoInicial = Mathf.Max(0.001f, larguraJatoInicial);
        gravidadeJatoInicial = Mathf.Max(0f, gravidadeJatoInicial);
        vidaParticulaJatoInicial = Mathf.Max(0.08f, vidaParticulaJatoInicial);
        inclinacaoJatoInicial = Mathf.Clamp(inclinacaoJatoInicial, -75f, 75f);
        quantidadeParticulasQueda = Mathf.Max(1f, quantidadeParticulasQueda);
        tamanhoParticulaQueda = Mathf.Max(0.001f, tamanhoParticulaQueda);
        velocidadeQueda = Mathf.Max(0.01f, velocidadeQueda);
        larguraQueda = Mathf.Max(0.001f, larguraQueda);
        gravidadeQueda = Mathf.Max(0f, gravidadeQueda);
        vidaParticulaQueda = Mathf.Max(0.08f, vidaParticulaQueda);
        alturaInicioQueda = Mathf.Max(0f, alturaInicioQueda);
        raioAreaQueda = Mathf.Max(0.001f, raioAreaQueda);
        velocidadeAnimacaoUV = Mathf.Max(0f, velocidadeAnimacaoUV);
        intensidadeOndulacao = Mathf.Max(0f, intensidadeOndulacao);
        velocidadeOndulacao = Mathf.Max(0f, velocidadeOndulacao);

        if (Application.isPlaying)
            AtualizarCorDosMateriais();
    }

    public void IniciarAgua()
    {
        if (criarAutomaticamente && (particleJato == null || particleQueda == null))
            CriarOuAtualizarSistema();
        else if (particleJato == null || particleQueda == null || particleRespingo == null)
            CapturarObjetosExistentes();

        if (particleJato != null && !particleJato.isPlaying)
            particleJato.Play(true);

        if (particleQueda != null && !particleQueda.isPlaying)
            particleQueda.Play(true);

        if (particleRespingo != null && !particleRespingo.isPlaying)
            particleRespingo.Play(true);
    }

    public void PararAgua()
    {
        if (particleJato != null)
            particleJato.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (particleQueda != null)
            particleQueda.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (particleRespingo != null)
            particleRespingo.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    [ContextMenu("Recriar Sistema de Agua")]
    public void RecriarSistema()
    {
        CriarOuAtualizarSistema();

        if (tocarAoIniciar)
            IniciarAgua();
        else
            PararAgua();
    }

    [ContextMenu("Atualizar Sistema de Agua")]
    public void AtualizarSistemaDeAgua()
    {
        CapturarObjetosExistentes();
        AtualizarObjetosExistentes();
    }

    private void CriarOuAtualizarSistema()
    {
        PrepararMateriais();

        // Reutiliza filhos existentes para evitar duplicatas no prefab/cena.
        if (origemJato != null)
        {
            GameObject jato = ObterOuCriarFilho(NomeJato);
            particleJato = ObterOuAdicionar<ParticleSystem>(jato);
            ConfigurarJato(particleJato);

            GameObject queda = ObterOuCriarFilho(NomeQueda);
            particleQueda = ObterOuAdicionar<ParticleSystem>(queda);
            ConfigurarQueda(particleQueda);
        }
        else
        {
            Debug.LogWarning("[FonteAgua] OrigemJato nao foi atribuido. O jato e a queda de agua nao serao criados.", this);
        }

        if (alvoJato != null)
        {
            GameObject respingo = ObterOuCriarFilho(NomeRespingo);
            particleRespingo = ObterOuAdicionar<ParticleSystem>(respingo);
            ConfigurarRespingo(particleRespingo);
        }
        else
        {
            Debug.LogWarning("[FonteAgua] AlvoJato nao foi atribuido. O respingo sera ignorado.", this);
        }

        aguaSuperior = CriarOuAtualizarDiscoAgua(
            NomeAguaSuperior,
            pratoSuperior,
            escalaAguaPratoSuperior,
            rotacaoAguaPratoSuperior,
            deslocamentoVerticalPratoSuperior);

        aguaMeio = CriarOuAtualizarDiscoAgua(
            NomeAguaMeio,
            pratoMeio,
            escalaAguaPratoMeio,
            rotacaoAguaPratoMeio,
            deslocamentoVerticalPratoMeio);

        aguaInferior = CriarOuAtualizarDiscoAgua(
            NomeAguaInferior,
            pratoInferior,
            escalaAguaPratoInferior,
            rotacaoAguaPratoInferior,
            deslocamentoVerticalPratoInferior);
    }

    private void CapturarObjetosExistentes()
    {
        particleJato = ObterParticleSystemExistente(NomeJato);
        particleQueda = ObterParticleSystemExistente(NomeQueda);
        particleRespingo = ObterParticleSystemExistente(NomeRespingo);
        aguaSuperior = ObterTransformExistente(NomeAguaSuperior);
        aguaMeio = ObterTransformExistente(NomeAguaMeio);
        aguaInferior = ObterTransformExistente(NomeAguaInferior);
        PrepararMateriais();
    }

    private void AtualizarObjetosExistentes()
    {
        if (particleJato != null)
            ConfigurarJato(particleJato);

        if (particleQueda != null)
            ConfigurarQueda(particleQueda);

        if (particleRespingo != null)
            ConfigurarRespingo(particleRespingo);

        AtualizarDiscoExistente(
            aguaSuperior,
            pratoSuperior,
            escalaAguaPratoSuperior,
            rotacaoAguaPratoSuperior,
            deslocamentoVerticalPratoSuperior);

        AtualizarDiscoExistente(
            aguaMeio,
            pratoMeio,
            escalaAguaPratoMeio,
            rotacaoAguaPratoMeio,
            deslocamentoVerticalPratoMeio);

        AtualizarDiscoExistente(
            aguaInferior,
            pratoInferior,
            escalaAguaPratoInferior,
            rotacaoAguaPratoInferior,
            deslocamentoVerticalPratoInferior);
    }

    private GameObject ObterOuCriarFilho(string nome)
    {
        Transform filho = transform.Find(nome);
        if (filho != null)
            return filho.gameObject;

        GameObject novo = new GameObject(nome);
        novo.transform.SetParent(transform, false);
        return novo;
    }

    private Transform ObterTransformExistente(string nome)
    {
        Transform filho = transform.Find(nome);
        return filho != null ? filho : null;
    }

    private ParticleSystem ObterParticleSystemExistente(string nome)
    {
        Transform filho = transform.Find(nome);
        return filho != null ? filho.GetComponent<ParticleSystem>() : null;
    }

    private T ObterOuAdicionar<T>(GameObject alvo) where T : Component
    {
        T componente = alvo.GetComponent<T>();
        return componente != null ? componente : alvo.AddComponent<T>();
    }

    private Transform CriarOuAtualizarDiscoAgua(
        string nome,
        Transform prato,
        float escala,
        Vector3 rotacaoManual,
        float deslocamentoVertical)
    {
        if (prato == null)
        {
            Debug.LogWarning($"[FonteAgua] {nome}: transform do prato nao foi atribuido.", this);
            return ObterTransformExistente(nome);
        }

        GameObject disco = ObterOuCriarFilho(nome);
        MeshFilter meshFilter = ObterOuAdicionar<MeshFilter>(disco);
        MeshRenderer meshRenderer = ObterOuAdicionar<MeshRenderer>(disco);

        meshFilter.sharedMesh = ObterMeshDiscoAgua();
        meshRenderer.sharedMaterial = ObterMaterialAgua();

        AtualizarTransformDisco(disco.transform, prato, escala, rotacaoManual, deslocamentoVertical, 0f);
        return disco.transform;
    }

    private void AtualizarDiscoExistente(
        Transform agua,
        Transform prato,
        float escala,
        Vector3 rotacaoManual,
        float deslocamentoVertical)
    {
        if (agua == null || prato == null)
            return;

        MeshFilter meshFilter = agua.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = agua.GetComponent<MeshRenderer>();

        if (meshFilter != null)
            meshFilter.sharedMesh = ObterMeshDiscoAgua();

        if (meshRenderer != null)
            meshRenderer.sharedMaterial = ObterMaterialAgua();

        AtualizarTransformDisco(agua, prato, escala, rotacaoManual, deslocamentoVertical, 0f);
    }

    private Mesh ObterMeshDiscoAgua()
    {
        if (meshDiscoAgua != null)
            return meshDiscoAgua;

        // Disco simples em XZ: barato para VR e suficiente para agua parada.
        const int segmentos = 48;
        Vector3[] vertices = new Vector3[segmentos + 1];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangulos = new int[segmentos * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segmentos; i++)
        {
            float angulo = (Mathf.PI * 2f * i) / segmentos;
            float x = Mathf.Cos(angulo) * 0.5f;
            float z = Mathf.Sin(angulo) * 0.5f;

            vertices[i + 1] = new Vector3(x, 0f, z);
            uvs[i + 1] = new Vector2(x + 0.5f, z + 0.5f);
        }

        for (int i = 0; i < segmentos; i++)
        {
            int baseTriangulo = i * 3;
            triangulos[baseTriangulo] = 0;
            triangulos[baseTriangulo + 1] = i == segmentos - 1 ? 1 : i + 2;
            triangulos[baseTriangulo + 2] = i + 1;
        }

        meshDiscoAgua = new Mesh
        {
            name = "Mesh_DiscoAgua_Fonte"
        };

        Vector3[] normais = new Vector3[vertices.Length];
        for (int i = 0; i < normais.Length; i++)
            normais[i] = Vector3.up;

        meshDiscoAgua.vertices = vertices;
        meshDiscoAgua.uv = uvs;
        meshDiscoAgua.normals = normais;
        meshDiscoAgua.triangles = triangulos;
        meshDiscoAgua.RecalculateBounds();

        return meshDiscoAgua;
    }

    private void ConfigurarJato(ParticleSystem sistema)
    {
        if (sistema == null)
            return;

        if (origemJato == null)
        {
            Debug.LogWarning("[FonteAgua] OrigemJato nao foi atribuido. FX_JatoAgua nao sera posicionado.", this);
            return;
        }

        bool estavaTocando = sistema.isPlaying;
        if (estavaTocando)
            sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Jato fino e forte: nasce exatamente na ponta da fonte.
        Vector3 direcaoJato = CalcularDirecaoJatoInicial();
        sistema.transform.position = origemJato.position;
        sistema.transform.rotation = RotacaoParaDirecao(direcaoJato);

        ParticleSystem.MainModule main = sistema.main;
        main.loop = true;
        main.playOnAwake = tocarAoIniciar;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = CalcularMaxParticulas(quantidadeParticulasJatoInicial, vidaParticulaJatoInicial, 2.2f);
        main.startLifetime = CalcularVidaJatoInicial();
        main.startSpeed = velocidadeJatoInicial;
        main.startSize = new ParticleSystem.MinMaxCurve(tamanhoParticulaJatoInicial * 0.8f, tamanhoParticulaJatoInicial * 1.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(CorComAlpha(corAgua, 0.92f));
        main.gravityModifier = gravidadeJatoInicial;

        ParticleSystem.EmissionModule emission = sistema.emission;
        emission.enabled = true;
        emission.rateOverTime = quantidadeParticulasJatoInicial;

        ParticleSystem.ShapeModule shape = sistema.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 4f;
        shape.radius = larguraJatoInicial;
        shape.length = 0f;

        ParticleSystem.ColorOverLifetimeModule color = sistema.colorOverLifetime;
        color.enabled = true;
        color.color = CriarGradienteAgua(0.95f, 0.25f);

        ParticleSystem.SizeOverLifetimeModule size = sistema.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.75f),
                new Keyframe(0.45f, 1f),
                new Keyframe(1f, 0.25f)));

        ParticleSystemRenderer renderer = sistema.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = ObterMaterialParticulas();
        renderer.sortingFudge = 0.1f;

        if (estavaTocando)
            sistema.Play(true);
    }

    private void ConfigurarQueda(ParticleSystem sistema)
    {
        if (sistema == null)
            return;

        if (origemJato == null)
        {
            Debug.LogWarning("[FonteAgua] OrigemJato nao foi atribuido. FX_QuedaAgua nao sera posicionado.", this);
            return;
        }

        bool estavaTocando = sistema.isPlaying;
        if (estavaTocando)
            sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Queda larga: nasce perto do topo do arco e cobre a bacia.
        sistema.transform.position = CalcularPosicaoQueda();
        sistema.transform.rotation = RotacaoParaDirecao(CalcularDirecaoQueda());

        ParticleSystem.MainModule main = sistema.main;
        main.loop = true;
        main.playOnAwake = tocarAoIniciar;
        main.duration = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = CalcularMaxParticulas(quantidadeParticulasQueda, vidaParticulaQueda, 2.1f);
        main.startLifetime = vidaParticulaQueda;
        main.startSpeed = velocidadeQueda;
        main.startSize = new ParticleSystem.MinMaxCurve(tamanhoParticulaQueda * 0.75f, tamanhoParticulaQueda * 1.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(CorComAlpha(corAgua, 0.78f));
        main.gravityModifier = gravidadeQueda;

        ParticleSystem.EmissionModule emission = sistema.emission;
        emission.enabled = true;
        emission.rateOverTime = quantidadeParticulasQueda;

        ParticleSystem.ShapeModule shape = sistema.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = espalharQuedaEmCirculo ? 48f : 28f;
        shape.radius = espalharQuedaEmCirculo ? raioAreaQueda : larguraQueda;
        shape.length = 0.1f;

        ParticleSystem.ColorOverLifetimeModule color = sistema.colorOverLifetime;
        color.enabled = true;
        color.color = CriarGradienteAgua(0.8f, 0.05f);

        ParticleSystem.SizeOverLifetimeModule size = sistema.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.55f),
                new Keyframe(0.35f, 1f),
                new Keyframe(1f, 0.2f)));

        ParticleSystemRenderer renderer = sistema.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = ObterMaterialParticulas();
        renderer.sortingFudge = 0.08f;

        if (estavaTocando)
            sistema.Play(true);
    }

    private void ConfigurarRespingo(ParticleSystem sistema)
    {
        if (sistema == null || alvoJato == null)
            return;

        bool estavaTocando = sistema.isPlaying;
        if (estavaTocando)
            sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Respingo curto e aberto no ponto em que a queda encontra a bacia.
        sistema.transform.position = alvoJato.position;
        sistema.transform.rotation = RotacaoParaDirecao(Vector3.up);

        ParticleSystem.MainModule main = sistema.main;
        main.loop = true;
        main.playOnAwake = tocarAoIniciar;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = CalcularMaxParticulas(quantidadeParticulasQueda * 0.22f, 0.45f, 2f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.75f);
        main.startSize = new ParticleSystem.MinMaxCurve(tamanhoParticulaQueda * 0.25f, tamanhoParticulaQueda * 0.65f);
        main.startColor = new ParticleSystem.MinMaxGradient(CorComAlpha(corAgua, 0.65f));
        main.gravityModifier = Mathf.Max(0.45f, gravidadeQueda * 0.65f);

        ParticleSystem.EmissionModule emission = sistema.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Clamp(quantidadeParticulasQueda * 0.22f, 18f, 75f);

        ParticleSystem.ShapeModule shape = sistema.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 78f;
        shape.radius = Mathf.Max(0.08f, raioAreaQueda * 0.22f);
        shape.length = 0f;

        ParticleSystem.ColorOverLifetimeModule color = sistema.colorOverLifetime;
        color.enabled = true;
        color.color = CriarGradienteAgua(0.6f, 0f);

        ParticleSystemRenderer renderer = sistema.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = ObterMaterialParticulas();
        renderer.sortingFudge = 0.05f;

        if (estavaTocando)
            sistema.Play(true);
    }

    private float CalcularVidaJatoInicial()
    {
        float vidaPorAltura = alturaJatoInicial / Mathf.Max(0.01f, velocidadeJatoInicial);
        return Mathf.Clamp(Mathf.Min(vidaParticulaJatoInicial, vidaPorAltura), 0.08f, vidaParticulaJatoInicial);
    }

    private int CalcularMaxParticulas(float emissao, float vida, float multiplicador)
    {
        return Mathf.Clamp(Mathf.CeilToInt(emissao * vida * multiplicador), 32, 1200);
    }

    private Vector3 CalcularDirecaoJatoInicial()
    {
        if (origemJato == null)
            return Vector3.up;

        Vector3 eixoCima = origemJato.up.sqrMagnitude > 0.001f ? origemJato.up.normalized : Vector3.up;
        Vector3 direcao = eixoCima;

        if (!jatoSempreParaCima && alvoJato != null)
        {
            Vector3 paraAlvo = alvoJato.position - origemJato.position;
            if (paraAlvo.sqrMagnitude > 0.001f)
                direcao = paraAlvo.normalized;
        }

        if (Mathf.Abs(inclinacaoJatoInicial) > 0.01f && alvoJato != null)
        {
            Vector3 horizontalParaAlvo = Vector3.ProjectOnPlane(alvoJato.position - origemJato.position, eixoCima);
            if (horizontalParaAlvo.sqrMagnitude > 0.001f)
            {
                float inclinacao = Mathf.Tan(inclinacaoJatoInicial * Mathf.Deg2Rad);
                direcao = (direcao + horizontalParaAlvo.normalized * inclinacao).normalized;
            }
        }

        return direcao.sqrMagnitude > 0.001f ? direcao.normalized : Vector3.up;
    }

    private Vector3 CalcularPosicaoQueda()
    {
        if (origemJato == null)
            return transform.position;

        return origemJato.position + origemJato.up * alturaInicioQueda;
    }

    private Vector3 CalcularDirecaoQueda()
    {
        Vector3 direcaoBaixo = origemJato != null && origemJato.up.sqrMagnitude > 0.001f
            ? -origemJato.up.normalized
            : Vector3.down;

        if (usarAlvoParaQueda && alvoJato != null)
        {
            Vector3 paraAlvo = alvoJato.position - CalcularPosicaoQueda();
            if (paraAlvo.sqrMagnitude > 0.001f)
                return (paraAlvo.normalized + direcaoBaixo * 1.35f).normalized;
        }

        return direcaoBaixo;
    }

    private Quaternion RotacaoParaDirecao(Vector3 direcao)
    {
        if (direcao.sqrMagnitude < 0.001f)
            direcao = Vector3.up;

        direcao.Normalize();

        Vector3 referenciaCima = Mathf.Abs(Vector3.Dot(direcao, Vector3.up)) > 0.95f
            ? Vector3.forward
            : Vector3.up;

        return Quaternion.LookRotation(direcao, referenciaCima);
    }

    private void AtualizarPosicoesFX()
    {
        if (particleJato != null && origemJato != null)
        {
            Vector3 direcaoJato = CalcularDirecaoJatoInicial();
            particleJato.transform.position = origemJato.position;
            particleJato.transform.rotation = RotacaoParaDirecao(direcaoJato);

            ParticleSystem.MainModule main = particleJato.main;
            main.startLifetime = CalcularVidaJatoInicial();
            main.startSpeed = velocidadeJatoInicial;
            main.gravityModifier = gravidadeJatoInicial;
            main.startSize = new ParticleSystem.MinMaxCurve(tamanhoParticulaJatoInicial * 0.8f, tamanhoParticulaJatoInicial * 1.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(CorComAlpha(corAgua, 0.92f));
            main.maxParticles = CalcularMaxParticulas(quantidadeParticulasJatoInicial, vidaParticulaJatoInicial, 2.2f);

            ParticleSystem.EmissionModule emission = particleJato.emission;
            emission.rateOverTime = quantidadeParticulasJatoInicial;

            ParticleSystem.ShapeModule shape = particleJato.shape;
            shape.angle = 4f;
            shape.radius = larguraJatoInicial;
        }

        if (particleQueda != null && origemJato != null)
        {
            particleQueda.transform.position = CalcularPosicaoQueda();
            particleQueda.transform.rotation = RotacaoParaDirecao(CalcularDirecaoQueda());

            ParticleSystem.MainModule main = particleQueda.main;
            main.startLifetime = vidaParticulaQueda;
            main.startSpeed = velocidadeQueda;
            main.gravityModifier = gravidadeQueda;
            main.startSize = new ParticleSystem.MinMaxCurve(tamanhoParticulaQueda * 0.75f, tamanhoParticulaQueda * 1.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(CorComAlpha(corAgua, 0.78f));
            main.maxParticles = CalcularMaxParticulas(quantidadeParticulasQueda, vidaParticulaQueda, 2.1f);

            ParticleSystem.EmissionModule emission = particleQueda.emission;
            emission.rateOverTime = quantidadeParticulasQueda;

            ParticleSystem.ShapeModule shape = particleQueda.shape;
            shape.angle = espalharQuedaEmCirculo ? 48f : 28f;
            shape.radius = espalharQuedaEmCirculo ? raioAreaQueda : larguraQueda;
        }

        if (particleRespingo != null && alvoJato != null)
        {
            particleRespingo.transform.position = alvoJato.position;
            particleRespingo.transform.rotation = RotacaoParaDirecao(Vector3.up);

            ParticleSystem.MainModule main = particleRespingo.main;
            main.startSize = new ParticleSystem.MinMaxCurve(tamanhoParticulaQueda * 0.25f, tamanhoParticulaQueda * 0.65f);
            main.startColor = new ParticleSystem.MinMaxGradient(CorComAlpha(corAgua, 0.65f));

            ParticleSystem.EmissionModule emission = particleRespingo.emission;
            emission.rateOverTime = Mathf.Clamp(quantidadeParticulasQueda * 0.22f, 18f, 75f);
        }
    }

    private void AnimarAguaDosPratos()
    {
        float tempo = Time.time * velocidadeOndulacao;

        // Pequena oscilacao vertical separada por prato para evitar movimento igual.
        AtualizarTransformDisco(
            aguaSuperior,
            pratoSuperior,
            escalaAguaPratoSuperior,
            rotacaoAguaPratoSuperior,
            deslocamentoVerticalPratoSuperior,
            Mathf.Sin(tempo) * intensidadeOndulacao);

        AtualizarTransformDisco(
            aguaMeio,
            pratoMeio,
            escalaAguaPratoMeio,
            rotacaoAguaPratoMeio,
            deslocamentoVerticalPratoMeio,
            Mathf.Sin(tempo + 1.7f) * intensidadeOndulacao);

        AtualizarTransformDisco(
            aguaInferior,
            pratoInferior,
            escalaAguaPratoInferior,
            rotacaoAguaPratoInferior,
            deslocamentoVerticalPratoInferior,
            Mathf.Sin(tempo + 3.4f) * intensidadeOndulacao);
    }

    private void AtualizarTransformDisco(
        Transform agua,
        Transform prato,
        float escala,
        Vector3 rotacaoManual,
        float deslocamentoVertical,
        float onda)
    {
        if (agua == null || prato == null)
            return;

        Vector3 eixoDeslocamento = usarRotacaoDoPrato ? prato.up : Vector3.up;
        Quaternion rotacaoBase = usarRotacaoDoPrato ? prato.rotation : Quaternion.identity;

        agua.position = prato.position + eixoDeslocamento * (deslocamentoVertical + onda);
        agua.rotation = rotacaoBase * Quaternion.Euler(rotacaoManual);
        agua.localScale = Vector3.one * escala;
    }

    private void AnimarMaterialAgua()
    {
        if (materialAguaInstancia == null || velocidadeAnimacaoUV <= 0f)
            return;

        offsetUV += new Vector2(velocidadeAnimacaoUV, velocidadeAnimacaoUV * 0.45f) * Time.deltaTime;

        if (materialAguaInstancia.HasProperty("_BaseMap"))
            materialAguaInstancia.SetTextureOffset("_BaseMap", offsetUV);

        if (materialAguaInstancia.HasProperty("_MainTex"))
            materialAguaInstancia.SetTextureOffset("_MainTex", offsetUV);
    }

    private void PrepararMateriais()
    {
        ObterMaterialAgua();
        ObterMaterialParticulas();
        AtualizarCorDosMateriais();
    }

    private Material ObterMaterialAgua()
    {
        if (materialAguaInstancia != null)
            return materialAguaInstancia;

        materialAguaInstancia = materialAgua != null
            ? new Material(materialAgua)
            : CriarMaterialBase(
                "M_FonteAgua_Runtime",
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit",
                "Unlit/Transparent",
                "Standard");

        materialAguaInstancia.name = materialAgua != null
            ? materialAgua.name + "_FonteInstancia"
            : "M_FonteAgua_Runtime";

        ConfigurarMaterialTransparente(materialAguaInstancia, corAgua, 0.9f);
        return materialAguaInstancia;
    }

    private Material ObterMaterialParticulas()
    {
        if (materialParticulaInstancia != null)
            return materialParticulaInstancia;

        materialParticulaInstancia = CriarMaterialBase(
            "M_FonteAgua_Particulas_Runtime",
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Universal Render Pipeline/Unlit",
            "Unlit/Transparent",
            "Sprites/Default");

        ConfigurarMaterialTransparente(materialParticulaInstancia, CorComAlpha(corAgua, 0.7f), 0.5f);
        return materialParticulaInstancia;
    }

    private Material CriarMaterialBase(string nome, params string[] shaders)
    {
        Shader shader = null;

        for (int i = 0; i < shaders.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(shaders[i]))
                continue;

            shader = Shader.Find(shaders[i]);

            if (shader != null)
                break;
        }

        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            name = nome
        };

        return material;
    }

    private void ConfigurarMaterialTransparente(Material material, Color cor, float smoothness)
    {
        if (material == null)
            return;

        // Compatibilidade simples com URP/Lit, Standard e shaders de particula.
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", cor);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", cor);

        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", cor);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);

        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness);

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);

        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);

        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        if (material.HasProperty("_CullMode"))
            material.SetFloat("_CullMode", (float)UnityEngine.Rendering.CullMode.Off);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.SetOverrideTag("RenderType", "Transparent");
        material.SetOverrideTag("Queue", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.doubleSidedGI = true;
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void AtualizarCorDosMateriais()
    {
        ConfigurarMaterialTransparente(materialAguaInstancia, corAgua, 0.9f);
        ConfigurarMaterialTransparente(materialParticulaInstancia, CorComAlpha(corAgua, 0.7f), 0.5f);

        if (particleJato != null)
        {
            ParticleSystem.MainModule main = particleJato.main;
            main.startColor = new ParticleSystem.MinMaxGradient(CorComAlpha(corAgua, 0.92f));

            ParticleSystem.ColorOverLifetimeModule color = particleJato.colorOverLifetime;
            color.color = CriarGradienteAgua(0.95f, 0.25f);
        }

        if (particleQueda != null)
        {
            ParticleSystem.MainModule main = particleQueda.main;
            main.startColor = new ParticleSystem.MinMaxGradient(CorComAlpha(corAgua, 0.78f));

            ParticleSystem.ColorOverLifetimeModule color = particleQueda.colorOverLifetime;
            color.color = CriarGradienteAgua(0.8f, 0.05f);
        }

        if (particleRespingo != null)
        {
            ParticleSystem.MainModule main = particleRespingo.main;
            main.startColor = new ParticleSystem.MinMaxGradient(CorComAlpha(corAgua, 0.65f));

            ParticleSystem.ColorOverLifetimeModule color = particleRespingo.colorOverLifetime;
            color.color = CriarGradienteAgua(0.6f, 0f);
        }
    }

    private ParticleSystem.MinMaxGradient CriarGradienteAgua(float alphaInicio, float alphaFim)
    {
        Gradient gradiente = new Gradient();
        gradiente.SetKeys(
            new[]
            {
                new GradientColorKey(corAgua, 0f),
                new GradientColorKey(Color.white, 0.35f),
                new GradientColorKey(corAgua, 1f)
            },
            new[]
            {
                new GradientAlphaKey(alphaInicio, 0f),
                new GradientAlphaKey(alphaInicio * 0.65f, 0.55f),
                new GradientAlphaKey(alphaFim, 1f)
            });

        return new ParticleSystem.MinMaxGradient(gradiente);
    }

    private Color CorComAlpha(Color cor, float alpha)
    {
        cor.a = Mathf.Clamp01(alpha);
        return cor;
    }
}
