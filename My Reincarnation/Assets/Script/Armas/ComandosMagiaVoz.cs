using System;
using System.Text;
using UnityEngine;

public enum IdiomaComandoVoz
{
    Portugues,
    Ingles,
    Qualquer
}

[Serializable]
public class VarianteComandoVoz
{
    public string frase;
    public IdiomaComandoVoz idioma;
}

[Serializable]
public class ComandoVozMagia
{
    public string idMagia;
    public VarianteComandoVoz[] variantes;
}

[DisallowMultipleComponent]
public class ComandosMagiaVoz : MonoBehaviour
{
    private const string IdBolaDeFogo = "BolaDeFogo";

    [Header("Referencias")]
    [SerializeField] private CajadoMagico cajadoMagico;
    [SerializeField] private CajadoMagicoVoz cajadoMagicoVoz;

    [Header("Comandos")]
    [SerializeField] private ComandoVozMagia[] comandos = CriarComandosPadrao();

    [Header("Configuracao")]
    [SerializeField] private bool aceitarComandosEmPortugues = true;
    [SerializeField] private bool aceitarComandosEmIngles = true;
    [SerializeField] private bool aceitarTranscricaoParcialRapida = true;
    [SerializeField, Min(0f)] private float intervaloMinimoMesmoComando = 1f;

    [Header("Tolerancia de Pronuncia / Sotaque")]
    [SerializeField] private bool usarToleranciaFonica = true;
    [SerializeField, Range(0f, 0.6f)] private float toleranciaFonicaPorPalavra = 0.3f;
    [SerializeField, Min(1)] private int tamanhoMinimoPalavraParaTolerancia = 3;

    [Header("Diagnostico do Comando")]
    [SerializeField] private bool mostrarDiagnosticoNoConsole = true;
    [SerializeField] private bool transcricaoRecebida;
    [SerializeField] private bool comandoIdentificado;
    [SerializeField] private bool magiaAtivada;
    [SerializeField] private int quantidadeTranscricoesRecebidas;
    [SerializeField] private int quantidadeComandosIdentificados;
    [SerializeField] private int quantidadeMagiasAtivadas;
    [SerializeField] private string ultimaTranscricaoRecebida;
    [SerializeField] private string ultimoIdMagiaIdentificado;
    [SerializeField, TextArea(2, 5)] private string statusDiagnosticoComando;

    private float horarioUltimoComandoAceito = -999f;
    private string ultimoComandoAceito;
    private bool listenerRegistrado;

    private void Awake()
    {
        GarantirReferencias();
        GarantirComandosPadraoSeNecessario();
    }

    private void OnEnable()
    {
        GarantirReferencias();
        GarantirComandosPadraoSeNecessario();
        RegistrarListenerVoz();
    }

    private void OnDisable()
    {
        RemoverListenerVoz();
    }

    private void OnValidate()
    {
        intervaloMinimoMesmoComando = Mathf.Max(0f, intervaloMinimoMesmoComando);
        tamanhoMinimoPalavraParaTolerancia = Mathf.Max(1, tamanhoMinimoPalavraParaTolerancia);
        GarantirReferencias();
        GarantirComandosPadraoSeNecessario();
    }

    private void Reset()
    {
        GarantirReferencias();
        comandos = CriarComandosPadrao();
        aceitarComandosEmPortugues = true;
        aceitarComandosEmIngles = true;
        aceitarTranscricaoParcialRapida = true;
        intervaloMinimoMesmoComando = 1f;
        usarToleranciaFonica = true;
        toleranciaFonicaPorPalavra = 0.3f;
        tamanhoMinimoPalavraParaTolerancia = 3;
    }

    public void ProcessarComandoVoz(string textoNormalizado)
    {
        ProcessarComandoVozInterno(textoNormalizado, false);
    }

    public void ProcessarComandoVozParcial(string textoNormalizado)
    {
        if (!aceitarTranscricaoParcialRapida)
            return;

        ProcessarComandoVozInterno(textoNormalizado, true);
    }

