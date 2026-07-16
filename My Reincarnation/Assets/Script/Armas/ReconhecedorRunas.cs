using System.Collections.Generic;
using UnityEngine;

public class ReconhecedorRunas
{
    private const string IdCirculoBolaDeFogo = ResultadoReconhecimentoRuna.IdCirculoBolaDeFogo;
    private const string IdRunaComplexa = ResultadoReconhecimentoRuna.IdCirculoMaisQuatroCirculos;

    private readonly List<Vector3> pontos3D = new();
    private readonly List<List<Vector2>> tracosProjetados = new();
    private readonly List<Traco2D> tracos = new();
    private readonly List<Componente2D> componentes = new();
    private readonly List<CirculoInfo> circulos = new();
    private readonly List<LinhaInfo> linhas = new();
    private readonly List<Vector2> bufferSimplificacao = new();

    public ResultadoReconhecimentoRuna Analisar(IReadOnlyList<List<Vector3>> pontosDosTracos, ConfiguracaoReconhecimentoRuna config)
    {
        if (!config.usarReconhecimentoRunico)
            return ResultadoReconhecimentoRuna.CriarIncompleto("Reconhecimento runico desativado.");

        LimparTemporarios();

        if (!ProjetarParaPlano2D(pontosDosTracos, out float escalaDesenho))
            return ResultadoReconhecimentoRuna.CriarIncompleto("Pontos insuficientes para calcular plano da runa.");

        PrepararTracos2D(escalaDesenho, config);

        if (tracos.Count == 0)
            return ResultadoReconhecimentoRuna.CriarIncompleto("Poucos tracos relevantes para reconhecer a runa.");

        float toleranciaConexao = Mathf.Max(config.margemMinimaAbsoluta, escalaDesenho * config.toleranciaConexaoRelativa);
        ConstruirComponentes(toleranciaConexao);
        ClassificarComponentes(config, escalaDesenho);

        ResultadoReconhecimentoRuna resultado = new ResultadoReconhecimentoRuna
        {
            reconhecida = false,
            idRuna = string.Empty,
            motivo = "Runa incompleta ou invalida.",
            quantidadeCirculosEncontrados = circulos.Count,
            quantidadeLinhasEncontradas = linhas.Count
        };

        switch (config.modoReconhecimentoRuna)
        {
            case ModoReconhecimentoRuna.CirculoSimplesBolaDeFogo:
                if (TentarReconhecerCirculoBolaDeFogo(config, escalaDesenho, resultado))
                    MarcarRunaReconhecida(resultado, IdCirculoBolaDeFogo);
                break;

            case ModoReconhecimentoRuna.RunaComplexa:
                if (TentarReconhecerCirculoMaisQuatroCirculos(config, escalaDesenho, resultado))
                    MarcarRunaReconhecida(resultado, IdRunaComplexa);
                break;

            case ModoReconhecimentoRuna.Todos:
                if (TentarReconhecerCirculoMaisQuatroCirculos(config, escalaDesenho, resultado))
                {
                    MarcarRunaReconhecida(resultado, IdRunaComplexa);
                }
                else if (TentarReconhecerCirculoBolaDeFogo(config, escalaDesenho, resultado))
                {
                    resultado.reconhecida = false;
                    resultado.idRuna = IdCirculoBolaDeFogo;
                    resultado.motivo = "Circulo simples detectado; aguardando possiveis tracos adicionais.";
                }
                break;
        }

        return resultado;
    }

    private void LimparTemporarios()
    {
        pontos3D.Clear();
        tracosProjetados.Clear();
        tracos.Clear();
        componentes.Clear();
        circulos.Clear();
        linhas.Clear();
        bufferSimplificacao.Clear();
    }

