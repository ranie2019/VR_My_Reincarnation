using System;
using System.Globalization;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class StatusPlayer : MonoBehaviour
{
    private const float MultiplicadorVidaManaPorNivel = 1.10f;
    private const int ValorInicialAtributo = 10;

    public event Action StatusAlterado;

    [Header("Identidade")]
    [SerializeField] private string nome = "Ranie";
    [SerializeField] private string raca = "Humano";
    [SerializeField] private string classe = "Aventureiro";
    [SerializeField] private string titulo = "Reencarnado";

    [Header("Level")]
    [SerializeField] private int nivel;
    [SerializeField] private int experienciaAtual;
    [SerializeField] private int experienciaParaProximoNivel = 100;
    [SerializeField] private float multiplicadorExperienciaPorNivel = 1.10f;
    [SerializeField] private int pontosStatusDisponiveis;
    [SerializeField] private int pontosStatusPorNivel = 10;

    [Header("Level Up")]
    public AudioSource audioSource;
    public AudioClip somLevelUp;
    public ParticleSystem efeitoLevelUp;

    [Header("Vida e Mana")]
    [SerializeField] private int vidaAtual = 100;
    [SerializeField] private int vidaMaxima = 100;
    [SerializeField] private int manaAtual = 50;
    [SerializeField] private int manaMaxima = 50;

    [Header("Atributos Base")]
    [SerializeField] private int forca = ValorInicialAtributo;
    [SerializeField] private int constituicao = ValorInicialAtributo;
    [SerializeField] private int agilidade = ValorInicialAtributo;
    [SerializeField] private int velocidade = ValorInicialAtributo;
    [SerializeField] private int inteligencia = ValorInicialAtributo;
    [SerializeField] private int espirito = ValorInicialAtributo;
    [SerializeField] private int sorte = ValorInicialAtributo;

    [Header("Atributos Extras")]
    [SerializeField] private int destreza = ValorInicialAtributo;
    [SerializeField] private int vigor = ValorInicialAtributo;
    [SerializeField] private int percepcao = ValorInicialAtributo;
    [SerializeField] private int resistencia = ValorInicialAtributo;
    [SerializeField] private int mana;
    [SerializeField] private int carisma = ValorInicialAtributo;
    [SerializeField] private int critico;
    [SerializeField] private int defesa;

    [Header("Bonus Atributos Base")]
    [SerializeField] private int bonusForca;
    [SerializeField] private int bonusConstituicao;
    [SerializeField] private int bonusAgilidade;
    [SerializeField] private int bonusVelocidade;
    [SerializeField] private int bonusInteligencia;
    [SerializeField] private int bonusEspirito;
    [SerializeField] private int bonusSorte;

    [Header("Bonus Atributos Extras")]
    [SerializeField] private int bonusDestreza;
    [SerializeField] private int bonusVigor;
    [SerializeField] private int bonusPercepcao;
    [SerializeField] private int bonusResistencia;
    [SerializeField] private int bonusMana;
    [SerializeField] private int bonusCarisma;
    [SerializeField] private int bonusCritico;
    [SerializeField] private int bonusDefesa;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        GarantirAtributosIniciais();
    }

    private void OnValidate()
    {
        nivel = Mathf.Max(0, nivel);
        experienciaAtual = Mathf.Max(0, experienciaAtual);
        experienciaParaProximoNivel = Mathf.Max(1, experienciaParaProximoNivel);
        multiplicadorExperienciaPorNivel = Mathf.Max(1.01f, multiplicadorExperienciaPorNivel);
        pontosStatusDisponiveis = Mathf.Max(0, pontosStatusDisponiveis);
        pontosStatusPorNivel = Mathf.Max(0, pontosStatusPorNivel);
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        manaMaxima = Mathf.Max(1, manaMaxima);
        manaAtual = Mathf.Clamp(manaAtual, 0, manaMaxima);
        GarantirAtributosIniciais();
    }

    public void ReceberExperiencia(int quantidade)
    {
        if (quantidade <= 0)
            return;

        experienciaAtual += quantidade;
        VerificarLevelUp();
        NotificarStatusAlterado();
    }

    private void VerificarLevelUp()
    {
        experienciaParaProximoNivel = Mathf.Max(1, experienciaParaProximoNivel);

        while (experienciaAtual >= experienciaParaProximoNivel)
        {
            experienciaAtual -= experienciaParaProximoNivel;
            SubirNivel();
        }
    }

    private void SubirNivel()
    {
        nivel += 1;
        pontosStatusDisponiveis += pontosStatusPorNivel;
        experienciaParaProximoNivel = Mathf.Max(
            1,
            Mathf.CeilToInt(experienciaParaProximoNivel * Mathf.Max(1.01f, multiplicadorExperienciaPorNivel)));
        vidaMaxima = Mathf.Max(1, Mathf.CeilToInt(vidaMaxima * MultiplicadorVidaManaPorNivel));
        manaMaxima = Mathf.Max(1, Mathf.CeilToInt(manaMaxima * MultiplicadorVidaManaPorNivel));
        vidaAtual = vidaMaxima;
        manaAtual = manaMaxima;
        TocarFeedbackLevelUp();
    }

    public bool GastarPontoEmAtributo(string nomeAtributo)
    {
        if (pontosStatusDisponiveis <= 0 || string.IsNullOrWhiteSpace(nomeAtributo))
            return false;

        string atributo = NormalizarNomeAtributo(nomeAtributo);

        switch (atributo)
        {
            case "forca":
            case "strength":
                return AdicionarPontoForca();
            case "constituicao":
            case "constitution":
                return AdicionarPontoConstituicao();
            case "agilidade":
            case "agility":
                return AdicionarPontoAgilidade();
            case "velocidade":
            case "speed":
                return AdicionarPontoVelocidade();
            case "inteligencia":
            case "intelligence":
                return AdicionarPontoInteligencia();
            case "espirito":
            case "spirit":
                return AdicionarPontoEspirito();
            case "sorte":
            case "luck":
                return AdicionarPontoSorte();
            case "destreza":
            case "dexterity":
                return AdicionarPontoDestreza();
            case "force":
            case "vigor":
                return AdicionarPontoForcaExtra();
            case "percepcao":
            case "perception":
                return AdicionarPontoPercepcao();
            case "resistencia":
            case "resistance":
                return AdicionarPontoResistencia();
            case "mana":
                return AdicionarPontoMana();
            case "carisma":
            case "charisma":
                return AdicionarPontoCarisma();
            case "critico":
                return AdicionarPontoCritico();
            case "defesa":
                return AdicionarPontoDefesa();
            default:
                return false;
        }
    }

    public bool AdicionarPontoForca()
    {
        return ConsumirPontoStatus(() => forca += 1);
    }

    public bool AdicionarPontoConstituicao()
    {
        return ConsumirPontoStatus(() => constituicao += 1);
    }

    public bool AdicionarPontoAgilidade()
    {
        return ConsumirPontoStatus(() => agilidade += 1);
    }

    public bool AdicionarPontoVelocidade()
    {
        return ConsumirPontoStatus(() => velocidade += 1);
    }

    public bool AdicionarPontoInteligencia()
    {
        return ConsumirPontoStatus(() => inteligencia += 1);
    }

    public bool AdicionarPontoEspirito()
    {
        return ConsumirPontoStatus(() => espirito += 1);
    }

    public bool AdicionarPontoDestreza()
    {
        return ConsumirPontoStatus(() => destreza += 1);
    }

    public bool AdicionarPontoForcaExtra()
    {
        return ConsumirPontoStatus(() => vigor += 1);
    }

    public bool AdicionarPontoVigor()
    {
        return AdicionarPontoForcaExtra();
    }

    public bool AdicionarPontoPercepcao()
    {
        return ConsumirPontoStatus(() => percepcao += 1);
    }

    public bool AdicionarPontoResistencia()
    {
        GarantirResistenciaInicial();
        return ConsumirPontoStatus(() => resistencia += 1);
    }

    public bool AdicionarPontoCarisma()
    {
        return ConsumirPontoStatus(() => carisma += 1);
    }

    public bool AdicionarPontoSorte()
    {
        return ConsumirPontoStatus(() => sorte += 1);
    }

    public bool AdicionarPontoMana()
    {
        return ConsumirPontoStatus(() => mana += 1);
    }

    public bool AdicionarPontoCritico()
    {
        return ConsumirPontoStatus(() => critico += 1);
    }

    public bool AdicionarPontoDefesa()
    {
        return ConsumirPontoStatus(() => defesa += 1);
    }

    private bool ConsumirPontoStatus(Action adicionarAtributo)
    {
        if (pontosStatusDisponiveis <= 0 || adicionarAtributo == null)
            return false;

        adicionarAtributo();
        pontosStatusDisponiveis -= 1;
        NotificarStatusAlterado();
        return true;
    }

    private void GarantirAtributosIniciais()
    {
        forca = Mathf.Max(ValorInicialAtributo, forca);
        constituicao = Mathf.Max(ValorInicialAtributo, constituicao);
        agilidade = Mathf.Max(ValorInicialAtributo, agilidade);
        velocidade = Mathf.Max(ValorInicialAtributo, velocidade);
        inteligencia = Mathf.Max(ValorInicialAtributo, inteligencia);
        espirito = Mathf.Max(ValorInicialAtributo, espirito);
        sorte = Mathf.Max(ValorInicialAtributo, sorte);
        destreza = Mathf.Max(ValorInicialAtributo, destreza);
        vigor = Mathf.Max(ValorInicialAtributo, vigor);
        percepcao = Mathf.Max(ValorInicialAtributo, percepcao);
        GarantirResistenciaInicial();
        carisma = Mathf.Max(ValorInicialAtributo, carisma);
    }

    private void GarantirResistenciaInicial()
    {
        resistencia = Mathf.Max(ValorInicialAtributo, resistencia);
    }

    private void TocarFeedbackLevelUp()
    {
        if (audioSource != null && somLevelUp != null)
            audioSource.PlayOneShot(somLevelUp);

        if (efeitoLevelUp != null)
            efeitoLevelUp.Play();
    }

    private void NotificarStatusAlterado()
    {
        StatusAlterado?.Invoke();
    }

    private static string NormalizarNomeAtributo(string nomeAtributo)
    {
        string texto = nomeAtributo.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder resultado = new StringBuilder(texto.Length);

        for (int i = 0; i < texto.Length; i++)
        {
            UnicodeCategory categoria = CharUnicodeInfo.GetUnicodeCategory(texto[i]);
            if (categoria != UnicodeCategory.NonSpacingMark)
                resultado.Append(texto[i]);
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }

    public int GetExperienciaAtual()
    {
        return experienciaAtual;
    }

    public int GetExperienciaParaProximoNivel()
    {
        return experienciaParaProximoNivel;
    }

    public int GetNivel()
    {
        return nivel;
    }

    public int GetPontosStatusDisponiveis()
    {
        return pontosStatusDisponiveis;
    }

    public string GetNome()
    {
        return nome;
    }

    public string GetRaca()
    {
        return raca;
    }

    public string GetClasse()
    {
        return classe;
    }

    public string GetTitulo()
    {
        return titulo;
    }

    public int GetVidaAtual()
    {
        return vidaAtual;
    }

    public int GetVidaMaxima()
    {
        return vidaMaxima;
    }

    public int GetManaAtual()
    {
        return manaAtual;
    }

    public int GetManaMaxima()
    {
        return manaMaxima;
    }

    public int GetForca()
    {
        return forca;
    }

    public int GetConstituicao()
    {
        return constituicao;
    }

    public int GetAgilidade()
    {
        return agilidade;
    }

    public int GetVelocidade()
    {
        return velocidade;
    }

    public int GetInteligencia()
    {
        return inteligencia;
    }

    public int GetEspirito()
    {
        return espirito;
    }

    public int GetSorte()
    {
        return sorte;
    }

    public int GetDestreza()
    {
        return destreza;
    }

    public int GetVigor()
    {
        return vigor;
    }

    public int GetPercepcao()
    {
        return percepcao;
    }

    public int GetResistencia()
    {
        GarantirResistenciaInicial();
        return resistencia;
    }

    public int GetMana()
    {
        return mana;
    }

    public int GetCarisma()
    {
        return carisma;
    }

    public int GetCritico()
    {
        return critico;
    }

    public int GetDefesa()
    {
        return defesa;
    }

    public int GetBonusForca()
    {
        return bonusForca;
    }

    public int GetBonusConstituicao()
    {
        return bonusConstituicao;
    }

    public int GetBonusAgilidade()
    {
        return bonusAgilidade;
    }

    public int GetBonusVelocidade()
    {
        return bonusVelocidade;
    }

    public int GetBonusInteligencia()
    {
        return bonusInteligencia;
    }

    public int GetBonusEspirito()
    {
        return bonusEspirito;
    }

    public int GetBonusSorte()
    {
        return bonusSorte;
    }

    public int GetBonusDestreza()
    {
        return bonusDestreza;
    }

    public int GetBonusVigor()
    {
        return bonusVigor;
    }

    public int GetBonusPercepcao()
    {
        return bonusPercepcao;
    }

    public int GetBonusResistencia()
    {
        return bonusResistencia;
    }

    public int GetBonusMana()
    {
        return bonusMana;
    }

    public int GetBonusCarisma()
    {
        return bonusCarisma;
    }

    public int GetBonusCritico()
    {
        return bonusCritico;
    }

    public int GetBonusDefesa()
    {
        return bonusDefesa;
    }

    public int GetForcaTotal()
    {
        return forca + bonusForca;
    }

    public int GetConstituicaoTotal()
    {
        return constituicao + bonusConstituicao;
    }

    public int GetAgilidadeTotal()
    {
        return agilidade + bonusAgilidade;
    }

    public int GetVelocidadeTotal()
    {
        return velocidade + bonusVelocidade;
    }

    public int GetInteligenciaTotal()
    {
        return inteligencia + bonusInteligencia;
    }

    public int GetEspiritoTotal()
    {
        return espirito + bonusEspirito;
    }

    public int GetSorteTotal()
    {
        return sorte + bonusSorte;
    }

    public int GetDestrezaTotal()
    {
        return destreza + bonusDestreza;
    }

    public int GetVigorTotal()
    {
        return vigor + bonusVigor;
    }

    public int GetPercepcaoTotal()
    {
        return percepcao + bonusPercepcao;
    }

    public int GetResistenciaTotal()
    {
        GarantirResistenciaInicial();
        return resistencia + bonusResistencia;
    }

    public int GetManaTotal()
    {
        return mana + bonusMana;
    }

    public int GetCarismaTotal()
    {
        return carisma + bonusCarisma;
    }

    public int GetCriticoTotal()
    {
        return critico + bonusCritico;
    }

    public int GetDefesaTotal()
    {
        return defesa + bonusDefesa;
    }
}
