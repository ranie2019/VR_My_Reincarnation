using UnityEngine;
using UnityEngine.Events;
using System.Collections;

[DisallowMultipleComponent]
public class MostroDeteccao : MonoBehaviour
{
    [Header("Configurações de Detecção do Player")]
    public string tagPlayer = "Player";
    public CapsuleCollider zonaDeteccao;

    [Header("Comportamento ao detectar")]
    [Tooltip("Tempo (segundos) que o slime fica parado ao detectar o player antes de perseguir.")]
    public float tempoParadoAoDetectar = 2f;

    [Tooltip("Rotação suave para olhar o player quando em alerta.")]
    public float velocidadeRotacao = 5f;

    [Tooltip("Evita ficar piscando alerta/desalerta quando o player encosta na borda do trigger.")]
    public float tempoMinimoAlerta = 0.25f;

    [Header("Eventos (opcional)")]
    public UnityEvent onDetectarPlayer;
    public UnityEvent onPerderPlayer;

    // refs
    private Animator anim;
    private MostroMovimentacao slimeMovimentacao;
    private MostroAtaque slimeAtaque; // opcional (se existir)
    private Transform playerTransform;

    // estado
    public bool EmAlerta { get; private set; } = false;
    private float tempoDesdeMudanca = 999f;

    private Coroutine rotinaParado;

    void Awake()
    {
        anim = GetComponent<Animator>();
        slimeMovimentacao = GetComponent<MostroMovimentacao>();
        slimeAtaque = GetComponent<MostroAtaque>();

        if (zonaDeteccao == null)
            zonaDeteccao = GetComponent<CapsuleCollider>();

        if (zonaDeteccao != null && !zonaDeteccao.isTrigger)
            zonaDeteccao.isTrigger = true;
    }

    void Start()
    {
        var p = GameObject.FindWithTag(tagPlayer);
        playerTransform = p ? p.transform : null;
    }

    void Update()
    {
        tempoDesdeMudanca += Time.deltaTime;

        if (EmAlerta && playerTransform != null)
            LookAtPlayer(Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(tagPlayer)) return;

        if (EmAlerta && tempoDesdeMudanca < tempoMinimoAlerta) return;

        EntrarAlerta(other.transform);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(tagPlayer)) return;

        if (!EmAlerta && tempoDesdeMudanca < tempoMinimoAlerta) return;

        SairAlerta();
    }

    // =========================
    // ALERTA
    // =========================
    private void EntrarAlerta(Transform player)
    {
        playerTransform = player;
        EmAlerta = true;
        tempoDesdeMudanca = 0f;

        if (anim != null)
            anim.SetBool("Alerta", true);

        // 1) para tudo por X segundos (alerta “encarando”)
        PararMovimento();

        // garante que não vai empilhar coroutines
        if (rotinaParado != null) StopCoroutine(rotinaParado);
        rotinaParado = StartCoroutine(EsperarEPerseguir());

        onDetectarPlayer?.Invoke();
    }

    private IEnumerator EsperarEPerseguir()
    {
        float t = Mathf.Max(0f, tempoParadoAoDetectar);
        if (t > 0f) yield return new WaitForSeconds(t);

        // 2) libera movimento para o SlimeAtaque perseguir
        LiberarMovimento();

        rotinaParado = null;
    }

    private void SairAlerta()
    {
        EmAlerta = false;
        tempoDesdeMudanca = 0f;

        if (anim != null)
            anim.SetBool("Alerta", false);

        // se saiu do range, cancela espera e volta ao normal
        if (rotinaParado != null)
        {
            StopCoroutine(rotinaParado);
            rotinaParado = null;
        }

        LiberarMovimento();

        onPerderPlayer?.Invoke();
    }

    // =========================
    // MOVIMENTO: parar/liberar
    // =========================
    private void PararMovimento()
    {
        // pausa patrulha se existir
        if (slimeMovimentacao != null) slimeMovimentacao.PausarMovimento();

        // pausa perseguição (se o SlimeAtaque tiver método). Se não tiver, ao menos congela animação.
        // Se você usar o SlimeAtaque que eu te passei atualizado, ele não depende disso.
        if (anim != null) anim.SetBool("Andar", false);
    }

    private void LiberarMovimento()
    {
        // libera patrulha (mas se o player ainda estiver perto, o SlimeAtaque vai assumir)
        if (slimeMovimentacao != null) slimeMovimentacao.RetomarMovimento();

        // NÃO força Andar=true aqui — quem manda é SlimeAtaque / SlimeMovimentacao
    }

    // =========================
    // LOOK AT
    // =========================
    private void LookAtPlayer(float dt)
    {
        Vector3 dir = playerTransform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, dt * velocidadeRotacao);
    }
}
