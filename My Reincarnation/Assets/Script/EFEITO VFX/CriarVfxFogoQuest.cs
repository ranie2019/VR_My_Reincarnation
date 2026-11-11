#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Gera efeitos de fogo leves para Quest 2/3 (URP), sem assets externos:
//   Assets/VFX/FogoQuest/Texturas  (texturas geradas por codigo, 256 px)
//   Assets/VFX/FogoQuest/Materiais (URP Particles/Unlit: aditivo e alpha)
//   Assets/VFX/FogoQuest/BolaDeFogo_VFX.prefab   -> filho do projetil da Bola de Fogo
//   Assets/VFX/FogoQuest/ExplosaoFogo_VFX.prefab -> efeito de explosao (se destroi sozinho)
// Uso: Tools > My Reincarnation > Criar VFX Fogo (Quest)
// Orcamento: bola ~85 particulas no maximo, explosao ~50. Sem colisao, sem noise, sem luz, sem soft particles.
public static class CriarVfxFogoQuest
{
    private const string Pasta = "Assets/VFX/FogoQuest";
    private const string PastaTex = Pasta + "/Texturas";
    private const string PastaMat = Pasta + "/Materiais";

    // Cores do fogo
    private static readonly Color Branco = new Color(1f, 0.97f, 0.85f);
    private static readonly Color Amarelo = new Color(1f, 0.78f, 0.25f);
    private static readonly Color Laranja = new Color(1f, 0.42f, 0.08f);
    private static readonly Color Vermelho = new Color(0.75f, 0.1f, 0.02f);
    private static readonly Color Fumaca = new Color(0.16f, 0.14f, 0.13f);

