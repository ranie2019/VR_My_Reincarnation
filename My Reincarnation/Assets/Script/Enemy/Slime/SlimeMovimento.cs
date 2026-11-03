using UnityEngine;

// Movimento opt-in dos slimes configurados; especies sem este componente mantem o caminho anterior.
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class SlimeMovimento : MonoBehaviour
{
    [Header("Solo (metros de mundo)")]
    [SerializeField] private LayerMask camadasSolo = ~0;
    [SerializeField, Range(1f, 60f)] private float inclinacaoMaxima = 45f;
    [SerializeField, Min(0.01f)] private float toleranciaSolo = 0.12f;
    [SerializeField, Min(0.01f)] private float toleranciaDesnivel = 0.18f;
    [SerializeField, Min(0.05f)] private float quedaMaxima = 0.6f;
    [SerializeField, Min(0.1f)] private float tempoRotaBloqueada = 2f;
    [SerializeField] private Collider colliderModelo;
    [Header("Investida propria do Vermelho")]
    [Tooltip("Habilita o perfil do Vermelho: arco de duracao fixa, animacoes e volume de respawn proprios.")]
    [SerializeField] private bool perfilVermelho;
    private bool controlandoArco, gravidadeAntesArco;
    public bool PreservarArcoInvestida => perfilVermelho;
    public float RaioMundial => Mathf.Min(capsula.bounds.extents.x, capsula.bounds.extents.z);
    public float AlturaMundial => capsula.bounds.size.y;
    public Vector3 CentroRelativo => capsula.bounds.center - transform.position;
    private readonly RaycastHit[] hits = new RaycastHit[24];
    private Rigidbody corpo;
    private CapsuleCollider capsula;
    private Vector3 destino;
    private float velocidade;
    private bool mover, suspenso, morto;
    private Vector3 normalSolo = Vector3.up;
    private float tempoSemProgresso;
    private float melhorDistancia = float.PositiveInfinity;
    private float yawAlvo;
    public Vector3 CentroCorpo => capsula != null ? capsula.bounds.center : transform.position + Vector3.up * 0.3f;
    public bool RotaBloqueada => tempoSemProgresso >= tempoRotaBloqueada;

    private void Awake()
    {
        corpo = GetComponent<Rigidbody>(); capsula = GetComponent<CapsuleCollider>();
        // Mantem o fundo do modelo na mesma altura, substituindo apenas a forma fisica.
        if (colliderModelo != null && colliderModelo.enabled)
        {
            Bounds b = colliderModelo.bounds;
            float escalaRaio = Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)));
            float escalaAltura = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
            float raio = Mathf.Max(0.05f, Mathf.Min(b.extents.x, b.extents.z) * 0.9f);
            float altura = Mathf.Max(raio * 2f, b.size.y);
            capsula.radius = raio / escalaRaio; capsula.height = altura / escalaAltura;
            capsula.center = transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + altura * 0.5f, b.center.z));
            colliderModelo.enabled = false;
        }
        capsula.direction = 1; capsula.enabled = true;
        corpo.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        corpo.interpolation = RigidbodyInterpolation.Interpolate;
        yawAlvo = transform.eulerAngles.y;
    }

    public bool AlturaDestinoCompativel(Vector3 alvo)
        => Mathf.Abs(alvo.y - transform.position.y) <= Mathf.Max(quedaMaxima, capsula.bounds.size.y + toleranciaDesnivel);
    public bool PodeSaltarPara(Vector3 alvo)
    {
        Bounds b = capsula.bounds;
        return ProcurarSolo(new Vector3(alvo.x, b.min.y + toleranciaDesnivel, alvo.z), toleranciaDesnivel + quedaMaxima, out _);
    }
    public void SolicitarMovimento(Vector3 alvo, float rapidez)
    {
        if (!mover || (alvo - destino).sqrMagnitude > 0.25f)
        { melhorDistancia = float.PositiveInfinity; tempoSemProgresso = 0f; }
        destino = alvo; velocidade = Mathf.Max(0f, rapidez); mover = velocidade > 0f;
    }
    public void Parar() { mover = false; tempoSemProgresso = 0f; }
    public void Olhar(Vector3 alvo)
    {
        Vector3 dir = alvo - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) yawAlvo = Quaternion.LookRotation(dir).eulerAngles.y;
    }
    public void Suspender(bool valor)
    {
        if (!valor && controlandoArco)
        {
            corpo.useGravity = gravidadeAntesArco;
            controlandoArco = false;
            corpo.linearVelocity = new Vector3(0f, Mathf.Min(0f, corpo.linearVelocity.y), 0f);
        }
        suspenso = valor; mover = false; tempoSemProgresso = 0f;
        if (corpo != null && !corpo.isKinematic)
            corpo.linearVelocity = new Vector3(0f, corpo.linearVelocity.y, 0f);
    }
    public void IniciarArco(Vector3 direcao)
    {
        Suspender(true);
        gravidadeAntesArco = corpo.useGravity; controlandoArco = true;
        corpo.useGravity = false; corpo.linearVelocity = Vector3.zero; corpo.angularVelocity = Vector3.zero;
        corpo.MoveRotation(Quaternion.LookRotation(direcao, Vector3.up));
    }
    internal static Vector3 CalcularArco(Vector3 inicio, Vector3 direcao, float distancia, float altura, float progresso)
    {
        float t = Mathf.Clamp01(progresso);
        return inicio + direcao * (distancia * t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * altura);
    }
    public bool MoverArco(Vector3 alvo, out Collider bloqueio)
    {
        bloqueio = null;
        Vector3 delta = alvo - corpo.position;
        float distancia = delta.magnitude;
        if (distancia <= 0.0001f) return false;
        Bounds b = capsula.bounds;
        float raio = Mathf.Min(b.extents.x, b.extents.z) * 0.95f;
        Vector3 baixo = b.center; baixo.y = b.min.y + raio;
        Vector3 alto = b.center; alto.y = Mathf.Max(baixo.y, b.max.y - raio);
        int n = Physics.CapsuleCastNonAlloc(baixo, alto, raio, delta.normalized, hits, distancia + 0.02f, ~0, QueryTriggerInteraction.Ignore);
        if (n == hits.Length) return true;
        float menor = distancia + 0.02f;
        for (int i = 0; i < n; i++)
        {
            var hit = hits[i];
            if (Ignorar(hit.collider) || (hit.distance <= 0.001f && Vector3.Dot(delta, hit.normal) >= 0f)) continue;
            if (hit.distance < menor) { menor = hit.distance; bloqueio = hit.collider; }
        }
        corpo.MovePosition(bloqueio != null ? corpo.position + delta.normalized * Mathf.Max(0f, menor - 0.02f) : alvo);
        return bloqueio != null;
    }
    public void Reiniciar() { morto = false; Suspender(false); melhorDistancia = float.PositiveInfinity; }
    public void Morrer() { morto = true; Suspender(true); }

    private bool Ignorar(Collider col)
    {
        return col == null || col.transform.IsChildOf(transform) || col.transform == transform;
    }
    private bool ProcurarSolo(Vector3 centro, float profundidade, out RaycastHit solo)
    {
        solo = default;
        int n = Physics.RaycastNonAlloc(centro, Vector3.down, hits, profundidade, camadasSolo, QueryTriggerInteraction.Ignore);
        if (n == hits.Length) return false; // Sem resultado completo, nao arriscar um salto/queda.
        float menor = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (Ignorar(h.collider) || h.collider.GetComponentInParent<StatusPlayer>() != null ||
                h.collider.GetComponentInParent<SlimeIA>() != null || h.distance >= menor) continue;
            menor = h.distance; solo = h;
        }
        return solo.collider != null && SoloTransitavel(solo.normal, inclinacaoMaxima);
    }
    internal static bool SoloTransitavel(Vector3 normal, float limite)
        => normal.y >= Mathf.Cos(Mathf.Clamp(limite, 1f, 60f) * Mathf.Deg2Rad);
    internal static Vector3 VelocidadeNoSolo(Vector3 direcao, Vector3 normal, float rapidez)
    {
        Vector3 tangente = Vector3.ProjectOnPlane(direcao, normal);
        return tangente.sqrMagnitude > 0.0001f ? tangente.normalized * Mathf.Max(0f, rapidez) : Vector3.zero;
    }

    private bool PassoSeguro(Vector3 dir, float distancia)
    {
        Bounds b = capsula.bounds;
        Vector3 origem = new Vector3(b.center.x, b.min.y + toleranciaDesnivel, b.center.z) + dir * distancia;
        if (!ProcurarSolo(origem, toleranciaDesnivel + quedaMaxima, out var frente)) return false;
        // Um cast acima do pequeno desnivel nao confunde a ondulacao com uma parede.
        float raio = Mathf.Max(0.03f, Mathf.Min(b.extents.x, b.extents.z) * 0.8f);
        Vector3 baixo = new Vector3(b.center.x, b.min.y + raio + toleranciaDesnivel, b.center.z);
        Vector3 alto = new Vector3(b.center.x, Mathf.Max(baixo.y, b.max.y - raio), b.center.z);
        int n = Physics.CapsuleCastNonAlloc(baixo, alto, raio, dir, hits, distancia, ~0, QueryTriggerInteraction.Ignore);
        if (n == hits.Length) return false;
        for (int i = 0; i < n; i++)
            if (!Ignorar(hits[i].collider) && !SoloTransitavel(hits[i].normal, inclinacaoMaxima)) return false;
        return true;
    }

    private void FixedUpdate()
    {
        if (corpo == null || corpo.isKinematic || suspenso || morto) return;
        corpo.MoveRotation(Quaternion.Slerp(corpo.rotation, Quaternion.Euler(0f, yawAlvo, 0f), 8f * Time.fixedDeltaTime));
        Bounds b = capsula.bounds;
        bool noSolo = ProcurarSolo(new Vector3(b.center.x, b.min.y + toleranciaDesnivel, b.center.z),
            toleranciaDesnivel + toleranciaSolo, out var solo);
        normalSolo = noSolo ? solo.normal : Vector3.up;
        Vector3 dir = destino - corpo.position; dir.y = 0f;
        Vector3 horizontal = Vector3.zero;
        if (mover && dir.sqrMagnitude > 0.0025f && noSolo)
        {
            dir.Normalize(); float passo = Mathf.Max(0.15f, velocidade * Time.fixedDeltaTime + 0.1f);
            if (!PassoSeguro(dir, passo))
            {
                Vector3 esquerda = Quaternion.Euler(0f, -55f, 0f) * dir;
                Vector3 direita = Quaternion.Euler(0f, 55f, 0f) * dir;
                if (PassoSeguro(esquerda, passo)) dir = esquerda;
                else if (PassoSeguro(direita, passo)) dir = direita;
                else dir = Vector3.zero;
            }
            horizontal = VelocidadeNoSolo(dir, normalSolo, velocidade);

        }
        if (mover)
        {
            Vector3 ateDestino = destino - corpo.position; ateDestino.y = 0f;
            float distancia = ateDestino.magnitude;
            if (distancia < melhorDistancia - 0.02f) { melhorDistancia = distancia; tempoSemProgresso = 0f; }
            else tempoSemProgresso += Time.fixedDeltaTime;
        }
        else tempoSemProgresso = 0f;
        // Sem contato, conserva a velocidade vertical: a gravidade continua sendo a unica aceleracao.
        bool apoiado = noSolo && solo.distance - toleranciaDesnivel <= 0.025f && corpo.linearVelocity.y <= 0.5f;
        float vertical = apoiado ? horizontal.y : corpo.linearVelocity.y;
        corpo.linearVelocity = new Vector3(horizontal.x, vertical, horizontal.z);
    }

}
