using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StatusUI : MonoBehaviour
{
    [Header("Referencia do Player")]
    [SerializeField] private StatusPlayer statusPlayer;

    [Header("Textos Identidade")]
    [SerializeField] private TextMeshProUGUI textoNomeValor;
    [SerializeField] private TextMeshProUGUI textoRacaValor;
    [SerializeField] private TextMeshProUGUI textoClasseValor;
    [SerializeField] private TextMeshProUGUI textoTituloValor;

    [Header("Textos Level")]
    [SerializeField] private TextMeshProUGUI textoNivelValor;
    [SerializeField] private TextMeshProUGUI textoPontosStatusValor;

    [Header("Textos Vida")]
    [SerializeField] private TextMeshProUGUI textoVidaAtualValor;
    [SerializeField] private TextMeshProUGUI textoVidaMaximaValor;

    [Header("Textos Mana")]
    [SerializeField] private TextMeshProUGUI textoManaAtualValor;
    [SerializeField] private TextMeshProUGUI textoManaMaximaValor;

    [Header("Textos Experiencia")]
    [SerializeField] private TextMeshProUGUI textoExperienciaAtualValor;
    [SerializeField] private TextMeshProUGUI textoExperienciaMaximaValor;

    [Header("Textos Moeda")]
    [SerializeField] private TextMeshProUGUI textoReinRotulo;
    [SerializeField] private TextMeshProUGUI textoReinValor;

    [Header("Textos Atributos Base")]
    [SerializeField] private TextMeshProUGUI textoForcaBaseValor;
    [SerializeField] private TextMeshProUGUI textoForcaBonusValor;
    [SerializeField] private TextMeshProUGUI textoConstituicaoBaseValor;
    [SerializeField] private TextMeshProUGUI textoConstituicaoBonusValor;
    [SerializeField] private TextMeshProUGUI textoAgilidadeBaseValor;
    [SerializeField] private TextMeshProUGUI textoAgilidadeBonusValor;
    [SerializeField] private TextMeshProUGUI textoVelocidadeBaseValor;
    [SerializeField] private TextMeshProUGUI textoVelocidadeBonusValor;
    [SerializeField] private TextMeshProUGUI textoInteligenciaBaseValor;
    [SerializeField] private TextMeshProUGUI textoInteligenciaBonusValor;
    [SerializeField] private TextMeshProUGUI textoEspiritoBaseValor;
    [SerializeField] private TextMeshProUGUI textoEspiritoBonusValor;

    [Header("Textos Atributos Extras")]
    [SerializeField] private TextMeshProUGUI textoDestrezaBaseValor;
    [SerializeField] private TextMeshProUGUI textoDestrezaBonusValor;
    [SerializeField] private TextMeshProUGUI textoVigorBaseValor;
    [SerializeField] private TextMeshProUGUI textoVigorBonusValor;
    [SerializeField] private TextMeshProUGUI textoPercepcaoBaseValor;
    [SerializeField] private TextMeshProUGUI textoPercepcaoBonusValor;
    [SerializeField] private TextMeshProUGUI textoResistenciaBaseValor;
    [SerializeField] private TextMeshProUGUI textoResistenciaBonusValor;
    [SerializeField] private TextMeshProUGUI textoDefesaBaseValor;
    [SerializeField] private TextMeshProUGUI textoDefesaBonusValor;
    [SerializeField] private TextMeshProUGUI textoCarismaBaseValor;
    [SerializeField] private TextMeshProUGUI textoCarismaBonusValor;
    [SerializeField] private TextMeshProUGUI textoSorteBaseValor;
    [SerializeField] private TextMeshProUGUI textoSorteBonusValor;

    [Header("Botoes Atributos Base")]
    public Button botaoForca;
    public Button botaoConstituicao;
    public Button botaoAgilidade;
    public Button botaoVelocidade;
    public Button botaoInteligencia;
    public Button botaoEspirito;

    [Header("Botoes Atributos Extras")]
    public Button botaoDestreza;
    public Button botaoForcaExtra;
    public Button botaoPercepcao;
    public Button botaoResistencia;
    public Button botaoCarisma;
    public Button botaoSorte;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip somCliqueBotao;
    public AudioClip somSemPonto;

    [Header("Configuracao Visual")]
    [SerializeField] private bool atualizarContinuamente = true;

    private StatusPlayer statusPlayerInscrito;

    private void Awake()
    {
        EncontrarStatusPlayerSeNecessario();
        EncontrarTextosSeNecessario();
        EncontrarBotoesSeNecessario();
        EncontrarAudioSourceSeNecessario();
        ConfigurarBotoes();
    }

    private void OnEnable()
    {
        EncontrarStatusPlayerSeNecessario();
        InscreverStatusPlayer();
        EncontrarAudioSourceSeNecessario();
        ConfigurarBotoes();
        AtualizarUI();
    }

    private void OnDisable()
    {
        DesinscreverStatusPlayer();
    }

    private void OnDestroy()
    {
        DesinscreverStatusPlayer();
        RemoverListenersBotoes();
    }

    private void Update()
    {
        if (atualizarContinuamente)
            AtualizarUI();
    }

    public void AtualizarUI()
    {
        EncontrarStatusPlayerSeNecessario();
        InscreverStatusPlayer();
        EncontrarTextosSeNecessario();

        if (statusPlayer == null)
        {
            AtualizarEstadoBotoes(false);
            return;
        }

        DefinirTexto(textoNomeValor, statusPlayer.GetNome());
        DefinirTexto(textoRacaValor, statusPlayer.GetRaca());
        DefinirTexto(textoClasseValor, statusPlayer.GetClasse());
        DefinirTexto(textoTituloValor, statusPlayer.GetTitulo());

        DefinirTexto(textoNivelValor, statusPlayer.GetNivel().ToString());
        DefinirTexto(textoPontosStatusValor, statusPlayer.GetPontosStatusDisponiveis().ToString());

        DefinirTexto(textoVidaAtualValor, statusPlayer.GetVidaAtual().ToString());
        DefinirTexto(textoVidaMaximaValor, statusPlayer.GetVidaMaxima().ToString());

        DefinirTexto(textoManaAtualValor, statusPlayer.GetManaAtual().ToString());
        DefinirTexto(textoManaMaximaValor, statusPlayer.GetManaMaxima().ToString());

        DefinirTexto(textoExperienciaAtualValor, statusPlayer.GetExperienciaAtual().ToString());
        DefinirTexto(textoExperienciaMaximaValor, statusPlayer.GetExperienciaParaProximoNivel().ToString());
        DefinirTexto(textoReinRotulo, "REIN");
        DefinirTexto(textoReinValor, statusPlayer.ObterReinFormatado());

        DefinirTexto(textoForcaBaseValor, statusPlayer.GetForca().ToString());
        DefinirTexto(textoForcaBonusValor, FormatarBonus(statusPlayer.GetBonusForca()));
        DefinirTexto(textoConstituicaoBaseValor, statusPlayer.GetConstituicao().ToString());
        DefinirTexto(textoConstituicaoBonusValor, FormatarBonus(statusPlayer.GetBonusConstituicao()));
        DefinirTexto(textoAgilidadeBaseValor, statusPlayer.GetAgilidade().ToString());
        DefinirTexto(textoAgilidadeBonusValor, FormatarBonus(statusPlayer.GetBonusAgilidade()));
        DefinirTexto(textoVelocidadeBaseValor, statusPlayer.GetVelocidade().ToString());
        DefinirTexto(textoVelocidadeBonusValor, FormatarBonus(statusPlayer.GetBonusVelocidade()));
        DefinirTexto(textoInteligenciaBaseValor, statusPlayer.GetInteligencia().ToString());
        DefinirTexto(textoInteligenciaBonusValor, FormatarBonus(statusPlayer.GetBonusInteligencia()));
        DefinirTexto(textoEspiritoBaseValor, statusPlayer.GetEspirito().ToString());
        DefinirTexto(textoEspiritoBonusValor, FormatarBonus(statusPlayer.GetBonusEspirito()));

        DefinirTexto(textoDestrezaBaseValor, statusPlayer.GetDestreza().ToString());
        DefinirTexto(textoDestrezaBonusValor, FormatarBonus(statusPlayer.GetBonusDestreza()));
        DefinirTexto(textoVigorBaseValor, statusPlayer.GetVigor().ToString());
        DefinirTexto(textoVigorBonusValor, FormatarBonus(statusPlayer.GetBonusVigor()));
        DefinirTexto(textoPercepcaoBaseValor, statusPlayer.GetPercepcao().ToString());
        DefinirTexto(textoPercepcaoBonusValor, FormatarBonus(statusPlayer.GetBonusPercepcao()));
        DefinirTexto(textoResistenciaBaseValor, statusPlayer.GetResistencia().ToString());
        DefinirTexto(textoResistenciaBonusValor, FormatarBonus(statusPlayer.GetBonusResistencia()));
        DefinirTexto(textoDefesaBaseValor, statusPlayer.GetDefesa().ToString());
        DefinirTexto(textoDefesaBonusValor, FormatarBonus(statusPlayer.GetBonusDefesa()));
        DefinirTexto(textoCarismaBaseValor, statusPlayer.GetCarisma().ToString());
        DefinirTexto(textoCarismaBonusValor, FormatarBonus(statusPlayer.GetBonusCarisma()));
        DefinirTexto(textoSorteBaseValor, statusPlayer.GetSorte().ToString());
        DefinirTexto(textoSorteBonusValor, FormatarBonus(statusPlayer.GetBonusSorte()));

        AtualizarEstadoBotoes(true);
    }

    private void EncontrarStatusPlayerSeNecessario()
    {
        if (statusPlayer == null)
            statusPlayer = FindFirstObjectByType<StatusPlayer>();
    }

    private void InscreverStatusPlayer()
    {
        if (statusPlayerInscrito == statusPlayer)
            return;

        DesinscreverStatusPlayer();
        statusPlayerInscrito = statusPlayer;

        if (statusPlayerInscrito != null)
            statusPlayerInscrito.StatusAlterado += AtualizarUI;
    }

    private void DesinscreverStatusPlayer()
    {
        if (statusPlayerInscrito != null)
            statusPlayerInscrito.StatusAlterado -= AtualizarUI;

        statusPlayerInscrito = null;
    }

    private void ConfigurarBotoes()
    {
        EncontrarBotoesSeNecessario();

        ConfigurarBotao(botaoForca, AoClicarForca);
        ConfigurarBotao(botaoConstituicao, AoClicarConstituicao);
        ConfigurarBotao(botaoAgilidade, AoClicarAgilidade);
        ConfigurarBotao(botaoVelocidade, AoClicarVelocidade);
        ConfigurarBotao(botaoInteligencia, AoClicarInteligencia);
        ConfigurarBotao(botaoEspirito, AoClicarEspirito);
        ConfigurarBotao(botaoDestreza, AoClicarDestreza);
        ConfigurarBotao(botaoForcaExtra, AoClicarForcaExtra);
        ConfigurarBotao(botaoPercepcao, AoClicarPercepcao);
        ConfigurarBotao(botaoResistencia, AoClicarResistencia);
        ConfigurarBotao(botaoCarisma, AoClicarCarisma);
        ConfigurarBotao(botaoSorte, AoClicarSorte);
    }

    private void ConfigurarBotao(Button botao, UnityAction acao)
    {
        if (botao == null || acao == null)
            return;

        botao.onClick.RemoveListener(acao);
        botao.onClick.AddListener(acao);
    }

    private void RemoverListenersBotoes()
    {
        RemoverListener(botaoForca, AoClicarForca);
        RemoverListener(botaoConstituicao, AoClicarConstituicao);
        RemoverListener(botaoAgilidade, AoClicarAgilidade);
        RemoverListener(botaoVelocidade, AoClicarVelocidade);
        RemoverListener(botaoInteligencia, AoClicarInteligencia);
        RemoverListener(botaoEspirito, AoClicarEspirito);
        RemoverListener(botaoDestreza, AoClicarDestreza);
        RemoverListener(botaoForcaExtra, AoClicarForcaExtra);
        RemoverListener(botaoPercepcao, AoClicarPercepcao);
        RemoverListener(botaoResistencia, AoClicarResistencia);
        RemoverListener(botaoCarisma, AoClicarCarisma);
        RemoverListener(botaoSorte, AoClicarSorte);
    }

    private void RemoverListener(Button botao, UnityAction acao)
    {
        if (botao != null && acao != null)
            botao.onClick.RemoveListener(acao);
    }

    private void AtualizarEstadoBotoes(bool podeGastarPonto)
    {
        DefinirInterativo(botaoForca, podeGastarPonto);
        DefinirInterativo(botaoConstituicao, podeGastarPonto);
        DefinirInterativo(botaoAgilidade, podeGastarPonto);
        DefinirInterativo(botaoVelocidade, podeGastarPonto);
        DefinirInterativo(botaoInteligencia, podeGastarPonto);
        DefinirInterativo(botaoEspirito, podeGastarPonto);
        DefinirInterativo(botaoDestreza, podeGastarPonto);
        DefinirInterativo(botaoForcaExtra, podeGastarPonto);
        DefinirInterativo(botaoPercepcao, podeGastarPonto);
        DefinirInterativo(botaoResistencia, podeGastarPonto);
        DefinirInterativo(botaoCarisma, podeGastarPonto);
        DefinirInterativo(botaoSorte, podeGastarPonto);
    }

    private void DefinirInterativo(Button botao, bool interativo)
    {
        if (botao != null)
            botao.interactable = interativo;
    }

    private void AoClicarForca()
    {
        GastarPonto(statusPlayer => statusPlayer.AdicionarPontoForca());
    }

    private void AoClicarConstituicao()
    {
        GastarPonto(statusPlayer => statusPlayer.AdicionarPontoConstituicao());
    }

    private void AoClicarAgilidade()
    {
        GastarPonto(statusPlayer => statusPlayer.AdicionarPontoAgilidade());
    }

    private void AoClicarVelocidade()
    {
        GastarPonto(statusPlayer => statusPlayer.AdicionarPontoVelocidade());
    }

    private void AoClicarInteligencia()
    {
        GastarPonto(statusPlayer => statusPlayer.AdicionarPontoInteligencia());
    }

    private void AoClicarEspirito()
    {
        GastarPonto(statusPlayer => statusPlayer.AdicionarPontoEspirito());
    }

    private void AoClicarDestreza()
    {
        GastarPonto(statusPlayer => statusPlayer.AdicionarPontoDestreza());
    }

    private void AoClicarForcaExtra()
    {
        GastarPonto(statusPlayer => statusPlayer.AdicionarPontoForcaExtra());
    }

    private void AoClicarPercepcao()
    {
        GastarPonto(statusPlayer => statusPlayer.AdicionarPontoPercepcao());
    }

    private void AoClicarResistencia()
    {
        GastarPonto(statusPlayer => statusPlayer.AdicionarPontoResistencia());
    }

    private void AoClicarCarisma()
    {
        GastarPonto(statusPlayer => statusPlayer.AdicionarPontoCarisma());
    }

    private void AoClicarSorte()
    {
        GastarPonto(statusPlayer => statusPlayer.AdicionarPontoSorte());
    }

    private void GastarPonto(Func<StatusPlayer, bool> acao)
    {
        EncontrarStatusPlayerSeNecessario();

        if (statusPlayer == null || acao == null)
            return;

        if (acao(statusPlayer))
        {
            TocarSom(somCliqueBotao);
            AtualizarUI();
            return;
        }

        TocarSom(somSemPonto);
    }

    private void EncontrarAudioSourceSeNecessario()
    {
        if (audioSource != null)
            return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void TocarSom(AudioClip clip)
    {
        if (clip == null)
            return;

        EncontrarAudioSourceSeNecessario();
        if (audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    private void EncontrarBotoesSeNecessario()
    {
        Button[] botoes = GetComponentsInChildren<Button>(true);

        botaoForca = EncontrarBotao(botaoForca, botoes, "strength", "forca");
        botaoConstituicao = EncontrarBotao(botaoConstituicao, botoes, "constitution", "constituicao");
        botaoAgilidade = EncontrarBotao(botaoAgilidade, botoes, "agility", "agilidade");
        botaoVelocidade = EncontrarBotao(botaoVelocidade, botoes, "speed", "velocidade");
        botaoInteligencia = EncontrarBotao(botaoInteligencia, botoes, "intelligence", "inteligencia");
        botaoEspirito = EncontrarBotao(botaoEspirito, botoes, "spirit", "espirito");
        botaoDestreza = EncontrarBotao(botaoDestreza, botoes, "dexterity", "destreza");
        botaoForcaExtra = EncontrarBotao(botaoForcaExtra, botoes, "force", "vigor", "forcaextra", "forcamagica");
        botaoPercepcao = EncontrarBotao(botaoPercepcao, botoes, "perception", "percepcao");
        botaoResistencia = EncontrarBotao(botaoResistencia, botoes, "resistance", "resistencia");
        botaoCarisma = EncontrarBotao(botaoCarisma, botoes, "charisma", "carisma");
        botaoSorte = EncontrarBotao(botaoSorte, botoes, "luck", "sorte");
    }

    private Button EncontrarBotao(Button atual, Button[] botoes, params string[] nomes)
    {
        if (botoes == null)
            return atual;

        Button candidato = null;
        for (int i = 0; i < botoes.Length; i++)
        {
            Button botao = botoes[i];
            if (botao == null)
                continue;

            string nomeNormalizado = NormalizarNomeObjeto(botao.gameObject.name);
            if (!CombinaComNomes(nomeNormalizado, nomes))
                continue;

            if (EhBotaoPreferencial(botao.gameObject.name, nomeNormalizado))
                return botao;

            if (candidato == null)
                candidato = botao;
        }

        return candidato != null ? candidato : atual;
    }

    private bool EhBotaoPreferencial(string nomeOriginal, string nomeNormalizado)
    {
        return Contem(nomeOriginal, "(4)") ||
               Contem(nomeNormalizado, "button") ||
               Contem(nomeNormalizado, "botao") ||
               Contem(nomeNormalizado, "plus") ||
               Contem(nomeNormalizado, "mais") ||
               Contem(nomeNormalizado, "up");
    }

    private void EncontrarTextosSeNecessario()
    {
        TextMeshProUGUI[] textos = GetComponentsInChildren<TextMeshProUGUI>(true);

        textoNomeValor = EncontrarTexto(textoNomeValor, textos, "NAME (1)");
        textoRacaValor = EncontrarTexto(textoRacaValor, textos, "RACE (1)");
        textoClasseValor = EncontrarTexto(textoClasseValor, textos, "CLASS (1)");
        textoTituloValor = EncontrarTexto(textoTituloValor, textos, "TITLE (1)", "TITLES (1)");
        textoNivelValor = EncontrarTexto(textoNivelValor, textos, "LEVEL (1)");
        textoPontosStatusValor = EncontrarTexto(textoPontosStatusValor, textos, "STATUS POINTS (1)");
        textoManaAtualValor = EncontrarTexto(textoManaAtualValor, textos, "MANNA (1)", "MANA (1)");
        textoManaMaximaValor = EncontrarTexto(textoManaMaximaValor, textos, "MANNA (3)", "MANA (3)");
        textoExperienciaAtualValor = EncontrarTexto(textoExperienciaAtualValor, textos, "EXPERIENCE (1)");
        textoExperienciaMaximaValor = EncontrarTexto(textoExperienciaMaximaValor, textos, "EXPERIENCE (3)");
        textoReinRotulo = EncontrarTexto(textoReinRotulo, textos, "REIN", "GOLD", "MOEDA", "CURRENCY");
        textoReinValor = EncontrarTexto(textoReinValor, textos, "REIN (1)", "GOLD (1)", "MOEDA (1)", "CURRENCY (1)");

        textoForcaBaseValor = EncontrarTexto(textoForcaBaseValor, textos, "STRENGTH (1)");
        textoForcaBonusValor = EncontrarTexto(textoForcaBonusValor, textos, "STRENGTH (2)");
        textoConstituicaoBaseValor = EncontrarTexto(textoConstituicaoBaseValor, textos, "CONSTITUTION (1)");
        textoConstituicaoBonusValor = EncontrarTexto(textoConstituicaoBonusValor, textos, "CONSTITUTION (2)");
        textoAgilidadeBaseValor = EncontrarTexto(textoAgilidadeBaseValor, textos, "AGILITY (1)");
        textoAgilidadeBonusValor = EncontrarTexto(textoAgilidadeBonusValor, textos, "AGILITY (2)");
        textoVelocidadeBaseValor = EncontrarTexto(textoVelocidadeBaseValor, textos, "SPEED (1)");
        textoVelocidadeBonusValor = EncontrarTexto(textoVelocidadeBonusValor, textos, "SPEED (2)");
        textoInteligenciaBaseValor = EncontrarTexto(textoInteligenciaBaseValor, textos, "INTELLIGENCE (1)");
        textoInteligenciaBonusValor = EncontrarTexto(textoInteligenciaBonusValor, textos, "INTELLIGENCE (2)");
        textoEspiritoBaseValor = EncontrarTexto(textoEspiritoBaseValor, textos, "SPIRIT (1)");
        textoEspiritoBonusValor = EncontrarTexto(textoEspiritoBonusValor, textos, "SPIRIT (2)");
        textoDestrezaBaseValor = EncontrarTexto(textoDestrezaBaseValor, textos, "DEXTERITY (1)");
        textoDestrezaBonusValor = EncontrarTexto(textoDestrezaBonusValor, textos, "DEXTERITY (2)");
        textoVigorBaseValor = EncontrarTexto(textoVigorBaseValor, textos, "FORCE (1)");
        textoVigorBonusValor = EncontrarTexto(textoVigorBonusValor, textos, "FORCE (2)");
        textoPercepcaoBaseValor = EncontrarTexto(textoPercepcaoBaseValor, textos, "PERCEPTION (1)");
        textoPercepcaoBonusValor = EncontrarTexto(textoPercepcaoBonusValor, textos, "PERCEPTION (2)");
        textoResistenciaBaseValor = EncontrarTexto(textoResistenciaBaseValor, textos, "RESISTANCE (1)");
        textoResistenciaBonusValor = EncontrarTexto(textoResistenciaBonusValor, textos, "RESISTANCE (2)");
        CorrigirReferenciasAntigasDeResistencia();
        textoCarismaBaseValor = EncontrarTexto(textoCarismaBaseValor, textos, "CHARISMA (1)");
        textoCarismaBonusValor = EncontrarTexto(textoCarismaBonusValor, textos, "CHARISMA (2)");
        textoSorteBaseValor = EncontrarTexto(textoSorteBaseValor, textos, "LUCK (1)");
        textoSorteBonusValor = EncontrarTexto(textoSorteBonusValor, textos, "LUCK (2)");
    }

    private void CorrigirReferenciasAntigasDeResistencia()
    {
        if (textoResistenciaBaseValor == null && TextoTemNome(textoDefesaBaseValor, "RESISTANCE (1)"))
            textoResistenciaBaseValor = textoDefesaBaseValor;

        if (textoResistenciaBonusValor == null && TextoTemNome(textoDefesaBonusValor, "RESISTANCE (2)"))
            textoResistenciaBonusValor = textoDefesaBonusValor;

        if (TextoTemNome(textoDefesaBaseValor, "RESISTANCE (1)", "RESISTANCE (2)"))
            textoDefesaBaseValor = null;

        if (TextoTemNome(textoDefesaBonusValor, "RESISTANCE (1)", "RESISTANCE (2)"))
            textoDefesaBonusValor = null;
    }

    private TextMeshProUGUI EncontrarTexto(TextMeshProUGUI atual, TextMeshProUGUI[] textos, params string[] nomes)
    {
        if (atual != null || textos == null)
            return atual;

        for (int i = 0; i < textos.Length; i++)
        {
            TextMeshProUGUI texto = textos[i];
            if (texto == null)
                continue;

            string nomeNormalizado = NormalizarNomeObjeto(texto.gameObject.name);
            if (CombinaComNomes(nomeNormalizado, nomes))
                return texto;
        }

        return null;
    }

    private bool CombinaComNomes(string nomeNormalizado, string[] nomes)
    {
        if (string.IsNullOrWhiteSpace(nomeNormalizado) || nomes == null)
            return false;

        for (int i = 0; i < nomes.Length; i++)
        {
            string alvo = NormalizarNomeObjeto(nomes[i]);
            if (string.IsNullOrWhiteSpace(alvo))
                continue;

            if (nomeNormalizado == alvo || nomeNormalizado.StartsWith(alvo, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private bool TextoTemNome(TextMeshProUGUI texto, params string[] nomes)
    {
        return texto != null && CombinaComNomes(NormalizarNomeObjeto(texto.gameObject.name), nomes);
    }

    private bool Contem(string texto, string trecho)
    {
        return !string.IsNullOrEmpty(texto) &&
               !string.IsNullOrEmpty(trecho) &&
               texto.IndexOf(trecho, StringComparison.Ordinal) >= 0;
    }

    private string NormalizarNomeObjeto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        string normalizado = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder resultado = new StringBuilder(normalizado.Length);

        for (int i = 0; i < normalizado.Length; i++)
        {
            UnicodeCategory categoria = CharUnicodeInfo.GetUnicodeCategory(normalizado[i]);
            if (categoria != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(normalizado[i]))
                resultado.Append(normalizado[i]);
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }

    private void DefinirTexto(TextMeshProUGUI texto, string valor)
    {
        if (texto != null)
            texto.text = valor;
    }

    private string FormatarBonus(int valor)
    {
        return valor >= 0 ? "+" + valor : valor.ToString();
    }
}