    private bool ProjetarParaPlano2D(IReadOnlyList<List<Vector3>> pontosDosTracos, out float escalaDesenho)
    {
        escalaDesenho = 0f;

        if (pontosDosTracos == null)
            return false;

        Vector3 centro = Vector3.zero;

        for (int i = 0; i < pontosDosTracos.Count; i++)
        {
            List<Vector3> traco = pontosDosTracos[i];

            if (traco == null)
                continue;

            for (int j = 0; j < traco.Count; j++)
            {
                pontos3D.Add(traco[j]);
                centro += traco[j];
            }
        }

        if (pontos3D.Count < 6)
            return false;

        centro /= pontos3D.Count;

        Vector3 eixoU = Vector3.zero;
        float maiorDistancia = 0f;

        for (int i = 0; i < pontos3D.Count; i++)
        {
            Vector3 candidato = pontos3D[i] - centro;
            float distancia = candidato.sqrMagnitude;

            if (distancia > maiorDistancia)
            {
                maiorDistancia = distancia;
                eixoU = candidato;
            }
        }

        if (maiorDistancia < 0.000001f)
            return false;

        eixoU.Normalize();

        Vector3 normal = Vector3.zero;
        float maiorArea = 0f;

        for (int i = 0; i < pontos3D.Count; i++)
        {
            Vector3 candidato = pontos3D[i] - centro;
            Vector3 cruz = Vector3.Cross(eixoU, candidato);
            float area = cruz.sqrMagnitude;

            if (area > maiorArea)
            {
                maiorArea = area;
                normal = cruz;
            }
        }

        if (maiorArea < 0.000001f)
            return false;

        normal.Normalize();
        Vector3 eixoV = Vector3.Cross(normal, eixoU).normalized;

        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;
        bool boundsIniciado = false;

        for (int i = 0; i < pontosDosTracos.Count; i++)
        {
            List<Vector3> traco3D = pontosDosTracos[i];
            List<Vector2> traco2D = new List<Vector2>();
            tracosProjetados.Add(traco2D);

            if (traco3D == null)
                continue;

            for (int j = 0; j < traco3D.Count; j++)
            {
                Vector3 relativo = traco3D[j] - centro;
                Vector2 p = new Vector2(Vector3.Dot(relativo, eixoU), Vector3.Dot(relativo, eixoV));
                traco2D.Add(p);

                if (!boundsIniciado)
                {
                    min = p;
                    max = p;
                    boundsIniciado = true;
                }
                else
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            }
        }

        escalaDesenho = Vector2.Distance(min, max);
        return escalaDesenho > 0.0001f;
    }

    private void PrepararTracos2D(float escalaDesenho, ConfiguracaoReconhecimentoRuna config)
    {
        float distanciaMinima = Mathf.Max(config.margemMinimaAbsoluta * 0.25f, escalaDesenho * 0.0025f);
        float epsilonSimplificacao = Mathf.Max(config.margemMinimaAbsoluta * 0.35f, escalaDesenho * 0.01f);

        for (int i = 0; i < tracosProjetados.Count; i++)
        {
            List<Vector2> projetado = tracosProjetados[i];

            if (projetado == null || projetado.Count < 2)
                continue;

            List<Vector2> limpo = new List<Vector2>();
            Vector2 ultimo = projetado[0];
            limpo.Add(ultimo);

            for (int j = 1; j < projetado.Count; j++)
            {
                Vector2 p = projetado[j];

                if (Vector2.Distance(ultimo, p) < distanciaMinima)
                    continue;

                limpo.Add(p);
                ultimo = p;
            }

            if (limpo.Count < 2)
                continue;

            GeometriaRuna.SimplificarRdp(limpo, epsilonSimplificacao, bufferSimplificacao);

            if (bufferSimplificacao.Count < 2)
                continue;

            List<Vector2> pontosFinais = new List<Vector2>(bufferSimplificacao);
            float comprimento = GeometriaRuna.ComprimentoPolilinha(pontosFinais);

            if (comprimento < Mathf.Max(config.margemMinimaAbsoluta, escalaDesenho * config.comprimentoMinimoElementoRelativo * 0.25f))
                continue;

            tracos.Add(new Traco2D(i, pontosFinais, comprimento));
        }
    }

