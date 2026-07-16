using System.Collections.Generic;
using UnityEngine;

public static class GeometriaRuna
{
    public static float ComprimentoPolilinha(IReadOnlyList<Vector2> pontos)
    {
        if (pontos == null || pontos.Count < 2)
            return 0f;

        float comprimento = 0f;

        for (int i = 1; i < pontos.Count; i++)
            comprimento += Vector2.Distance(pontos[i - 1], pontos[i]);

        return comprimento;
    }

    public static Bounds2D CalcularBounds(IReadOnlyList<Vector2> pontos)
    {
        if (pontos == null || pontos.Count == 0)
            return new Bounds2D(Vector2.zero, Vector2.zero);

        Vector2 min = pontos[0];
        Vector2 max = pontos[0];

        for (int i = 1; i < pontos.Count; i++)
        {
            Vector2 p = pontos[i];
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        return new Bounds2D(min, max);
    }

    public static float AreaPoligono(IReadOnlyList<Vector2> pontos)
    {
        if (pontos == null || pontos.Count < 3)
            return 0f;

        float area = 0f;

        for (int i = 0; i < pontos.Count; i++)
        {
            Vector2 a = pontos[i];
            Vector2 b = pontos[(i + 1) % pontos.Count];
            area += a.x * b.y - b.x * a.y;
        }

        return Mathf.Abs(area) * 0.5f;
    }

    public static float DistanciaPontoSegmento(Vector2 ponto, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float denominador = Vector2.Dot(ab, ab);

        if (denominador <= 0.000001f)
            return Vector2.Distance(ponto, a);

        float t = Mathf.Clamp01(Vector2.Dot(ponto - a, ab) / denominador);
        Vector2 projecao = a + ab * t;
        return Vector2.Distance(ponto, projecao);
    }

    public static float DistanciaPontoPolilinha(Vector2 ponto, IReadOnlyList<Vector2> pontos, bool fechar)
    {
        if (pontos == null || pontos.Count == 0)
            return float.MaxValue;

        if (pontos.Count == 1)
            return Vector2.Distance(ponto, pontos[0]);

        float menor = float.MaxValue;

        for (int i = 1; i < pontos.Count; i++)
            menor = Mathf.Min(menor, DistanciaPontoSegmento(ponto, pontos[i - 1], pontos[i]));

        if (fechar)
            menor = Mathf.Min(menor, DistanciaPontoSegmento(ponto, pontos[pontos.Count - 1], pontos[0]));

        return menor;
    }

    public static float DistanciaSegmentos(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        if (SegmentosIntersectam(a1, a2, b1, b2))
            return 0f;

        float d1 = DistanciaPontoSegmento(a1, b1, b2);
        float d2 = DistanciaPontoSegmento(a2, b1, b2);
        float d3 = DistanciaPontoSegmento(b1, a1, a2);
        float d4 = DistanciaPontoSegmento(b2, a1, a2);
        return Mathf.Min(Mathf.Min(d1, d2), Mathf.Min(d3, d4));
    }

    public static float DistanciaPolilinhas(IReadOnlyList<Vector2> a, bool fecharA, IReadOnlyList<Vector2> b, bool fecharB)
    {
        if (a == null || b == null || a.Count < 2 || b.Count < 2)
            return float.MaxValue;

        float menor = float.MaxValue;
        int segmentosA = fecharA ? a.Count : a.Count - 1;
        int segmentosB = fecharB ? b.Count : b.Count - 1;

        for (int i = 0; i < segmentosA; i++)
        {
            Vector2 a1 = a[i];
            Vector2 a2 = a[(i + 1) % a.Count];

            for (int j = 0; j < segmentosB; j++)
            {
                Vector2 b1 = b[j];
                Vector2 b2 = b[(j + 1) % b.Count];
                menor = Mathf.Min(menor, DistanciaSegmentos(a1, a2, b1, b2));
            }
        }

        return menor;
    }

    public static bool SegmentosIntersectam(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        float d1 = Direcao(a1, a2, b1);
        float d2 = Direcao(a1, a2, b2);
        float d3 = Direcao(b1, b2, a1);
        float d4 = Direcao(b1, b2, a2);

        if (((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) &&
            ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f)))
            return true;

        const float eps = 0.000001f;
        return Mathf.Abs(d1) <= eps && PontoEmSegmento(a1, a2, b1) ||
               Mathf.Abs(d2) <= eps && PontoEmSegmento(a1, a2, b2) ||
               Mathf.Abs(d3) <= eps && PontoEmSegmento(b1, b2, a1) ||
               Mathf.Abs(d4) <= eps && PontoEmSegmento(b1, b2, a2);
    }

