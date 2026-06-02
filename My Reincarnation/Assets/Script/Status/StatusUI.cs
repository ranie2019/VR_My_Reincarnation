using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class StatusUI : MonoBehaviour
{
    [Header("Referencia do Player")]
    [SerializeField] private StatusPlayer statusPlayer;

    [Header("Textos Identidade")]
    [SerializeField] private TextMeshProUGUI textoNomeValor;
    [SerializeField] private TextMeshProUGUI textoRacaValor;
    [SerializeField] private TextMeshProUGUI textoClasseValor;

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
    [SerializeField] private TextMeshProUGUI textoDefesaBaseValor;
    [SerializeField] private TextMeshProUGUI textoDefesaBonusValor;
    [SerializeField] private TextMeshProUGUI textoCarismaBaseValor;
    [SerializeField] private TextMeshProUGUI textoCarismaBonusValor;
    [SerializeField] private TextMeshProUGUI textoSorteBaseValor;
    [SerializeField] private TextMeshProUGUI textoSorteBonusValor;

    [Header("Configuracao Visual")]
    [SerializeField] private bool atualizarContinuamente = true;

    private void Awake()
    {
        EncontrarStatusPlayerSeNecessario();
    }

    private void OnEnable()
    {
        AtualizarUI();
    }

    private void Update()
    {
        if (atualizarContinuamente)
            AtualizarUI();
    }

    public void AtualizarUI()
    {
        EncontrarStatusPlayerSeNecessario();

        if (statusPlayer == null)
            return;

        DefinirTexto(textoNomeValor, statusPlayer.GetNome());
        DefinirTexto(textoRacaValor, statusPlayer.GetRaca());
        DefinirTexto(textoClasseValor, statusPlayer.GetClasse());

        DefinirTexto(textoNivelValor, statusPlayer.GetNivel().ToString());
        DefinirTexto(textoPontosStatusValor, statusPlayer.GetPontosStatusDisponiveis().ToString());

        DefinirTexto(textoVidaAtualValor, statusPlayer.GetVidaAtual().ToString());
        DefinirTexto(textoVidaMaximaValor, statusPlayer.GetVidaMaxima().ToString());

        DefinirTexto(textoManaAtualValor, statusPlayer.GetManaAtual().ToString());
        DefinirTexto(textoManaMaximaValor, statusPlayer.GetManaMaxima().ToString());

        DefinirTexto(textoExperienciaAtualValor, statusPlayer.GetExperienciaAtual().ToString());
        DefinirTexto(textoExperienciaMaximaValor, statusPlayer.GetExperienciaParaProximoNivel().ToString());

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
        DefinirTexto(textoDefesaBaseValor, statusPlayer.GetDefesa().ToString());
        DefinirTexto(textoDefesaBonusValor, FormatarBonus(statusPlayer.GetBonusDefesa()));
        DefinirTexto(textoCarismaBaseValor, statusPlayer.GetCarisma().ToString());
        DefinirTexto(textoCarismaBonusValor, FormatarBonus(statusPlayer.GetBonusCarisma()));
        DefinirTexto(textoSorteBaseValor, statusPlayer.GetSorte().ToString());
        DefinirTexto(textoSorteBonusValor, FormatarBonus(statusPlayer.GetBonusSorte()));
    }

    private void EncontrarStatusPlayerSeNecessario()
    {
        if (statusPlayer == null)
            statusPlayer = FindFirstObjectByType<StatusPlayer>();
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
