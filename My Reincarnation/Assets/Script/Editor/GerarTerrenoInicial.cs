using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Gera um terreno NOVO e uma cena NOVA ("Terreno Inicial"), sem alterar terrenos ou cenas existentes.
/// Layout (u = Leste, v = Norte, 0..1):
///  - Area aberta e plana da cidade inicial (sudoeste/centro-oeste).
///  - Montanhas no lado leste.
///  - Florestas abertas no norte (poucas arvores e clareiras para os monstros andarem).
///  - Mata padrao (densa) no sul e no oeste.
/// Sem grama: nao usa Terrain Layer de grama nem detalhes de grama.
/// Menu: Tools > My Reincarnation > Gerar Terreno Inicial (Novo)
/// </summary>
public static class GerarTerrenoInicial
{
    private const string PastaRaiz = "Assets/Terrenos";
    private const string PastaSaida = "Assets/Terrenos/TerrenoInicial";
    private const string PastaLayers = "Assets/ProceduralTerrainPainter/_Demo/Layers/";
    private const string PastaMeshesArvores = "Assets/Natureza/Arvores/Meshes/";

    private const float Largura = 1000f;
    private const float Comprimento = 1000f;
    private const float AlturaMaxima = 140f;
    private const int ResHeightmap = 513;
    private const int ResPintura = 512;
    private const int Seed = 20241112;

    // Limite de arvores pensado para Meta Quest 2 (ajuste conforme o teste no headset).
    private const int MaxArvores = 4500;
    private const float EspacamentoArvores = 6f;

    private static readonly Vector2 CentroCidade = new Vector2(0.30f, 0.35f);
    private const float RaioCidade = 0.15f;
    private const float TransicaoCidade = 0.09f;
    private const float AlturaCidade = 0.06f;

    private static readonly string[] ArvoresFbx =
    {
        "Tree_01.fbx", "Tree_02.fbx", "Tree_03.fbx", "Tree_04.fbx", "Tree_05.fbx"
    };

