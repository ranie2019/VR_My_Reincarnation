using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class MostroMovimentacao : MonoBehaviour
{
    [Header("Patrulha")]
    [Tooltip("Pontos de patrulha (Transforms)")]
    public Transform[] pontos;

    [Tooltip("Velocidade de movimento")]
    public float velocidade = 2f;

    [Tooltip("Distância mínima para considerar que chegou ao ponto")]
    public float distanciaChegada = 0.15f;

    [Tooltip("Tempo parado vigiando ao chegar no ponto")]
    public float tempoVigia = 5f;

    [Header("Rotação")]
    public float velocidadeRotacao = 5f;

    // estado interno
    private int indiceAtual = -1;
    private bool pausado = false;
    private bool vigiando = false;

    // refs
    private Animator anim;
    private Coroutine rotinaVigia;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    void Start()
    {
        if (pontos == null || pontos.Length == 0)
        {
            Debug.LogWarning($"[{name}] Nenhum ponto de patrulha atribuído.");
            return;
        }

        EscolherNovoDestino();
    }

    void Update()
    {
        if (pausado || vigiando) return;
        MoverParaDestino(Time.deltaTime);
    }

    // =========================================================
    // MOVIMENTO
    // =========================================================
    void MoverParaDestino(float dt)
    {
        if (indiceAtual < 0 || indiceAtual >= pontos.Length) return;

        Transform alvo = pontos[indiceAtual];
        if (alvo == null) return;

        Vector3 destino = alvo.position;
        destino.y = transform.position.y;

        transform.position = Vector3.MoveTowards(transform.position, destino, velocidade * dt);

        Vector3 dir = destino - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion rot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, dt * velocidadeRotacao);
        }

        bool chegou = Vector3.Distance(transform.position, destino) <= distanciaChegada;
        anim.SetBool("Andar", !chegou);

        if (chegou && !vigiando)
            IniciarVigia();
    }

    // =========================================================
    // VIGIA
    // =========================================================
    void IniciarVigia()
    {
        if (vigiando) return;

        vigiando = true;
        pausado = true;

        anim.SetBool("Andar", false);
        anim.SetBool("Vigia", true);

        rotinaVigia = StartCoroutine(VigiarCoroutine());
    }

    IEnumerator VigiarCoroutine()
    {
        yield return new WaitForSeconds(tempoVigia);

        anim.SetBool("Vigia", false);
        vigiando = false;
        pausado = false;

        EscolherNovoDestino();
    }

    // =========================================================
    // DESTINO
    // =========================================================
    void EscolherNovoDestino()
    {
        if (pontos == null || pontos.Length == 0) return;

        int novo = indiceAtual;

        if (pontos.Length == 1)
        {
            indiceAtual = 0;
            return;
        }

        while (novo == indiceAtual)
            novo = Random.Range(0, pontos.Length);

        indiceAtual = novo;
    }

    // =========================================================
    // CONTROLE EXTERNO
    // =========================================================
    public void PausarMovimento()
    {
        pausado = true;
        anim.SetBool("Andar", false);
    }

    public void RetomarMovimento()
    {
        pausado = false;
        if (!vigiando)
            anim.SetBool("Andar", true);
    }

    public bool EstaPausado() => pausado || vigiando;
}
