using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Controla apenas o painel de atributos do menu de status.
///
/// ATTRIBUTES:      Strength, Constitution, Agility, Speed, Intelligence, Spirit
/// EXTRA ATTRIBUTES: Dexterity, Force, Perception, Resistance, Charisma, Luck
///
/// Cada atributo exibe: valor base | botão + | valor bonus.
/// O botão só fica interativo quando há pontos de status disponíveis.
/// </summary>
[DisallowMultipleComponent]
public class MenuAtributos : MonoBehaviour
{
    [Header("Referência do Player")]
    [SerializeField] private StatusPlayer statusPlayer;

    // =========================================================
    // ATTRIBUTES
    // =========================================================

    [Header("Strength")]
    [SerializeField] private TextMeshProUGUI textoForcaBase;
    [SerializeField] private TextMeshProUGUI textoForcaBonus;
    [SerializeField] private Button          botaoForca;

    [Header("Constitution")]
    [SerializeField] private TextMeshProUGUI textoConstituicaoBase;
    [SerializeField] private TextMeshProUGUI textoConstituicaoBonus;
    [SerializeField] private Button          botaoConstituicao;

    [Header("Agility")]
    [SerializeField] private TextMeshProUGUI textoAgilidadeBase;
    [SerializeField] private TextMeshProUGUI textoAgilidadeBonus;
    [SerializeField] private Button          botaoAgilidade;

    [Header("Speed")]
    [SerializeField] private TextMeshProUGUI textoVelocidadeBase;
    [SerializeField] private TextMeshProUGUI textoVelocidadeBonus;
    [SerializeField] private Button          botaoVelocidade;

    [Header("Intelligence")]
    [SerializeField] private TextMeshProUGUI textoInteligenciaBase;
    [SerializeField] private TextMeshProUGUI textoInteligenciaBonus;
    [SerializeField] private Button          botaoInteligencia;

    [Header("Spirit")]
    [SerializeField] private TextMeshProUGUI textoEspiritoBase;
    [SerializeField] private TextMeshProUGUI textoEspiritoBonus;
    [SerializeField] private Button          botaoEspirito;

    // =========================================================
    // EXTRA ATTRIBUTES
    // =========================================================

    [Header("Dexterity")]
    [SerializeField] private TextMeshProUGUI textoDestrezaBase;
    [SerializeField] private TextMeshProUGUI textoDestrezaBonus;
    [SerializeField] private Button          botaoDestreza;

    [Header("Force (Vigor)")]
    [SerializeField] private TextMeshProUGUI textoVigorBase;
    [SerializeField] private TextMeshProUGUI textoVigorBonus;
    [SerializeField] private Button          botaoVigor;

    [Header("Perception")]
    [SerializeField] private TextMeshProUGUI textoPercepcaoBase;
    [SerializeField] private TextMeshProUGUI textoPercepcaoBonus;
    [SerializeField] private Button          botaoPercepcao;

    [Header("Resistance")]
    [SerializeField] private TextMeshProUGUI textoResistenciaBase;
    [SerializeField] private TextMeshProUGUI textoResistenciaBonus;
    [SerializeField] private Button          botaoResistencia;

    [Header("Charisma")]
    [SerializeField] private TextMeshProUGUI textoCarismaBase;
    [SerializeField] private TextMeshProUGUI textoCarismaBonus;
    [SerializeField] private Button          botaoCarisma;

    [Header("Luck")]
    [SerializeField] private TextMeshProUGUI textoSorteBase;
    [SerializeField] private TextMeshProUGUI textoSorteBonus;
    [SerializeField] private Button          botaoSorte;

    // =========================================================
    // ÁUDIO
    // =========================================================

