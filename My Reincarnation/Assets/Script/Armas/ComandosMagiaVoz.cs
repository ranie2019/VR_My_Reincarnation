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

        if (string.IsNullOrEmpty(fraseNormalizada))
        {
            IgnorarComando("Texto vazio.", veioDeParcial);
            return;
        }

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

        cajadoMagico.TentarPrepararBolaDeFogoPorVoz();
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
        if (comandos != null && comandos.Length > 0)
            return;

        comandos = CriarComandosPadrao();
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
                if (!string.Equals(fraseNormalizada, fraseVariante, StringComparison.Ordinal))
                    continue;

                comandoEncontrado = comando;
                return true;
            }
        }

        return false;
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
                    new VarianteComandoVoz { frase = "fireball", idioma = IdiomaComandoVoz.Ingles },
                    new VarianteComandoVoz { frase = "cast fireball", idioma = IdiomaComandoVoz.Ingles },
                    new VarianteComandoVoz { frase = "create fireball", idioma = IdiomaComandoVoz.Ingles },
                    new VarianteComandoVoz { frase = "prepare fireball", idioma = IdiomaComandoVoz.Ingles },
                    new VarianteComandoVoz { frase = "summon fireball", idioma = IdiomaComandoVoz.Ingles }
                }
            }
        };
    }
}
