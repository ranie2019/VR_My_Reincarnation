using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class MostroAtaque : MonoBehaviour
{
    private enum Estado { Patrulha, Aguardando, Perseguindo, Atacando }

    [Header("Player")]
    public string tagPlayer = "Player";

    [Header("Perseguição")]
    public float distanciaPerseguicao = 6f;
    public float velocidade = 3f;
    public float velocidadeRotacao = 6f;

    [Header("Ataque")]
    public float distanciaAtaque = 1.5f;      // alcance real do golpe
    public float margemAtaque = 0.35f;        // espaço pra não grudar
    public float toleranciaParada = 0.05f;    // quão perto do ponto de parada ele precisa chegar
    public float tempoEntreAtaques = 0.8f;
    public int dano = 1;

    [Header("Ao detectar (antes de perseguir)")]
    public float tempoParadoAoDetectar = 2f;

    [Header("Perder player / voltar patrulha")]
    [Tooltip("Se o player ficar fora do alcance por esse tempo, volta patrulha.")]
    public float tempoParaVoltarPatrulha = 1.5f;

    [Tooltip("Se você tiver SlimeDeteccao, o slime também pode perder pelo trigger.")]
    public bool usarTriggerComoVisao = true;

    // refs
    private Transform player;
    private Animator anim;
    private MostroMovimentacao patrulha;
    private MostroDeteccao deteccao;

    // estado
    private Estado estado = Estado.Patrulha;
    private bool playerDetectado = false;
    private bool atacando = false;

    private Coroutine rotinaAtaque;
    private Coroutine rotinaAguardar;

    private float tempoFora = 999f; // contador pra voltar patrulha
    private float StopDist => Mathf.Max(0.05f, distanciaAtaque + margemAtaque);

    void Awake()
    {
        anim = GetComponent<Animator>();
        patrulha = GetComponent<MostroMovimentacao>();
        deteccao = GetComponent<MostroDeteccao>();
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag(tagPlayer);
        player = p ? p.transform : null;
    }

    void Update()
    {
        if (player == null) return;

        // --- visão por trigger (se tiver SlimeDeteccao) ---
        if (usarTriggerComoVisao && deteccao != null)
        {
            // EmAlerta = player dentro do trigger
            if (deteccao.EmAlerta && !playerDetectado)
            {
                DetectouPlayer();
            }
            else if (!deteccao.EmAlerta && playerDetectado)
            {
                // se saiu do trigger, começa a contar pra voltar patrulha
                tempoFora += Time.deltaTime;
            }
        }

        float dist = Vector3.Distance(transform.position, player.position);

        // --- visão por distância (fallback) ---
        if (!usarTriggerComoVisao || deteccao == null)
        {
            if (!playerDetectado && dist <= distanciaPerseguicao)
                DetectouPlayer();

            if (playerDetectado)
            {
                if (dist > distanciaPerseguicao * 1.2f) tempoFora += Time.deltaTime;
                else tempoFora = 0f;
            }
        }
        else
        {
            // se estiver no trigger, zera tempo fora
            if (deteccao != null && deteccao.EmAlerta) tempoFora = 0f;
        }

        // --- voltar patrulha se perdeu ---
        if (playerDetectado && tempoFora >= tempoParaVoltarPatrulha)
        {
            VoltarPatrulha();
            return;
        }

        // --- máquina de estados ---
        switch (estado)
        {
            case Estado.Patrulha:
                // nada aqui, a patrulha roda no próprio script
                break;

            case Estado.Aguardando:
                // fica parado olhando
                anim.SetBool("Andar", false);
                OlharParaPlayer(Time.deltaTime);
                break;

            case Estado.Perseguindo:
                AtualizarPerseguicao(Time.deltaTime);
                break;

            case Estado.Atacando:
                // ✅ no ataque ele NÃO anda, só olha e a coroutine ataca
                anim.SetBool("Andar", false);
                OlharParaPlayer(Time.deltaTime);

                // se player saiu do alcance de ataque, volta perseguir
                if (dist > StopDist + 0.15f)
                    EntrarPerseguindo();
                break;
        }
    }

    // =========================
    // DETECTOU PLAYER
    // =========================
    private void DetectouPlayer()
    {
        playerDetectado = true;
        tempoFora = 0f;

        // pausa patrulha imediatamente
        if (patrulha != null) patrulha.PausarMovimento();

        // entra em modo “alerta parado”
        EntrarAguardando();
    }

    private void EntrarAguardando()
    {
        estado = Estado.Aguardando;
        anim.SetBool("Alerta", true);
        anim.SetBool("Andar", false);

        if (rotinaAguardar != null) StopCoroutine(rotinaAguardar);
        rotinaAguardar = StartCoroutine(AguardarAntesDePerseguir());
    }

    private IEnumerator AguardarAntesDePerseguir()
    {
        float t = Mathf.Max(0f, tempoParadoAoDetectar);
        if (t > 0f) yield return new WaitForSeconds(t);

        rotinaAguardar = null;
        EntrarPerseguindo();
    }

    // =========================
    // PERSEGUIR (parando na distância de ataque)
    // =========================
    private void EntrarPerseguindo()
    {
        estado = Estado.Perseguindo;
        anim.SetBool("Alerta", true);
        anim.SetBool("Andar", true);

        PararAtaqueSeRodando();
    }

    private void AtualizarPerseguicao(float dt)
    {
        if (atacando) return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        float dist = toPlayer.magnitude;
        if (dist < 0.0001f) return;

        Vector3 dir = toPlayer / dist;

        // alvo = parar a StopDist do player
        Vector3 alvo = player.position - dir * StopDist;
        alvo.y = transform.position.y;

        float distAlvo = Vector3.Distance(transform.position, alvo);

        // ✅ chegou no ponto de parada -> entra em ataque (parado)
        if (distAlvo <= toleranciaParada)
        {
            anim.SetBool("Andar", false);
            EntrarAtacando();
            return;
        }

        // move até o alvo
        transform.position = Vector3.MoveTowards(transform.position, alvo, velocidade * dt);
        anim.SetBool("Andar", true);

        Rotacionar(dir, dt);
    }

    // =========================
    // ATACAR (parado)
    // =========================
    private void EntrarAtacando()
    {
        estado = Estado.Atacando;
        anim.SetBool("Andar", false);

        if (rotinaAtaque == null)
            rotinaAtaque = StartCoroutine(AtacarContinuo());
    }

    private IEnumerator AtacarContinuo()
    {
        atacando = true;

        while (playerDetectado && player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);

            // se saiu do alcance, para de atacar
            if (dist > StopDist + 0.15f)
                break;

            anim.SetTrigger("Ataque");

            // ✅ aqui entra seu dano real:
            // player.GetComponent<VidaPlayer>()?.TomarDano(dano);

            yield return new WaitForSeconds(tempoEntreAtaques);
        }

        atacando = false;
        rotinaAtaque = null;
    }

    private void PararAtaqueSeRodando()
    {
        if (rotinaAtaque != null)
        {
            StopCoroutine(rotinaAtaque);
            rotinaAtaque = null;
        }
        atacando = false;
    }

    // =========================
    // VOLTAR PATRULHA
    // =========================
    private void VoltarPatrulha()
    {
        playerDetectado = false;
        tempoFora = 999f;

        // para coroutines
        if (rotinaAguardar != null) { StopCoroutine(rotinaAguardar); rotinaAguardar = null; }
        PararAtaqueSeRodando();

        // animações
        anim.SetBool("Alerta", false);
        anim.SetBool("Andar", true);

        // retoma patrulha
        if (patrulha != null) patrulha.RetomarMovimento();

        estado = Estado.Patrulha;
    }

    // =========================
    // ROTAÇÃO
    // =========================
    private void OlharParaPlayer(float dt)
    {
        if (player == null) return;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Rotacionar(dir.normalized, dt);
    }

    private void Rotacionar(Vector3 dir, float dt)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, dt * velocidadeRotacao);
    }
}
