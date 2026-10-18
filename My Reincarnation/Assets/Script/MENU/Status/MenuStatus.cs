using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Exibe apenas as informações do painel de status da imagem:
/// NAME, RACE, CLASS, LEVEL, BLOOD, MANNA, STATUS POINTS, REIN, EXPERIENCE.
/// Arraste os TextMeshProUGUI no Inspector ou nomeie os objetos conforme
/// os nomes esperados — o script os encontra automaticamente.
/// </summary>
[DisallowMultipleComponent]
public class MenuStatus : MonoBehaviour
{
    [Header("Referência do Player")]
    [SerializeField] private StatusPlayer statusPlayer;

    [Header("Identidade")]
    [SerializeField] private TextMeshProUGUI textoNome;
    [SerializeField] private TextMeshProUGUI textoRaca;
    [SerializeField] private TextMeshProUGUI textoClasse;

    [Header("Level")]
    [SerializeField] private TextMeshProUGUI textoNivel;
    [SerializeField] private TextMeshProUGUI textoPontosStatus;

    [Header("Blood (Vida)")]
    [SerializeField] private TextMeshProUGUI textoVidaAtual;
    [SerializeField] private TextMeshProUGUI textoVidaMaxima;

    [Header("Manna (Mana)")]
    [SerializeField] private TextMeshProUGUI textoManaAtual;
    [SerializeField] private TextMeshProUGUI textoManaMaxima;

    [Header("Rein (Moeda)")]
    [SerializeField] private TextMeshProUGUI textoRein;

    [Header("Experience")]
    [SerializeField] private TextMeshProUGUI textoExpAtual;
    [SerializeField] private TextMeshProUGUI textoExpMaxima;

    [Header("Configuração")]
    [SerializeField] private bool atualizarContinuamente = true;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        BuscarStatusPlayer();
        BuscarTextos();
    }

    private void OnEnable()
    {
        BuscarStatusPlayer();
        BuscarTextos();
        AtualizarUI();
    }

    private void Update()
    {
        if (atualizarContinuamente)
            AtualizarUI();
    }

    // =========================================================
    // ATUALIZAÇÃO DA UI
    // =========================================================

    public void AtualizarUI()
    {
        BuscarStatusPlayer();

        if (statusPlayer == null) return;

        Definir(textoNome,          statusPlayer.GetNome());
        Definir(textoRaca,          statusPlayer.GetRaca());
        Definir(textoClasse,        statusPlayer.GetClasse());
        Definir(textoNivel,         statusPlayer.GetNivel().ToString());
        Definir(textoPontosStatus,  statusPlayer.GetPontosStatusDisponiveis().ToString());
        Definir(textoVidaAtual,     statusPlayer.GetVidaAtual().ToString());
        Definir(textoVidaMaxima,    statusPlayer.GetVidaMaxima().ToString());
        Definir(textoManaAtual,     statusPlayer.GetManaAtual().ToString());
        Definir(textoManaMaxima,    statusPlayer.GetManaMaxima().ToString());
        Definir(textoRein,          statusPlayer.ObterReinFormatado());
        Definir(textoExpAtual,      statusPlayer.GetExperienciaAtual().ToString());
        Definir(textoExpMaxima,     statusPlayer.GetExperienciaParaProximoNivel().ToString());
    }

    // =========================================================
    // BUSCA AUTOMÁTICA — StatusPlayer
    // =========================================================

    private void BuscarStatusPlayer()
    {
        if (statusPlayer != null) return;
        statusPlayer = FindFirstObjectByType<StatusPlayer>();
    }

    // =========================================================
    // BUSCA AUTOMÁTICA — Textos por nome do GameObject
    // Nomes esperados na hierarquia (case-insensitive, sem acento):
    //   NAME (1), RACE (1), CLASS (1), LEVEL (1), STATUS POINTS (1),
    //   BLOOD (1), BLOOD (3), MANNA (1), MANNA (3),
    //   REIN (1), EXPERIENCE (1), EXPERIENCE (3)
    // =========================================================

    private void BuscarTextos()
    {
        TextMeshProUGUI[] todos = GetComponentsInChildren<TextMeshProUGUI>(true);

        textoNome         = Buscar(textoNome,         todos, "NAME (1)");
        textoRaca         = Buscar(textoRaca,         todos, "RACE (1)");
        textoClasse       = Buscar(textoClasse,       todos, "CLASS (1)");
        textoNivel        = Buscar(textoNivel,        todos, "LEVEL (1)");
        textoPontosStatus = Buscar(textoPontosStatus, todos, "STATUS POINTS (1)");
        textoVidaAtual    = Buscar(textoVidaAtual,    todos, "BLOOD (1)");
        textoVidaMaxima   = Buscar(textoVidaMaxima,   todos, "BLOOD (3)");
        textoManaAtual    = Buscar(textoManaAtual,    todos, "MANNA (1)", "MANA (1)");
        textoManaMaxima   = Buscar(textoManaMaxima,   todos, "MANNA (3)", "MANA (3)");
        textoRein         = Buscar(textoRein,         todos, "REIN (1)", "GOLD (1)");
        textoExpAtual     = Buscar(textoExpAtual,     todos, "EXPERIENCE (1)");
        textoExpMaxima    = Buscar(textoExpMaxima,    todos, "EXPERIENCE (3)");
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void Definir(TextMeshProUGUI campo, string valor)
    {
        if (campo != null) campo.text = valor;
    }

    private TextMeshProUGUI Buscar(TextMeshProUGUI atual, TextMeshProUGUI[] lista, params string[] nomes)
    {
        if (atual != null) return atual;

        for (int i = 0; i < lista.Length; i++)
        {
            if (lista[i] == null) continue;
            string normalizado = Normalizar(lista[i].gameObject.name);
            for (int j = 0; j < nomes.Length; j++)
            {
                if (normalizado == Normalizar(nomes[j]))
                    return lista[i];
            }
        }
        return null;
    }

    private string Normalizar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        string s = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb  = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(c) || c == ' ' || c == '(' || c == ')'))
                sb.Append(c);
        }
        return sb.ToString().Trim().Normalize(NormalizationForm.FormC);
    }
}