    [MenuItem("Tools/My Reincarnation/Gerar Terreno Inicial (Novo)")]
    public static void Gerar()
    {
        TerrainLayer camadaTerra = CarregarLayer("Dirt");
        if (camadaTerra == null)
        {
            EditorUtility.DisplayDialog("Terreno Inicial",
                "Nao encontrei " + PastaLayers + "Dirt.terrainlayer. Verifique a pasta ProceduralTerrainPainter.", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("Terreno Inicial", "Preparando pastas e arvores...", 0.05f);
            GarantirPasta(PastaRaiz);
            GarantirPasta(PastaSaida);

            // ---- Camadas de pintura (sem grama) ----
            TerrainLayer camadaCascalho = CarregarLayer("Gravel");
            TerrainLayer camadaRocha = CarregarLayer("Cliffs");
            TerrainLayer camadaNeve = CarregarLayer("Snow");

            List<TerrainLayer> camadas = new List<TerrainLayer> { camadaTerra };
            int idxTerra = 0;
            int idxCascalho = AdicionarCamada(camadas, camadaCascalho);
            int idxRocha = AdicionarCamada(camadas, camadaRocha);
            int idxNeve = AdicionarCamada(camadas, camadaNeve);

            // ---- Prototipos de arvores ----
            List<TreePrototype> prototipos = new List<TreePrototype>();
            foreach (string fbx in ArvoresFbx)
            {
                string nome = System.IO.Path.GetFileNameWithoutExtension(fbx);
                GameObject prefab = CriarPrefabArvore(PastaMeshesArvores + fbx, PastaSaida + "/" + nome + "_Terreno.prefab");
                if (prefab != null)
                {
                    prototipos.Add(new TreePrototype { prefab = prefab });
                }
            }

            // ---- Ruido ----
            System.Random rng = new System.Random(Seed);
            Vector2 oBase = NovoOffset(rng);
            Vector2 oMont = NovoOffset(rng);
            Vector2 oWarp = NovoOffset(rng);
            Vector2 oZona = NovoOffset(rng);
            Vector2 oClareira = NovoOffset(rng);
            Vector2 oPintura = NovoOffset(rng);

            // ---- TerrainData ----
            TerrainData td = new TerrainData();
            td.heightmapResolution = ResHeightmap;
            td.size = new Vector3(Largura, AlturaMaxima, Comprimento);
            td.baseMapResolution = 256;

            EditorUtility.DisplayProgressBar("Terreno Inicial", "Gerando relevo...", 0.2f);
            float[,] alturas = new float[ResHeightmap, ResHeightmap];
            for (int y = 0; y < ResHeightmap; y++)
            {
                float v = y / (float)(ResHeightmap - 1);
                for (int x = 0; x < ResHeightmap; x++)
                {
                    float u = x / (float)(ResHeightmap - 1);

                    float dx = u - CentroCidade.x;
                    float dz = v - CentroCidade.y;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    float pesoCidade = 1f - Suave(RaioCidade, RaioCidade + TransicaoCidade, dist);

                    float baseH = 0.07f + (Fbm(u * 4f, v * 4f, oBase, 4) - 0.5f) * 0.08f;

                    float warp = (Ruido(u * 3f, v * 3f, oWarp) - 0.5f) * 0.14f;
                    float m = Suave(0.60f, 0.90f, u + warp);
                    float cume = Cume(u * 5f, v * 5f, oMont) * 0.65f + Fbm(u * 14f, v * 14f, oMont, 3) * 0.35f;
                    float montanha = m * (0.20f + 0.80f * cume);

                    float borda = 1f - Suave(0f, 0.05f, Mathf.Min(u, Mathf.Min(v, 1f - v)));

                    float h = baseH + montanha * 0.85f + borda * 0.07f;
                    h = Mathf.Lerp(h, AlturaCidade, pesoCidade);
                    alturas[y, x] = Mathf.Clamp01(h);
                }
            }
            td.SetHeights(0, 0, alturas);

            // ---- Pintura ----
            EditorUtility.DisplayProgressBar("Terreno Inicial", "Pintando texturas...", 0.45f);
            td.terrainLayers = camadas.ToArray();
            td.alphamapResolution = ResPintura;
            int nCamadas = camadas.Count;
            float[,,] mapas = new float[ResPintura, ResPintura, nCamadas];
            for (int y = 0; y < ResPintura; y++)
            {
                float v = y / (float)(ResPintura - 1);
                for (int x = 0; x < ResPintura; x++)
                {
                    float u = x / (float)(ResPintura - 1);
                    float inclinacao = td.GetSteepness(u, v);
                    float alt = td.GetInterpolatedHeight(u, v) / AlturaMaxima;

                    float dx = u - CentroCidade.x;
                    float dz = v - CentroCidade.y;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    float variacao = Ruido(u * 40f, v * 40f, oPintura);

                    float rocha = idxRocha >= 0 ? Suave(30f, 44f, inclinacao) : 0f;
                    float neve = idxNeve >= 0 ? Suave(0.70f, 0.85f, alt) * (1f - rocha * 0.7f) : 0f;

                    float cascalho = 0f;
                    if (idxCascalho >= 0)
                    {
                        float cascalhoCidade = (1f - Suave(RaioCidade * 0.9f, RaioCidade + TransicaoCidade * 0.5f, dist)) * (0.6f + 0.4f * variacao);
                        float cascalhoMontanha = Suave(0.20f, 0.45f, alt) * 0.6f * variacao;
                        cascalho = Mathf.Max(cascalhoCidade, cascalhoMontanha) * (1f - rocha) * (1f - neve);
                    }

                    float terra = Mathf.Max(0f, 1f - rocha - neve - cascalho);
                    float soma = terra + rocha + neve + cascalho;
                    if (soma < 0.0001f)
                    {
                        terra = 1f;
                        soma = 1f;
                    }

                    mapas[y, x, idxTerra] = terra / soma;
                    if (idxCascalho >= 0) mapas[y, x, idxCascalho] = cascalho / soma;
                    if (idxRocha >= 0) mapas[y, x, idxRocha] = rocha / soma;
                    if (idxNeve >= 0) mapas[y, x, idxNeve] = neve / soma;
                }
            }
            td.SetAlphamaps(0, 0, mapas);

            // ---- Arvores ----
            EditorUtility.DisplayProgressBar("Terreno Inicial", "Plantando arvores...", 0.75f);
            int totalArvores = 0;
            if (prototipos.Count > 0)
            {
                td.treePrototypes = prototipos.ToArray();
                List<TreeInstance> arvores = new List<TreeInstance>();
                System.Random rngArvores = new System.Random(Seed + 1);

                for (float z = 0f; z < Comprimento; z += EspacamentoArvores)
                {
                    for (float x = 0f; x < Largura; x += EspacamentoArvores)
                    {
                        float px = x + (float)(rngArvores.NextDouble() - 0.5) * EspacamentoArvores * 0.9f;
                        float pz = z + (float)(rngArvores.NextDouble() - 0.5) * EspacamentoArvores * 0.9f;
                        float nu = Mathf.Clamp01(px / Largura);
                        float nv = Mathf.Clamp01(pz / Comprimento);

                        float alt = td.GetInterpolatedHeight(nu, nv) / AlturaMaxima;
                        float densidade = DensidadeArvores(nu, nv, alt, oZona, oClareira);
                        if (densidade <= 0f || rngArvores.NextDouble() > densidade) continue;
                        if (td.GetSteepness(nu, nv) > 32f) continue;

                        float escala = 0.85f + (float)rngArvores.NextDouble() * 0.4f;
                        TreeInstance t = new TreeInstance();
                        t.prototypeIndex = rngArvores.Next(prototipos.Count);
                        t.position = new Vector3(nu, 0f, nv);
                        t.widthScale = escala * (0.9f + (float)rngArvores.NextDouble() * 0.2f);
                        t.heightScale = escala;
                        t.rotation = (float)rngArvores.NextDouble() * Mathf.PI * 2f;
                        t.color = new Color32(255, 255, 255, 255);
                        t.lightmapColor = new Color32(255, 255, 255, 255);
                        arvores.Add(t);
                    }
                }

                while (arvores.Count > MaxArvores)
                {
                    int i = rngArvores.Next(arvores.Count);
                    arvores[i] = arvores[arvores.Count - 1];
                    arvores.RemoveAt(arvores.Count - 1);
                }

                td.SetTreeInstances(arvores.ToArray(), true);
                totalArvores = arvores.Count;
            }

            // ---- Salvar TerrainData ----
            EditorUtility.DisplayProgressBar("Terreno Inicial", "Salvando assets e cena...", 0.9f);
            string caminhoTd = AssetDatabase.GenerateUniqueAssetPath(PastaSaida + "/TerrainData_Inicial.asset");
            AssetDatabase.CreateAsset(td, caminhoTd);
            AssetDatabase.SaveAssets();

            // ---- Nova cena ----
            Scene cena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject goTerreno = Terrain.CreateTerrainGameObject(td);
            goTerreno.name = "Terreno Inicial";
            goTerreno.transform.position = new Vector3(-Largura * 0.5f, 0f, -Comprimento * 0.5f);

            Terrain terreno = goTerreno.GetComponent<Terrain>();
            terreno.treeDistance = 250f;
            terreno.treeBillboardDistance = 100f;
            terreno.treeCrossFadeLength = 30f;
            terreno.treeMaximumFullLODCount = 50;
            terreno.detailObjectDistance = 0f;
            terreno.heightmapPixelError = 10f;
            terreno.basemapDistance = 300f;
            terreno.drawInstanced = true;
            terreno.shadowCastingMode = ShadowCastingMode.Off;

            Vector3 centroMundo = new Vector3(
                goTerreno.transform.position.x + CentroCidade.x * Largura,
                0f,
                goTerreno.transform.position.z + CentroCidade.y * Comprimento);
            centroMundo.y = terreno.SampleHeight(centroMundo) + goTerreno.transform.position.y;

            GameObject centro = new GameObject("Centro da Cidade");
            centro.transform.position = centroMundo;
            GameObject spawn = new GameObject("Spawn Jogador (referencia)");
            spawn.transform.position = centroMundo + Vector3.up * 0.1f;

            string caminhoCena = AssetDatabase.GenerateUniqueAssetPath(PastaSaida + "/Terreno Inicial.unity");
            EditorSceneManager.SaveScene(cena, caminhoCena);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = goTerreno;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("[GerarTerrenoInicial] Concluido. Cena: " + caminhoCena + " | Arvores: " + totalArvores +
                      " | Camadas: " + nCamadas + " | Terreno: " + Largura + "x" + Comprimento + "m");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ------------------------------------------------------------------ zonas de arvores

    private static float DensidadeArvores(float u, float v, float alt01, Vector2 oZona, Vector2 oClareira)
    {
        float dx = u - CentroCidade.x;
        float dz = v - CentroCidade.y;
        float dist = Mathf.Sqrt(dx * dx + dz * dz);
        if (dist < RaioCidade + TransicaoCidade * 0.6f) return 0f; // cidade: area aberta

        float uw = u + (Ruido(u * 4f, v * 4f, oZona) - 0.5f) * 0.10f;
        float vw = v + (Ruido(u * 4f + 50f, v * 4f + 50f, oZona) - 0.5f) * 0.10f;
        float aglomerado = 0.6f + 0.8f * Ruido(u * 9f, v * 9f, oZona);

        if (uw > 0.60f) // sope das montanhas: poucas arvores, so em baixo
        {
            return 0.12f * (1f - Suave(0.25f, 0.50f, alt01)) * aglomerado;
        }
        if (vw < 0.22f || uw < 0.12f) // mata padrao (densa)
        {
            return Mathf.Clamp01(0.55f * aglomerado);
        }
        if (vw > 0.52f) // florestas abertas com clareiras
        {
            float clareira = Ruido(u * 6f, v * 6f, oClareira);
            if (clareira > 0.58f) return 0.015f;
            return 0.14f * aglomerado;
        }
        return 0.05f * aglomerado; // campos entre a cidade e as matas
    }

    // ------------------------------------------------------------------ utilitarios

    private static TerrainLayer CarregarLayer(string nome)
    {
        return AssetDatabase.LoadAssetAtPath<TerrainLayer>(PastaLayers + nome + ".terrainlayer");
    }

    private static int AdicionarCamada(List<TerrainLayer> lista, TerrainLayer camada)
    {
        if (camada == null) return -1;
        lista.Add(camada);
        return lista.Count - 1;
    }

    private static GameObject CriarPrefabArvore(string caminhoFbx, string destino)
    {
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(caminhoFbx);
        if (modelo == null)
        {
            Debug.LogWarning("[GerarTerrenoInicial] Modelo de arvore nao encontrado: " + caminhoFbx);
            return null;
        }

        GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        instancia.transform.position = Vector3.zero;
        instancia.transform.rotation = Quaternion.identity;
        instancia.transform.localScale = Vector3.one;

        Renderer[] renderers = instancia.GetComponentsInChildren<Renderer>();
        Bounds limites = new Bounds(Vector3.zero, Vector3.zero);
        bool primeiro = true;
        foreach (Renderer r in renderers)
        {
            if (primeiro)
            {
                limites = r.bounds;
                primeiro = false;
            }
            else
            {
                limites.Encapsulate(r.bounds);
            }
        }

        // Colisor apenas no tronco, para o jogador e os monstros nao atravessarem as arvores.
        float altura = Mathf.Max(limites.size.y * 0.6f, 1f);
        CapsuleCollider cap = instancia.AddComponent<CapsuleCollider>();
        cap.direction = 1;
        cap.radius = Mathf.Clamp(Mathf.Min(limites.size.x, limites.size.z) * 0.04f, 0.2f, 0.5f);
        cap.height = altura;
        cap.center = new Vector3(0f, limites.min.y + altura * 0.5f, 0f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instancia, destino);
        Object.DestroyImmediate(instancia);
        return prefab;
    }

    private static void GarantirPasta(string caminho)
    {
        if (AssetDatabase.IsValidFolder(caminho)) return;
        int ultima = caminho.LastIndexOf('/');
        string pai = caminho.Substring(0, ultima);
        string nome = caminho.Substring(ultima + 1);
        GarantirPasta(pai);
        AssetDatabase.CreateFolder(pai, nome);
    }

    private static Vector2 NovoOffset(System.Random rng)
    {
        return new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
    }

    private static float Suave(float a, float b, float x)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
    }

    private static float Ruido(float x, float y, Vector2 o)
    {
        return Mathf.PerlinNoise(x + o.x, y + o.y);
    }

    private static float Fbm(float x, float y, Vector2 o, int oitavas)
    {
        float soma = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int i = 0; i < oitavas; i++)
        {
            soma += Ruido(x * freq, y * freq, o) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return soma / norm;
    }

    private static float Cume(float x, float y, Vector2 o)
    {
        float soma = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int i = 0; i < 4; i++)
        {
            float n = 1f - Mathf.Abs(Ruido(x * freq, y * freq, o) * 2f - 1f);
            n *= n;
            soma += n * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return soma / norm;
    }
}
