using UnityEngine;

// =====================================================================
// TopeiraAnimacao
// Unico ponto de contato com o Animator da Topeira.
// Parametros bool do controller "Free Burrow": Andar, Ataque, Dano, Morto.
// Parado = nenhum parametro ligado. Apenas UM parametro fica ligado por vez.
// =====================================================================
[DisallowMultipleComponent]
public class TopeiraAnimacao : MonoBehaviour
{
    #region Configuracao
    [Tooltip("Se vazio, procura no proprio objeto e nos filhos.")]
    [SerializeField] private Animator animator;
    #endregion

    #region Parametros do Animator
    private static readonly int AndarHash = Animator.StringToHash("Andar");
    private static readonly int AtaqueHash = Animator.StringToHash("Ataque");
    private static readonly int DanoHash = Animator.StringToHash("Dano");
    private static readonly int MortoHash = Animator.StringToHash("Morto");

    private bool temAndar;
    private bool temAtaque;
    private bool temDano;
    private bool temMorto;
    #endregion

    #region Ciclo de vida
    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        CachearParametros();
    }
    #endregion

    #region API publica (um metodo por acao)
    public void Parado() => Aplicar(false, false, false, false);
    public void Andando() => Aplicar(true, false, false, false);
    public void Atacando() => Aplicar(false, true, false, false);
    public void TomandoDano() => Aplicar(false, false, true, false);
    public void Morto() => Aplicar(false, false, false, true);
    #endregion

    #region Interno
    private void CachearParametros()
    {
        temAndar = temAtaque = temDano = temMorto = false;
        if (animator == null || animator.runtimeAnimatorController == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Bool) continue;
            if (p.nameHash == AndarHash) temAndar = true;
            else if (p.nameHash == AtaqueHash) temAtaque = true;
            else if (p.nameHash == DanoHash) temDano = true;
            else if (p.nameHash == MortoHash) temMorto = true;
        }
    }

    private void Aplicar(bool andar, bool ataque, bool dano, bool morto)
    {
        SetBoolSeguro(AndarHash, temAndar, andar);
        SetBoolSeguro(AtaqueHash, temAtaque, ataque);
        SetBoolSeguro(DanoHash, temDano, dano);
        SetBoolSeguro(MortoHash, temMorto, morto);
    }

    private void SetBoolSeguro(int hash, bool existe, bool valor)
    {
        if (animator != null && existe) animator.SetBool(hash, valor);
    }
    #endregion
}
