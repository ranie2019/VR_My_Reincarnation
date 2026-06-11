using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GerarCenarioRPG : EditorWindow
{
    [SerializeField] private bool gerarSomenteRelevoEPintura = true;

    [SerializeField] private float larguraTerreno = 2000f;
    [SerializeField] private float comprimentoTerreno = 2000f;
    [SerializeField] private float alturaMaximaMontanhas = 300f;
    [SerializeField] private int resolucaoHeightmap = 513;
    [SerializeField] private int resolucaoPintura = 512;
    [SerializeField] private int seedAleatoria = 12345;

    [SerializeField] private float raioAreaCentralLisa = 520f;
    [SerializeField] private float suavidadeTransicaoCentroMontanha = 520f;
    [SerializeField] private float ondulacaoCentro = 0.018f;
    [SerializeField] private float intensidadeCadeiasLaterais = 0.86f;
    [SerializeField] private float irregularidadeCadeiasLaterais = 0.62f;
    [SerializeField] private int quantidadeCristasMontanha = 10;
    [SerializeField] private float forcaCristasMontanha = 0.28f;
    [SerializeField] private float rugosidadeMontanha = 0.085f;
    [SerializeField] private bool montanhaEsquerda = true;
    [SerializeField] private bool montanhaDireita = true;
    [SerializeField] private bool montanhaFundo = true;
    [SerializeField] private bool montanhaFrente = true;

    [SerializeField] private bool usarVales = false;
    [SerializeField] private int quantidadeVales = 0;
    [SerializeField] private float forcaVales = 0f;

    [SerializeField] private float forcaPinturaGrama = 1.15f;
    [SerializeField] private float forcaPinturaTerra = 0.9f;
    [SerializeField] private float forcaPinturaRocha = 1.25f;
    [SerializeField] private float alturaMinimaRocha = 0.24f;
    [SerializeField] private float inclinacaoMinimaRocha = 24f;
    [SerializeField] private bool usarNeve = false;
    [SerializeField] private float alturaMinimaNeve = 0.82f;
    [SerializeField] private float forcaPinturaNeve = 0.35f;

    private Vector2 scroll;

    private struct ValeLocal
    {
        public Vector2 centro;
        public float raio;
        public float intensidade;
    }

    [MenuItem("Tools/My Reincarnation/Gerar Cenario RPG")]
    public static void AbrirJanela()
    {
        GetWindow<GerarCenarioRPG>("Gerar Cenario RPG");
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "Ferramenta focada somente no Terrain: gera centro liso jogavel, cadeias de montanhas laterais e pintura automatica por Terrain Layers.",
            MessageType.Info);

        gerarSomenteRelevoEPintura = EditorGUILayout.Toggle("Gerar Somente Relevo e Pintura", gerarSomenteRelevoEPintura);
        if (!gerarSomenteRelevoEPintura)
        {
            EditorGUILayout.HelpBox(
                "Esta versao nao instancia prefabs. Mesmo com esta opcao desligada, a ferramenta trabalha apenas no Terrain.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Terreno", EditorStyles.boldLabel);
        larguraTerreno = EditorGUILayout.FloatField("Largura do Terreno", larguraTerreno);
        comprimentoTerreno = EditorGUILayout.FloatField("Comprimento do Terreno", comprimentoTerreno);
        alturaMaximaMontanhas = EditorGUILayout.FloatField("Altura Maxima Montanhas", alturaMaximaMontanhas);
        resolucaoHeightmap = EditorGUILayout.IntPopup(
            "Resolucao Heightmap",
            resolucaoHeightmap,
            new[] { "257", "513", "1025" },
            new[] { 257, 513, 1025 });
        resolucaoPintura = EditorGUILayout.IntPopup(
            "Resolucao Pintura",
            resolucaoPintura,
            new[] { "256", "512", "1024" },
            new[] { 256, 512, 1024 });
        seedAleatoria = EditorGUILayout.IntField("Seed Aleatoria", seedAleatoria);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Centro e Transicao", EditorStyles.boldLabel);
        raioAreaCentralLisa = EditorGUILayout.FloatField("Raio Area Central Lisa", raioAreaCentralLisa);
        suavidadeTransicaoCentroMontanha = EditorGUILayout.FloatField("Suavidade Transicao Centro Montanha", suavidadeTransicaoCentroMontanha);
        ondulacaoCentro = EditorGUILayout.Slider("Ondulacao Centro", ondulacaoCentro, 0f, 0.08f);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Cadeias de Montanhas Laterais", EditorStyles.boldLabel);
        intensidadeCadeiasLaterais = EditorGUILayout.Slider("Intensidade Cadeias Laterais", intensidadeCadeiasLaterais, 0f, 1.25f);
        irregularidadeCadeiasLaterais = EditorGUILayout.Slider("Irregularidade Cadeias Laterais", irregularidadeCadeiasLaterais, 0f, 1f);
        quantidadeCristasMontanha = EditorGUILayout.IntSlider("Quantidade Cristas Montanha", quantidadeCristasMontanha, 0, 24);
        forcaCristasMontanha = EditorGUILayout.Slider("Forca Cristas Montanha", forcaCristasMontanha, 0f, 0.6f);
        rugosidadeMontanha = EditorGUILayout.Slider("Rugosidade Montanha", rugosidadeMontanha, 0f, 0.22f);
        montanhaEsquerda = EditorGUILayout.Toggle("Montanha Esquerda", montanhaEsquerda);
        montanhaDireita = EditorGUILayout.Toggle("Montanha Direita", montanhaDireita);
        montanhaFundo = EditorGUILayout.Toggle("Montanha Fundo", montanhaFundo);
        montanhaFrente = EditorGUILayout.Toggle("Montanha Frente", montanhaFrente);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Vales Opcionais", EditorStyles.boldLabel);
        usarVales = EditorGUILayout.Toggle("Usar Vales", usarVales);
        using (new EditorGUI.DisabledScope(!usarVales))
        {
            quantidadeVales = EditorGUILayout.IntSlider("Quantidade de Vales", quantidadeVales, 0, 8);
            forcaVales = EditorGUILayout.Slider("Forca dos Vales", forcaVales, 0f, 0.12f);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Pintura Automatica", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Prioridade de layers: 0 = grama, 1 = terra, 2 = rocha, 3 = neve opcional.", MessageType.None);
        forcaPinturaGrama = EditorGUILayout.Slider("Forca Grama", forcaPinturaGrama, 0f, 2f);
        forcaPinturaTerra = EditorGUILayout.Slider("Forca Terra", forcaPinturaTerra, 0f, 2f);
        forcaPinturaRocha = EditorGUILayout.Slider("Forca Rocha", forcaPinturaRocha, 0f, 2f);
        alturaMinimaRocha = EditorGUILayout.Slider("Altura Minima Rocha", alturaMinimaRocha, 0f, 1f);
        inclinacaoMinimaRocha = EditorGUILayout.Slider("Inclinacao Minima Rocha", inclinacaoMinimaRocha, 0f, 80f);
        usarNeve = EditorGUILayout.Toggle("Usar Neve", usarNeve);
        using (new EditorGUI.DisabledScope(!usarNeve))
        {
            alturaMinimaNeve = EditorGUILayout.Slider("Altura Minima Neve", alturaMinimaNeve, 0f, 1f);
            forcaPinturaNeve = EditorGUILayout.Slider("Forca Neve", forcaPinturaNeve, 0f, 2f);
        }

        EditorGUILayout.Space(12f);
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.65f);
        if (GUILayout.Button("Gerar Relevo e Pintura", GUILayout.Height(34f)))
            ConfirmarEGerar();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    private void ConfirmarEGerar()
    {
        NormalizarParametros();

        string mensagem =
            "Isto ajustara o Terrain da cena atual, gerando novo heightmap e nova pintura por Terrain Layers. " +
            "Nenhum prefab, arvore, pedra, inimigo ou objeto de cenario sera instanciado.";

        if (!EditorUtility.DisplayDialog("Gerar Cenario RPG", mensagem, "Gerar", "Cancelar"))
            return;

        GerarCenario();
    }

    private void GerarCenario()
    {
        Terrain terrain = ObterOuCriarTerrain();
        if (terrain == null)
        {
            EditorUtility.DisplayDialog("Gerar Cenario RPG", "Nao foi possivel localizar ou criar um Terrain.", "Ok");
            return;
        }

        Undo.RegisterCompleteObjectUndo(terrain.gameObject, "Gerar cenario RPG");
        TerrainData terrainData = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(terrainData, "Gerar relevo e pintura RPG");

        ConfigurarTerrain(terrain, terrainData);

        float[,] heights = GerarRelevo(terrainData);
        terrainData.SetHeights(0, 0, heights);
        PintarTerrain(terrainData, heights);

        EditorUtility.SetDirty(terrainData);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"[GerarCenarioRPG] Relevo e pintura gerados no Terrain '{terrain.name}'. Nenhum prefab foi instanciado.");
    }

    private Terrain ObterOuCriarTerrain()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
            terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();

        if (terrain != null)
            return terrain;

        TerrainData terrainData = CriarTerrainDataAsset();
        terrainData.heightmapResolution = resolucaoHeightmap;
        terrainData.alphamapResolution = resolucaoPintura;
        terrainData.size = new Vector3(larguraTerreno, alturaMaximaMontanhas, comprimentoTerreno);

        GameObject terrainObj = Terrain.CreateTerrainGameObject(terrainData);
        terrainObj.name = "Terrain";
        terrainObj.transform.position = new Vector3(-larguraTerreno * 0.5f, 0f, -comprimentoTerreno * 0.5f);
        Undo.RegisterCreatedObjectUndo(terrainObj, "Criar Terrain RPG");

        return terrainObj.GetComponent<Terrain>();
    }

    private TerrainData CriarTerrainDataAsset()
    {
        GarantirPasta("Assets/Generated");
        GarantirPasta("Assets/Generated/Terrain");

        TerrainData terrainData = new TerrainData();
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Generated/Terrain/Terrain_CenarioRPG.asset");
        AssetDatabase.CreateAsset(terrainData, path);
        AssetDatabase.SaveAssets();
        return terrainData;
    }

    private void ConfigurarTerrain(Terrain terrain, TerrainData terrainData)
    {
        if (terrainData.heightmapResolution != resolucaoHeightmap)
            terrainData.heightmapResolution = resolucaoHeightmap;

        if (terrainData.alphamapResolution != resolucaoPintura)
            terrainData.alphamapResolution = resolucaoPintura;

        terrainData.size = new Vector3(larguraTerreno, alturaMaximaMontanhas, comprimentoTerreno);
        terrain.heightmapPixelError = 4f;
        terrain.basemapDistance = 2600f;
        terrain.detailObjectDistance = 120f;
        terrain.treeDistance = 650f;

        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
        if (collider != null)
            collider.terrainData = terrainData;
    }

    private float[,] GerarRelevo(TerrainData terrainData)
    {
        int res = terrainData.heightmapResolution;
        float[,] heights = new float[res, res];
        List<ValeLocal> vales = usarVales ? CriarValesLocais(terrainData.size.x, terrainData.size.z) : new List<ValeLocal>();

        float largura = terrainData.size.x;
        float comprimento = terrainData.size.z;
        float seedOffsetA = seedAleatoria * 0.0173f;
        float seedOffsetB = seedAleatoria * 0.0297f;

        for (int z = 0; z < res; z++)
        {
            float nz = z / (res - 1f);
            float localZ = nz * comprimento - comprimento * 0.5f;

            for (int x = 0; x < res; x++)
            {
                float nx = x / (res - 1f);
                float localX = nx * largura - largura * 0.5f;
                float distanciaCentro = Mathf.Sqrt(localX * localX + localZ * localZ);
                float centro = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(raioAreaCentralLisa * 0.72f, raioAreaCentralLisa, distanciaCentro));
                float transicaoMontanha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(raioAreaCentralLisa, raioAreaCentralLisa + suavidadeTransicaoCentroMontanha, distanciaCentro));

                float ruidoCentro = FractalNoise(nx * 7.5f + seedOffsetA, nz * 7.5f + seedOffsetB, 3) - 0.5f;
                float ruidoPlanicie = FractalNoise(nx * 4.2f + seedOffsetB, nz * 4.2f + seedOffsetA, 4) - 0.48f;
                float planicie = 0.032f + ruidoCentro * ondulacaoCentro;
                float colinaLeve = Mathf.Max(0f, ruidoPlanicie) * 0.045f * (1f - centro);
                float cadeias = CalcularCadeiasLaterais(nx, nz, largura, comprimento);
                float valesLocais = CalcularValesLocais(localX, localZ, vales) * transicaoMontanha;

                float altura = planicie + colinaLeve;
                altura += cadeias * transicaoMontanha;
                altura -= valesLocais;
                altura = Mathf.Lerp(altura, 0.032f + ruidoCentro * ondulacaoCentro * 0.45f, centro * 0.86f);

                heights[z, x] = Mathf.Clamp01(altura);
            }
        }

        for (int i = 0; i < 4; i++)
            heights = SuavizarHeightmap(heights);

        ReforcarAreaCentralJogavel(heights, largura, comprimento);
        return heights;
    }

    private float CalcularCadeiasLaterais(float nx, float nz, float largura, float comprimento)
    {
        float altura = 0f;

        if (montanhaEsquerda)
            altura = Mathf.Max(altura, CalcularCadeiaLateral(nx * largura, nz, 0));

        if (montanhaDireita)
            altura = Mathf.Max(altura, CalcularCadeiaLateral((1f - nx) * largura, nz, 1));

        if (montanhaFundo)
            altura = Mathf.Max(altura, CalcularCadeiaLateral((1f - nz) * comprimento, nx, 2));

        if (montanhaFrente)
            altura = Mathf.Max(altura, CalcularCadeiaLateral(nz * comprimento, nx, 3));

        return Mathf.Clamp01(altura * intensidadeCadeiasLaterais);
    }

    private float CalcularCadeiaLateral(float distanciaBorda, float coordenadaLateral, int lado)
    {
        float influencia = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, suavidadeTransicaoCentroMontanha, distanciaBorda));
        if (influencia <= 0f)
            return 0f;

        float seedLado = seedAleatoria * 0.011f + lado * 31.7f;
        float ruidoGrande = FractalNoise(coordenadaLateral * 3.1f + seedLado, lado * 4.73f + seedLado, 5);
        float ruidoMedio = FractalNoise(coordenadaLateral * 9.0f + seedLado * 0.7f, lado * 13.1f + seedLado, 3);
        float ruidoFino = FractalNoise(coordenadaLateral * 24f + seedLado, lado * 19.3f + seedLado, 2);
        float irregularidade = Mathf.Lerp(0.72f, ruidoGrande * 0.9f + ruidoMedio * 0.45f, irregularidadeCadeiasLaterais);
        float baseBloqueio = Mathf.Pow(influencia, 2.25f) * 0.24f;
        float massaMontanha = Mathf.Pow(influencia, 1.28f) * (0.08f + irregularidade * 0.48f);
        float cristas = CalcularCristas(coordenadaLateral, distanciaBorda, lado);
        float rugosidade = (ruidoFino - 0.5f) * rugosidadeMontanha * influencia;

        return Mathf.Clamp01(baseBloqueio + massaMontanha + cristas + rugosidade);
    }

    private float CalcularCristas(float coordenadaLateral, float distanciaBorda, int lado)
    {
        float valor = 0f;
        float faixaBorda = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(suavidadeTransicaoCentroMontanha * 0.12f, suavidadeTransicaoCentroMontanha * 0.88f, distanciaBorda));

        for (int i = 0; i < quantidadeCristasMontanha; i++)
        {
            float centro = Hash01(lado * 1000 + i * 17 + 3);
            float larguraCrista = Mathf.Lerp(0.025f, 0.105f, Hash01(lado * 1000 + i * 23 + 7));
            float forca = Mathf.Lerp(0.45f, 1f, Hash01(lado * 1000 + i * 29 + 11));
            float d = Mathf.Abs(coordenadaLateral - centro);
            float crista = Mathf.Exp(-(d * d) / Mathf.Max(0.0001f, larguraCrista * larguraCrista));
            valor += crista * faixaBorda * forca * forcaCristasMontanha;
        }

        return valor;
    }

    private void PintarTerrain(TerrainData terrainData, float[,] heights)
    {
        TerrainLayer[] layers = terrainData.terrainLayers;
        if (layers == null || layers.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Terrain sem Layers",
                "O relevo foi gerado, mas o Terrain nao possui Terrain Layers. Adicione layers de grama, terra, rocha e opcionalmente neve para ativar a pintura automatica.",
                "Ok");
            return;
        }

        if (layers.Length < 3)
        {
            EditorUtility.DisplayDialog(
                "Terrain com poucas Layers",
                "O relevo foi gerado. Para pintura realista de montanha, configure pelo menos 3 Terrain Layers: 0 grama, 1 terra, 2 rocha. A ferramenta usara as layers disponiveis.",
                "Ok");
        }

        int layerGrama = 0;
        int layerTerra = Mathf.Min(1, layers.Length - 1);
        int layerRocha = Mathf.Min(2, layers.Length - 1);
        int layerNeve = usarNeve && layers.Length >= 4 ? 3 : -1;

        int width = terrainData.alphamapWidth;
        int height = terrainData.alphamapHeight;
        int layerCount = layers.Length;
        float[,,] alpha = new float[height, width, layerCount];

        for (int z = 0; z < height; z++)
        {
            float nz = z / (height - 1f);

            for (int x = 0; x < width; x++)
            {
                float nx = x / (width - 1f);
                float h = AmostrarAltura(heights, nx, nz);
                float slope = terrainData.GetSteepness(nx, nz);
                float rochaPorInclinacao = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inclinacaoMinimaRocha - 6f, inclinacaoMinimaRocha + 22f, slope));
                float rochaPorAltura = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(alturaMinimaRocha, alturaMinimaRocha + 0.32f, h));
                float transicaoTerra = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 26f, slope));

                float rocha = Mathf.Clamp01(Mathf.Max(rochaPorInclinacao, rochaPorAltura * 0.72f) * forcaPinturaRocha);
                float terra = Mathf.Clamp01((transicaoTerra * 0.65f + rochaPorAltura * 0.22f) * forcaPinturaTerra);
                float neve = layerNeve >= 0
                    ? Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(alturaMinimaNeve, alturaMinimaNeve + 0.12f, h)) * (1f - rochaPorInclinacao * 0.25f) * forcaPinturaNeve)
                    : 0f;
                float grama = Mathf.Clamp01((1f - rocha * 0.76f - terra * 0.42f - neve) * forcaPinturaGrama);

                float[] pesos = new float[layerCount];
                AdicionarPeso(pesos, layerGrama, grama);
                AdicionarPeso(pesos, layerTerra, terra);
                AdicionarPeso(pesos, layerRocha, rocha);
                AdicionarPeso(pesos, layerNeve, neve);
                NormalizarPesos(pesos);

                for (int layer = 0; layer < layerCount; layer++)
                    alpha[z, x, layer] = pesos[layer];
            }
        }

        terrainData.SetAlphamaps(0, 0, alpha);
    }

    private List<ValeLocal> CriarValesLocais(float largura, float comprimento)
    {
        List<ValeLocal> vales = new();
        System.Random random = new System.Random(seedAleatoria + 997);

        for (int i = 0; i < quantidadeVales; i++)
        {
            float x = RandomRange(random, -largura * 0.35f, largura * 0.35f);
            float z = RandomRange(random, -comprimento * 0.35f, comprimento * 0.35f);

            vales.Add(new ValeLocal
            {
                centro = new Vector2(x, z),
                raio = RandomRange(random, 90f, 190f),
                intensidade = RandomRange(random, 0.45f, 1f)
            });
        }

        return vales;
    }

    private float CalcularValesLocais(float localX, float localZ, List<ValeLocal> vales)
    {
        float valor = 0f;
        Vector2 ponto = new Vector2(localX, localZ);

        for (int i = 0; i < vales.Count; i++)
        {
            ValeLocal vale = vales[i];
            float dist = Vector2.Distance(ponto, vale.centro);
            float faixa = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, vale.raio, dist));
            valor += faixa * vale.intensidade * forcaVales;
        }

        return Mathf.Clamp01(valor);
    }

    private float[,] SuavizarHeightmap(float[,] origem)
    {
        int altura = origem.GetLength(0);
        int largura = origem.GetLength(1);
        float[,] destino = new float[altura, largura];

        for (int z = 0; z < altura; z++)
        {
            for (int x = 0; x < largura; x++)
            {
                float soma = 0f;
                int contagem = 0;

                for (int dz = -1; dz <= 1; dz++)
                {
                    int zz = Mathf.Clamp(z + dz, 0, altura - 1);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = Mathf.Clamp(x + dx, 0, largura - 1);
                        soma += origem[zz, xx];
                        contagem++;
                    }
                }

                destino[z, x] = soma / contagem;
            }
        }

        return destino;
    }

    private void ReforcarAreaCentralJogavel(float[,] heights, float larguraTerrenoAtual, float comprimentoTerrenoAtual)
    {
        int resZ = heights.GetLength(0);
        int resX = heights.GetLength(1);

        for (int z = 0; z < resZ; z++)
        {
            float nz = z / (resZ - 1f);
            float localZ = nz * comprimentoTerrenoAtual - comprimentoTerrenoAtual * 0.5f;

            for (int x = 0; x < resX; x++)
            {
                float nx = x / (resX - 1f);
                float localX = nx * larguraTerrenoAtual - larguraTerrenoAtual * 0.5f;
                float dist = Mathf.Sqrt(localX * localX + localZ * localZ);
                float centro = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(raioAreaCentralLisa * 0.72f, raioAreaCentralLisa, dist));

                if (centro <= 0f)
                    continue;

                float alturaAmigavel = 0.032f;
                heights[z, x] = Mathf.Lerp(heights[z, x], alturaAmigavel, centro * 0.9f);
            }
        }
    }

    private void AdicionarPeso(float[] pesos, int indice, float valor)
    {
        if (pesos == null || indice < 0 || indice >= pesos.Length || valor <= 0f)
            return;

        pesos[indice] += valor;
    }

    private void NormalizarPesos(float[] pesos)
    {
        float soma = 0f;
        for (int i = 0; i < pesos.Length; i++)
            soma += pesos[i];

        if (soma <= 0f)
        {
            if (pesos.Length > 0)
                pesos[0] = 1f;
            return;
        }

        for (int i = 0; i < pesos.Length; i++)
            pesos[i] /= soma;
    }

    private float AmostrarAltura(float[,] heights, float nx, float nz)
    {
        int maxZ = heights.GetLength(0) - 1;
        int maxX = heights.GetLength(1) - 1;
        int x = Mathf.Clamp(Mathf.RoundToInt(nx * maxX), 0, maxX);
        int z = Mathf.Clamp(Mathf.RoundToInt(nz * maxZ), 0, maxZ);
        return heights[z, x];
    }

    private float FractalNoise(float x, float y, int oitavas)
    {
        float valor = 0f;
        float amplitude = 0.5f;
        float frequencia = 1f;
        float somaAmplitude = 0f;

        for (int i = 0; i < oitavas; i++)
        {
            valor += Mathf.PerlinNoise(x * frequencia, y * frequencia) * amplitude;
            somaAmplitude += amplitude;
            amplitude *= 0.5f;
            frequencia *= 2f;
        }

        return somaAmplitude > 0f ? valor / somaAmplitude : 0f;
    }

    private float Hash01(int salt)
    {
        float v = Mathf.Sin((seedAleatoria + salt * 37.719f) * 12.9898f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    private float RandomRange(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    private void NormalizarParametros()
    {
        larguraTerreno = Mathf.Max(500f, larguraTerreno);
        comprimentoTerreno = Mathf.Max(500f, comprimentoTerreno);
        alturaMaximaMontanhas = Mathf.Max(40f, alturaMaximaMontanhas);
        raioAreaCentralLisa = Mathf.Clamp(raioAreaCentralLisa, 50f, Mathf.Min(larguraTerreno, comprimentoTerreno) * 0.45f);
        suavidadeTransicaoCentroMontanha = Mathf.Clamp(suavidadeTransicaoCentroMontanha, 100f, Mathf.Min(larguraTerreno, comprimentoTerreno));
        ondulacaoCentro = Mathf.Clamp(ondulacaoCentro, 0f, 0.08f);
        resolucaoHeightmap = ValidarResolucao(resolucaoHeightmap, 513);
        resolucaoPintura = Mathf.Clamp(resolucaoPintura, 32, 2048);
        intensidadeCadeiasLaterais = Mathf.Clamp(intensidadeCadeiasLaterais, 0f, 1.25f);
        irregularidadeCadeiasLaterais = Mathf.Clamp01(irregularidadeCadeiasLaterais);
        quantidadeCristasMontanha = Mathf.Clamp(quantidadeCristasMontanha, 0, 24);
        forcaCristasMontanha = Mathf.Clamp(forcaCristasMontanha, 0f, 0.6f);
        rugosidadeMontanha = Mathf.Clamp(rugosidadeMontanha, 0f, 0.22f);
        quantidadeVales = usarVales ? Mathf.Clamp(quantidadeVales, 0, 8) : 0;
        forcaVales = usarVales ? Mathf.Clamp(forcaVales, 0f, 0.12f) : 0f;
        forcaPinturaGrama = Mathf.Max(0f, forcaPinturaGrama);
        forcaPinturaTerra = Mathf.Max(0f, forcaPinturaTerra);
        forcaPinturaRocha = Mathf.Max(0f, forcaPinturaRocha);
        alturaMinimaRocha = Mathf.Clamp01(alturaMinimaRocha);
        inclinacaoMinimaRocha = Mathf.Clamp(inclinacaoMinimaRocha, 0f, 80f);
        alturaMinimaNeve = Mathf.Clamp01(alturaMinimaNeve);
        forcaPinturaNeve = Mathf.Max(0f, forcaPinturaNeve);
        gerarSomenteRelevoEPintura = true;
    }

    private int ValidarResolucao(int valor, int fallback)
    {
        return valor == 257 || valor == 513 || valor == 1025 ? valor : fallback;
    }

    private void GarantirPasta(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
        string nome = System.IO.Path.GetFileName(path);

        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(nome))
            return;

        if (!AssetDatabase.IsValidFolder(parent))
            GarantirPasta(parent);

        AssetDatabase.CreateFolder(parent, nome);
    }
}