    private void ProcessarComandoVozInterno(string textoNormalizado, bool veioDeParcial)
    {
        string fraseNormalizada = NormalizarFrase(textoNormalizado);
        transcricaoRecebida = true;
        quantidadeTranscricoesRecebidas++;
        ultimaTranscricaoRecebida = fraseNormalizada;

        if (string.IsNullOrEmpty(fraseNormalizada))
        {
            IgnorarComando("Texto vazio.", veioDeParcial);
            return;
        }

        DefinirDiagnostico(
            $"COMANDO RECEBIDO ({(veioDeParcial ? "parcial" : "final")}): \"{fraseNormalizada}\".");

        GarantirReferencias();

        if (cajadoMagico == null)
        {
            IgnorarComando("CajadoMagico nao configurado.", veioDeParcial);
            return;
        }

        if (cajadoMagicoVoz == null)
        {
            IgnorarComando("CajadoMagicoVoz nao configurado.", veioDeParcial);
            return;
        }

        if (!cajadoMagico.CajadoSegurado)
        {
            IgnorarComando("Cajado nao esta segurado.", veioDeParcial);
            return;
        }

        if (cajadoMagico.EstaDesenhando)
        {
            IgnorarComando("Jogador esta desenhando uma runa.", veioDeParcial);
            return;
        }

        if (!TentarIdentificarComando(fraseNormalizada, out ComandoVozMagia comando))
        {
            IgnorarComando("Nenhum comando cadastrado bateu com o texto entendido.", veioDeParcial);
            return;
        }

        comandoIdentificado = true;
        quantidadeComandosIdentificados++;
        ultimoIdMagiaIdentificado = comando.idMagia;
        DefinirDiagnostico(
            $"Comando identificado: '{fraseNormalizada}' -> {comando.idMagia} ({(veioDeParcial ? "parcial" : "final")}).");

        if (EhMesmoComandoRepetido(fraseNormalizada))
        {
            IgnorarComando("Comando repetido ignorado pelo intervalo minimo.", veioDeParcial);
            return;
        }

        if (!string.Equals(comando.idMagia, IdBolaDeFogo, StringComparison.OrdinalIgnoreCase))
        {
            IgnorarComando("Comando reconhecido, mas a magia ainda nao esta implementada.", veioDeParcial);
            return;
        }

        ultimoComandoAceito = fraseNormalizada;
        horarioUltimoComandoAceito = Time.time;

        bool preparouMagia = cajadoMagico.TentarPrepararBolaDeFogoPorVoz();
        if (preparouMagia)
        {
            magiaAtivada = true;
            quantidadeMagiasAtivadas++;
            DefinirDiagnostico($"Magia ativada por voz: {comando.idMagia}.");
            return;
        }

        DefinirDiagnostico(ObterMotivoFalhaPreparacao(), true);
    }

    private void RegistrarListenerVoz()
    {
        if (listenerRegistrado || cajadoMagicoVoz == null || cajadoMagicoVoz.AoComandoVozRecebido == null)
            return;

        cajadoMagicoVoz.AoComandoVozRecebido.RemoveListener(ProcessarComandoVoz);
        cajadoMagicoVoz.AoComandoVozRecebido.AddListener(ProcessarComandoVoz);
        cajadoMagicoVoz.AoTextoParcialVozRecebido.RemoveListener(ProcessarComandoVozParcial);
        cajadoMagicoVoz.AoTextoParcialVozRecebido.AddListener(ProcessarComandoVozParcial);
        listenerRegistrado = true;
    }

    private void RemoverListenerVoz()
    {
        if (!listenerRegistrado || cajadoMagicoVoz == null || cajadoMagicoVoz.AoComandoVozRecebido == null)
            return;

        cajadoMagicoVoz.AoComandoVozRecebido.RemoveListener(ProcessarComandoVoz);
        cajadoMagicoVoz.AoTextoParcialVozRecebido.RemoveListener(ProcessarComandoVozParcial);
        listenerRegistrado = false;
    }

    private void GarantirReferencias()
    {
        if (cajadoMagico == null)
            cajadoMagico = GetComponent<CajadoMagico>();

        if (cajadoMagicoVoz == null)
            cajadoMagicoVoz = GetComponent<CajadoMagicoVoz>();
    }

    private void GarantirComandosPadraoSeNecessario()
    {
        if (comandos == null || comandos.Length == 0)
        {
            comandos = CriarComandosPadrao();
            return;
        }

        GarantirVariantesPadraoBolaDeFogo();
    }

    private bool TentarIdentificarComando(string fraseNormalizada, out ComandoVozMagia comandoEncontrado)
    {
        comandoEncontrado = null;

        if (comandos == null)
            return false;

        for (int i = 0; i < comandos.Length; i++)
        {
            ComandoVozMagia comando = comandos[i];
            if (comando == null || comando.variantes == null)
                continue;

            for (int j = 0; j < comando.variantes.Length; j++)
            {
                VarianteComandoVoz variante = comando.variantes[j];
                if (variante == null || !IdiomaPermitido(variante.idioma))
                    continue;

                string fraseVariante = NormalizarFrase(variante.frase);

                if (string.IsNullOrEmpty(fraseVariante))
                    continue;

                if (!FraseContemComTolerancia(fraseNormalizada, fraseVariante))
                    continue;

                comandoEncontrado = comando;
                return true;
            }
        }

        return false;
    }

