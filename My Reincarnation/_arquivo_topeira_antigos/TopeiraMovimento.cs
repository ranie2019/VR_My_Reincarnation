using UnityEngine;

// =====================================================================
// TopeiraMovimento
// Locomocao fisica (Rigidbody + capsula): andar ate um destino seguindo o
// chao, girar e parar. Nao escolhe destino, nao ataca e nao anima.
// Informa a IA quando a rota esta bloqueada (sem deslocamento por um tempo).
// =====================================================================
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class TopeiraMovimento : MonoBehaviour
{
    #region Configuracao
    [Header("Corpo")]
    [Tooltip("Ajusta a capsula pelos Renderers filhos ao iniciar o jogo.")]
    [SerializeField] private bool ajustarCapsuleAoModelo = true;
    [SerializeField, Min(0f)] private float velocidadeRotacao = 8f;

    [Header("Solo (metros de mundo)")]
    [SerializeField] private LayerMask camadasSolo = ~0;
    [SerializeField, Range(1f, 60f)] private float inclinacaoMaxima = 45f;
    [SerializeField, Min(0.05f)] private float alturaSondaSolo = 0.3f;
    [SerializeField, Min(0.01f)] private float toleranciaSolo = 0.15f;
    [SerializeField, Min(0.05f)] private float quedaMaxima = 0.6f;
    [SerializeField, Min(0.1f)] private float distanciaPassoSeguro = 0.5f;

    [Header("Rota bloqueada")]
    [SerializeField, Min(0.1f)] private float tempoRotaBloqueada = 2f;
    [SerializeField, Min(0.005f)] private float deslocamentoMinimo = 0.02f;
    [SerializeField, Min(0.01f)] private float distanciaParada = 0.05f;
    #endregion

    #region Estado
    private Rigidbody corpo;
    private CapsuleCollider capsula;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    private Vector3 destino;
    private float velocidade;
    private bool mover;
    private bool morto;
    private float yawAlvo;

    private Vector3 ultimaPosicaoComProgresso;
    private float tempoSemProgresso;

    /// <summary>Sem deslocamento ha tempoRotaBloqueada segundos enquanto tenta andar.</summary>
    public bool RotaBloqueada => tempoSemProgresso >= tempoRotaBloqueada;
    #endregion

    #region Ciclo de vida
    private void Reset() => AjustarCapsule();

    private void Awake()
    {
        corpo = GetComponent<Rigidbody>();
        capsula = GetComponent<CapsuleCollider>();
        if (ajustarCapsuleAoModelo) AjustarCapsule();

        capsula.direction = 1;
        corpo.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        corpo.interpolation = RigidbodyInterpolation.Interpolate;
        yawAlvo = transform.eulerAngles.y;
    }

    private void OnDisable() => Parar();
    #endregion

    #region API para a IA
    public void SolicitarMovimento(Vector3 alvo, float rapidez)
    {
        if (!mover)
        {
            ultimaPosicaoComProgresso = corpo != null ? corpo.position : transform.position;
            tempoSemProgresso = 0f;
        }
        destino = alvo;
        velocidade = Mathf.Max(0f, rapidez);
        mover = velocidade > 0f;
    }

    public void Parar()
    {
        mover = false;
        tempoSemProgresso = 0f;
    }

    public void Olhar(Vector3 alvo)
    {
        Vector3 dir = alvo - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) yawAlvo = Quaternion.LookRotation(dir).eulerAngles.y;
    }

    /// <summary>Congela o corpo e libera o espaco: a animacao de morte assume.</summary>
    public void Morrer()
    {
        morto = true;
        Parar();
        if (corpo != null)
        {
            corpo.linearVelocity = Vector3.zero;
            corpo.angularVelocity = Vector3.zero;
            corpo.isKinematic = true;
        }
        if (capsula != null) capsula.enabled = false;
    }

    /// <summary>Projeta um ponto qualquer no chao (para pontos de patrulha).</summary>
    public bool TentarProjetarNoChao(Vector3 ponto, out Vector3 noChao)
    {
        noChao = ponto;
        if (!ProcurarSolo(ponto + Vector3.up * 2f, 4f, out RaycastHit solo)) return false;
        noChao = solo.point;
        return true;
    }
    #endregion

    #region Locomocao (fisica)
    private void FixedUpdate()
    {
        if (corpo == null || corpo.isKinematic || morto) return;

        corpo.MoveRotation(Quaternion.Slerp(corpo.rotation, Quaternion.Euler(0f, yawAlvo, 0f),
            velocidadeRotacao * Time.fixedDeltaTime));

        Vector3 pe = PontoNoPe();
        bool noSolo = ProcurarSolo(pe + Vector3.up * alturaSondaSolo, alturaSondaSolo + toleranciaSolo, out RaycastHit solo);
        Vector3 normal = noSolo ? solo.normal : Vector3.up;

        Vector3 horizontal = Vector3.zero;
        if (mover && noSolo)
        {
            Vector3 dir = destino - corpo.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > distanciaParada * distanciaParada)
            {
                dir.Normalize();
                if (!PassoSeguro(pe, dir)) dir = EscolherDesvio(pe, dir);
                horizontal = VelocidadeNoSolo(dir, normal, velocidade);
            }
        }

        AtualizarProgresso();

        // Sem contato com o solo, a gravidade continua sendo a unica aceleracao vertical.
        bool apoiado = noSolo && corpo.linearVelocity.y <= 0.5f;
        float vertical = apoiado ? horizontal.y : corpo.linearVelocity.y;
        corpo.linearVelocity = new Vector3(horizontal.x, vertical, horizontal.z);
    }

    private static Vector3 VelocidadeNoSolo(Vector3 direcao, Vector3 normal, float rapidez)
    {
        Vector3 tangente = Vector3.ProjectOnPlane(direcao, normal);
        return tangente.sqrMagnitude > 0.0001f ? tangente.normalized * rapidez : Vector3.zero;
    }
    #endregion

    #region Desvio de buracos e precipicios
    private bool PassoSeguro(Vector3 pe, Vector3 dir)
    {
        Vector3 origem = pe + dir * distanciaPassoSeguro + Vector3.up * alturaSondaSolo;
        return ProcurarSolo(origem, alturaSondaSolo + quedaMaxima, out _);
    }

    private Vector3 EscolherDesvio(Vector3 pe, Vector3 dir)
    {
        Vector3 esquerda = Quaternion.Euler(0f, -55f, 0f) * dir;
        if (PassoSeguro(pe, esquerda)) return esquerda;
        Vector3 direita = Quaternion.Euler(0f, 55f, 0f) * dir;
        return PassoSeguro(pe, direita) ? direita : Vector3.zero;
    }
    #endregion

    #region Rota bloqueada (progresso por deslocamento real)
    private void AtualizarProgresso()
    {
        if (!mover) { tempoSemProgresso = 0f; return; }

        Vector3 moveu = corpo.position - ultimaPosicaoComProgresso;
        moveu.y = 0f;
        if (moveu.sqrMagnitude >= deslocamentoMinimo * deslocamentoMinimo)
        {
            ultimaPosicaoComProgresso = corpo.position;
            tempoSemProgresso = 0f;
            return;
        }

        Vector3 ate = destino - corpo.position;
        ate.y = 0f;
        // Parada por ter chegado nao conta como bloqueio.
        tempoSemProgresso = ate.sqrMagnitude <= distanciaParada * distanciaParada
            ? 0f
            : tempoSemProgresso + Time.fixedDeltaTime;
    }
    #endregion

    #region Sondagem do solo
    private Vector3 PontoNoPe()
    {
        Bounds b = capsula.bounds;
        return new Vector3(b.center.x, b.min.y, b.center.z);
    }

    private bool ProcurarSolo(Vector3 origem, float profundidade, out RaycastHit solo)
    {
        solo = default;
        int n = Physics.RaycastNonAlloc(origem, Vector3.down, hits, profundidade, camadasSolo, QueryTriggerInteraction.Ignore);
        if (n == hits.Length) return false; // resultado incompleto: nao arriscar

        float menor = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = hits[i];
            Transform t = h.collider.transform;
            if (t == transform || t.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<StatusPlayer>() != null) continue;
            if (h.distance >= menor) continue;
            menor = h.distance;
            solo = h;
        }
        return solo.collider != null && solo.normal.y >= Mathf.Cos(inclinacaoMaxima * Mathf.Deg2Rad);
    }
    #endregion

    #region Ajuste da capsula pelo modelo
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
}
