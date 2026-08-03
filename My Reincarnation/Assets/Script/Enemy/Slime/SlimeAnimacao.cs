using UnityEngine;

/// <summary>
/// Centraliza toda a logica de animacao de um inimigo (Slime, Tartaruga, etc).
/// O script de IA (ex: SlimeIA) NAO mexe mais no Animator diretamente -
/// ele so chama os metodos publicos daqui (AnimarPatrulhando, AnimarAlerta...).
///
/// Detecta automaticamente quais parametros bool existem no Animator Controller
/// atribuido (via AtualizarCacheParametros), entao funciona tanto com animators
/// que tem so os 6 parametros basicos (Andar, Patrulha, Alerta, Ataque, Dano,
/// Morrer) quanto com animators que tem parametros extras, como "Correr"
/// (ex: TurtleShell.controller da Tartaruga Azul).
///
/// Regra de perseguicao: se o Animator tiver o parametro "Correr", a perseguicao
/// usa ele (corrida). Se nao tiver, cai para "Andar" (mesmo comportamento
/// que o Slime ja usava).
/// </summary>
[DisallowMultipleComponent]
public class SlimeAnimacao : MonoBehaviour
{
    [Tooltip("Se vazio, tenta encontrar automaticamente (no proprio objeto ou nos filhos).")]
    [SerializeField] private Animator animator;

    // Hashes dos parametros. Os nomes precisam bater EXATAMENTE com os
    // parametros criados no Animator Controller (aba Parameters).
    private static readonly int AndarHash = Animator.StringToHash("Andar");
    private static readonly int PatrulhaHash = Animator.StringToHash("Patrulha");
    private static readonly int AlertaHash = Animator.StringToHash("Alerta");
    private static readonly int AtaqueHash = Animator.StringToHash("Ataque");
    private static readonly int DanoHash = Animator.StringToHash("Dano");
    private static readonly int MorrerHash = Animator.StringToHash("Morrer");
    private static readonly int CorrerHash = Animator.StringToHash("Correr");

    // Indica se cada parametro realmente existe no Animator Controller atual.
    private bool temAndar;
    private bool temPatrulha;
    private bool temAlerta;
    private bool temAtaque;
    private bool temDano;
    private bool temMorrer;
    private bool temCorrer;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        AtualizarCacheParametros();
    }

    /// <summary>
    /// Re-escaneia os parametros do Animator atual. Chame isso se o Animator
    /// Controller for trocado em tempo de execucao, ou apos um respawn.
    /// </summary>
    public void AtualizarCacheParametros()
    {
        temAndar = false;
        temPatrulha = false;
        temAlerta = false;
        temAtaque = false;
        temDano = false;
        temMorrer = false;
        temCorrer = false;

        if (animator == null)
            return;

        AnimatorControllerParameter[] parametros = animator.parameters;
        for (int i = 0; i < parametros.Length; i++)
        {
            AnimatorControllerParameter parametro = parametros[i];
            if (parametro.type != AnimatorControllerParameterType.Bool)
                continue;

            if (parametro.nameHash == AndarHash)
                temAndar = true;
            else if (parametro.nameHash == PatrulhaHash)
                temPatrulha = true;
            else if (parametro.nameHash == AlertaHash)
                temAlerta = true;
            else if (parametro.nameHash == AtaqueHash)
                temAtaque = true;
            else if (parametro.nameHash == DanoHash)
                temDano = true;
            else if (parametro.nameHash == MorrerHash)
                temMorrer = true;
            else if (parametro.nameHash == CorrerHash)
                temCorrer = true;
        }
    }

    // ---------------------------------------------------------------
    // API publica: um metodo por acao. O SlimeIA (ou qualquer outro
    // script de IA) so precisa chamar o metodo certo pro estado atual.
    // ---------------------------------------------------------------

    /// <summary>Parado/idle. Nenhuma animacao de acao ligada.</summary>
    public void AnimarParado()
    {
        AplicarUnico(nenhum: true);
    }

    /// <summary>Indo ate o ponto de patrulha.</summary>
    public void AnimarPatrulhando()
    {
        AplicarUnico(andar: true);
    }

    /// <summary>Chegou no ponto de patrulha, observando ao redor.</summary>
    public void AnimarObservando()
    {
        AplicarUnico(patrulha: true);
    }

    /// <summary>Avistou o player, ainda fora de alcance de perseguicao.</summary>
    public void AnimarAlerta()
    {
        AplicarUnico(alerta: true);
    }

    /// <summary>
    /// Perseguindo o player. Usa "Correr" se o Animator tiver esse parametro
    /// (ex: Tartaruga), senao usa "Andar" (ex: Slime).
    /// </summary>
    public void AnimarPerseguindo()
    {
        if (temCorrer)
            AplicarUnico(correr: true);
        else
            AplicarUnico(andar: true);
    }

    /// <summary>Atacando o player.</summary>
    public void AnimarAtacando()
    {
        AplicarUnico(ataque: true);
    }

    /// <summary>Acabou de tomar dano.</summary>
    public void AnimarTomandoDano()
    {
        AplicarUnico(dano: true);
    }

    /// <summary>Morreu.</summary>
    public void AnimarMorto()
    {
        AplicarUnico(morrer: true);
    }

    /// <summary>
    /// Liga exatamente UM dos parametros passados como true e desliga todos
    /// os outros. Garante que nunca fica mais de uma animacao de acao ativa
    /// ao mesmo tempo.
    /// </summary>
    private void AplicarUnico(
        bool nenhum = false,
        bool andar = false,
        bool patrulha = false,
        bool alerta = false,
        bool ataque = false,
        bool dano = false,
        bool morrer = false,
        bool correr = false)
    {
        SetBoolSeguro(AndarHash, temAndar, andar);
        SetBoolSeguro(PatrulhaHash, temPatrulha, patrulha);
        SetBoolSeguro(AlertaHash, temAlerta, alerta);
        SetBoolSeguro(AtaqueHash, temAtaque, ataque);
        SetBoolSeguro(DanoHash, temDano, dano);
        SetBoolSeguro(MorrerHash, temMorrer, morrer);
        SetBoolSeguro(CorrerHash, temCorrer, correr);
    }

    private void SetBoolSeguro(int hash, bool existeParametro, bool valor)
    {
        if (animator != null && existeParametro)
            animator.SetBool(hash, valor);
    }
}