using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// TopeiraIA  -  UM unico script, uma regiao por logica.
//
//   1. NUCLEO ............ tipos, componentes, ciclo de vida
//   2. VIDA E DANO ....... vida, nivel, barra, dano recebido (golpes e contato)
//   3. PATRULHA .......... pontos manuais/automaticos
//   4. CAMPO DE VISAO .... deteccao do jogador, obstaculos, memoria
//   5. MOVIMENTO ......... fisica: andar no chao, desvio de buraco, rota bloqueada
//   6. ATAQUE ............ ataque + combate + investida (tudo junto)
//   7. MORTE E RESPAWN ... recompensas (XP, REIN, missao, drops) e respawn
//   8. ANIMACAO / AUDIO
//   9. MAQUINA DE ESTADOS  (Update)
//  10. DEBUG (gizmos)
// =====================================================================
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(ExperienciaInimigo))]
public class TopeiraIA : MonoBehaviour, IDano
{
    #region 1. NUCLEO
    protected enum Estado { Parado, Patrulhando, Observando, Alerta, Perseguindo, Atacando, TomandoDano, Morto }

    private Estado estadoAtual = Estado.Parado;
    private Estado estadoAntesDano = Estado.Parado;
    private bool morto;
    private bool comportamentoIniciado;
    private float fimReacaoDano;
    private ExperienciaInimigo experienciaInimigo;

    public bool Morto => morto;

    private void Reset() => AjustarCapsule();

    private void Awake()
    {
        experienciaInimigo = GetComponent<ExperienciaInimigo>();
        ConfigurarCorpo();
        ConfigurarAnimator();
        ConfigurarAudio();
        ConfigurarUIVida();

        vidaAtual = vidaAtual <= 0 ? vidaMaxima : Mathf.Clamp(vidaAtual, 1, vidaMaxima);
        AtualizarUIVida();
    }

    private void Start() => IniciarComportamento();