    private void GarantirVariantesPadraoBolaDeFogo()
    {
        ComandoVozMagia comandoBolaDeFogo = null;

        for (int i = 0; i < comandos.Length; i++)
        {
            if (comandos[i] == null)
                continue;

            if (string.Equals(comandos[i].idMagia, IdBolaDeFogo, StringComparison.OrdinalIgnoreCase))
            {
                comandoBolaDeFogo = comandos[i];
                break;
            }
        }

        ComandoVozMagia comandoPadrao = CriarComandosPadrao()[0];

        if (comandoBolaDeFogo == null)
        {
            int tamanhoAtual = comandos.Length;
            Array.Resize(ref comandos, tamanhoAtual + 1);
            comandos[tamanhoAtual] = comandoPadrao;
            return;
        }

        if (comandoBolaDeFogo.variantes == null)
            comandoBolaDeFogo.variantes = Array.Empty<VarianteComandoVoz>();

        for (int i = 0; i < comandoPadrao.variantes.Length; i++)
        {
            VarianteComandoVoz variantePadrao = comandoPadrao.variantes[i];
            if (variantePadrao == null || ExisteVariante(comandoBolaDeFogo, variantePadrao.frase, variantePadrao.idioma))
                continue;

            int tamanhoAtual = comandoBolaDeFogo.variantes.Length;
            Array.Resize(ref comandoBolaDeFogo.variantes, tamanhoAtual + 1);
            comandoBolaDeFogo.variantes[tamanhoAtual] = new VarianteComandoVoz
            {
                frase = variantePadrao.frase,
                idioma = variantePadrao.idioma
            };
        }
    }

