using System;

[Serializable]
public enum ModoReconhecimentoRuna
{
    CirculoSimplesBolaDeFogo,
    RunaComplexa,
    Todos
}

[Serializable]
public class ResultadoReconhecimentoRuna
{
    public const string IdCirculoBolaDeFogo = "CirculoBolaDeFogo";
    public const string IdCirculoMaisQuatroCirculos = "CirculoMaisQuatroCirculos";

    public bool reconhecida;
    public float pontuacao;
    public string idRuna;
    public string motivo;
    public int quantidadeCirculosEncontrados;
    public int quantidadeLinhasEncontradas;
    public bool circuloExternoValido;
    public bool sinalMaisValido;
    public bool quadrantesValidos;
    public bool separacaoValida;
    public bool elementosContidosNoCirculoExterno;

    public static ResultadoReconhecimentoRuna CriarIncompleto(string motivo)
    {
        return new ResultadoReconhecimentoRuna
        {
            reconhecida = false,
            pontuacao = 0f,
            idRuna = string.Empty,
            motivo = motivo
        };
    }
}

[Serializable]
public struct ConfiguracaoReconhecimentoRuna
{
    public bool usarReconhecimentoRunico;
    public ModoReconhecimentoRuna modoReconhecimentoRuna;
    public float diametroMinimoRuna;
    public float toleranciaFechamentoRelativa;
    public float toleranciaConexaoRelativa;
    public float variacaoRaioMaxima;
    public float voltaMinimaGraus;
    public float voltaMaximaGraus;
    public float anguloMinimoDoMais;
    public float anguloMaximoDoMais;
    public float toleranciaRetilinearRelativa;
    public float toleranciaCentroDoMaisRelativa;
    public float margemMinimaEntreElementosRelativa;
    public float margemMinimaAbsoluta;
    public float comprimentoMinimoElementoRelativo;
    public float pontuacaoMinima;
}
