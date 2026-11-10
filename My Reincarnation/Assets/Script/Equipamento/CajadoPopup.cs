using UnityEngine;

/// <summary>
/// Dados individuais e apresentacao do cajado. Reutiliza o nucleo da arma:
/// sorteio, save, durabilidade, bonus enquanto empunhado e popup por laser.
/// Runas, voz, consumo de mana e dano dos feiticos continuam em seus componentes.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("My Reincarnation/Equipamento/Cajado Popup")]
public class CajadoPopup : Espada
{
    private enum AtributoRestricaoMagica
    {
        [InspectorName("Automático")] Automatico,
        [InspectorName("MP (Mana)")] Mana,
        [InspectorName("Inteligência")] Inteligencia,
        [InspectorName("Espírito")] Espirito,
        [InspectorName("Percepção")] Percepcao
    }

    [SerializeField] private AtributoRestricaoMagica atributoRestricaoMagica = AtributoRestricaoMagica.Automatico;

    public bool destruirQuandoVidaZerar = true;
    [Min(1)] public int desgastePorMagiaLancada = 1;

    protected override bool DestruirAoQuebrar => destruirQuandoVidaZerar;

    internal bool PodeLancarMagia(StatusPlayer jogador) =>
        GarantirGeracao() && !EstaQuebrada && PodeUsar(jogador);

    // Somente lancamentos efetivos consomem durabilidade; preparar/cancelar nao consome.
    public void RegistrarMagiaLancada() => AplicarDesgasteCombate(Mathf.Max(1, desgastePorMagiaLancada));

    protected override bool DefinirRestricaoNova(EstadoEspada estado)
    {
        if (valorMinimoParaUsar < 0) return false;
        estado.restricaoDefinida = true;
        estado.atributoExigido = atributoRestricaoMagica switch
        {
            AtributoRestricaoMagica.Mana => TipoAtributoManopla.Mana,
            AtributoRestricaoMagica.Inteligencia => TipoAtributoManopla.Inteligencia,
            AtributoRestricaoMagica.Espirito => TipoAtributoManopla.Espirito,
            AtributoRestricaoMagica.Percepcao => TipoAtributoManopla.Percepcao,
            _ => SortearRequisitoDaArma(UnityEngine.Random.Range(0, 100))
        };
        estado.requisitoExigido = valorMinimoParaUsar;
        return true;
    }

    private static TipoAtributoManopla SortearRequisitoDaArma(int sorteio)
    {
        return sorteio < 25 ? TipoAtributoManopla.Mana :
            sorteio < 50 ? TipoAtributoManopla.Inteligencia :
            sorteio < 75 ? TipoAtributoManopla.Espirito : TipoAtributoManopla.Percepcao;
    }

    // Um componente de informacoes de cajado nao deve transformar contato em ataque de espada.
    protected override void OnCollisionEnter(Collision collision) { }
    protected override void OnTriggerEnter(Collider other) { }
    protected override int ObterDanoCalculado() => 0;
}
