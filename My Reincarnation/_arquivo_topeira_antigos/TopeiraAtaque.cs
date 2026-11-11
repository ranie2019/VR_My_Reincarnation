using System.Collections;
using UnityEngine;

// =====================================================================
// TopeiraAtaque
// Ataque corpo a corpo: cooldown, tempo ate o impacto, escudo e dano.
// Nao anda e nao anima: a TopeiraIA chama Iniciar() e acompanha EmAndamento.
// Implementa IDano para ser reconhecida como fonte de dano de inimigo
// (StatusPlayer e Escudo ignoram o contato fisico e deixam este fluxo resolver).
// =====================================================================
[DisallowMultipleComponent]
public class TopeiraAtaque : MonoBehaviour, IDano
{
    #region Configuracao
    [Header("Dano")]
    [SerializeField, Min(0f)] private float danoAtaque = 4f;

    [Header("Alcance")]
    [Tooltip("Distancia (plana) ao alvo em que a IA comeca o ataque.")]
    [SerializeField, Min(0.1f)] private float distanciaAtaque = 1.4f;
    [SerializeField, Min(0.1f)] private float alcanceImpacto = 1.8f;
    [SerializeField, Min(0.05f)] private float raioImpacto = 0.5f;
    [SerializeField, Min(0f)] private float alturaImpacto = 0.5f;
    [SerializeField] private LayerMask layersAtaque = ~0;

    [Header("Tempos (clip Ataque dura ~1.9 s)")]
    [Tooltip("Segundos depois do inicio do ataque em que o golpe atinge.")]
    [SerializeField, Min(0f)] private float atrasoImpacto = 0.8f;
    [SerializeField, Min(0.1f)] private float duracaoAtaque = 1.9f;
    [SerializeField, Min(0f)] private float cooldownAtaque = 1f;
    #endregion

    #region Estado publico
    private bool emAndamento;
    private float proximoAtaque;
    private Coroutine rotina;
    private readonly RaycastHit[] hits = new RaycastHit[16];
    private readonly Collider[] sobrepostos = new Collider[16];

    public bool EmAndamento => emAndamento;
    public float DistanciaAtaque => distanciaAtaque;
    public bool PodeAtacar => !emAndamento && Time.time >= proximoAtaque;
    #endregion

    #region IDano
    public float ObterDano() => danoAtaque;
    public GameObject ObterDono() => gameObject;
    #endregion

    #region Ciclo de vida
    private void OnDisable() => Cancelar();

    private void OnValidate()
    {
        alcanceImpacto = Mathf.Max(0.1f, alcanceImpacto);
        duracaoAtaque = Mathf.Max(atrasoImpacto, duracaoAtaque);
    }
    #endregion

    #region Controle do ataque
    public bool Iniciar(Transform alvo)
    {
        if (alvo == null || !PodeAtacar) return false;
        rotina = StartCoroutine(RotinaAtaque(alvo));
        return true;
    }

    /// <summary>Interrompe um ataque em andamento (dano recebido, morte, desativacao).</summary>
    public void Cancelar()
    {
        if (rotina != null) StopCoroutine(rotina);
        rotina = null;
        if (emAndamento) proximoAtaque = Time.time + cooldownAtaque;
        emAndamento = false;
    }

    private IEnumerator RotinaAtaque(Transform alvo)
    {
        emAndamento = true;

        yield return new WaitForSeconds(atrasoImpacto);
        if (alvo != null) ResolverImpacto(alvo);

        yield return new WaitForSeconds(Mathf.Max(0f, duracaoAtaque - atrasoImpacto));

        emAndamento = false;
        rotina = null;
        proximoAtaque = Time.time + cooldownAtaque;
    }
    #endregion

    #region Impacto
    private void ResolverImpacto(Transform alvo)
    {
        Vector3 origem = transform.position + Vector3.up * alturaImpacto;
        Vector3 direcao = alvo.position - transform.position;
        direcao.y = 0f;
        direcao = direcao.sqrMagnitude > 0.0001f ? direcao.normalized : transform.forward;

        Collider contato = null;
        float menor = float.PositiveInfinity;

        // Alvo ja encostado: o SphereCast nao detecta colliders em que ja comeca dentro.
        int dentro = Physics.OverlapSphereNonAlloc(origem, raioImpacto, sobrepostos, layersAtaque, QueryTriggerInteraction.Ignore);
        if (dentro == sobrepostos.Length) return;
        for (int i = 0; i < dentro; i++)
        {
            Collider c = sobrepostos[i];
            if (c == null || PertenceAEsta(c) || Escudo.IgnorarNoAtaque(c, alvo)) continue;
            float d = Vector3.Distance(c.ClosestPoint(origem), origem);
            if (d < menor) { menor = d; contato = c; }
        }

        int n = Physics.SphereCastNonAlloc(origem, raioImpacto, direcao, hits, alcanceImpacto,
            layersAtaque, QueryTriggerInteraction.Ignore);
        if (n == hits.Length) return;
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (c == null || PertenceAEsta(c) || Escudo.IgnorarNoAtaque(c, alvo)) continue;
            if (hits[i].distance < menor) { menor = hits[i].distance; contato = c; }
        }

        if (contato != null) AplicarNoContato(contato, alvo);
    }

    private void AplicarNoContato(Collider contato, Transform alvo)
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
    #endregion

    #region Pertencimento de colliders
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

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 origem = transform.position + Vector3.up * alturaImpacto;
        Gizmos.DrawWireSphere(origem, distanciaAtaque);
        Gizmos.DrawWireSphere(origem + transform.forward * alcanceImpacto, raioImpacto);
    }
    #endregion
}
