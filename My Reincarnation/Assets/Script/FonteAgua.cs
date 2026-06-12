using UnityEngine;

[DisallowMultipleComponent]
public class FonteAgua : MonoBehaviour
{
    private const string NomeJato = "FX_JatoAgua";
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

    [Header("Jato de Agua")]
    [SerializeField] private float quantidadeParticulasJato = 120f;
    [SerializeField] private float tamanhoParticulaJato = 0.08f;
    [SerializeField] private float velocidadeJato = 3.5f;
    [SerializeField] private float gravidadeJato = 0.65f;
    [SerializeField] private float larguraJato = 0.08f;
    [SerializeField] private float vidaParticulaJato = 1.2f;
    [SerializeField] private float alturaExtraJato = 1.0f;
    [SerializeField] private int maxParticulasJato = 800;
    [SerializeField] private bool usarJatoMaisCheio = true;

    [Header("Animacao")]
    [SerializeField] private float velocidadeAnimacaoUV = 0.05f;
    [SerializeField] private float intensidadeOndulacao = 0.015f;
    [SerializeField] private float velocidadeOndulacao = 1.6f;
    [SerializeField] private Color corAgua = new Color(0.45f, 0.85f, 1f, 0.5f);

    [Header("Sistema")]
    [SerializeField] private bool criarAutomaticamente = true;
    [SerializeField] private bool tocarAoIniciar = true;

    private ParticleSystem particleJato;
    private ParticleSystem particleRespingo;
    private Transform aguaSuperior;
    private Transform aguaMeio;
    private Transform aguaInferior;
    private Mesh meshDiscoAgua;
    private Material materialAguaInstancia;
    private Material materialParticulaInstancia;
    private Vector2 offsetUV;

    // Guarda a direcao e os parametros usados para apontar o jato.
    private struct DadosLancamento
    {
        public Vector3 direcao;
        public float velocidade;
        public float tempoVoo;
        public float gravidadeModifier;
    }

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
        quantidadeParticulasJato = Mathf.Max(1f, quantidadeParticulasJato);
        tamanhoParticulaJato = Mathf.Max(0.001f, tamanhoParticulaJato);
        velocidadeJato = Mathf.Max(0.01f, velocidadeJato);
        gravidadeJato = Mathf.Max(0f, gravidadeJato);
        larguraJato = Mathf.Max(0.001f, larguraJato);
        vidaParticulaJato = Mathf.Max(0.05f, vidaParticulaJato);
        alturaExtraJato = Mathf.Max(0f, alturaExtraJato);
        maxParticulasJato = Mathf.Max(1, maxParticulasJato);
        velocidadeAnimacaoUV = Mathf.Max(0f, velocidadeAnimacaoUV);
        intensidadeOndulacao = Mathf.Max(0f, intensidadeOndulacao);
        velocidadeOndulacao = Mathf.Max(0f, velocidadeOndulacao);