    private void OnDisable()
    {
        CancelarAtaque();
        PararMovimento();
        PararSomAndar();
        EsquecerAlvo();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < pontosAutomaticos.Count; i++)
            if (pontosAutomaticos[i] != null) Destroy(pontosAutomaticos[i]);
    }

    private void OnValidate()
    {
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        nivelMonstro = Mathf.Max(1, nivelMonstro);
        raioCampoVisao = Mathf.Max(0f, raioCampoVisao);
        distanciaIniciarPerseguicao = Mathf.Clamp(distanciaIniciarPerseguicao, 0f, raioCampoVisao);
        alcanceAtaque = Mathf.Max(0.1f, alcanceAtaque);
        duracaoAnimacaoAtaque = Mathf.Max(atrasoInvestida + duracaoInvestida, duracaoAnimacaoAtaque);
        NormalizarDrops();
    }

    private void Update()
    {
        if (morto) return;
        AtualizarDeteccao();
        AtualizarEstado();
    }

    private void FixedUpdate() => AtualizarMovimentoFisico();
    #endregion

    #region 2. VIDA E DANO
    [SerializeField, Min(1)] private int vidaMaxima = 10;
    [SerializeField] private int vidaAtual = 10;
    [Tooltip("Apenas exibicao na barra de vida (nao altera atributos).")]
    [SerializeField, Min(1)] private int nivelMonstro = 1;
    [Tooltip("Tempo parada reagindo ao dano (clip Dano dura ~0.8 s).")]
    [SerializeField, Min(0f)] private float tempoAnimacaoDano = 0.8f;
    [SerializeField, Min(0f)] private float tempoParaMorrer = 3f;

    [SerializeField] private Image imagemBarraVida;
    [SerializeField] private Canvas canvasVida;
    [Tooltip("Texto do nivel (TMP). Se vazio, procura um TMP chamado Nivel/Level dentro do canvas.")]
    [SerializeField] private TMP_Text textoNivel;
    [SerializeField] private bool buscarBarraVidaAutomaticamente = true;
    [SerializeField] private bool esconderCanvasAoMorrer = true;

    private Transform ultimoPlayerResponsavel;

    public int VidaAtual => vidaAtual;
    public int VidaMaxima => vidaMaxima;
    public int NivelMonstro => nivelMonstro;

    public void DefinirNivel(int novoNivel)
    {
        nivelMonstro = Mathf.Max(1, novoNivel);
        AtualizarUIVida();
    }

    // ---- UI ----
    private void ConfigurarUIVida()
    {
        if (canvasVida == null) canvasVida = GetComponentInChildren<Canvas>(true);
        if (!buscarBarraVidaAutomaticamente) return;

        if (imagemBarraVida == null)
        {
            foreach (Image imagem in GetComponentsInChildren<Image>(true))
                if (imagem.name == "Vida Frente") { imagemBarraVida = imagem; break; }
        }

        if (textoNivel == null)
        {
            foreach (TMP_Text texto in GetComponentsInChildren<TMP_Text>(true))
            {
                string nome = texto.name.ToLowerInvariant();
                if (nome.Contains("nivel") || nome.Contains("level")) { textoNivel = texto; break; }
            }
        }
    }

    private void AtualizarUIVida()
    {
        if (imagemBarraVida != null)
            imagemBarraVida.fillAmount = Mathf.Clamp01((float)vidaAtual / Mathf.Max(1, vidaMaxima));
        if (textoNivel != null)
            textoNivel.text = nivelMonstro.ToString(CultureInfo.InvariantCulture);
    }

    // ---- Entrada de dano (usada por Espada, Flecha, Fogo, Machado, Picareta via ReceptorDanoInimigo) ----
    public void ReceberDano(int dano) => ReceberDano(dano, (GameObject)null);

    public void ReceberDano(int dano, Transform origemDano) =>
        ReceberDano(dano, origemDano != null ? origemDano.gameObject : null);

    public void ReceberDano(int dano, GameObject origemDano)
    {
        if (morto || dano <= 0) return;
        RegistrarPrimeiroAtacante(origemDano);
        AplicarDanoRecebido(dano, IdentificarPlayerResponsavel(origemDano != null ? origemDano.transform : null));
    }

    public void AcionarDano() => ReceberDano(1);
    public void AcionarMorte() => Morrer();

    private void AplicarDanoRecebido(int dano, Transform playerResponsavel)
    {
        if (morto || dano <= 0) return;

        if (playerResponsavel != null) ultimoPlayerResponsavel = playerResponsavel;

        vidaAtual = Mathf.Max(0, vidaAtual - dano);
        AtualizarUIVida();
        TocarSomUmaVez(somDano, volumeDano);

        if (vidaAtual <= 0) { Morrer(); return; }

        CancelarAtaque();
        PararMovimento();
        if (estadoAtual != Estado.TomandoDano)
            estadoAntesDano = estadoAtual == Estado.Atacando ? Estado.Perseguindo : estadoAtual;

        fimReacaoDano = Time.time + tempoAnimacaoDano;
        MudarEstado(Estado.TomandoDano);
    }

    private void RegistrarPrimeiroAtacante(GameObject origemDano)
    {
        if (origemDano == null) return;
        if (experienciaInimigo == null) experienciaInimigo = GetComponent<ExperienciaInimigo>();
        if (experienciaInimigo != null) experienciaInimigo.RegistrarPrimeiroAtacante(origemDano);
    }

    // Quem recebe o credito do golpe: o Player na hierarquia da origem; senao o alvo atual.
    private Transform IdentificarPlayerResponsavel(Transform origem)
    {
        if (origem != null && !string.IsNullOrWhiteSpace(tagPlayer))
            for (Transform t = origem; t != null; t = t.parent)
                if (string.Equals(t.tag, tagPlayer, StringComparison.Ordinal)) return t;
        return alvoPlayer;
    }
    #endregion

    #region 3. PATRULHA
    [Tooltip("Pontos manuais. Se vazio e 'Gerar Pontos Automaticamente' ligado, cria pontos em volta.")]
    [SerializeField] private Transform[] pontosPatrulha;
    [SerializeField, Min(0f)] private float velocidadePatrulha = 1.2f;
    [SerializeField, Min(0.05f)] private float distanciaChegadaPonto = 0.4f;
    [SerializeField, Min(0f)] private float tempoObservando = 2f;
    [SerializeField] private bool gerarPontosAutomaticamente = true;
    [SerializeField, Min(1)] private int quantidadePontosAuto = 4;
    [SerializeField, Min(0.5f)] private float raioPatrulhaAuto = 4f;

    private int indicePatrulha = -1;
    private float tempoRestanteObservando;
    private readonly List<GameObject> pontosAutomaticos = new List<GameObject>();

    public Transform[] ObterPontosPatrulha() => pontosPatrulha;

    public void DefinirPontosPatrulha(Transform[] novosPontos)
    {
        if (novosPontos == null || novosPontos.Length == 0) return;
        pontosPatrulha = novosPontos;
        indicePatrulha = -1;
    }

    private void GerarPontosAutomaticos()
    {
        var validos = new List<Transform>(quantidadePontosAuto);
        for (int i = 0; i < quantidadePontosAuto; i++)
        {
            float angulo = i * (360f / quantidadePontosAuto) * Mathf.Deg2Rad;
            Vector3 desejado = transform.position + new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * raioPatrulhaAuto;

            // Sem chao nesse ponto: descarta, em vez de patrulhar no ar ou em parede.
            if (!TentarProjetarNoChao(desejado, out Vector3 noChao)) continue;

            var ponto = new GameObject("PontoAuto_" + name + "_" + i) { hideFlags = HideFlags.HideInHierarchy };
            ponto.transform.position = noChao;
            pontosAutomaticos.Add(ponto);
            validos.Add(ponto.transform);
        }

        if (validos.Count == 0) return;
        pontosPatrulha = validos.ToArray();
        indicePatrulha = -1;
    }

    private int QuantidadePontosValidos()
    {
        if (pontosPatrulha == null) return 0;
        int n = 0;
        for (int i = 0; i < pontosPatrulha.Length; i++)
            if (pontosPatrulha[i] != null) n++;
        return n;
    }

    private void EscolherProximoPonto()
    {
        int validos = QuantidadePontosValidos();
        if (validos == 0) { indicePatrulha = -1; return; }

        int escolhido = -1;
        for (int tentativa = 0; tentativa < 16 && escolhido < 0; tentativa++)
        {
            int candidato = UnityEngine.Random.Range(0, pontosPatrulha.Length);
            if (pontosPatrulha[candidato] != null && (validos == 1 || candidato != indicePatrulha))
                escolhido = candidato;
        }
        for (int i = 0; i < pontosPatrulha.Length && escolhido < 0; i++)
            if (pontosPatrulha[i] != null) escolhido = i;

        indicePatrulha = escolhido;
    }

    private Transform ObterPontoAtual()
    {
        if (pontosPatrulha == null || pontosPatrulha.Length == 0) return null;
        bool invalido = indicePatrulha < 0 || indicePatrulha >= pontosPatrulha.Length || pontosPatrulha[indicePatrulha] == null;
        if (invalido) EscolherProximoPonto();
        return indicePatrulha >= 0 && indicePatrulha < pontosPatrulha.Length ? pontosPatrulha[indicePatrulha] : null;
    }

    private void IniciarObservacao()
    {
        PararMovimento();
        tempoRestanteObservando = tempoObservando;
        EscolherProximoPonto();
        MudarEstado(Estado.Observando);
    }

    private void VoltarParaPatrulha() =>
        MudarEstado(QuantidadePontosValidos() > 0 ? Estado.Patrulhando : Estado.Parado);
    #endregion

    #region 4. CAMPO DE VISAO
    [SerializeField] private string tagPlayer = "Player";
    [SerializeField, Min(0f)] private float raioCampoVisao = 15f;
    [SerializeField, Range(1f, 360f)] private float anguloCampoVisao = 210f;
    [Tooltip("Camadas que bloqueiam a visao (padrao: todas menos Ignore Raycast).")]
    [SerializeField] private LayerMask layersObstaculo = ~(1 << 2);
    [Tooltip("Dentro do campo de visao, so comeca a perseguir abaixo desta distancia.")]
    [SerializeField, Min(0f)] private float distanciaIniciarPerseguicao = 10f;
    [Tooltip("Continua indo ate onde viu o jogador por este tempo depois de perde-lo de vista.")]
    [SerializeField, Min(0f)] private float tempoMemoriaAlvo = 1.5f;

    private const float IntervaloBuscaPlayer = 0.25f;
    private Transform alvoPlayer;
    private bool alvoVisivel;
    private bool podeIniciarPerseguicao;
    private float ultimaVezVisto = float.NegativeInfinity;
    private float proximaBuscaPlayer;
    private readonly RaycastHit[] hitsVisao = new RaycastHit[32];
    private Transform raizEmCache;
    private Transform corpoEmCache;

    private bool PercebeAlvo => alvoPlayer != null && (alvoVisivel || Time.time - ultimaVezVisto <= tempoMemoriaAlvo);
    private Vector3 PosicaoAlvo => alvoPlayer != null ? PosicaoJogador(alvoPlayer) : transform.position;

    private Vector3 Olhos => pontoOlhos != null ? pontoOlhos.position : transform.position + Vector3.up * alturaOlhos;

    private void EsquecerAlvo()
    {
        alvoPlayer = null;
        alvoVisivel = false;
        podeIniciarPerseguicao = false;
        ultimaVezVisto = float.NegativeInfinity;
    }

    private void AtualizarDeteccao()
    {
        if (Time.time < proximaBuscaPlayer) return;
        proximaBuscaPlayer = Time.time + IntervaloBuscaPlayer;

        if (alvoPlayer != null && !alvoPlayer.gameObject.activeInHierarchy) EsquecerAlvo();
        if (alvoPlayer == null) alvoPlayer = ProcurarJogadorVisivel();

        float distancia = 0f;
        alvoVisivel = alvoPlayer != null && Visivel(alvoPlayer, out distancia);
        podeIniciarPerseguicao = alvoVisivel && distancia <= distanciaIniciarPerseguicao;

        if (alvoVisivel) ultimaVezVisto = Time.time;
        else if (alvoPlayer != null && !PercebeAlvo) EsquecerAlvo();
    }

    private Transform ProcurarJogadorVisivel()
    {
        GameObject[] candidatos = ObterJogadores(tagPlayer);
        Transform melhor = null;
        float menor = float.PositiveInfinity;

        for (int i = 0; i < candidatos.Length; i++)
        {
            GameObject c = candidatos[i];
            if (c == null || !c.activeInHierarchy) continue;
            float d = (PosicaoJogador(c.transform) - transform.position).sqrMagnitude;
            if (d < menor && Visivel(c.transform, out _)) { menor = d; melhor = c.transform; }
        }
        return melhor;
    }

    private bool Visivel(Transform raiz, out float distancia)
    {
        Vector3 origem = Olhos;
        Vector3 dir = PosicaoJogador(raiz) - origem;
        distancia = dir.magnitude;
        if (distancia > raioCampoVisao) return false;

        Vector3 plano = dir; plano.y = 0f;
        Vector3 frente = transform.forward; frente.y = 0f;
        if (plano.sqrMagnitude > 0.0001f && frente.sqrMagnitude > 0.0001f &&
            Vector3.Angle(frente, plano) > anguloCampoVisao * 0.5f) return false;

        if (distancia < 0.0001f) return true;
        return !ObstaculoNoCaminho(origem, dir / distancia, distancia, raiz);
    }

    private bool ObstaculoNoCaminho(Vector3 origem, Vector3 direcao, float distancia, Transform raizAlvo)
    {
        int n = Physics.RaycastNonAlloc(origem, direcao, hitsVisao, distancia, layersObstaculo, QueryTriggerInteraction.Ignore);
        if (n == hitsVisao.Length) return true; // resultado incompleto: nao arriscar

        for (int i = 0; i < n; i++)
        {
            Transform t = hitsVisao[i].collider.transform;
            if (t == transform || t.IsChildOf(transform)) continue;  // proprio corpo
            if (t == raizAlvo || t.IsChildOf(raizAlvo)) continue;    // o proprio alvo
            return true;
        }
        return false;
    }

    // Posicao do corpo rastreado em VR (AvatarBody -> camera -> raiz).
    private Vector3 PosicaoJogador(Transform raiz)
    {
        if (raiz == null) return transform.position;
        if (raiz != raizEmCache || corpoEmCache == null)
        {
            raizEmCache = raiz;
            AvatarBody avatar = raiz.GetComponent<AvatarBody>();
            corpoEmCache = avatar != null ? avatar.corpoAvatar : null;
            if (corpoEmCache == null)
            {
                Camera cam = raiz.GetComponentInChildren<Camera>();
                corpoEmCache = cam != null ? cam.transform : raiz;
            }
        }
        return corpoEmCache.position;
    }

    // Cache compartilhado: varios monstros nao fazem FindGameObjectsWithTag no mesmo frame.
    private sealed class CacheJogadores { public GameObject[] itens; public float proximaConsulta; }
    private static readonly Dictionary<string, CacheJogadores> cachePorTag = new Dictionary<string, CacheJogadores>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparCacheJogadores() => cachePorTag.Clear();

    private static GameObject[] ObterJogadores(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return Array.Empty<GameObject>();

        if (!cachePorTag.TryGetValue(tag, out CacheJogadores cache))
        {
            cache = new CacheJogadores();
            cachePorTag.Add(tag, cache);
        }
        if (cache.itens == null || Time.time >= cache.proximaConsulta)
        {
            try { cache.itens = GameObject.FindGameObjectsWithTag(tag); }
            catch (UnityException) { cache.itens = Array.Empty<GameObject>(); } // tag nao existe
            cache.proximaConsulta = Time.time + 0.75f;
        }
        return cache.itens;
    }
    #endregion

    #region 5. MOVIMENTO (fisica)
    [SerializeField] private Rigidbody rb;
    [Tooltip("Ajusta a capsula pelos Renderers filhos ao iniciar o jogo.")]
    [SerializeField] private bool ajustarCapsuleAoModelo = true;
    [SerializeField, Min(0f)] private float velocidadeRotacao = 8f;
    [SerializeField] private LayerMask camadasSolo = ~0;
    [SerializeField, Range(1f, 60f)] private float inclinacaoMaxima = 45f;
    [SerializeField, Min(0.05f)] private float alturaSondaSolo = 0.3f;
    [SerializeField, Min(0.01f)] private float toleranciaSolo = 0.15f;
    [SerializeField, Min(0.05f)] private float quedaMaxima = 0.6f;
    [SerializeField, Min(0.1f)] private float distanciaPassoSeguro = 0.5f;
    [Tooltip("Sem deslocamento por este tempo enquanto tenta andar = rota bloqueada.")]
    [SerializeField, Min(0.1f)] private float tempoRotaBloqueada = 2f;
    [SerializeField, Min(0.005f)] private float deslocamentoMinimo = 0.02f;
    [SerializeField, Min(0.01f)] private float distanciaParada = 0.05f;

    private CapsuleCollider capsula;
    private readonly RaycastHit[] hitsSolo = new RaycastHit[16];
    private Vector3 destinoMovimento;
    private float velocidadeMovimento;
    private bool movendo;
    private float yawAlvo;
    private Vector3 ultimaPosicaoComProgresso;
    private float tempoSemProgresso;

    private bool RotaBloqueada => tempoSemProgresso >= tempoRotaBloqueada;

    private void ConfigurarCorpo()
    {
        rb = rb != null ? rb : GetComponent<Rigidbody>();
        capsula = GetComponent<CapsuleCollider>();
        if (ajustarCapsuleAoModelo) AjustarCapsule();

        capsula.direction = 1;
        rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        yawAlvo = transform.eulerAngles.y;
    }

    private void MoverPara(Vector3 destino, float velocidade)
    {
        if (!movendo)
        {
            ultimaPosicaoComProgresso = rb.position;
            tempoSemProgresso = 0f;
        }
        destinoMovimento = destino;
        velocidadeMovimento = Mathf.Max(0f, velocidade);
        movendo = velocidadeMovimento > 0f;
        OlharPara(destino);
    }

    private void PararMovimento()
    {
        movendo = false;
        tempoSemProgresso = 0f;
    }

    private void OlharPara(Vector3 alvo)
    {
        Vector3 dir = alvo - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) yawAlvo = Quaternion.LookRotation(dir).eulerAngles.y;
    }

    private float DistanciaXZ(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void AtualizarMovimentoFisico()
    {
        if (rb == null || rb.isKinematic || morto || emInvestida) return;

        rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.Euler(0f, yawAlvo, 0f), velocidadeRotacao * Time.fixedDeltaTime));

        Vector3 pe = PontoNoPe();
        bool noSolo = ProcurarSolo(pe + Vector3.up * alturaSondaSolo, alturaSondaSolo + toleranciaSolo, out RaycastHit solo);
        Vector3 normal = noSolo ? solo.normal : Vector3.up;

        Vector3 horizontal = Vector3.zero;
        if (movendo && noSolo)
        {
            Vector3 dir = destinoMovimento - rb.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > distanciaParada * distanciaParada)
            {
                dir.Normalize();
                if (!PassoSeguro(pe, dir)) dir = EscolherDesvio(pe, dir);
                Vector3 tangente = Vector3.ProjectOnPlane(dir, normal);
                if (tangente.sqrMagnitude > 0.0001f) horizontal = tangente.normalized * velocidadeMovimento;
            }
        }

        AtualizarProgresso();

        // Sem contato com o solo, a gravidade continua sendo a unica aceleracao vertical.
        // Subindo ladeira a propria velocidade vertical comandada passa de 0,5: nao conta como "no ar".
        bool apoiado = noSolo && rb.linearVelocity.y <= Mathf.Max(0.5f, horizontal.y + 0.5f);
        float vertical = apoiado ? horizontal.y : rb.linearVelocity.y;
        rb.linearVelocity = new Vector3(horizontal.x, vertical, horizontal.z);
    }

    private bool PassoSeguro(Vector3 pe, Vector3 dir)
    {
        // Em subida o chao a frente fica ACIMA do pe: a sonda parte alto o bastante para a inclinacao maxima,
        // senao ela nasce dentro do terreno e a ladeira parece um abismo.
        float subidaMaxima = distanciaPassoSeguro * Mathf.Tan(inclinacaoMaxima * Mathf.Deg2Rad);
        Vector3 origem = pe + dir * distanciaPassoSeguro + Vector3.up * (alturaSondaSolo + subidaMaxima);
        return ProcurarSolo(origem, alturaSondaSolo + subidaMaxima + quedaMaxima, out _);
    }

    private Vector3 EscolherDesvio(Vector3 pe, Vector3 dir)
    {
        Vector3 esquerda = Quaternion.Euler(0f, -55f, 0f) * dir;
        if (PassoSeguro(pe, esquerda)) return esquerda;
        Vector3 direita = Quaternion.Euler(0f, 55f, 0f) * dir;
        return PassoSeguro(pe, direita) ? direita : Vector3.zero;
    }

    private void AtualizarProgresso()
    {
        if (!movendo) { tempoSemProgresso = 0f; return; }

        Vector3 moveu = rb.position - ultimaPosicaoComProgresso;
        moveu.y = 0f;
        if (moveu.sqrMagnitude >= deslocamentoMinimo * deslocamentoMinimo)
        {
            ultimaPosicaoComProgresso = rb.position;
            tempoSemProgresso = 0f;
            return;
        }

        Vector3 ate = destinoMovimento - rb.position;
        ate.y = 0f;
        // Parada por ter chegado nao conta como bloqueio.
        tempoSemProgresso = ate.sqrMagnitude <= distanciaParada * distanciaParada ? 0f : tempoSemProgresso + Time.fixedDeltaTime;
    }

    private Vector3 PontoNoPe()
    {
        Bounds b = capsula.bounds;
        return new Vector3(b.center.x, b.min.y, b.center.z);
    }

    private bool ProcurarSolo(Vector3 origem, float profundidade, out RaycastHit solo)
    {
        solo = default;
        int n = Physics.RaycastNonAlloc(origem, Vector3.down, hitsSolo, profundidade, camadasSolo, QueryTriggerInteraction.Ignore);
        if (n == hitsSolo.Length) return false; // resultado incompleto: nao arriscar

        float menor = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = hitsSolo[i];
            Transform t = h.collider.transform;
            if (t == transform || t.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<StatusPlayer>() != null) continue;
            if (h.distance >= menor) continue;
            menor = h.distance;
            solo = h;
        }
        return solo.collider != null && solo.normal.y >= Mathf.Cos(inclinacaoMaxima * Mathf.Deg2Rad);
    }

    private bool TentarProjetarNoChao(Vector3 ponto, out Vector3 noChao)
    {
        noChao = ponto;
        if (!ProcurarSolo(ponto + Vector3.up * 2f, 4f, out RaycastHit solo)) return false;
        noChao = solo.point;
        return true;
    }

    private void AjustarCapsule()
    {
        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (cap == null || renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        Vector3 escala = transform.lossyScale;
        float escalaXZ = Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(escala.x), Mathf.Abs(escala.z)));
        float escalaY = Mathf.Max(0.001f, Mathf.Abs(escala.y));
        float raio = Mathf.Max(0.05f, Mathf.Min(b.extents.x, b.extents.z) * 0.9f);
        float altura = Mathf.Max(raio * 2f, b.size.y);

        cap.direction = 1;
        cap.radius = raio / escalaXZ;
        cap.height = altura / escalaY;
        cap.center = transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + altura * 0.5f, b.center.z));
    }
    #endregion

    #region 6. ATAQUE (ataque + combate + investida)
    [SerializeField, Min(0f)] private float velocidadePerseguicao = 2.4f;
    [Tooltip("Distancia (plana) ao alvo em que comeca o ataque.")]
    [SerializeField, Min(0.1f)] private float distanciaAtaque = 1.3f;

    [SerializeField, Min(0)] private int danoAtaque = 4;
    [Tooltip("Origem do golpe. Se vazio, usa a posicao da Topeira + Altura Origem Ataque.")]
    [SerializeField] private Transform pontoOrigemAtaque;
    [SerializeField] private float alturaOrigemAtaque = 0.5f;
    [SerializeField, Min(0.05f)] private float raioAtaque = 0.5f;
    [SerializeField, Min(0.1f)] private float alcanceAtaque = 1.8f;
    [SerializeField] private LayerMask layersAtaque = ~0;

    [SerializeField, Min(0f)] private float distanciaInvestida = 0.8f;
    [SerializeField, Min(0f)] private float alturaInvestida = 0.1f;
    [SerializeField, Min(0.01f)] private float duracaoInvestida = 0.35f;
    [Tooltip("Espera depois de iniciar a animacao ate a investida sair (clip Ataque dura ~1.9 s).")]
    [SerializeField, Min(0f)] private float atrasoInvestida = 0.8f;
    [SerializeField, Min(0.1f)] private float duracaoAnimacaoAtaque = 1.9f;
    [SerializeField, Min(0f)] private float cooldownAtaque = 1.2f;
    [SerializeField, Min(0.05f)] private float raioVerificacaoColisaoInvestida = 0.3f;
    [SerializeField] private LayerMask layersBloqueioInvestida = ~0;
    [SerializeField, Min(0f)] private float margemParedeInvestida = 0.15f;

    private bool emAtaque;
    private bool emInvestida;
    private bool impactoResolvido;
    private float proximoAtaque;
    private Coroutine rotinaAtaque;
    private readonly RaycastHit[] hitsAtaque = new RaycastHit[16];
    private readonly Collider[] sobrepostosAtaque = new Collider[16];

    private bool PodeAtacar => !emAtaque && Time.time >= proximoAtaque;

    public int DanoAtaque
    {
        get => danoAtaque;
        set => danoAtaque = Mathf.Max(0, value);
    }

    // IDano: StatusPlayer e Escudo reconhecem a Topeira como fonte de dano de inimigo.
    public float ObterDano() => danoAtaque;
    public GameObject ObterDono() => gameObject;

    private void IniciarAtaque()
    {
        if (!PodeAtacar || alvoPlayer == null) return;

        TocarSomUmaVez(somAtacar, volumeAtacar);
        PrepararVarianteAtaque();
        MudarEstado(Estado.Atacando);
        rotinaAtaque = StartCoroutine(RotinaAtaque(alvoPlayer));
    }

    protected virtual void PrepararVarianteAtaque() { }
    protected virtual bool AtaqueAvanca => true;
    protected virtual bool AguardarFimObservacao() => false;

    private void CancelarAtaque()
    {
        if (rotinaAtaque != null) StopCoroutine(rotinaAtaque);
        rotinaAtaque = null;
        if (emAtaque) proximoAtaque = Time.time + cooldownAtaque;
        emAtaque = false;
        emInvestida = false;
    }

    private IEnumerator RotinaAtaque(Transform alvo)
    {
        emAtaque = true;
        impactoResolvido = false;

        yield return new WaitForSeconds(atrasoInvestida);          // preparacao (animacao)
        float tempoAvanco = AtaqueAvanca ? duracaoInvestida : 0f;
        if (AtaqueAvanca) yield return Investida(alvo);
        if (!impactoResolvido) ResolverImpactoAtaque(alvo);        // golpe

        float restante = duracaoAnimacaoAtaque - atrasoInvestida - tempoAvanco;
        if (restante > 0f) yield return new WaitForSeconds(restante);

        emAtaque = false;
        rotinaAtaque = null;
        proximoAtaque = Time.time + cooldownAtaque;
    }

    // ---- Investida ----
    private IEnumerator Investida(Transform alvo)
    {
        Vector3 inicio = rb.position;
        Vector3 direcao = alvo != null ? PosicaoAlvo - inicio : transform.forward;
        direcao.y = 0f;
        direcao = direcao.sqrMagnitude > 0.0001f ? direcao.normalized : transform.forward;

        yawAlvo = Quaternion.LookRotation(direcao).eulerAngles.y;
        rb.rotation = Quaternion.Euler(0f, yawAlvo, 0f);

        float distancia = distanciaInvestida;
        if (alvo != null) distancia = Mathf.Min(distancia, DistanciaXZ(inicio, PosicaoAlvo)); // nao atravessa o alvo

        emInvestida = true;
        rb.linearVelocity = Vector3.zero;

        float duracao = Mathf.Max(0.01f, duracaoInvestida);
        float tempo = 0f;
        bool bloqueada = false;

        while (tempo < duracao && !morto)
        {
            yield return new WaitForFixedUpdate();
            tempo += Time.fixedDeltaTime;
            float k = Mathf.Clamp01(tempo / duracao);

            Vector3 proxima = inicio + direcao * (distancia * k);
            proxima.y = inicio.y + Mathf.Sin(k * Mathf.PI) * alturaInvestida;

            if (InvestidaBloqueada(rb.position, proxima)) { bloqueada = true; break; }
            rb.MovePosition(proxima);
        }

        rb.linearVelocity = Vector3.zero;
        emInvestida = false;

        // Bateu em algo: o golpe sai agora, no ponto de contato.
        if (bloqueada && !morto) ResolverImpactoAtaque(alvo);
    }

    private bool InvestidaBloqueada(Vector3 de, Vector3 para)
    {
        Vector3 delta = para - de;
        delta.y = 0f;
        float passo = delta.magnitude;
        if (passo < 0.0001f) return false;

        float raio = Mathf.Min(raioVerificacaoColisaoInvestida, Mathf.Max(0.05f, alturaOrigemAtaque - 0.05f));
        Vector3 origem = de + Vector3.up * Mathf.Max(alturaOrigemAtaque, raio + 0.05f);
        int n = Physics.SphereCastNonAlloc(origem, raio, delta / passo, hitsAtaque, passo + margemParedeInvestida,
            layersBloqueioInvestida, QueryTriggerInteraction.Ignore);
        if (n == hitsAtaque.Length) return true; // resultado incompleto: nao arriscar

        for (int i = 0; i < n; i++)
        {
            Collider c = hitsAtaque[i].collider;
            if (c == null || PertenceAEsta(c)) continue;
            if (hitsAtaque[i].normal.y >= 0.7f) continue; // chao caminhavel nao bloqueia
            return true;
        }
        return false;
    }

    // ---- Impacto (golpe) ----
    private Vector3 OrigemAtaque => pontoOrigemAtaque != null
        ? pontoOrigemAtaque.position
        : transform.position + Vector3.up * alturaOrigemAtaque;

    private void ResolverImpactoAtaque(Transform alvo)
    {
        impactoResolvido = true;
        if (alvo == null) return;

        Vector3 origem = OrigemAtaque;
        Vector3 direcao = PosicaoAlvo - transform.position;
        direcao.y = 0f;
        direcao = direcao.sqrMagnitude > 0.0001f ? direcao.normalized : transform.forward;

        Collider contato = null;
        float menor = float.PositiveInfinity;

        // Alvo ja encostado: o SphereCast nao detecta colliders em que ja comeca dentro.
        int dentro = Physics.OverlapSphereNonAlloc(origem, raioAtaque, sobrepostosAtaque, layersAtaque, QueryTriggerInteraction.Ignore);
        if (dentro == sobrepostosAtaque.Length) return;
        for (int i = 0; i < dentro; i++)
        {
            Collider c = sobrepostosAtaque[i];
            if (c == null || PertenceAEsta(c) || Escudo.IgnorarNoAtaque(c, alvo)) continue;
            float d = Vector3.Distance(c.ClosestPoint(origem), origem);
            if (d < menor) { menor = d; contato = c; }
        }

        int n = Physics.SphereCastNonAlloc(origem, raioAtaque, direcao, hitsAtaque, alcanceAtaque, layersAtaque, QueryTriggerInteraction.Ignore);
        if (n == hitsAtaque.Length) return;
        for (int i = 0; i < n; i++)
        {
            Collider c = hitsAtaque[i].collider;
            if (c == null || PertenceAEsta(c) || Escudo.IgnorarNoAtaque(c, alvo)) continue;
            if (hitsAtaque[i].distance < menor) { menor = hitsAtaque[i].distance; contato = c; }
        }

        if (contato != null) AplicarGolpeNoContato(contato, alvo);
    }

    private void AplicarGolpeNoContato(Collider contato, Transform alvo)
    {
        if (Escudo.TentarInterceptarGolpe(contato, gameObject, alvo, danoAtaque, out float restante))
        {
            DanificarPlayer(alvo, restante);   // o escudo absorveu parte do golpe
            return;
        }

        if (!BloqueiaMesmoSendoDoPlayer(contato, alvo) && PertenceAoAlvo(contato, alvo))
            DanificarPlayer(alvo, danoAtaque);
    }

    private void DanificarPlayer(Transform alvo, float dano)
    {
        if (dano <= 0f) return;
        StatusPlayer status = alvo.GetComponentInParent<StatusPlayer>();
        if (status == null) status = alvo.GetComponentInChildren<StatusPlayer>(true);
        if (status != null) status.ReceberDano(dano, gameObject);
    }

    private bool PertenceAEsta(Collider c)
    {
        Transform t = c.transform;
        return t == transform || t.IsChildOf(transform);
    }

    private static bool PertenceAoAlvo(Collider c, Transform alvo)
    {
        Transform t = c.transform;
        if (t == alvo || t.IsChildOf(alvo)) return true;
        Transform raiz = t.root;
        return raiz != null && (raiz == alvo || raiz.IsChildOf(alvo));
    }

    // Escudo e espada do jogador podem estar no caminho do golpe.
    private static bool BloqueiaMesmoSendoDoPlayer(Collider c, Transform alvo)
    {
        Escudo escudo = c.GetComponentInParent<Escudo>();
        if (escudo != null) return escudo.EstaProtegendoPlayer(alvo);
        return c.GetComponentInParent<Espada>() != null;
    }
    #endregion

    #region 7. MORTE E RESPAWN
    [Serializable]
    public class SpawnNormalConfig
    {
        public GameObject prefab;
        [HideInInspector] public float chance = 50f;
        [Min(0)] public int quantidadeMinima = 1;
        [Min(0)] public int quantidadeMaxima = 1;
        public Vector3 offsetSpawn = new Vector3(0f, 0.5f, 0f);
    }

    [Serializable]
    public class SpawnMissaoConfig
    {
        public GameObject prefab;
        [HideInInspector] public float chance = 100f;
        [Min(0)] public int quantidadeMinima = 1;
        [Min(0)] public int quantidadeMaxima = 1;
        public Vector3 offsetSpawn = new Vector3(0f, 0.5f, 0f);
        public string idMissao;
        public string idObjetivo;
        public bool exigirMissaoAtiva = true;
    }

    [Tooltip("Deve existir em RespawnMonstro com o prefab da Topeira. Tambem e o id usado pelas missoes de matar inimigos.")]
    [SerializeField] private string idRespawnMonstro = "Topeira";

    // Um unico sorteio por morte; as chances sempre somam 100 (o restante e 'Nada'). Editadas no Inspector por sliders.
    // Valor negativo em chanceNada = prefab antigo: Nada recebe o que sobrar de 100.
    [SerializeField] private SpawnNormalConfig[] prefabsSpawnNormal;
    [SerializeField, HideInInspector] private float chanceNada = -1f;

    [SerializeField] private SpawnMissaoConfig[] prefabsSpawnMissao;
    [SerializeField, HideInInspector] private float chanceNadaMissao = -1f;

    [SerializeField] private bool darReinAoMorrer = true;
    [SerializeField] private string reinMinimo = "0.01";
    [SerializeField] private string reinMaximo = "0.05";
    [SerializeField, Range(0, 5)] private int casasDecimaisSorteioRein = 5;

    private bool recompensasConcedidas;

    // ---- Morte ----
    private void Morrer()
    {
        if (morto) return;
        morto = true;
        vidaAtual = 0;
        AtualizarUIVida();

        CancelarAtaque();
        PararMovimento();
        PararSomAndar();
        MudarEstado(Estado.Morto);
        TocarSomMorrer();
        CongelarCorpo();

        // Cada etapa e isolada: uma falha nao impede as demais.
        ExecutarEtapa(EntregarExperiencia);
        ExecutarEtapa(PagarRecompensaRein);
        ExecutarEtapa(NotificarMissao);
        ExecutarEtapa(SpawnarPrefabsNormais);
        ExecutarEtapa(SpawnarPrefabsMissao);
        ExecutarEtapa(AgendarRespawn);

        if (esconderCanvasAoMorrer && canvasVida != null) canvasVida.gameObject.SetActive(false);
        Destroy(gameObject, tempoParaMorrer);
    }

    private static void ExecutarEtapa(Action etapa)
    {
        try { etapa(); } catch (Exception erro) { Debug.LogException(erro); }
    }

    private void CongelarCorpo()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        if (capsula != null) capsula.enabled = false; // libera o espaco; a animacao de morte assume
    }

    // ---- Recompensas (uma unica vez) ----
    private void EntregarExperiencia()
    {
        if (recompensasConcedidas) return;
        recompensasConcedidas = true;
        if (experienciaInimigo == null) experienciaInimigo = GetComponent<ExperienciaInimigo>();
        if (experienciaInimigo != null) experienciaInimigo.EntregarExperiencia();
    }

    private void PagarRecompensaRein()
    {
        if (!darReinAoMorrer) return;

        StatusPlayer status = experienciaInimigo != null ? experienciaInimigo.PrimeiroAtacante : null;
        if (status == null && ultimoPlayerResponsavel != null)
            status = ultimoPlayerResponsavel.GetComponentInParent<StatusPlayer>();
        if (status == null) status = FindFirstObjectByType<StatusPlayer>();

        CarteiraReinPlayer carteira = status != null ? status.ObterCarteiraRein() : null;
        if (carteira == null) return;

        long unidades = SortearUnidadesRein();
        if (unidades > 0) carteira.AdicionarReinUnidades(unidades);
    }

    private long SortearUnidadesRein()
    {
        long minimo = ConverterRein(reinMinimo);
        long maximo = ConverterRein(reinMaximo);
        if (maximo < minimo) { long t = minimo; minimo = maximo; maximo = t; }

        // 1 REIN = 100.000 unidades; 5 casas decimais => passo 1.
        long passo = 1;
        for (int i = casasDecimaisSorteioRein; i < 5; i++) passo *= 10;

        minimo = ((minimo + passo - 1) / passo) * passo;
        maximo = (maximo / passo) * passo;
        if (maximo <= minimo) return minimo;

        long passos = ((maximo - minimo) / passo) + 1;
        long indice = Math.Min(passos - 1, (long)Math.Floor(UnityEngine.Random.value * passos));
        return minimo + indice * passo;
    }

    private static long ConverterRein(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return 0;
        string normalizado = texto.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal valor)) return 0;
        return CarteiraReinPlayer.ConverterDecimalParaUnidades(valor);
    }

    private void NotificarMissao()
    {
        GerenciadorMissoes gerenciador = FindFirstObjectByType<GerenciadorMissoes>();
        if (gerenciador != null) gerenciador.NotificarInimigoMorto(idRespawnMonstro);
    }

    private void SpawnarPrefabsNormais()
    {
        if (prefabsSpawnNormal == null || prefabsSpawnNormal.Length == 0) return;

        var chances = new float[prefabsSpawnNormal.Length + 1];
        for (int i = 0; i < prefabsSpawnNormal.Length; i++)
        {
            SpawnNormalConfig d = prefabsSpawnNormal[i];
            chances[i] = d != null && d.prefab != null ? DropChances.Sanitizar(d.chance) : float.NaN;
        }
        chances[prefabsSpawnNormal.Length] = chanceNada;

        int indice = DropChances.Sortear(chances);
        if (indice < 0) return;
        SpawnNormalConfig item = prefabsSpawnNormal[indice];
        SpawnarPrefab(item.prefab, item.offsetSpawn, item.quantidadeMinima, item.quantidadeMaxima);
    }

    private void SpawnarPrefabsMissao()
    {
        if (prefabsSpawnMissao == null || prefabsSpawnMissao.Length == 0) return;

        Transform player = ultimoPlayerResponsavel != null ? ultimoPlayerResponsavel : alvoPlayer;
        if (player == null) return;

        var chances = new float[prefabsSpawnMissao.Length + 1];
        for (int i = 0; i < prefabsSpawnMissao.Length; i++)
        {
            SpawnMissaoConfig d = prefabsSpawnMissao[i];
            chances[i] = d != null && d.prefab != null ? DropChances.Sanitizar(d.chance) : float.NaN;
        }
        chances[prefabsSpawnMissao.Length] = chanceNadaMissao;

        int indice = DropChances.Sortear(chances);
        if (indice < 0) return;
        SpawnMissaoConfig item = prefabsSpawnMissao[indice];
        if (PlayerTemMissaoVinculada(item))
            SpawnarPrefab(item.prefab, item.offsetSpawn, item.quantidadeMinima, item.quantidadeMaxima);
    }

    // Mesmo comportamento do SlimeIA: a integracao com o estado real das missoes ainda nao existe.
    private bool PlayerTemMissaoVinculada(SpawnMissaoConfig config) => config != null && !config.exigirMissaoAtiva;

    private void SpawnarPrefab(GameObject prefab, Vector3 offset, int quantidadeMinima, int quantidadeMaxima)
    {
        if (prefab == null) return;
        int minimo = Mathf.Max(0, quantidadeMinima);
        int quantidade = UnityEngine.Random.Range(minimo, Mathf.Max(minimo, quantidadeMaxima) + 1);
        Vector3 posicao = transform.position + offset;
        for (int i = 0; i < quantidade; i++) Instantiate(prefab, posicao, Quaternion.identity);
    }

    private void NormalizarDrops()
    {
        if (prefabsSpawnNormal != null)
            for (int i = 0; i < prefabsSpawnNormal.Length; i++)
            {
                SpawnNormalConfig d = prefabsSpawnNormal[i];
                if (d != null) d.quantidadeMaxima = Mathf.Max(d.quantidadeMinima, d.quantidadeMaxima);
            }
        if (prefabsSpawnMissao != null)
            for (int i = 0; i < prefabsSpawnMissao.Length; i++)
            {
                SpawnMissaoConfig d = prefabsSpawnMissao[i];
                if (d != null) d.quantidadeMaxima = Mathf.Max(d.quantidadeMinima, d.quantidadeMaxima);
            }
    }

    // ---- Respawn ----
    private void AgendarRespawn()
    {
        if (RespawnMonstro.Instancia == null) return;
        // Pontos automaticos pertencem a esta instancia: o novo corpo cria os seus. Os manuais seguem.
        Transform[] pontos = pontosAutomaticos.Count > 0 ? null : pontosPatrulha;
        RespawnMonstro.Instancia.AgendarRespawn(idRespawnMonstro, transform.position, transform.rotation, pontos);
    }

    /// <summary>Chamado pelo RespawnMonstro logo depois de criar a nova Topeira.</summary>
    public void ReiniciarAposRespawn()
    {
        CancelarAtaque();
        morto = false;
        recompensasConcedidas = false;
        ultimoPlayerResponsavel = null;
        EsquecerAlvo();
        estadoAntesDano = Estado.Parado;
        proximoAtaque = 0f;
        tempoRestanteObservando = 0f;

        vidaAtual = vidaMaxima;
        AtualizarUIVida();
        if (canvasVida != null) canvasVida.gameObject.SetActive(true);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        if (capsula != null) capsula.enabled = true;
        AtualizarCacheAnimator();

        comportamentoIniciado = false;
        IniciarComportamento();
    }

    private void IniciarComportamento()
    {
        if (comportamentoIniciado) return;
        comportamentoIniciado = true;

        if (QuantidadePontosValidos() == 0 && gerarPontosAutomaticamente) GerarPontosAutomaticos();
        EscolherProximoPonto();
        MudarEstado(QuantidadePontosValidos() > 0 ? Estado.Patrulhando : Estado.Parado, true);
    }
    #endregion

    #region 8. ANIMACAO E AUDIO
    [Tooltip("Se vazio, procura nos filhos. Parametros bool: Andar, Ataque, Dano, Morto.")]
    [SerializeField] protected Animator animator;

    private static readonly int AndarHash = Animator.StringToHash("Andar");
    private static readonly int AtaqueHash = Animator.StringToHash("Ataque");
    private static readonly int DanoHash = Animator.StringToHash("Dano");
    private static readonly int MortoHash = Animator.StringToHash("Morto");
    private bool temAndar, temAtaque, temDano, temMorto;

    protected virtual void ConfigurarAnimator()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        AtualizarCacheAnimator();
    }

    private void AtualizarCacheAnimator()
    {
        temAndar = temAtaque = temDano = temMorto = false;
        if (animator == null || animator.runtimeAnimatorController == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Bool) continue;
            if (p.nameHash == AndarHash) temAndar = true;
            else if (p.nameHash == AtaqueHash) temAtaque = true;
            else if (p.nameHash == DanoHash) temDano = true;
            else if (p.nameHash == MortoHash) temMorto = true;
        }
    }

    // Apenas UM parametro fica ligado por vez; Parado = todos desligados.
    private void AplicarAnimacao(bool andar, bool ataque, bool dano, bool morte)
    {
        if (animator == null) return;
        if (temAndar) animator.SetBool(AndarHash, andar);
        if (temAtaque) animator.SetBool(AtaqueHash, ataque);
        if (temDano) animator.SetBool(DanoHash, dano);
        if (temMorto) animator.SetBool(MortoHash, morte);
    }

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somAndar;
    [SerializeField] private AudioClip somAtacar;
    [SerializeField] private AudioClip somDano;
    [SerializeField] private AudioClip somMorrer;
    [SerializeField, Range(0f, 1f)] private float volumeAndar = 1f;
    [SerializeField, Range(0f, 1f)] private float volumeAtacar = 1f;
    [SerializeField, Range(0f, 1f)] private float volumeDano = 1f;
    [SerializeField, Range(0f, 1f)] private float volumeMorrer = 1f;

    private void ConfigurarAudio()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
        }
        audioSource.playOnAwake = false;
    }

    private void TocarSomAndar()
    {
        if (somAndar == null || audioSource == null) return;
        if (audioSource.isPlaying && audioSource.clip == somAndar) return;
        audioSource.clip = somAndar;
        audioSource.volume = volumeAndar;
        audioSource.loop = true;
        audioSource.Play();
    }

    private void PararSomAndar()
    {
        if (audioSource != null && audioSource.loop && audioSource.clip == somAndar && audioSource.isPlaying)
            audioSource.Stop();
    }

    private void TocarSomUmaVez(AudioClip clip, float volume)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, volume);
    }

    // O objeto e destruido logo depois: o som de morte toca no ponto, fora do objeto.
    private void TocarSomMorrer()
    {
        if (somMorrer != null) AudioSource.PlayClipAtPoint(somMorrer, transform.position, volumeMorrer);
    }
    #endregion

    #region 9. MAQUINA DE ESTADOS
    private void AtualizarEstado()
    {
        switch (estadoAtual)
        {
            case Estado.Parado:      AtualizarParado();      break;
            case Estado.Patrulhando: AtualizarPatrulha();    break;
            case Estado.Observando:  AtualizarObservacao();  break;
            case Estado.Alerta:      AtualizarAlerta();      break;
            case Estado.Perseguindo: AtualizarPerseguicao(); break;
            case Estado.Atacando:    AtualizarAtaque();      break;
            case Estado.TomandoDano: AtualizarReacaoDano();  break;
        }
    }

    private void MudarEstado(Estado novo, bool forcar = false)
    {
        if (morto && novo != Estado.Morto) return;
        if (estadoAtual == novo && !forcar) return;

        estadoAtual = novo;
        AplicarAnimacaoDoEstado();
        AnimarEstado(novo);
    }

    protected virtual void AnimarEstado(Estado estado) { }

    private void AplicarAnimacaoDoEstado()
    {
        switch (estadoAtual)
        {
            case Estado.Patrulhando:
            case Estado.Perseguindo:
                AplicarAnimacao(true, false, false, false);
                TocarSomAndar();
                return;
            case Estado.Atacando:    AplicarAnimacao(false, true, false, false); break;
            case Estado.TomandoDano: AplicarAnimacao(false, false, true, false); break;
            case Estado.Morto:       AplicarAnimacao(false, false, false, true); break;
            default:                 AplicarAnimacao(false, false, false, false); break;
        }
        PararSomAndar();
    }

    // Mesmo fluxo do SlimeIA: viu o jogador -> Alerta; perto o bastante -> Perseguindo.
    private bool ReagirAoAlvoVisivel()
    {
        if (!alvoVisivel) return false;
        MudarEstado(podeIniciarPerseguicao ? Estado.Perseguindo : Estado.Alerta);
        return true;
    }

    private void AtualizarParado()
    {
        if (ReagirAoAlvoVisivel()) return;
        PararMovimento();
    }

    private void AtualizarAlerta()
    {
        if (!alvoVisivel)
        {
            EsquecerAlvo();
            VoltarParaPatrulha();
            return;
        }

        PararMovimento();
        OlharPara(PosicaoAlvo);
        if (podeIniciarPerseguicao) MudarEstado(Estado.Perseguindo);
    }

    private void AtualizarPatrulha()
    {
        if (ReagirAoAlvoVisivel()) return;

        Transform ponto = ObterPontoAtual();
        if (ponto == null) { MudarEstado(Estado.Parado); return; }

        if (RotaBloqueada) { IniciarObservacao(); return; }

        MoverPara(ponto.position, velocidadePatrulha);
        if (DistanciaXZ(transform.position, ponto.position) <= distanciaChegadaPonto) IniciarObservacao();
    }

    private void AtualizarObservacao()
    {
        if (ReagirAoAlvoVisivel()) return;

        PararMovimento();
        tempoRestanteObservando -= Time.deltaTime;
        if (tempoRestanteObservando <= 0f && !AguardarFimObservacao()) VoltarParaPatrulha();
    }

    private void AtualizarPerseguicao()
    {
        if (!PercebeAlvo) { VoltarParaPatrulha(); return; }

        if (RotaBloqueada)
        {
            // Sem caminho ate o alvo: desiste, observa e volta a patrulhar.
            EsquecerAlvo();
            IniciarObservacao();
            return;
        }

        Vector3 posicaoAlvo = PosicaoAlvo;
        if (DistanciaXZ(transform.position, posicaoAlvo) <= distanciaAtaque)
        {
            PararMovimento();
            OlharPara(posicaoAlvo);
            if (PodeAtacar) IniciarAtaque();
            return; // dentro do alcance mas em cooldown: espera encarando o alvo
        }

        MoverPara(posicaoAlvo, velocidadePerseguicao);
    }

    private void AtualizarAtaque()
    {
        if (!emInvestida)
        {
            PararMovimento();
            if (alvoPlayer != null) OlharPara(PosicaoAlvo);
        }

        if (emAtaque) return;

        // Ataque terminou: alvo longe demais -> Alerta; ainda perto -> Perseguindo; perdido -> patrulha.
        if (!PercebeAlvo) { VoltarParaPatrulha(); return; }
        bool longe = DistanciaXZ(transform.position, PosicaoAlvo) > distanciaIniciarPerseguicao;
        MudarEstado(longe && alvoVisivel ? Estado.Alerta : Estado.Perseguindo);
    }

    private void AtualizarReacaoDano()
    {
        PararMovimento();
        if (Time.time < fimReacaoDano) return;
        MudarEstado(estadoAntesDano == Estado.Morto ? Estado.Parado : estadoAntesDano);
    }
    #endregion

    #region 10. DEBUG
    [SerializeField] private bool desenharGizmos = true;
    [SerializeField] private bool desenharGizmosSempre = true;
    [SerializeField, Min(0f)] private float alturaOlhos = 0.6f;
    [SerializeField] private Transform pontoOlhos;

    private void OnDrawGizmos()
    {
        if (desenharGizmos && desenharGizmosSempre) DesenharGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (desenharGizmos && !desenharGizmosSempre) DesenharGizmos();
    }

    private void DesenharGizmos()
    {
        // Campo de visao
        Gizmos.color = Color.yellow;
        Vector3 o = Olhos;
        Gizmos.DrawWireSphere(o, raioCampoVisao);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(o, distanciaIniciarPerseguicao);
        Vector3 f = transform.forward; f.y = 0f;
        if (f.sqrMagnitude > 0.0001f)
        {
            f.Normalize();
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(o, o + Quaternion.Euler(0f, -anguloCampoVisao * 0.5f, 0f) * f * raioCampoVisao);
            Gizmos.DrawLine(o, o + Quaternion.Euler(0f, anguloCampoVisao * 0.5f, 0f) * f * raioCampoVisao);
        }

        // Ataque e investida
        Gizmos.color = Color.red;
        Vector3 origem = OrigemAtaque;
        Gizmos.DrawWireSphere(origem, distanciaAtaque);
        Gizmos.DrawWireSphere(origem + transform.forward * alcanceAtaque, raioAtaque);
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position + Vector3.up * alturaOrigemAtaque,
            transform.position + Vector3.up * alturaOrigemAtaque + transform.forward * distanciaInvestida);

        // Patrulha
        if (pontosPatrulha == null) return;
        Gizmos.color = Color.cyan;
        foreach (Transform ponto in pontosPatrulha)
            if (ponto != null) Gizmos.DrawWireSphere(ponto.position, distanciaChegadaPonto);
    }
    #endregion
}
