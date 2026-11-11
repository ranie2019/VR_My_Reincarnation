using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// TopeiraDeteccao
// Campo de visao e memoria do alvo. Nao move nem ataca: so informa
// quem e o alvo, onde ele esta e se esta sendo visto.
// =====================================================================
[DisallowMultipleComponent]
public class TopeiraDeteccao : MonoBehaviour
{
    #region Configuracao
    [Header("Alvo")]
    [SerializeField] private string tagPlayer = "Player";

    [Header("Campo de visao")]
    [SerializeField, Min(0f)] private float raioVisao = 12f;
    [SerializeField, Range(1f, 360f)] private float anguloVisao = 200f;
    [SerializeField, Min(0f)] private float alturaOlhos = 0.6f;
    [Tooltip("Camadas que bloqueiam a visao (padrao: todas menos Ignore Raycast).")]
    [SerializeField] private LayerMask layersObstaculo = ~(1 << 2);

    [Header("Busca e memoria")]
    [SerializeField, Min(0.05f)] private float intervaloBusca = 0.25f;
    [SerializeField, Min(0f)] private float tempoMemoria = 1.5f;
    #endregion

    #region Estado publico
    private Transform alvo;
    private bool alvoVisivel;
    private float ultimaVezVisto = float.NegativeInfinity;

    public Transform Alvo => alvo;
    /// <summary>Alvo dentro do campo de visao e sem obstaculo agora.</summary>
    public bool AlvoVisivel => alvoVisivel;
    /// <summary>Visivel agora ou visto ha menos que o tempo de memoria.</summary>
    public bool PercebeAlvo => alvo != null && (alvoVisivel || Time.time - ultimaVezVisto <= tempoMemoria);
    public Vector3 PosicaoAlvo => alvo != null ? PosicaoJogador(alvo) : transform.position;
    public float RaioVisao => raioVisao;

    public void EsquecerAlvo()
    {
        alvo = null;
        alvoVisivel = false;
        ultimaVezVisto = float.NegativeInfinity;
    }
    #endregion

    #region Ciclo de vida
    private float proximaBusca;
    private readonly RaycastHit[] hitsVisao = new RaycastHit[32];

    private void OnDisable() => EsquecerAlvo();

    private void Update()
    {
        if (Time.time < proximaBusca) return;
        proximaBusca = Time.time + intervaloBusca;
        AtualizarAlvo();
    }

    private void OnValidate()
    {
        raioVisao = Mathf.Max(0f, raioVisao);
        alturaOlhos = Mathf.Max(0f, alturaOlhos);
    }
    #endregion

    #region Busca do alvo
    private void AtualizarAlvo()
    {
        if (alvo != null && !alvo.gameObject.activeInHierarchy) EsquecerAlvo();

        if (alvo == null) alvo = ProcurarJogadorVisivel();

        alvoVisivel = alvo != null && Visivel(alvo);
        if (alvoVisivel) ultimaVezVisto = Time.time;
        else if (alvo != null && !PercebeAlvo) EsquecerAlvo();
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
            if (d < menor && Visivel(c.transform)) { menor = d; melhor = c.transform; }
        }
        return melhor;
    }
    #endregion

    #region Cache compartilhado de jogadores por tag
    private sealed class CacheJogadores { public GameObject[] itens; public float proximaConsulta; }
    private static readonly Dictionary<string, CacheJogadores> cachePorTag = new Dictionary<string, CacheJogadores>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparCache() => cachePorTag.Clear();

    private static GameObject[] ObterJogadores(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return System.Array.Empty<GameObject>();

        if (!cachePorTag.TryGetValue(tag, out CacheJogadores cache))
        {
            cache = new CacheJogadores();
            cachePorTag.Add(tag, cache);
        }

        if (cache.itens == null || Time.time >= cache.proximaConsulta)
        {
            try { cache.itens = GameObject.FindGameObjectsWithTag(tag); }
            catch (UnityException) { cache.itens = System.Array.Empty<GameObject>(); } // tag nao existe
            cache.proximaConsulta = Time.time + 0.75f;
        }
        return cache.itens;
    }
    #endregion

    #region Posicao do jogador (corpo rastreado em VR)
    private Transform raizEmCache;
    private Transform corpoEmCache;

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
    #endregion

    #region Visibilidade (distancia, angulo e obstaculos)
    private Vector3 Olhos => transform.position + Vector3.up * alturaOlhos;

    private bool Visivel(Transform raiz)
    {
        Vector3 origem = Olhos;
        Vector3 dir = PosicaoJogador(raiz) - origem;
        float distancia = dir.magnitude;
        if (distancia > raioVisao) return false;

        Vector3 plano = dir; plano.y = 0f;
        Vector3 frente = transform.forward; frente.y = 0f;
        if (plano.sqrMagnitude > 0.0001f && frente.sqrMagnitude > 0.0001f &&
            Vector3.Angle(frente, plano) > anguloVisao * 0.5f) return false;

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
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 o = Olhos;
        Gizmos.DrawWireSphere(o, raioVisao);
        Vector3 f = transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) return;
        f.Normalize();
        Gizmos.DrawLine(o, o + Quaternion.Euler(0f, -anguloVisao * 0.5f, 0f) * f * raioVisao);
        Gizmos.DrawLine(o, o + Quaternion.Euler(0f, anguloVisao * 0.5f, 0f) * f * raioVisao);
    }
    #endregion
}