        if (Application.isPlaying)
            AtualizarCorDosMateriais();
    }

    public void IniciarAgua()
    {
        if (criarAutomaticamente && particleJato == null)
            CriarOuAtualizarSistema();
        else if (particleJato == null || particleRespingo == null)
            CapturarObjetosExistentes();

        if (particleJato != null && !particleJato.isPlaying)
            particleJato.Play(true);

        if (particleRespingo != null && !particleRespingo.isPlaying)
            particleRespingo.Play(true);
    }

    public void PararAgua()
    {
        if (particleJato != null)
            particleJato.Stop(true, ParticleSystemStopBehavior.StopEmitting);

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
        }
        else
        {
            Debug.LogWarning("[FonteAgua] OrigemJato nao foi atribuido. O jato de agua nao sera criado.", this);
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

    private float QuantidadeJatoAtual()
    {
        return usarJatoMaisCheio ? quantidadeParticulasJato * 1.75f : quantidadeParticulasJato;
    }

    private float TamanhoJatoAtual()
    {
        return usarJatoMaisCheio ? tamanhoParticulaJato * 1.25f : tamanhoParticulaJato;
    }

    private Color CorJatoAtual()
    {
        return CorComAlpha(corAgua, usarJatoMaisCheio ? 0.9f : 0.72f);
    }

    private void ConfigurarJato(ParticleSystem sistema)
    {
        if (sistema == null)
            return;

        // Um unico ParticleSystem em mundo simula o arco sem VFX Graph.
        DadosLancamento lancamento = CalcularLancamento();
        sistema.transform.position = origemJato.position;
        sistema.transform.rotation = Quaternion.LookRotation(lancamento.direcao, Vector3.up);

        ParticleSystem.MainModule main = sistema.main;
        main.loop = true;
        main.playOnAwake = tocarAoIniciar;
        main.duration = 2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = maxParticulasJato;
        main.startLifetime = lancamento.tempoVoo;
        main.startSpeed = lancamento.velocidade;
        main.startSize = new ParticleSystem.MinMaxCurve(TamanhoJatoAtual() * 0.75f, TamanhoJatoAtual() * 1.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(CorJatoAtual());
        main.gravityModifier = lancamento.gravidadeModifier;

        ParticleSystem.EmissionModule emission = sistema.emission;
        emission.enabled = true;
        emission.rateOverTime = QuantidadeJatoAtual();

        ParticleSystem.ShapeModule shape = sistema.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = usarJatoMaisCheio ? 2.5f : 3.5f;
        shape.radius = larguraJato;
        shape.length = 0f;

        ParticleSystem.ColorOverLifetimeModule color = sistema.colorOverLifetime;
        color.enabled = true;
        color.color = CriarGradienteAgua(usarJatoMaisCheio ? 0.95f : 0.78f, 0.18f);

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
    }

    private void ConfigurarRespingo(ParticleSystem sistema)
    {
        if (sistema == null || alvoJato == null)
            return;

        // Respingo pequeno e opcional no ponto de queda do jato.
        sistema.transform.position = alvoJato.position;
        sistema.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);

        ParticleSystem.MainModule main = sistema.main;
        main.loop = true;
        main.playOnAwake = tocarAoIniciar;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Clamp(maxParticulasJato / 4, 80, 220);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(TamanhoJatoAtual() * 0.22f, TamanhoJatoAtual() * 0.55f);
        main.startColor = new ParticleSystem.MinMaxGradient(CorComAlpha(corAgua, 0.65f));
        main.gravityModifier = Mathf.Max(0.15f, gravidadeJato * 0.7f);

        ParticleSystem.EmissionModule emission = sistema.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Clamp(quantidadeParticulasJato * 0.22f, 18f, 75f);

        ParticleSystem.ShapeModule shape = sistema.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 34f;
        shape.radius = Mathf.Max(0.035f, larguraJato * 1.4f);
        shape.length = 0f;

        ParticleSystem.ColorOverLifetimeModule color = sistema.colorOverLifetime;
        color.enabled = true;
        color.color = CriarGradienteAgua(0.6f, 0f);

        ParticleSystemRenderer renderer = sistema.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = ObterMaterialParticulas();
        renderer.sortingFudge = 0.05f;
    }

    private DadosLancamento CalcularLancamento()
    {
        if (origemJato == null)
        {
            return new DadosLancamento
            {
                direcao = Vector3.up,
                velocidade = velocidadeJato,
                tempoVoo = vidaParticulaJato,
                gravidadeModifier = gravidadeJato
            };
        }

        if (alvoJato == null)
        {
            Vector3 direcaoSemAlvo = (origemJato.forward + Vector3.up * Mathf.Max(0.15f, alturaExtraJato)).normalized;
            return new DadosLancamento
            {
                direcao = direcaoSemAlvo,
                velocidade = velocidadeJato,
                tempoVoo = vidaParticulaJato,
                gravidadeModifier = gravidadeJato
            };
        }

        Vector3 origem = origemJato.position;
        Vector3 alvo = alvoJato.position;
        Vector3 direcaoParaAlvo = alvo - origem;
        Vector3 direcaoComAltura = direcaoParaAlvo + Vector3.up * alturaExtraJato;

        if (direcaoComAltura.sqrMagnitude < 0.001f)
            direcaoComAltura = Vector3.up;

        return new DadosLancamento
        {
            direcao = direcaoComAltura.normalized,
            velocidade = velocidadeJato,
            tempoVoo = vidaParticulaJato,
            gravidadeModifier = gravidadeJato
        };
    }

    private void AtualizarPosicoesFX()
    {
        if (particleJato != null && origemJato != null)
        {
            DadosLancamento lancamento = CalcularLancamento();

            particleJato.transform.position = origemJato.position;
            particleJato.transform.rotation = Quaternion.LookRotation(lancamento.direcao, Vector3.up);

            ParticleSystem.MainModule main = particleJato.main;
            main.startLifetime = lancamento.tempoVoo;
            main.startSpeed = lancamento.velocidade;
            main.gravityModifier = lancamento.gravidadeModifier;
            main.startSize = new ParticleSystem.MinMaxCurve(TamanhoJatoAtual() * 0.75f, TamanhoJatoAtual() * 1.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(CorJatoAtual());
            main.maxParticles = maxParticulasJato;

            ParticleSystem.EmissionModule emission = particleJato.emission;
            emission.rateOverTime = QuantidadeJatoAtual();

            ParticleSystem.ShapeModule shape = particleJato.shape;
            shape.angle = usarJatoMaisCheio ? 2.5f : 3.5f;
            shape.radius = larguraJato;
        }

        if (particleRespingo != null && alvoJato != null)
        {
            particleRespingo.transform.position = alvoJato.position;

            ParticleSystem.MainModule main = particleRespingo.main;
            main.startSize = new ParticleSystem.MinMaxCurve(TamanhoJatoAtual() * 0.22f, TamanhoJatoAtual() * 0.55f);
            main.startColor = new ParticleSystem.MinMaxGradient(CorComAlpha(corAgua, 0.65f));

            ParticleSystem.EmissionModule emission = particleRespingo.emission;
            emission.rateOverTime = Mathf.Clamp(quantidadeParticulasJato * 0.22f, 18f, 75f);
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
            main.startColor = new ParticleSystem.MinMaxGradient(CorJatoAtual());

            ParticleSystem.ColorOverLifetimeModule color = particleJato.colorOverLifetime;
            color.color = CriarGradienteAgua(usarJatoMaisCheio ? 0.95f : 0.78f, 0.18f);
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
