using UnityEngine;

[DisallowMultipleComponent]
public class SlimeAnimacao : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private bool reiniciarAcoesPorGolpe;
    [SerializeField] private AnimationClip clipMorte;
    [SerializeField, Min(0f)] private float margemTransicaoMorte = 0.25f;

    private readonly SlimeIA.SlimeAnimacao animacao = new SlimeIA.SlimeAnimacao();

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        AtualizarCacheParametros();
    }

    public void AtualizarCacheParametros() =>
        animacao.Configurar(animator, reiniciarAcoesPorGolpe, clipMorte, margemTransicaoMorte);

    public void ReiniciarAcaoConfigurada(string estado) => animacao.ReiniciarAcaoConfigurada(estado);
    public float ObterTempoMorteConfigurado() => animacao.ObterTempoMorteConfigurado();
    public void AnimarParado() => animacao.AnimarParado();
    public void AnimarPatrulhando() => animacao.AnimarPatrulhando();
    public void AnimarObservando() => animacao.AnimarObservando();
    public void AnimarAlerta() => animacao.AnimarAlerta();
    public void AnimarPerseguindo() => animacao.AnimarPerseguindo();
    public void AnimarAtacando() => animacao.AnimarAtacando();
    public void AnimarTomandoDano() => animacao.AnimarTomandoDano();
    public void AnimarMorto() => animacao.AnimarMorto();
}
