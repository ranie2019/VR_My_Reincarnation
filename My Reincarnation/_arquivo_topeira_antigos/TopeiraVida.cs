using System;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// TopeiraVida
// Vida, recebimento de dano e morte. NAO decide comportamento: avisa
// quem estiver ouvindo (a TopeiraIA) pelos eventos Danificado e Morreu.
// Este e o unico componente da Topeira com ReceberDano(int, GameObject),
// que Espada, Flecha, Fogo etc. encontram (direto ou via ReceptorDanoInimigo).
// =====================================================================
[DisallowMultipleComponent]
public class TopeiraVida : MonoBehaviour
{
    #region Configuracao
    [Header("Vida")]
    [SerializeField, Min(1)] private int vidaMaxima = 10;
    [SerializeField] private int vidaAtual = 10;

    [Header("UI de vida (opcional)")]
    [SerializeField] private Image imagemBarraVida;
    [SerializeField] private Canvas canvasVida;
    [SerializeField] private bool buscarBarraAutomaticamente = true;
    [SerializeField] private bool esconderCanvasAoMorrer = true;
    #endregion

    #region Estado e eventos
    private bool morto;
    private GameObject ultimaOrigemDano;
    private ExperienciaInimigo experiencia;

    public int VidaAtual => vidaAtual;
    public int VidaMaxima => vidaMaxima;
    public bool Morto => morto;
    public GameObject UltimaOrigemDano => ultimaOrigemDano;

    /// <summary>Dano aplicado e origem. Nao dispara na morte (use Morreu).</summary>
    public event Action<int, GameObject> Danificado;
    /// <summary>Disparado uma unica vez, com a origem do ultimo dano.</summary>
    public event Action<GameObject> Morreu;
    #endregion

    #region Ciclo de vida
    private void Awake()
    {
        experiencia = GetComponent<ExperienciaInimigo>();
        vidaAtual = Mathf.Clamp(vidaAtual <= 0 ? vidaMaxima : vidaAtual, 1, vidaMaxima);
        ConfigurarUI();
        AtualizarBarra();
    }

    private void OnValidate()
    {
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
    }
    #endregion

    #region Dano
    public void ReceberDano(int dano, GameObject origem)
    {
        if (morto || dano <= 0) return;

        ultimaOrigemDano = origem != null ? origem : ultimaOrigemDano;
        if (experiencia != null) experiencia.RegistrarPrimeiroAtacante(origem);

        vidaAtual = Mathf.Max(0, vidaAtual - dano);
        AtualizarBarra();

        if (vidaAtual <= 0)
        {
            Morrer();
            return;
        }

        Danificado?.Invoke(dano, origem);
    }
    #endregion

    #region Morte
    private void Morrer()
    {
        if (morto) return;
        morto = true;

        if (esconderCanvasAoMorrer && canvasVida != null) canvasVida.gameObject.SetActive(false);
        Morreu?.Invoke(ultimaOrigemDano);
    }
    #endregion

    #region UI
    private void ConfigurarUI()
    {
        if (canvasVida == null) canvasVida = GetComponentInChildren<Canvas>(true);
        if (imagemBarraVida != null || !buscarBarraAutomaticamente) return;

        foreach (Image imagem in GetComponentsInChildren<Image>(true))
        {
            if (imagem.name == "Vida Frente") { imagemBarraVida = imagem; break; }
        }
    }

    private void AtualizarBarra()
    {
        if (imagemBarraVida != null)
            imagemBarraVida.fillAmount = Mathf.Clamp01((float)vidaAtual / vidaMaxima);
    }
    #endregion
}