    private static bool ExisteVariante(ComandoVozMagia comando, string frase, IdiomaComandoVoz idioma)
    {
        string fraseNormalizada = NormalizarFrase(frase);

        for (int i = 0; i < comando.variantes.Length; i++)
        {
            VarianteComandoVoz variante = comando.variantes[i];
            if (variante == null || variante.idioma != idioma)
                continue;

            if (string.Equals(NormalizarFrase(variante.frase), fraseNormalizada, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private bool FraseContemComTolerancia(string fraseNormalizada, string fraseVariante)
    {
        if (fraseNormalizada.IndexOf(fraseVariante, StringComparison.Ordinal) >= 0)
            return true;

        string[] palavrasFrase = fraseNormalizada.Split(' ');
        string[] palavrasVariante = fraseVariante.Split(' ');

        if (palavrasVariante.Length == 0)
            return false;

        int indiceBusca = 0;

        for (int i = 0; i < palavrasVariante.Length; i++)
        {
            bool encontrou = false;

            for (int j = indiceBusca; j < palavrasFrase.Length; j++)
            {
                if (!PalavrasParecidas(palavrasFrase[j], palavrasVariante[i]))
                    continue;

                indiceBusca = j + 1;
                encontrou = true;
                break;
            }

            if (!encontrou)
                return false;
        }

        return true;
    }

    private bool PalavrasParecidas(string palavraOuvida, string palavraEsperada)
    {
        if (string.IsNullOrEmpty(palavraOuvida) || string.IsNullOrEmpty(palavraEsperada))
            return string.Equals(palavraOuvida, palavraEsperada, StringComparison.Ordinal);

        if (string.Equals(palavraOuvida, palavraEsperada, StringComparison.Ordinal))
            return true;

        if (!usarToleranciaFonica || palavraEsperada.Length <= tamanhoMinimoPalavraParaTolerancia)
            return false;

        int distancia = DistanciaLevenshtein(palavraOuvida, palavraEsperada);
        int tamanhoMaximo = Mathf.Max(palavraOuvida.Length, palavraEsperada.Length);
        return tamanhoMaximo > 0 && (float)distancia / tamanhoMaximo <= toleranciaFonicaPorPalavra;
    }

    private static int DistanciaLevenshtein(string a, string b)
    {
        int[,] distancias = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++)
            distancias[i, 0] = i;

        for (int j = 0; j <= b.Length; j++)
            distancias[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int custo = a[i - 1] == b[j - 1] ? 0 : 1;
                int remocao = distancias[i - 1, j] + 1;
                int insercao = distancias[i, j - 1] + 1;
                int substituicao = distancias[i - 1, j - 1] + custo;
                distancias[i, j] = Mathf.Min(remocao, Mathf.Min(insercao, substituicao));
            }
        }

        return distancias[a.Length, b.Length];
    }

    private bool IdiomaPermitido(IdiomaComandoVoz idioma)
    {
        switch (idioma)
        {
            case IdiomaComandoVoz.Portugues:
                return aceitarComandosEmPortugues;
            case IdiomaComandoVoz.Ingles:
                return aceitarComandosEmIngles;
            case IdiomaComandoVoz.Qualquer:
            default:
                return true;
        }
    }

    private bool EhMesmoComandoRepetido(string fraseNormalizada)
    {
        if (string.IsNullOrEmpty(ultimoComandoAceito) || intervaloMinimoMesmoComando <= 0f)
            return false;

        return string.Equals(ultimoComandoAceito, fraseNormalizada, StringComparison.Ordinal)
            && Time.time - horarioUltimoComandoAceito < intervaloMinimoMesmoComando;
    }

    private string ObterMotivoFalhaPreparacao()
    {
        if (cajadoMagico.PreparacaoBloqueadaPorFaltaDeMana)
            return "Comando reconhecido, mas nao ha mana suficiente.";

        if (cajadoMagico.BolaDeFogoPreparadaAtual != null)
            return "Comando reconhecido, mas ja existe uma magia preparada.";

        return string.IsNullOrEmpty(cajadoMagico.StatusPreparacaoMagia)
            ? "Comando reconhecido, mas a magia nao foi preparada."
            : cajadoMagico.StatusPreparacaoMagia;
    }

    private void IgnorarComando(string motivo, bool veioDeParcial)
    {
        string origem = veioDeParcial ? "transcricao parcial" : "transcricao final";
        DefinirDiagnostico(
            $"Comando ignorado ({origem}): {motivo}",
            !veioDeParcial,
            !veioDeParcial);
    }

    private void DefinirDiagnostico(string mensagem, bool aviso = false, bool registrarNoConsole = true)
    {
        statusDiagnosticoComando = mensagem ?? string.Empty;

        if (!mostrarDiagnosticoNoConsole || !registrarNoConsole)
            return;

        if (aviso)
            Debug.LogWarning($"[COMANDO VOZ CAJADO] {statusDiagnosticoComando}", this);
        else
            Debug.Log($"[COMANDO VOZ CAJADO] {statusDiagnosticoComando}", this);
    }

    private static string NormalizarFrase(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        string limpo = texto.Trim().Trim('.', ',', '!', '?').Trim().ToLowerInvariant();
        StringBuilder builder = new StringBuilder(limpo.Length);
        bool espacoAnterior = false;

        for (int i = 0; i < limpo.Length; i++)
        {
            char caractere = limpo[i];

            if (char.IsWhiteSpace(caractere) || EhSeparadorDeFrase(caractere))
            {
                if (!espacoAnterior)
                {
                    builder.Append(' ');
                    espacoAnterior = true;
                }

                continue;
            }

            builder.Append(caractere);
            espacoAnterior = false;
        }

        return builder.ToString().Trim();
    }

    private static bool EhSeparadorDeFrase(char caractere)
    {
        return caractere == '-' || caractere == '_' || caractere == '.' ||
               caractere == ',' || caractere == '!' || caractere == '?' ||
               caractere == ';' || caractere == ':';
    }

    private static ComandoVozMagia[] CriarComandosPadrao()
    {
        return new[]
        {
            new ComandoVozMagia
            {
                idMagia = IdBolaDeFogo,
                variantes = new[]
                {
                    new VarianteComandoVoz { frase = "bola de fogo", idioma = IdiomaComandoVoz.Portugues },
                    new VarianteComandoVoz { frase = "conjurar bola de fogo", idioma = IdiomaComandoVoz.Portugues },
                    new VarianteComandoVoz { frase = "criar bola de fogo", idioma = IdiomaComandoVoz.Portugues },
                    new VarianteComandoVoz { frase = "preparar bola de fogo", idioma = IdiomaComandoVoz.Portugues },
                    new VarianteComandoVoz { frase = "invoque bola de fogo", idioma = IdiomaComandoVoz.Portugues },
                    new VarianteComandoVoz { frase = "invocar bola de fogo", idioma = IdiomaComandoVoz.Portugues },
                    new VarianteComandoVoz { frase = "bola fogo", idioma = IdiomaComandoVoz.Portugues },
                    new VarianteComandoVoz { frase = "fireball", idioma = IdiomaComandoVoz.Ingles },
                    new VarianteComandoVoz { frase = "fire ball", idioma = IdiomaComandoVoz.Ingles },
                    new VarianteComandoVoz { frase = "cast fireball", idioma = IdiomaComandoVoz.Ingles },
                    new VarianteComandoVoz { frase = "cast fire ball", idioma = IdiomaComandoVoz.Ingles },
                    new VarianteComandoVoz { frase = "create fireball", idioma = IdiomaComandoVoz.Ingles },
                    new VarianteComandoVoz { frase = "prepare fireball", idioma = IdiomaComandoVoz.Ingles },
                    new VarianteComandoVoz { frase = "summon fireball", idioma = IdiomaComandoVoz.Ingles }
                }
            }
        };
    }
}