    private void ConstruirComponentes(float toleranciaConexao)
    {
        int n = tracos.Count;
        int[] pai = new int[n];

        for (int i = 0; i < n; i++)
            pai[i] = i;

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (ExtremidadesConectadas(tracos[i], tracos[j], toleranciaConexao))
                    Unir(pai, i, j);
            }
        }

        for (int i = 0; i < n; i++)
        {
            int raiz = Encontrar(pai, i);
            Componente2D componente = null;

            for (int j = 0; j < componentes.Count; j++)
            {
                if (componentes[j].raiz == raiz)
                {
                    componente = componentes[j];
                    break;
                }
            }

            if (componente == null)
            {
                componente = new Componente2D(raiz);
                componentes.Add(componente);
            }

            componente.tracos.Add(tracos[i]);
        }

        for (int i = 0; i < componentes.Count; i++)
        {
            componentes[i].pontosOrdenados = OrdenarPontosDoComponente(componentes[i].tracos, toleranciaConexao);
            componentes[i].comprimento = GeometriaRuna.ComprimentoPolilinha(componentes[i].pontosOrdenados);
        }
    }

    private void ClassificarComponentes(ConfiguracaoReconhecimentoRuna config, float escalaDesenho)
    {
        for (int i = 0; i < componentes.Count; i++)
        {
            Componente2D componente = componentes[i];

            if (componente.pontosOrdenados == null || componente.pontosOrdenados.Count < 2)
                continue;

            if (TentarCriarCirculo(componente, config, escalaDesenho, out CirculoInfo circulo))
            {
                circulos.Add(circulo);
                continue;
            }

            if (TentarCriarLinha(componente, config, escalaDesenho, out LinhaInfo linha))
                linhas.Add(linha);
        }
    }

    private bool TentarReconhecerCirculoMaisQuatroCirculos(ConfiguracaoReconhecimentoRuna config, float escalaDesenho, ResultadoReconhecimentoRuna resultado)
    {
        if (tracos.Count < 3)
        {
            resultado.motivo = "Poucos tracos relevantes para reconhecer a runa complexa.";
            CalcularPontuacao(resultado);
            return false;
        }

        CirculoInfo externo = null;
        float maiorArea = 0f;

        for (int i = 0; i < circulos.Count; i++)
        {
            CirculoInfo circulo = circulos[i];

            if (circulo.diametro < config.diametroMinimoRuna)
                continue;

            if (circulo.area > maiorArea)
            {
                maiorArea = circulo.area;
                externo = circulo;
            }
        }

        resultado.circuloExternoValido = externo != null;

        if (externo == null)
        {
            resultado.motivo = "Circulo externo grande e fechado nao encontrado.";
            CalcularPontuacao(resultado);
            return false;
        }

        float margemFinal = Mathf.Max(config.margemMinimaAbsoluta, externo.diametro * config.margemMinimaEntreElementosRelativa);
        List<CirculoInfo> internos = ColetarCirculosInternos(externo, config, margemFinal);
        bool quatroCirculosInternos = internos.Count == 4;

        LinhaInfo linhaA;
        LinhaInfo linhaB;
        Vector2 intersecaoMais;
        resultado.sinalMaisValido = TentarEncontrarSinalMais(externo, config, margemFinal, out linhaA, out linhaB, out intersecaoMais);

        if (!resultado.sinalMaisValido)
        {
            resultado.motivo = "Sinal de mais valido nao encontrado.";
            CalcularPontuacao(resultado);
            return false;
        }

        resultado.elementosContidosNoCirculoExterno = ElementosDentroDoCirculoExterno(externo, internos, linhaA, linhaB, margemFinal);
        resultado.quadrantesValidos = quatroCirculosInternos && CirculosEmQuatroQuadrantes(internos, linhaA, linhaB, intersecaoMais, margemFinal);
        resultado.separacaoValida = quatroCirculosInternos &&
                                    resultado.elementosContidosNoCirculoExterno &&
                                    SeparacaoEntreElementosValida(externo, internos, linhaA, linhaB, margemFinal);

        CalcularPontuacao(resultado, quatroCirculosInternos);

        if (!quatroCirculosInternos)
        {
            resultado.motivo = $"Esperados 4 circulos internos fechados; encontrados {internos.Count}.";
            return false;
        }

        if (!resultado.elementosContidosNoCirculoExterno)
        {
            resultado.motivo = "Nem todos os elementos principais estao dentro do circulo externo.";
            return false;
        }

        if (!resultado.quadrantesValidos)
        {
            resultado.motivo = "Os quatro circulos internos nao ocupam quadrantes diferentes.";
            return false;
        }

        if (!resultado.separacaoValida)
        {
            resultado.motivo = "Elementos da runa estao encostando ou cruzando.";
            return false;
        }

        if (resultado.pontuacao < config.pontuacaoMinima)
        {
            resultado.motivo = $"Pontuacao insuficiente: {resultado.pontuacao:0.00}.";
            return false;
        }

        return true;
    }

    private bool TentarReconhecerCirculoBolaDeFogo(ConfiguracaoReconhecimentoRuna config, float escalaDesenho, ResultadoReconhecimentoRuna resultado)
    {
        CirculoInfo circuloPrincipal = null;
        float maiorArea = 0f;

        for (int i = 0; i < circulos.Count; i++)
        {
            CirculoInfo circulo = circulos[i];

            if (circulo.diametro < config.diametroMinimoRuna)
                continue;

            if (circulo.area > maiorArea)
            {
                maiorArea = circulo.area;
                circuloPrincipal = circulo;
            }
        }

        resultado.circuloExternoValido = circuloPrincipal != null;

        if (circuloPrincipal == null)
        {
            resultado.pontuacao = circulos.Count > 0 ? 0.35f : 0f;
            resultado.motivo = componentes.Count > 0 ? "Circulo ainda esta aberto." : "Circulo fechado nao encontrado.";
            return false;
        }

        if (ExistemElementosExtrasRelevantes(circuloPrincipal, config, escalaDesenho))
        {
            resultado.pontuacao = 0.70f;
            resultado.motivo = "Circulo possui elementos extras relevantes.";
            return false;
        }

        resultado.pontuacao = 1f;
        resultado.motivo = "Circulo simples reconhecido.";
        return true;
    }

    private bool ExistemElementosExtrasRelevantes(CirculoInfo circuloPrincipal, ConfiguracaoReconhecimentoRuna config, float escalaDesenho)
    {
        float limiteExtra = Mathf.Max(
            config.margemMinimaAbsoluta * 2f,
            Mathf.Min(escalaDesenho, circuloPrincipal.diametro) * config.comprimentoMinimoElementoRelativo * 1.5f);

        for (int i = 0; i < componentes.Count; i++)
        {
            Componente2D componente = componentes[i];

            if (componente == null || componente.pontosOrdenados == null)
                continue;

            if (ReferenceEquals(componente.pontosOrdenados, circuloPrincipal.pontos))
                continue;

            if (componente.comprimento >= limiteExtra)
                return true;
        }

        return false;
    }

    private static void MarcarRunaReconhecida(ResultadoReconhecimentoRuna resultado, string idRuna)
    {
        resultado.reconhecida = true;
        resultado.idRuna = idRuna;
        resultado.motivo = "Runa reconhecida.";
    }

    private List<CirculoInfo> ColetarCirculosInternos(CirculoInfo externo, ConfiguracaoReconhecimentoRuna config, float margemFinal)
    {
        List<CirculoInfo> internos = new List<CirculoInfo>();
        float diametroMinimoInterno = Mathf.Max(config.margemMinimaAbsoluta * 2f, externo.diametro * config.comprimentoMinimoElementoRelativo);

        for (int i = 0; i < circulos.Count; i++)
        {
            CirculoInfo circulo = circulos[i];

            if (ReferenceEquals(circulo, externo))
                continue;

            if (circulo.diametro < diametroMinimoInterno)
                continue;

            if (circulo.diametro >= externo.diametro * 0.65f)
                continue;

            if (!PontoDentroDoCirculoAproximado(externo, circulo.centro, margemFinal))
                continue;

            internos.Add(circulo);
        }

        return internos;
    }

    private bool TentarEncontrarSinalMais(CirculoInfo externo, ConfiguracaoReconhecimentoRuna config, float margemFinal, out LinhaInfo linhaA, out LinhaInfo linhaB, out Vector2 intersecao)
    {
        linhaA = null;
        linhaB = null;
        intersecao = Vector2.zero;

        if (linhas.Count < 2)
            return false;

        float melhorCusto = float.MaxValue;

        for (int i = 0; i < linhas.Count; i++)
        {
            for (int j = i + 1; j < linhas.Count; j++)
            {
                LinhaInfo a = linhas[i];
                LinhaInfo b = linhas[j];
                float angulo = Vector2.Angle(a.direcao, b.direcao);

                if (angulo > 90f)
                    angulo = 180f - angulo;

                if (angulo < config.anguloMinimoDoMais || angulo > config.anguloMaximoDoMais)
                    continue;

                if (!GeometriaRuna.TentarIntersecaoLinhas(a.inicio, a.fim, b.inicio, b.fim, out Vector2 ponto, out float ta, out float tb))
                    continue;

                float margemParamA = margemFinal / Mathf.Max(a.comprimento, 0.0001f);
                float margemParamB = margemFinal / Mathf.Max(b.comprimento, 0.0001f);

                if (ta < -margemParamA || ta > 1f + margemParamA || tb < -margemParamB || tb > 1f + margemParamB)
                    continue;

                float distanciaCentro = Vector2.Distance(ponto, externo.centro);

                if (distanciaCentro > externo.diametro * config.toleranciaCentroDoMaisRelativa)
                    continue;

                if (!LinhaDentroESeparadaDoExterno(externo, a, margemFinal) || !LinhaDentroESeparadaDoExterno(externo, b, margemFinal))
                    continue;

                float custo = distanciaCentro + Mathf.Abs(90f - angulo) * 0.001f;

                if (custo < melhorCusto)
                {
                    melhorCusto = custo;
                    linhaA = a;
                    linhaB = b;
                    intersecao = ponto;
                }
            }
        }

        return linhaA != null && linhaB != null;
    }

    private bool ElementosDentroDoCirculoExterno(CirculoInfo externo, List<CirculoInfo> internos, LinhaInfo linhaA, LinhaInfo linhaB, float margemFinal)
    {
        if (!LinhaDentroESeparadaDoExterno(externo, linhaA, margemFinal) || !LinhaDentroESeparadaDoExterno(externo, linhaB, margemFinal))
            return false;

        for (int i = 0; i < internos.Count; i++)
        {
            CirculoInfo interno = internos[i];

            if (!PontoDentroDoCirculoAproximado(externo, interno.centro, margemFinal))
                return false;

            if (GeometriaRuna.DistanciaPolilinhas(interno.pontos, true, externo.pontos, true) < margemFinal)
                return false;
        }

        return true;
    }

    private bool CirculosEmQuatroQuadrantes(List<CirculoInfo> internos, LinhaInfo linhaA, LinhaInfo linhaB, Vector2 origem, float margemFinal)
    {
        if (internos.Count != 4)
            return false;

        bool pp = false;
        bool pn = false;
        bool np = false;
        bool nn = false;

        Vector2 eixoX = linhaA.direcao.normalized;
        Vector2 eixoY = linhaB.direcao.normalized;

        for (int i = 0; i < internos.Count; i++)
        {
            Vector2 rel = internos[i].centro - origem;
            float x = Vector2.Dot(rel, eixoX);
            float y = Vector2.Dot(rel, eixoY);

            if (Mathf.Abs(x) < margemFinal || Mathf.Abs(y) < margemFinal)
                return false;

            if (x > 0f && y > 0f)
            {
                if (pp) return false;
                pp = true;
            }
            else if (x > 0f && y < 0f)
            {
                if (pn) return false;
                pn = true;
            }
            else if (x < 0f && y > 0f)
            {
                if (np) return false;
                np = true;
            }
            else
            {
                if (nn) return false;
                nn = true;
            }
        }

        return pp && pn && np && nn;
    }

    private bool SeparacaoEntreElementosValida(CirculoInfo externo, List<CirculoInfo> internos, LinhaInfo linhaA, LinhaInfo linhaB, float margemFinal)
    {
        if (!LinhaDentroESeparadaDoExterno(externo, linhaA, margemFinal) || !LinhaDentroESeparadaDoExterno(externo, linhaB, margemFinal))
            return false;

        List<Vector2> linhaAPontos = linhaA.CriarPontos();
        List<Vector2> linhaBPontos = linhaB.CriarPontos();

        for (int i = 0; i < internos.Count; i++)
        {
            CirculoInfo interno = internos[i];

            if (GeometriaRuna.DistanciaPolilinhas(interno.pontos, true, externo.pontos, true) < margemFinal)
                return false;

            if (GeometriaRuna.DistanciaPolilinhas(interno.pontos, true, linhaAPontos, false) < margemFinal)
                return false;

            if (GeometriaRuna.DistanciaPolilinhas(interno.pontos, true, linhaBPontos, false) < margemFinal)
                return false;

            for (int j = i + 1; j < internos.Count; j++)
            {
                if (GeometriaRuna.DistanciaPolilinhas(interno.pontos, true, internos[j].pontos, true) < margemFinal)
                    return false;
            }
        }

        return true;
    }

    private void CalcularPontuacao(ResultadoReconhecimentoRuna resultado, bool quatroCirculosInternos = false)
    {
        resultado.pontuacao = 0f;

        if (resultado.circuloExternoValido)
            resultado.pontuacao += 0.20f;

        if (resultado.sinalMaisValido)
            resultado.pontuacao += 0.20f;

        if (quatroCirculosInternos)
            resultado.pontuacao += 0.20f;

        if (resultado.quadrantesValidos)
            resultado.pontuacao += 0.20f;

        if (resultado.separacaoValida)
            resultado.pontuacao += 0.20f;
    }

    private bool TentarCriarCirculo(Componente2D componente, ConfiguracaoReconhecimentoRuna config, float escalaDesenho, out CirculoInfo circulo)
    {
        circulo = null;
        List<Vector2> pontos = componente.pontosOrdenados;

        if (pontos == null || pontos.Count < 6)
            return false;

        GeometriaRuna.Bounds2D bounds = GeometriaRuna.CalcularBounds(pontos);
        float diametro = bounds.Diametro;
        float diametroMinimo = Mathf.Max(config.margemMinimaAbsoluta * 2f, escalaDesenho * config.comprimentoMinimoElementoRelativo);

        if (diametro < diametroMinimo)
            return false;

        float toleranciaFechamento = Mathf.Max(config.margemMinimaAbsoluta, diametro * config.toleranciaFechamentoRelativa);
        float abertura = Vector2.Distance(pontos[0], pontos[pontos.Count - 1]);

        if (abertura > toleranciaFechamento)
            return false;

        Vector2 centro = bounds.Centro;
        float raioX = Mathf.Max(bounds.Largura * 0.5f, 0.0001f);
        float raioY = Mathf.Max(bounds.Altura * 0.5f, 0.0001f);
        float variacao = CalcularVariacaoEliptica(pontos, centro, raioX, raioY);

        if (variacao > config.variacaoRaioMaxima)
            return false;

        float volta = CalcularVoltaEmGraus(pontos, centro);

        if (volta < config.voltaMinimaGraus || volta > config.voltaMaximaGraus)
            return false;

        float area = GeometriaRuna.AreaPoligono(pontos);
        float areaBounds = Mathf.Max(bounds.Largura * bounds.Altura, 0.0001f);

        if (area / areaBounds < 0.18f)
            return false;

        if (ContarAutoIntersecoes(pontos) > 2)
            return false;

        circulo = new CirculoInfo
        {
            pontos = pontos,
            centro = centro,
            diametro = diametro,
            area = area,
            raioMedio = (raioX + raioY) * 0.5f
        };

        return true;
    }

    private bool TentarCriarLinha(Componente2D componente, ConfiguracaoReconhecimentoRuna config, float escalaDesenho, out LinhaInfo linha)
    {
        linha = null;
        List<Vector2> pontos = componente.pontosOrdenados;

        if (pontos == null || pontos.Count < 2)
            return false;

        Vector2 inicio = pontos[0];
        Vector2 fim = pontos[pontos.Count - 1];
        Vector2 delta = fim - inicio;
        float comprimentoReto = delta.magnitude;
        float comprimentoMinimo = Mathf.Max(config.margemMinimaAbsoluta * 3f, escalaDesenho * config.comprimentoMinimoElementoRelativo);

        if (comprimentoReto < comprimentoMinimo)
            return false;

        float comprimentoCaminho = GeometriaRuna.ComprimentoPolilinha(pontos);

        if (comprimentoCaminho <= 0.0001f)
            return false;

        float maiorDesvio = 0f;

        for (int i = 1; i < pontos.Count - 1; i++)
            maiorDesvio = Mathf.Max(maiorDesvio, GeometriaRuna.DistanciaPontoSegmento(pontos[i], inicio, fim));

        if (maiorDesvio / comprimentoReto > config.toleranciaRetilinearRelativa)
            return false;

        if (comprimentoCaminho / comprimentoReto > 1.35f)
            return false;

        linha = new LinhaInfo
        {
            inicio = inicio,
            fim = fim,
            centro = (inicio + fim) * 0.5f,
            direcao = delta.normalized,
            comprimento = comprimentoReto,
            pontos = pontos
        };

        return true;
    }

    private bool LinhaDentroESeparadaDoExterno(CirculoInfo externo, LinhaInfo linha, float margemFinal)
    {
        if (linha == null)
            return false;

        if (!PontoDentroDoCirculoAproximado(externo, linha.inicio, margemFinal) ||
            !PontoDentroDoCirculoAproximado(externo, linha.fim, margemFinal) ||
            !PontoDentroDoCirculoAproximado(externo, linha.centro, margemFinal))
            return false;

        float distanciaInicio = GeometriaRuna.DistanciaPontoPolilinha(linha.inicio, externo.pontos, true);
        float distanciaFim = GeometriaRuna.DistanciaPontoPolilinha(linha.fim, externo.pontos, true);

        return distanciaInicio >= margemFinal && distanciaFim >= margemFinal;
    }

    private bool PontoDentroDoCirculoAproximado(CirculoInfo circulo, Vector2 ponto, float margem)
    {
        GeometriaRuna.Bounds2D bounds = GeometriaRuna.CalcularBounds(circulo.pontos);
        float raioX = Mathf.Max(bounds.Largura * 0.5f - margem, 0.0001f);
        float raioY = Mathf.Max(bounds.Altura * 0.5f - margem, 0.0001f);
        Vector2 rel = ponto - circulo.centro;
        float valor = (rel.x * rel.x) / (raioX * raioX) + (rel.y * rel.y) / (raioY * raioY);
        return valor <= 1f;
    }

    private float CalcularVariacaoEliptica(IReadOnlyList<Vector2> pontos, Vector2 centro, float raioX, float raioY)
    {
        float maiorVariacao = 0f;

        for (int i = 0; i < pontos.Count; i++)
        {
            Vector2 rel = pontos[i] - centro;
            float normalizado = Mathf.Sqrt((rel.x * rel.x) / (raioX * raioX) + (rel.y * rel.y) / (raioY * raioY));
            maiorVariacao = Mathf.Max(maiorVariacao, Mathf.Abs(1f - normalizado));
        }

        return maiorVariacao;
    }

    private float CalcularVoltaEmGraus(IReadOnlyList<Vector2> pontos, Vector2 centro)
    {
        if (pontos.Count < 3)
            return 0f;

        float acumulado = 0f;
        Vector2 anterior = pontos[0] - centro;

        for (int i = 1; i < pontos.Count; i++)
        {
            Vector2 atual = pontos[i] - centro;

            if (anterior.sqrMagnitude > 0.000001f && atual.sqrMagnitude > 0.000001f)
                acumulado += Vector2.SignedAngle(anterior, atual);

            anterior = atual;
        }

        return Mathf.Abs(acumulado);
    }

    private int ContarAutoIntersecoes(IReadOnlyList<Vector2> pontos)
    {
        int intersecoes = 0;

        for (int i = 0; i < pontos.Count - 1; i++)
        {
            Vector2 a1 = pontos[i];
            Vector2 a2 = pontos[i + 1];

            for (int j = i + 2; j < pontos.Count - 1; j++)
            {
                if (j == i + 1)
                    continue;

                if (i == 0 && j == pontos.Count - 2)
                    continue;

                if (GeometriaRuna.SegmentosIntersectam(a1, a2, pontos[j], pontos[j + 1]))
                    intersecoes++;
            }
        }

        return intersecoes;
    }

    private bool ExtremidadesConectadas(Traco2D a, Traco2D b, float tolerancia)
    {
        return Vector2.Distance(a.inicio, b.inicio) <= tolerancia ||
               Vector2.Distance(a.inicio, b.fim) <= tolerancia ||
               Vector2.Distance(a.fim, b.inicio) <= tolerancia ||
               Vector2.Distance(a.fim, b.fim) <= tolerancia;
    }

    private List<Vector2> OrdenarPontosDoComponente(List<Traco2D> tracosComponente, float tolerancia)
    {
        List<Vector2> resultado = new List<Vector2>();

        if (tracosComponente == null || tracosComponente.Count == 0)
            return resultado;

        int indiceInicial = 0;
        float maiorComprimento = tracosComponente[0].comprimento;

        for (int i = 1; i < tracosComponente.Count; i++)
        {
            if (tracosComponente[i].comprimento > maiorComprimento)
            {
                maiorComprimento = tracosComponente[i].comprimento;
                indiceInicial = i;
            }
        }

        List<Traco2D> restantes = new List<Traco2D>(tracosComponente);
        Traco2D inicial = restantes[indiceInicial];
        restantes.RemoveAt(indiceInicial);
        AdicionarPontos(resultado, inicial.pontos, false, false);

        bool adicionou = true;

        while (restantes.Count > 0 && adicionou)
        {
            adicionou = false;
            Vector2 inicioAtual = resultado[0];
            Vector2 fimAtual = resultado[resultado.Count - 1];

            for (int i = 0; i < restantes.Count; i++)
            {
                Traco2D traco = restantes[i];

                if (Vector2.Distance(fimAtual, traco.inicio) <= tolerancia)
                {
                    AdicionarPontos(resultado, traco.pontos, false, true);
                    restantes.RemoveAt(i);
                    adicionou = true;
                    break;
                }

                if (Vector2.Distance(fimAtual, traco.fim) <= tolerancia)
                {
                    AdicionarPontos(resultado, traco.pontos, true, true);
                    restantes.RemoveAt(i);
                    adicionou = true;
                    break;
                }

                if (Vector2.Distance(inicioAtual, traco.fim) <= tolerancia)
                {
                    AdicionarPontosNoInicio(resultado, traco.pontos, false);
                    restantes.RemoveAt(i);
                    adicionou = true;
                    break;
                }

                if (Vector2.Distance(inicioAtual, traco.inicio) <= tolerancia)
                {
                    AdicionarPontosNoInicio(resultado, traco.pontos, true);
                    restantes.RemoveAt(i);
                    adicionou = true;
                    break;
                }
            }
        }

        for (int i = 0; i < restantes.Count; i++)
            AdicionarPontos(resultado, restantes[i].pontos, false, true);

        return resultado;
    }

    private void AdicionarPontos(List<Vector2> destino, List<Vector2> origem, bool inverter, bool ignorarPrimeiro)
    {
        if (!inverter)
        {
            for (int i = ignorarPrimeiro ? 1 : 0; i < origem.Count; i++)
                destino.Add(origem[i]);
        }
        else
        {
            for (int i = origem.Count - (ignorarPrimeiro ? 2 : 1); i >= 0; i--)
                destino.Add(origem[i]);
        }
    }

    private void AdicionarPontosNoInicio(List<Vector2> destino, List<Vector2> origem, bool inverter)
    {
        List<Vector2> temporario = new List<Vector2>();

        if (!inverter)
        {
            for (int i = 0; i < origem.Count - 1; i++)
                temporario.Add(origem[i]);
        }
        else
        {
            for (int i = origem.Count - 1; i >= 1; i--)
                temporario.Add(origem[i]);
        }

        for (int i = temporario.Count - 1; i >= 0; i--)
            destino.Insert(0, temporario[i]);
    }

    private int Encontrar(int[] pai, int i)
    {
        while (pai[i] != i)
        {
            pai[i] = pai[pai[i]];
            i = pai[i];
        }

        return i;
    }

    private void Unir(int[] pai, int a, int b)
    {
        int raizA = Encontrar(pai, a);
        int raizB = Encontrar(pai, b);

        if (raizA != raizB)
            pai[raizB] = raizA;
    }

    private class Traco2D
    {
        public readonly int indiceOriginal;
        public readonly List<Vector2> pontos;
        public readonly Vector2 inicio;
        public readonly Vector2 fim;
        public readonly float comprimento;

        public Traco2D(int indiceOriginal, List<Vector2> pontos, float comprimento)
        {
            this.indiceOriginal = indiceOriginal;
            this.pontos = pontos;
            this.comprimento = comprimento;
            inicio = pontos[0];
            fim = pontos[pontos.Count - 1];
        }
    }

    private class Componente2D
    {
        public readonly int raiz;
        public readonly List<Traco2D> tracos = new();
        public List<Vector2> pontosOrdenados;
        public float comprimento;

        public Componente2D(int raiz)
        {
            this.raiz = raiz;
        }
    }

    private class CirculoInfo
    {
        public List<Vector2> pontos;
        public Vector2 centro;
        public float diametro;
        public float area;
        public float raioMedio;
    }

    private class LinhaInfo
    {
        public List<Vector2> pontos;
        public Vector2 inicio;
        public Vector2 fim;
        public Vector2 centro;
        public Vector2 direcao;
        public float comprimento;

        public List<Vector2> CriarPontos()
        {
            return new List<Vector2> { inicio, fim };
        }
    }
}