    [Header("Áudio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   somClique;
    [SerializeField] private AudioClip   somSemPonto;

    // =========================================================
    // CONFIGURAÇÃO
    // =========================================================

    [Header("Configuração")]
    [SerializeField] private bool atualizarContinuamente = true;

    // =========================================================
    // ESTADO INTERNO
    // =========================================================

    private StatusPlayer playerInscrito;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        BuscarStatusPlayer();
        BuscarTextos();
        BuscarBotoes();
        PrepararAudio();
        ConfigurarBotoes();
    }

    private void OnEnable()
    {
        BuscarStatusPlayer();
        InscreverPlayer();
        PrepararAudio();
        ConfigurarBotoes();
        AtualizarUI();
    }

    private void OnDisable()  => DesinscreverPlayer();
    private void OnDestroy()  { DesinscreverPlayer(); RemoverListeners(); }

    private void Update()
    {
        if (atualizarContinuamente)
            AtualizarUI();
    }

    // =========================================================
    // ATUALIZAÇÃO
    // =========================================================

    public void AtualizarUI()
    {
        BuscarStatusPlayer();
        if (statusPlayer == null) { AtualizarBotoes(false); return; }

        // ATTRIBUTES
        Definir(textoForcaBase,        statusPlayer.GetForca().ToString());
        Definir(textoForcaBonus,       Bonus(statusPlayer.GetBonusForca()));
        Definir(textoConstituicaoBase,  statusPlayer.GetConstituicao().ToString());
        Definir(textoConstituicaoBonus, Bonus(statusPlayer.GetBonusConstituicao()));
        Definir(textoAgilidadeBase,    statusPlayer.GetAgilidade().ToString());
        Definir(textoAgilidadeBonus,   Bonus(statusPlayer.GetBonusAgilidade()));
        Definir(textoVelocidadeBase,   statusPlayer.GetVelocidade().ToString());
        Definir(textoVelocidadeBonus,  Bonus(statusPlayer.GetBonusVelocidade()));
        Definir(textoInteligenciaBase,  statusPlayer.GetInteligencia().ToString());
        Definir(textoInteligenciaBonus, Bonus(statusPlayer.GetBonusInteligencia()));
        Definir(textoEspiritoBase,     statusPlayer.GetEspirito().ToString());
        Definir(textoEspiritoBonus,    Bonus(statusPlayer.GetBonusEspirito()));

        // EXTRA ATTRIBUTES
        Definir(textoDestrezaBase,     statusPlayer.GetDestreza().ToString());
        Definir(textoDestrezaBonus,    Bonus(statusPlayer.GetBonusDestreza()));
        Definir(textoVigorBase,        statusPlayer.GetVigor().ToString());
        Definir(textoVigorBonus,       Bonus(statusPlayer.GetBonusVigor()));
        Definir(textoPercepcaoBase,    statusPlayer.GetPercepcao().ToString());
        Definir(textoPercepcaoBonus,   Bonus(statusPlayer.GetBonusPercepcao()));
        Definir(textoResistenciaBase,  statusPlayer.GetResistencia().ToString());
        Definir(textoResistenciaBonus, Bonus(statusPlayer.GetBonusResistencia()));
        Definir(textoCarismaBase,      statusPlayer.GetCarisma().ToString());
        Definir(textoCarismaBonus,     Bonus(statusPlayer.GetBonusCarisma()));
        Definir(textoSorteBase,        statusPlayer.GetSorte().ToString());
        Definir(textoSorteBonus,       Bonus(statusPlayer.GetBonusSorte()));

        // Botões ativos somente se há pontos disponíveis
        bool temPonto = statusPlayer.GetPontosStatusDisponiveis() > 0;
        AtualizarBotoes(temPonto);
    }

    // =========================================================
    // CLIQUES DOS BOTÕES
    // =========================================================

    private void AoClicarForca()        => Gastar(p => p.AdicionarPontoForca());
    private void AoClicarConstituicao() => Gastar(p => p.AdicionarPontoConstituicao());
    private void AoClicarAgilidade()    => Gastar(p => p.AdicionarPontoAgilidade());
    private void AoClicarVelocidade()   => Gastar(p => p.AdicionarPontoVelocidade());
    private void AoClicarInteligencia() => Gastar(p => p.AdicionarPontoInteligencia());
    private void AoClicarEspirito()     => Gastar(p => p.AdicionarPontoEspirito());
    private void AoClicarDestreza()     => Gastar(p => p.AdicionarPontoDestreza());
    private void AoClicarVigor()        => Gastar(p => p.AdicionarPontoForcaExtra());
    private void AoClicarPercepcao()    => Gastar(p => p.AdicionarPontoPercepcao());
    private void AoClicarResistencia()  => Gastar(p => p.AdicionarPontoResistencia());
    private void AoClicarCarisma()      => Gastar(p => p.AdicionarPontoCarisma());
    private void AoClicarSorte()        => Gastar(p => p.AdicionarPontoSorte());

    private void Gastar(Func<StatusPlayer, bool> acao)
    {
        BuscarStatusPlayer();
        if (statusPlayer == null || acao == null) return;

        if (acao(statusPlayer))
        {
            Tocar(somClique);
            AtualizarUI();
        }
        else
        {
            Tocar(somSemPonto);
        }
    }

    // =========================================================
    // CONFIGURAÇÃO DE BOTÕES
    // =========================================================

    private void ConfigurarBotoes()
    {
        Ligar(botaoForca,        AoClicarForca);
        Ligar(botaoConstituicao, AoClicarConstituicao);
        Ligar(botaoAgilidade,    AoClicarAgilidade);
        Ligar(botaoVelocidade,   AoClicarVelocidade);
        Ligar(botaoInteligencia, AoClicarInteligencia);
        Ligar(botaoEspirito,     AoClicarEspirito);
        Ligar(botaoDestreza,     AoClicarDestreza);
        Ligar(botaoVigor,        AoClicarVigor);
        Ligar(botaoPercepcao,    AoClicarPercepcao);
        Ligar(botaoResistencia,  AoClicarResistencia);
        Ligar(botaoCarisma,      AoClicarCarisma);
        Ligar(botaoSorte,        AoClicarSorte);
    }

    private void RemoverListeners()
    {
        Desligar(botaoForca,        AoClicarForca);
        Desligar(botaoConstituicao, AoClicarConstituicao);
        Desligar(botaoAgilidade,    AoClicarAgilidade);
        Desligar(botaoVelocidade,   AoClicarVelocidade);
        Desligar(botaoInteligencia, AoClicarInteligencia);
        Desligar(botaoEspirito,     AoClicarEspirito);
        Desligar(botaoDestreza,     AoClicarDestreza);
        Desligar(botaoVigor,        AoClicarVigor);
        Desligar(botaoPercepcao,    AoClicarPercepcao);
        Desligar(botaoResistencia,  AoClicarResistencia);
        Desligar(botaoCarisma,      AoClicarCarisma);
        Desligar(botaoSorte,        AoClicarSorte);
    }

    private void AtualizarBotoes(bool ativo)
    {
        Interativo(botaoForca,        ativo);
        Interativo(botaoConstituicao, ativo);
        Interativo(botaoAgilidade,    ativo);
        Interativo(botaoVelocidade,   ativo);
        Interativo(botaoInteligencia, ativo);
        Interativo(botaoEspirito,     ativo);
        Interativo(botaoDestreza,     ativo);
        Interativo(botaoVigor,        ativo);
        Interativo(botaoPercepcao,    ativo);
        Interativo(botaoResistencia,  ativo);
        Interativo(botaoCarisma,      ativo);
        Interativo(botaoSorte,        ativo);
    }

    private void Ligar(Button b, UnityAction a)    { if (b != null && a != null) { b.onClick.RemoveListener(a); b.onClick.AddListener(a); } }
    private void Desligar(Button b, UnityAction a) { if (b != null && a != null) b.onClick.RemoveListener(a); }
    private void Interativo(Button b, bool v)      { if (b != null) b.interactable = v; }

    // =========================================================
    // INSCRIÇÃO NO EVENTO DO PLAYER
    // =========================================================

    private void InscreverPlayer()
    {
        if (playerInscrito == statusPlayer) return;
        DesinscreverPlayer();
        playerInscrito = statusPlayer;
        if (playerInscrito != null) playerInscrito.StatusAlterado += AtualizarUI;
    }

    private void DesinscreverPlayer()
    {
        if (playerInscrito != null) playerInscrito.StatusAlterado -= AtualizarUI;
        playerInscrito = null;
    }

    // =========================================================
    // BUSCA AUTOMÁTICA — Player
    // =========================================================

    private void BuscarStatusPlayer()
    {
        if (statusPlayer == null)
            statusPlayer = FindFirstObjectByType<StatusPlayer>();
    }

    // =========================================================
    // BUSCA AUTOMÁTICA — Textos por nome do GameObject
    // Nomes esperados (case-insensitive, sem acento):
    //   STRENGTH (1)/(2), CONSTITUTION (1)/(2), AGILITY (1)/(2),
    //   SPEED (1)/(2), INTELLIGENCE (1)/(2), SPIRIT (1)/(2),
    //   DEXTERITY (1)/(2), FORCE (1)/(2), PERCEPTION (1)/(2),
    //   RESISTANCE (1)/(2), CHARISMA (1)/(2), LUCK (1)/(2)
    // =========================================================

    private void BuscarTextos()
    {
        TextMeshProUGUI[] t = GetComponentsInChildren<TextMeshProUGUI>(true);

        textoForcaBase        = Buscar(textoForcaBase,        t, "STRENGTH (1)");
        textoForcaBonus       = Buscar(textoForcaBonus,       t, "STRENGTH (2)");
        textoConstituicaoBase  = Buscar(textoConstituicaoBase,  t, "CONSTITUTION (1)");
        textoConstituicaoBonus = Buscar(textoConstituicaoBonus, t, "CONSTITUTION (2)");
        textoAgilidadeBase    = Buscar(textoAgilidadeBase,    t, "AGILITY (1)");
        textoAgilidadeBonus   = Buscar(textoAgilidadeBonus,   t, "AGILITY (2)");
        textoVelocidadeBase   = Buscar(textoVelocidadeBase,   t, "SPEED (1)");
        textoVelocidadeBonus  = Buscar(textoVelocidadeBonus,  t, "SPEED (2)");
        textoInteligenciaBase  = Buscar(textoInteligenciaBase,  t, "INTELLIGENCE (1)");
        textoInteligenciaBonus = Buscar(textoInteligenciaBonus, t, "INTELLIGENCE (2)");
        textoEspiritoBase     = Buscar(textoEspiritoBase,     t, "SPIRIT (1)");
        textoEspiritoBonus    = Buscar(textoEspiritoBonus,    t, "SPIRIT (2)");

        textoDestrezaBase     = Buscar(textoDestrezaBase,     t, "DEXTERITY (1)");
        textoDestrezaBonus    = Buscar(textoDestrezaBonus,    t, "DEXTERITY (2)");
        textoVigorBase        = Buscar(textoVigorBase,        t, "FORCE (1)");
        textoVigorBonus       = Buscar(textoVigorBonus,       t, "FORCE (2)");
        textoPercepcaoBase    = Buscar(textoPercepcaoBase,    t, "PERCEPTION (1)");
        textoPercepcaoBonus   = Buscar(textoPercepcaoBonus,   t, "PERCEPTION (2)");
        textoResistenciaBase  = Buscar(textoResistenciaBase,  t, "RESISTANCE (1)");
        textoResistenciaBonus = Buscar(textoResistenciaBonus, t, "RESISTANCE (2)");
        textoCarismaBase      = Buscar(textoCarismaBase,      t, "CHARISMA (1)");
        textoCarismaBonus     = Buscar(textoCarismaBonus,     t, "CHARISMA (2)");
        textoSorteBase        = Buscar(textoSorteBase,        t, "LUCK (1)");
        textoSorteBonus       = Buscar(textoSorteBonus,       t, "LUCK (2)");
    }

    // =========================================================
    // BUSCA AUTOMÁTICA — Botões por nome do GameObject
    // =========================================================

    private void BuscarBotoes()
    {
        Button[] b = GetComponentsInChildren<Button>(true);

        botaoForca        = BuscarBotao(botaoForca,        b, "strength",      "forca");
        botaoConstituicao = BuscarBotao(botaoConstituicao, b, "constitution",  "constituicao");
        botaoAgilidade    = BuscarBotao(botaoAgilidade,    b, "agility",       "agilidade");
        botaoVelocidade   = BuscarBotao(botaoVelocidade,   b, "speed",         "velocidade");
        botaoInteligencia = BuscarBotao(botaoInteligencia, b, "intelligence",  "inteligencia");
        botaoEspirito     = BuscarBotao(botaoEspirito,     b, "spirit",        "espirito");
        botaoDestreza     = BuscarBotao(botaoDestreza,     b, "dexterity",     "destreza");
        botaoVigor        = BuscarBotao(botaoVigor,        b, "force",         "vigor", "forcaextra");
        botaoPercepcao    = BuscarBotao(botaoPercepcao,    b, "perception",    "percepcao");
        botaoResistencia  = BuscarBotao(botaoResistencia,  b, "resistance",    "resistencia");
        botaoCarisma      = BuscarBotao(botaoCarisma,      b, "charisma",      "carisma");
        botaoSorte        = BuscarBotao(botaoSorte,        b, "luck",          "sorte");
    }

    private Button BuscarBotao(Button atual, Button[] lista, params string[] nomes)
    {
        if (atual != null) return atual;
        for (int i = 0; i < lista.Length; i++)
        {
            if (lista[i] == null) continue;
            string n = Normalizar(lista[i].gameObject.name);
            for (int j = 0; j < nomes.Length; j++)
                if (n.Contains(Normalizar(nomes[j]))) return lista[i];
        }
        return null;
    }

    // =========================================================
    // ÁUDIO
    // =========================================================

    private void PrepararAudio()
    {
        if (audioSource != null) return;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake  = false;
        audioSource.spatialBlend = 0f;
    }

    private void Tocar(AudioClip clip)
    {
        if (clip == null) return;
        PrepararAudio();
        audioSource?.PlayOneShot(clip);
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void Definir(TextMeshProUGUI campo, string valor)
    {
        if (campo != null) campo.text = valor;
    }

    private string Bonus(int valor) => valor >= 0 ? "+" + valor : valor.ToString();

    private TextMeshProUGUI Buscar(TextMeshProUGUI atual, TextMeshProUGUI[] lista, params string[] nomes)
    {
        if (atual != null) return atual;
        for (int i = 0; i < lista.Length; i++)
        {
            if (lista[i] == null) continue;
            string n = Normalizar(lista[i].gameObject.name);
            for (int j = 0; j < nomes.Length; j++)
                if (n == Normalizar(nomes[j])) return lista[i];
        }
        return null;
    }

    private string Normalizar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        string s = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != UnicodeCategory.NonSpacingMark &&
                (char.IsLetterOrDigit(c) || c == ' ' || c == '(' || c == ')'))
                sb.Append(c);
        }
        return sb.ToString().Trim().Normalize(NormalizationForm.FormC);
    }
}