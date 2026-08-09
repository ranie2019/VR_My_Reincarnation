using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class GoblinIA : MonoBehaviour
{
    public enum Estado
    {
        Parado,
        Patrulhando,
        Observando,
        Perseguindo,
        Atacando,
        Morto
    }

    [Header("Vida")]
    [SerializeField] private float vidaMaxima = 100f;
    [SerializeField] private float vidaAtual = 100f;
    [SerializeField] private Image barraVida;
    [SerializeField] private GameObject canvasVida;
    [SerializeField] private bool buscarBarraVidaAutomaticamente = true;
    [SerializeField] private bool esconderCanvasAoMorrer = true;

    [Header("Patrulha")]
    [SerializeField] private Transform[] pontosPatrulha;
    [SerializeField] private float velocidadePatrulha = 2.2f;
    [SerializeField] private float distanciaChegadaPonto = 0.35f;
    [SerializeField] private float tempoObservando = 2f;

    [Header("Campo de Visão")]
    [SerializeField] private string tagPlayer = "Player";
    [SerializeField] private float distanciaVisao = 12f;
    [SerializeField, Range(1f, 360f)] private float anguloVisao = 110f;
    [SerializeField] private LayerMask layerObstaculos;
    [SerializeField] private float tempoMemoriaJogador = 4f;

    [Header("Combate")]
    [Tooltip("Distância em que o Goblin para de correr e começa a atacar")]
    public float distanciaAtaque = 1.8f;

    [SerializeField] private float velocidadePerseguicao = 4.5f;
    [SerializeField] private float cooldownAtaque = 1.4f;
    [SerializeField] private float danoAtaque = 15f;
    [SerializeField] private float tempoAnimacaoAtaque = 0.9f;

    [Header("Morte")]
    [SerializeField] private float tempoParaDestruir = 2f;

    [Header("Referências")]
    [SerializeField] private GoblinAnimacao animacao;

    // Internos
    private Transform player;
    private NavMeshAgent agent;
    private Estado estadoAtual = Estado.Parado;
    private int indicePontoAtual = -1;
    private float tempoRestanteObservando;
    private float timerMemoria;
    private float proximoAtaque;
    private float tempoRestanteAtaque;
    private bool viuJogador;
    private Vector3 ultimaPosicaoConhecida;
    private bool morto;

    private const float VelocidadeRotacao = 8f;
    private const float AlturaOlhos = 0.9f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animacao == null) animacao = GetComponent<GoblinAnimacao>();

        if (agent != null)
        {
            agent.updateRotation = false;
            agent.speed = velocidadePatrulha;
        }

        ConfigurarUIVida();
    }

    private void Start()
    {
        vidaAtual = vidaMaxima;
        AtualizarBarraVida();

        GameObject p = GameObject.FindGameObjectWithTag(tagPlayer);
        if (p != null) player = p.transform;

        EscolherProximoPontoAleatorio();
        MudarEstado(QuantidadePontosValidos() > 0 ? Estado.Patrulhando : Estado.Parado);
    }

    private void Update()
    {
        if (morto) return;

        // Memória do jogador
        if (viuJogador)
        {
            timerMemoria -= Time.deltaTime;
            if (timerMemoria <= 0f)
                viuJogador = false;
        }

        // Detecção pelo Campo de Visão
        if (PodeVerJogador())
        {
            viuJogador = true;
            timerMemoria = tempoMemoriaJogador;
            if (player != null)
                ultimaPosicaoConhecida = player.position;
        }

        // Máquina de estados
        switch (estadoAtual)
        {
            case Estado.Parado:      ExecutarParado();      break;
            case Estado.Patrulhando: ExecutarPatrulha();    break;
            case Estado.Observando:  ExecutarObservacao();  break;
            case Estado.Perseguindo: ExecutarPerseguicao(); break;
            case Estado.Atacando:    ExecutarAtaque();      break;
        }

        // Transições de combate
        if (estadoAtual != Estado.Perseguindo && estadoAtual != Estado.Atacando && viuJogador)
            MudarEstado(Estado.Perseguindo);
        else if (estadoAtual == Estado.Perseguindo && !viuJogador)
            MudarEstado(QuantidadePontosValidos() > 0 ? Estado.Patrulhando : Estado.Parado);

        // Animação de locomoção
        AtualizarAnimacaoLocomocao();
    }

    #region Estados

    private void ExecutarParado()
    {
        PararMovimento();
    }

    private void ExecutarPatrulha()
    {
        Transform ponto = ObterPontoAtual();
        if (ponto == null)
        {
            MudarEstado(Estado.Parado);
            return;
        }

        if (DistanciaXZ(transform.position, ponto.position) > distanciaChegadaPonto)
        {
            MoverPara(ponto.position, velocidadePatrulha);
        }
        else
        {
            tempoRestanteObservando = tempoObservando;
            MudarEstado(Estado.Observando);
        }
    }

    private void ExecutarObservacao()
    {
        PararMovimento();

        tempoRestanteObservando -= Time.deltaTime;
        if (tempoRestanteObservando <= 0f)
        {
            EscolherProximoPontoAleatorio();
            MudarEstado(QuantidadePontosValidos() > 0 ? Estado.Patrulhando : Estado.Parado);
        }
    }

    private void ExecutarPerseguicao()
    {
        if (player == null) return;

        float dist = DistanciaXZ(transform.position, player.position);

        // Chegou na distância de ataque → troca para Atacar
        if (dist <= distanciaAtaque && Time.time >= proximoAtaque)
        {
            MudarEstado(Estado.Atacando);
            return;
        }

        Vector3 destino = viuJogador ? player.position : ultimaPosicaoConhecida;
        MoverPara(destino, velocidadePerseguicao);
    }

    private void ExecutarAtaque()
    {
        PararMovimento();

        if (player != null)
            OlharPara(player.position);

        tempoRestanteAtaque -= Time.deltaTime;

        if (tempoRestanteAtaque <= 0f)
        {
            TentarAplicarDanoNoPlayer();
            proximoAtaque = Time.time + cooldownAtaque;
            MudarEstado(Estado.Perseguindo);
        }
    }

    private void MudarEstado(Estado novoEstado)
    {
        if (morto && novoEstado != Estado.Morto) return;
        if (estadoAtual == novoEstado) return;

        estadoAtual = novoEstado;

        switch (novoEstado)
        {
            case Estado.Atacando:
                tempoRestanteAtaque = tempoAnimacaoAtaque;
                animacao?.Atacar();
                break;

            case Estado.Morto:
                // Tratado no método Morrer()
                break;
        }
    }

    #endregion

    #region Animação de Locomoção

    private void AtualizarAnimacaoLocomocao()
    {
        if (animacao == null) return;
        if (estadoAtual == Estado.Morto || estadoAtual == Estado.Atacando) return;

        switch (estadoAtual)
        {
            case Estado.Patrulhando:
                animacao.Andar();
                break;

            case Estado.Perseguindo:
                animacao.Correr();
                break;

            case Estado.Observando:
            case Estado.Parado:
            default:
                animacao.Parado();
                break;
        }
    }

    #endregion

    #region Combate e Dano

    /// <summary>
    /// Chame este método do script do player/arma.
    /// O Goblin perde exatamente a quantidade de dano recebida.
    /// </summary>
    public void ReceberDano(float dano)
    {
        if (morto) return;

        // Perde exatamente o valor do dano
        vidaAtual -= dano;
        vidaAtual = Mathf.Max(0f, vidaAtual);

        AtualizarBarraVida();

        // Animação de dano
        animacao?.TomarDano();

        // Fica agressivo
        if (estadoAtual != Estado.Perseguindo && estadoAtual != Estado.Atacando)
        {
            viuJogador = true;
            timerMemoria = tempoMemoriaJogador;

            if (player != null)
                ultimaPosicaoConhecida = player.position;

            MudarEstado(Estado.Perseguindo);
        }

        // Se a vida chegou a zero → morre
        if (vidaAtual <= 0f)
        {
            Morrer();
        }
    }

    private void TentarAplicarDanoNoPlayer()
    {
        if (player == null) return;
        if (DistanciaXZ(transform.position, player.position) > distanciaAtaque + 0.4f) return;

        Debug.Log($"Goblin causou {danoAtaque} de dano no player");
        // Coloque aqui a chamada real do seu sistema de dano do player
        // Exemplo: player.GetComponent<StatusPlayer>()?.ReceberDano(danoAtaque);
    }

    private void Morrer()
    {
        if (morto) return;

        morto = true;
        vidaAtual = 0f;
        AtualizarBarraVida();

        // Para todo movimento
        PararMovimento();
        if (agent != null)
            agent.enabled = false;

        // Animação de morte
        animacao?.Morrer();

        // Esconde a barra de vida
        if (esconderCanvasAoMorrer && canvasVida != null)
            canvasVida.SetActive(false);

        // Espera o tempo definido e depois destrói o Goblin
        Destroy(gameObject, tempoParaDestruir);
    }

    #endregion

    #region Movimento

    private void MoverPara(Vector3 destino, float velocidade)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = velocidade;
            agent.SetDestination(destino);
            OlharPara(destino);
            return;
        }

        Vector3 pos = transform.position;
        Vector3 dest = new Vector3(destino.x, pos.y, destino.z);
        transform.position = Vector3.MoveTowards(pos, dest, velocidade * Time.deltaTime);
        OlharPara(dest);
    }

    private void PararMovimento()
    {
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            if (agent.hasPath) agent.ResetPath();
        }
    }

    private void OlharPara(Vector3 alvo)
    {
        Vector3 dir = alvo - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, VelocidadeRotacao * Time.deltaTime);

        Vector3 e = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, e.y, 0f);
    }

    private float DistanciaXZ(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    #endregion

    #region Patrulha

    private void EscolherProximoPontoAleatorio()
    {
        int qtd = QuantidadePontosValidos();
        if (qtd == 0) { indicePontoAtual = -1; return; }

        int novo = indicePontoAtual;
        for (int i = 0; i < 16; i++)
        {
            int c = Random.Range(0, pontosPatrulha.Length);
            if (pontosPatrulha[c] == null) continue;
            if (qtd == 1 || c != indicePontoAtual) { novo = c; break; }
        }

        if (novo < 0 || novo >= pontosPatrulha.Length || pontosPatrulha[novo] == null)
            novo = PrimeiroIndicePontoValido();

        indicePontoAtual = novo;
    }

    private Transform ObterPontoAtual()
    {
        if (pontosPatrulha == null || pontosPatrulha.Length == 0) return null;
        if (indicePontoAtual < 0 || indicePontoAtual >= pontosPatrulha.Length || pontosPatrulha[indicePontoAtual] == null)
            EscolherProximoPontoAleatorio();
        return (indicePontoAtual >= 0 && indicePontoAtual < pontosPatrulha.Length) ? pontosPatrulha[indicePontoAtual] : null;
    }

    private int QuantidadePontosValidos()
    {
        if (pontosPatrulha == null) return 0;
        int q = 0;
        for (int i = 0; i < pontosPatrulha.Length; i++)
            if (pontosPatrulha[i] != null) q++;
        return q;
    }

    private int PrimeiroIndicePontoValido()
    {
        if (pontosPatrulha == null) return -1;
        for (int i = 0; i < pontosPatrulha.Length; i++)
            if (pontosPatrulha[i] != null) return i;
        return -1;
    }

    #endregion

    #region Campo de Visão

    private bool PodeVerJogador()
    {
        if (player == null) return false;

        Vector3 origem = transform.position + Vector3.up * AlturaOlhos;
        Vector3 dir = player.position - origem;
        float dist = dir.magnitude;
        if (dist > distanciaVisao) return false;

        Vector3 dirPlano = dir; dirPlano.y = 0f;
        Vector3 frente = transform.forward; frente.y = 0f;

        if (dirPlano.sqrMagnitude > 0.0001f && frente.sqrMagnitude > 0.0001f)
        {
            if (Vector3.Angle(frente.normalized, dirPlano.normalized) > anguloVisao * 0.5f)
                return false;
        }

        if (Physics.Raycast(origem, dir.normalized, out RaycastHit hit, dist, layerObstaculos))
        {
            if (hit.transform != player && !hit.collider.CompareTag(tagPlayer))
                return false;
        }
        return true;
    }

    #endregion

    #region Vida

    private void ConfigurarUIVida()
    {
        if (buscarBarraVidaAutomaticamente && barraVida == null)
        {
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img != null && img.name == "Vida Frente")
                {
                    barraVida = img;
                    break;
                }
            }
        }
        if (canvasVida == null)
            canvasVida = GetComponentInChildren<Canvas>(true)?.gameObject;
    }

    private void AtualizarBarraVida()
    {
        if (barraVida != null)
            barraVida.fillAmount = vidaMaxima > 0 ? Mathf.Clamp01(vidaAtual / vidaMaxima) : 0f;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Vector3 origem = transform.position + Vector3.up * AlturaOlhos;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origem, distanciaVisao);

        Vector3 frente = transform.forward; frente.y = 0f;
        if (frente.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(origem, Quaternion.Euler(0, -anguloVisao * 0.5f, 0) * frente.normalized * distanciaVisao);
            Gizmos.DrawRay(origem, Quaternion.Euler(0,  anguloVisao * 0.5f, 0) * frente.normalized * distanciaVisao);
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, distanciaAtaque);

        if (pontosPatrulha != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < pontosPatrulha.Length; i++)
            {
                if (pontosPatrulha[i] == null) continue;
                Gizmos.DrawSphere(pontosPatrulha[i].position, 0.3f);
                if (i < pontosPatrulha.Length - 1 && pontosPatrulha[i + 1] != null)
                    Gizmos.DrawLine(pontosPatrulha[i].position, pontosPatrulha[i + 1].position);
            }
        }
    }

    #endregion
}