    [MenuItem("Tools/My Reincarnation/Criar VFX Fogo (Quest)")]
    private static void Criar()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            EditorUtility.DisplayDialog("VFX Fogo", "Shader 'Universal Render Pipeline/Particles/Unlit' nao encontrado.", "OK");
            return;
        }

        CriarPasta("Assets", "VFX");
        CriarPasta("Assets/VFX", "FogoQuest");
        CriarPasta(Pasta, "Texturas");
        CriarPasta(Pasta, "Materiais");

        Texture2D texBrilho = SalvarTextura("Brilho", GerarBrilho(128));
        Texture2D texChama = SalvarTextura("Chama_2x2", GerarFolha(256, 0.85f, 1.6f, 11f));
        Texture2D texFumaca = SalvarTextura("Fumaca_2x2", GerarFolha(256, 0.55f, 1.1f, 47f));
        Texture2D texAnel = SalvarTextura("Anel", GerarAnel(128));

        Material matBrilho = SalvarMaterial("Fogo_Brilho_Aditivo", shader, texBrilho, true);
        Material matChama = SalvarMaterial("Fogo_Chama_Aditivo", shader, texChama, true);
        Material matFumaca = SalvarMaterial("Fogo_Fumaca_Alpha", shader, texFumaca, false);
        Material matAnel = SalvarMaterial("Fogo_Anel_Aditivo", shader, texAnel, true);

        CriarBolaDeFogo(matBrilho, matChama, matFumaca);
        CriarExplosao(matBrilho, matChama, matFumaca, matAnel);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Pasta + "/BolaDeFogo_VFX.prefab");
        Debug.Log("[VFX Fogo] Criado em " + Pasta);
    }

    // =================================================================
    // BOLA DE FOGO (fica como filho do projetil; ao mover deixa rastro)
    // =================================================================
    private static void CriarBolaDeFogo(Material brilho, Material chama, Material fumaca)
    {
        var raiz = new GameObject("BolaDeFogo_VFX");

        // Nucleo: brilho quente e pulsante no centro (espaco local, acompanha o cajado)
        ParticleSystem nucleo = Novo("Nucleo", raiz.transform, brilho);
        {
            var m = nucleo.main;
            m.loop = true; m.duration = 1f;
            m.startLifetime = 0.3f; m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.42f);
            m.startColor = Branco;
            m.simulationSpace = ParticleSystemSimulationSpace.Local;
            m.maxParticles = 10;
            var e = nucleo.emission; e.rateOverTime = 20f;
            var c = nucleo.colorOverLifetime; c.enabled = true;
            c.color = Gradiente((0f, Branco, 0f), (0.25f, Amarelo, 1f), (1f, Laranja, 0f));
            nucleo.GetComponent<ParticleSystemRenderer>().sortingFudge = -10f; // desenha por cima das chamas
        }

        // Chamas: lingua de fogo em volta; espaco do mundo -> rastro quando a bola anda
        ParticleSystem chamas = Novo("Chamas", raiz.transform, chama);
        {
            var m = chamas.main;
            m.loop = true; m.duration = 1f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.36f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = Color.white;
            m.gravityModifier = -0.15f; // sobe levemente
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 40;
            var e = chamas.emission; e.rateOverTime = 35f; e.rateOverDistance = 18f;
            var s = chamas.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.08f;
            var c = chamas.colorOverLifetime; c.enabled = true;
            c.color = Gradiente((0f, Amarelo, 0f), (0.15f, Amarelo, 0.9f), (0.5f, Laranja, 0.7f), (1f, Vermelho, 0f));
            var t = chamas.sizeOverLifetime; t.enabled = true;
            t.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.35f)));
            var r = chamas.rotationOverLifetime; r.enabled = true;
            r.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            Folha2x2(chamas);
        }

        // Brasas: pontinhos que soltam e sobem
        ParticleSystem brasas = Novo("Brasas", raiz.transform, brilho);
        {
            var m = brasas.main;
            m.loop = true; m.duration = 1f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
            m.startColor = new ParticleSystem.MinMaxGradient(Amarelo, Laranja);
            m.gravityModifier = -0.25f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 20;
            var e = brasas.emission; e.rateOverTime = 10f; e.rateOverDistance = 6f;
            var s = brasas.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.12f;
            var c = brasas.colorOverLifetime; c.enabled = true;
            c.color = Gradiente((0f, Amarelo, 1f), (0.6f, Laranja, 1f), (1f, Vermelho, 0f));
        }

        // Fumaca: pouca, escura, so no rastro
        ParticleSystem fum = Novo("Fumaca", raiz.transform, fumaca);
        {
            var m = fum.main;
            m.loop = true; m.duration = 1f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.08f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 15;
            var e = fum.emission; e.rateOverTime = 2f; e.rateOverDistance = 5f;
            var s = fum.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.1f;
            var c = fum.colorOverLifetime; c.enabled = true;
            c.color = Gradiente((0f, Fumaca, 0f), (0.25f, Fumaca, 0.35f), (1f, Fumaca, 0f));
            var t = fum.sizeOverLifetime; t.enabled = true;
            t.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 2f));
            Folha2x2(fum);
            fum.GetComponent<ParticleSystemRenderer>().sortingFudge = 10f; // atras do fogo
        }

        SalvarPrefab(raiz, Pasta + "/BolaDeFogo_VFX.prefab");
    }

    // =================================================================
    // EXPLOSAO (toca uma vez e se destroi em ~2.5 s)
    // =================================================================
    private static void CriarExplosao(Material brilho, Material chama, Material fumaca, Material anel)
    {
        var raiz = new GameObject("ExplosaoFogo_VFX");

        // Controle na raiz: sem particulas, so define o tempo de vida e destroi o objeto.
        ParticleSystem controle = raiz.AddComponent<ParticleSystem>();
        {
            var m = controle.main;
            m.loop = false; m.duration = 2.5f; m.playOnAwake = true;
            m.stopAction = ParticleSystemStopAction.Destroy;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var e = controle.emission; e.enabled = false;
            var s = controle.shape; s.enabled = false;
            controle.GetComponent<ParticleSystemRenderer>().enabled = false;
        }

        // Clarao: flash curto
        ParticleSystem clarao = Novo("Clarao", raiz.transform, brilho);
        {
            var m = UmaVez(clarao, 0.18f, 0.18f);
            m.startSize = 3f; m.startSpeed = 0f; m.startColor = Branco; m.maxParticles = 1;
            Rajada(clarao, 1);
            var c = clarao.colorOverLifetime; c.enabled = true;
            c.color = Gradiente((0f, Branco, 1f), (1f, Laranja, 0f));
            var t = clarao.sizeOverLifetime; t.enabled = true;
            t.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1f));
        }

        // Bola de fogo que expande
        ParticleSystem bola = Novo("BolaFogo", raiz.transform, chama);
        {
            var m = UmaVez(bola, 0.45f, 0.8f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.8f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.2f; m.maxParticles = 14;
            Rajada(bola, 12);
            var s = bola.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.25f;
            var v = bola.limitVelocityOverLifetime; v.enabled = true; v.limit = 0.3f; v.dampen = 0.15f; // freia: "bolo" de fogo
            var c = bola.colorOverLifetime; c.enabled = true;
            c.color = Gradiente((0f, Branco, 1f), (0.15f, Amarelo, 1f), (0.5f, Laranja, 0.8f), (1f, Vermelho, 0f));
            var t = bola.sizeOverLifetime; t.enabled = true;
            t.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.2f)));
            var r = bola.rotationOverLifetime; r.enabled = true; r.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            Folha2x2(bola);
        }

        // Faiscas: rapidas, esticadas pela velocidade, caem com gravidade
        ParticleSystem faiscas = Novo("Faiscas", raiz.transform, brilho);
        {
            var m = UmaVez(faiscas, 0.35f, 0.8f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            m.startColor = new ParticleSystem.MinMaxGradient(Amarelo, Laranja);
            m.gravityModifier = 1f; m.maxParticles = 25;
            Rajada(faiscas, 25);
            var s = faiscas.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Hemisphere; s.radius = 0.2f;
            s.rotation = new Vector3(-90f, 0f, 0f); // abre para cima
            var c = faiscas.colorOverLifetime; c.enabled = true;
            c.color = Gradiente((0f, Branco, 1f), (0.5f, Laranja, 1f), (1f, Vermelho, 0f));
            var rend = faiscas.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.06f; rend.lengthScale = 1f;
        }

        // Onda de choque: anel deitado no chao
        ParticleSystem onda = Novo("Onda", raiz.transform, anel);
        {
            var m = UmaVez(onda, 0.4f, 0.4f);
            m.startSize = 5f; m.startSpeed = 0f; m.startColor = Laranja; m.maxParticles = 1;
            Rajada(onda, 1);
            var c = onda.colorOverLifetime; c.enabled = true;
            c.color = Gradiente((0f, Amarelo, 1f), (1f, Vermelho, 0f));
            var t = onda.sizeOverLifetime; t.enabled = true;
            t.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.1f, 0f, 3f), new Keyframe(1f, 1f)));
            onda.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            onda.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        }

        // Fumaca: sobe devagar depois do fogo
        ParticleSystem fum = Novo("Fumaca", raiz.transform, fumaca);
        {
            var m = UmaVez(fum, 1.4f, 2.1f);
            m.startDelay = 0.12f;
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.06f; m.maxParticles = 8;
            Rajada(fum, 8);
            var s = fum.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.4f;
            var v = fum.limitVelocityOverLifetime; v.enabled = true; v.limit = 0.4f; v.dampen = 0.1f;
            var c = fum.colorOverLifetime; c.enabled = true;
            c.color = Gradiente((0f, Fumaca, 0f), (0.15f, Fumaca, 0.45f), (1f, Fumaca, 0f));
            var t = fum.sizeOverLifetime; t.enabled = true;
            t.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.8f));
            Folha2x2(fum);
            fum.GetComponent<ParticleSystemRenderer>().sortingFudge = 10f;
        }

        SalvarPrefab(raiz, Pasta + "/ExplosaoFogo_VFX.prefab");
    }

    // =================================================================
    // Particle System helpers
    // =================================================================
    private static ParticleSystem Novo(string nome, Transform pai, Material mat)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var m = ps.main;
        m.playOnAwake = true;
        m.scalingMode = ParticleSystemScalingMode.Hierarchy;   // escala junto com o objeto pai
        m.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
        m.startColor = Color.white;
        var s = ps.shape; s.enabled = false;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        r.maxParticleSize = 0.5f; // limita overdraw quando o efeito esta colado no rosto
        return ps;
    }

    private static ParticleSystem.MainModule UmaVez(ParticleSystem ps, float vidaMin, float vidaMax)
    {
        var m = ps.main;
        m.loop = false;
        m.duration = 0.5f;
        m.startLifetime = new ParticleSystem.MinMaxCurve(vidaMin, vidaMax);
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        return m;
    }

    private static void Rajada(ParticleSystem ps, short quantidade)
    {
        var e = ps.emission;
        e.rateOverTime = 0f;
        e.SetBursts(new[] { new ParticleSystem.Burst(0f, quantidade) });
    }

    private static void Folha2x2(ParticleSystem ps)
    {
        var f = ps.textureSheetAnimation;
        f.enabled = true;
        f.numTilesX = 2; f.numTilesY = 2;
        f.animation = ParticleSystemAnimationType.WholeSheet;
        f.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        f.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f); // quadro aleatorio, sem trocar (barato e sem "pulo")
    }

    private static ParticleSystem.MinMaxGradient Gradiente(params (float t, Color cor, float alfa)[] chaves)
    {
        var cores = new GradientColorKey[chaves.Length];
        var alfas = new GradientAlphaKey[chaves.Length];
        for (int i = 0; i < chaves.Length; i++)
        {
            cores[i] = new GradientColorKey(chaves[i].cor, chaves[i].t);
            alfas[i] = new GradientAlphaKey(chaves[i].alfa, chaves[i].t);
        }
        var g = new Gradient();
        g.SetKeys(cores, alfas);
        return new ParticleSystem.MinMaxGradient(g);
    }

    // =================================================================
    // Texturas geradas por codigo (branco + alfa; a cor vem das particulas)
    // =================================================================
    private static Color[] GerarBrilho(int t)
    {
        var px = new Color[t * t];
        for (int y = 0; y < t; y++)
            for (int x = 0; x < t; x++)
            {
                float r = Raio(x, y, t);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                px[y * t + x] = new Color(1f, 1f, 1f, a);
            }
        return px;
    }

    private static Color[] GerarAnel(int t)
    {
        var px = new Color[t * t];
        for (int y = 0; y < t; y++)
            for (int x = 0; x < t; x++)
            {
                float r = Raio(x, y, t);
                float d = (r - 0.78f) / 0.09f;
                float a = Mathf.Exp(-d * d) * Mathf.Clamp01((1f - r) * 8f);
                a += 0.12f * Mathf.Pow(Mathf.Clamp01(r / 0.78f), 3f) * (r < 0.78f ? 1f : 0f); // leve preenchimento interno
                px[y * t + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        return px;
    }

    // Folha 2x2: quatro "nuvens" de ruido com borda suave. contraste alto = chama, baixo = fumaca.
    private static Color[] GerarFolha(int t, float contraste, float escalaRuido, float semente)
    {
        var px = new Color[t * t];
        int q = t / 2;
        for (int y = 0; y < t; y++)
            for (int x = 0; x < t; x++)
            {
                int quadro = (y / q) * 2 + (x / q);
                int lx = x % q, ly = y % q;
                float r = Raio(lx, ly, q);
                float u = (lx / (float)q) * 2f - 1f, v = (ly / (float)q) * 2f - 1f;
                float o = semente + quadro * 17.3f;

                // Borda irregular: o raio e deformado por duas camadas de ruido (lingua de fogo / nuvem).
                float n = Fbm(u * escalaRuido * 2f + o, v * escalaRuido * 2f + o * 0.7f);
                float n2 = Fbm(u * escalaRuido * 5f + o * 1.3f, v * escalaRuido * 5f + o);
                float irregular = contraste * 1.3f;
                float rr = r + (n - 0.5f) * irregular + (n2 - 0.5f) * irregular * 0.4f;
                float a = Mathf.Clamp01((0.85f - rr) / 0.85f * (1f + contraste));
                a = a * a * (3f - 2f * a);
                a *= 0.55f + 0.9f * (n2 - 0.3f);           // detalhe interno
                a *= 0.6f + 0.4f * Mathf.Clamp01(1f - r);  // centro mais quente
                a = Mathf.Clamp01(a) * Mathf.Clamp01((1f - r) * 5f); // borda zerada (sem quadrado visivel)
                px[y * t + x] = new Color(1f, 1f, 1f, a);
            }
        return px;
    }

    private static float Raio(int x, int y, int t)
    {
        float u = ((x + 0.5f) / t) * 2f - 1f, v = ((y + 0.5f) / t) * 2f - 1f;
        return Mathf.Sqrt(u * u + v * v);
    }

    private static float Fbm(float x, float y)
    {
        float soma = 0f, amp = 0.5f, freq = 1f;
        for (int i = 0; i < 4; i++)
        {
            soma += amp * Mathf.PerlinNoise(x * freq + 31.7f * i, y * freq + 13.1f * i);
            freq *= 2f; amp *= 0.5f;
        }
        return soma / 0.9375f;
    }

    // =================================================================
    // Assets
    // =================================================================
    private static Texture2D SalvarTextura(string nome, Color[] px)
    {
        int t = Mathf.RoundToInt(Mathf.Sqrt(px.Length));
        var tex = new Texture2D(t, t, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        string caminho = PastaTex + "/" + nome + ".png";
        File.WriteAllBytes(caminho, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
        imp.textureType = TextureImporterType.Default;
        imp.alphaSource = TextureImporterAlphaSource.FromInput;
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.mipmapEnabled = true;
        imp.maxTextureSize = 256;
        imp.textureCompression = TextureImporterCompression.Compressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
    }

    private static Material SalvarMaterial(string nome, Shader shader, Texture2D tex, bool aditivo)
    {
        string caminho = PastaMat + "/" + nome + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, caminho);
        }
        mat.shader = shader;
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);

        // Transparente; aditivo para fogo/brilho, alpha para fumaca. Sem soft particles (caro no Quest).
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", aditivo ? 2f : 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", aditivo ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", aditivo ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_SoftParticlesEnabled", 0f);
        mat.SetFloat("_CameraFadingEnabled", 0f);
        mat.SetFloat("_DistortionEnabled", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;

        AjustarComEditorDoURP(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Pede ao editor do URP para recalcular keywords/blend (mesmo que mudar no Inspector). Se nao achar, os valores acima ja valem.
    private static void AjustarComEditorDoURP(Material mat)
    {
        try
        {
            System.Type gui = System.Type.GetType("UnityEditor.BaseShaderGUI, Unity.RenderPipelines.Universal.Editor");
            MethodInfo metodo = gui?.GetMethod("SetupMaterialBlendMode", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(Material) }, null);
            metodo?.Invoke(null, new object[] { mat });
        }
        catch (System.Exception erro) { Debug.LogWarning("[VFX Fogo] Ajuste automatico do material URP falhou: " + erro.Message); }
    }

    private static void SalvarPrefab(GameObject raiz, string caminho)
    {
        PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
        Object.DestroyImmediate(raiz);
    }

    private static void CriarPasta(string pai, string nome)
    {
        if (!AssetDatabase.IsValidFolder(pai + "/" + nome)) AssetDatabase.CreateFolder(pai, nome);
    }
}
#endif