    public static bool TentarIntersecaoLinhas(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out Vector2 ponto, out float ta, out float tb)
    {
        ponto = Vector2.zero;
        ta = 0f;
        tb = 0f;

        Vector2 r = a2 - a1;
        Vector2 s = b2 - b1;
        float denominador = Cross(r, s);

        if (Mathf.Abs(denominador) < 0.000001f)
            return false;

        Vector2 diff = b1 - a1;
        ta = Cross(diff, s) / denominador;
        tb = Cross(diff, r) / denominador;
        ponto = a1 + r * ta;
        return true;
    }

    public static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    public static void SimplificarRdp(IReadOnlyList<Vector2> entrada, float epsilon, List<Vector2> saida)
    {
        saida.Clear();

        if (entrada == null || entrada.Count == 0)
            return;

        if (entrada.Count < 3 || epsilon <= 0f)
        {
            for (int i = 0; i < entrada.Count; i++)
                saida.Add(entrada[i]);

            return;
        }

        bool[] manter = new bool[entrada.Count];
        manter[0] = true;
        manter[entrada.Count - 1] = true;
        SimplificarRdpRecursivo(entrada, 0, entrada.Count - 1, epsilon, manter);

        for (int i = 0; i < entrada.Count; i++)
        {
            if (manter[i])
                saida.Add(entrada[i]);
        }
    }

    private static void SimplificarRdpRecursivo(IReadOnlyList<Vector2> pontos, int inicio, int fim, float epsilon, bool[] manter)
    {
        if (fim <= inicio + 1)
            return;

        float maiorDistancia = 0f;
        int indice = -1;

        for (int i = inicio + 1; i < fim; i++)
        {
            float distancia = DistanciaPontoSegmento(pontos[i], pontos[inicio], pontos[fim]);

            if (distancia > maiorDistancia)
            {
                maiorDistancia = distancia;
                indice = i;
            }
        }

        if (indice < 0 || maiorDistancia <= epsilon)
            return;

        manter[indice] = true;
        SimplificarRdpRecursivo(pontos, inicio, indice, epsilon, manter);
        SimplificarRdpRecursivo(pontos, indice, fim, epsilon, manter);
    }

    private static float Direcao(Vector2 a, Vector2 b, Vector2 c)
    {
        return Cross(c - a, b - a);
    }

    private static bool PontoEmSegmento(Vector2 a, Vector2 b, Vector2 p)
    {
        return p.x >= Mathf.Min(a.x, b.x) - 0.000001f &&
               p.x <= Mathf.Max(a.x, b.x) + 0.000001f &&
               p.y >= Mathf.Min(a.y, b.y) - 0.000001f &&
               p.y <= Mathf.Max(a.y, b.y) + 0.000001f;
    }

    public readonly struct Bounds2D
    {
        public readonly Vector2 min;
        public readonly Vector2 max;

        public Bounds2D(Vector2 min, Vector2 max)
        {
            this.min = min;
            this.max = max;
        }

        public Vector2 Centro => (min + max) * 0.5f;
        public Vector2 Tamanho => max - min;
        public float Largura => max.x - min.x;
        public float Altura => max.y - min.y;
        public float Diametro => Mathf.Max(Largura, Altura);
        public float Diagonal => Vector2.Distance(min, max);
    }
}
