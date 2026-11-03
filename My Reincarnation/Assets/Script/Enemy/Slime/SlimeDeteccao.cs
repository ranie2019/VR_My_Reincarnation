using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class SlimeDeteccao : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float intervaloVisao = 0.15f;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private Transform jogador, corpoVR;
    private float proximaConsulta;
    private bool ultimaVisao;
    private Transform ultimoAlvo;
    private Vector3 ultimoDestino;

    private sealed class CacheJogadores { public GameObject[] itens; public float consulta; }
    private static readonly Dictionary<string, CacheJogadores> jogadoresPorTag = new Dictionary<string, CacheJogadores>();
    public Transform ProcurarJogador(string tagJogador, Vector3 origem, Vector3 frente, float raio, float angulo, LayerMask obstaculos)
    {
        if (string.IsNullOrWhiteSpace(tagJogador)) return null;
        if (!jogadoresPorTag.TryGetValue(tagJogador, out var cache))
        { cache = new CacheJogadores(); jogadoresPorTag.Add(tagJogador, cache); }
        if (cache.itens == null || Time.time >= cache.consulta || Time.time < cache.consulta - 0.75f)
        {
            try { cache.itens = GameObject.FindGameObjectsWithTag(tagJogador); }
            catch (UnityException) { cache.itens = System.Array.Empty<GameObject>(); }
            cache.consulta = Time.time + 0.75f;
        }
        Transform melhor = null; float distancia = float.PositiveInfinity;
        foreach (var candidato in cache.itens)
        {
            if (candidato == null || !candidato.activeInHierarchy) continue;
            float d = (PosicaoJogador(candidato.transform) - origem).sqrMagnitude;
            if (d < distancia && Visivel(candidato.transform, origem, frente, raio, angulo, obstaculos))
            { melhor = candidato.transform; distancia = d; }
        }
        return melhor;
    }
    public Vector3 PosicaoJogador(Transform raiz)
    {
        if (raiz == null) return transform.position;
        if (jogador != raiz || corpoVR == null)
        {
            jogador = raiz;
            var avatar = raiz.GetComponent<AvatarBody>();
            corpoVR = avatar != null ? avatar.corpoAvatar : null;
            if (corpoVR == null)
            {
                var cam = raiz.GetComponentInChildren<Camera>();
                corpoVR = cam != null ? cam.transform : raiz;
            }
        }
        return corpoVR.position;
    }
    public bool Visivel(Transform raiz, Vector3 origem, Vector3 frente, float raio, float angulo, LayerMask obstaculos)
    {
        if (raiz == null) return false;
        Vector3 destino = PosicaoJogador(raiz);
        if (ultimoAlvo == raiz && Time.time < proximaConsulta && (destino - ultimoDestino).sqrMagnitude < 0.04f) return ultimaVisao;
        ultimoAlvo = raiz; ultimoDestino = destino; proximaConsulta = Time.time + intervaloVisao;
        Vector3 dir = destino - origem;
        Vector3 plano = dir; plano.y = 0f;
        ultimaVisao = dir.sqrMagnitude <= raio * raio &&
            (plano.sqrMagnitude < 0.0001f || Vector3.Angle(frente, plano) <= angulo * 0.5f);
        if (!ultimaVisao || dir.sqrMagnitude < 0.0001f) return ultimaVisao;
        int n = Physics.RaycastNonAlloc(origem, dir.normalized, hits, dir.magnitude, obstaculos, QueryTriggerInteraction.Ignore);
        if (n == hits.Length) return ultimaVisao = false;
        for (int i = 0; i < n; i++)
        {
            var col = hits[i].collider;
            if (col == null || col.transform.IsChildOf(transform) || col.transform == transform ||
                col.transform.IsChildOf(raiz) || col.transform == raiz) continue;
            ultimaVisao = false; break;
        }
        return ultimaVisao;
    }
}
