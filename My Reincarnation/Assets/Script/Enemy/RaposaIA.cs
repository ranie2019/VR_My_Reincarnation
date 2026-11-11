using UnityEngine;

public sealed class RaposaIA : TopeiraIA
{
    [SerializeField, Range(0f, 100f)] private float chanceAtaqueCauda = 30f;
    [SerializeField, Range(0f, 100f)] private float chanceSentar = 35f;

    private bool ataqueCauda;
    private bool sentada;
    private bool levantando;
    private float limiteLevantar;

    protected override bool AtaqueAvanca => !ataqueCauda;

    protected override void PrepararVarianteAtaque() =>
        ataqueCauda = Random.value * 100f < chanceAtaqueCauda || chanceAtaqueCauda >= 100f;

    protected override void ConfigurarAnimator()
    {
        base.ConfigurarAnimator();
        if (animator != null) animator.applyRootMotion = false;
    }

    protected override void AnimarEstado(Estado estado)
    {
        sentada = estado == Estado.Observando && (Random.value * 100f < chanceSentar || chanceSentar >= 100f);
        levantando = false;

        switch (estado)
        {
            case Estado.Patrulhando: Tocar("Fox_Walk_InPlace"); break;
            case Estado.Perseguindo: Tocar("Fox_Run_InPlace"); break;
            case Estado.Observando: Tocar(sentada ? "Fox_Sit1" : "Fox_Idle"); break;
            case Estado.Atacando: Tocar(ataqueCauda ? "Fox_Attack_Tail" : "Fox_Attack_Paws"); break;
            case Estado.TomandoDano: Tocar("Fox_Jump_Pivot_InPlace"); break;
            case Estado.Morto: Tocar("Fox_Falling_Left"); break;
            default: Tocar("Fox_Idle"); break;
        }
    }

    protected override bool AguardarFimObservacao()
    {
        if (!sentada || animator == null || animator.runtimeAnimatorController == null)
            return false;

        if (!levantando)
        {
            levantando = Tocar("Fox_Sit3_StandUp");
            limiteLevantar = Time.time + 3f;
            return levantando;
        }

        return Time.time < limiteLevantar && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f;
    }

    private bool Tocar(string estado)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        int hash = Animator.StringToHash("Base Layer." + estado);
        if (!animator.HasState(0, hash)) return false;
        animator.Play(hash, 0, 0f);
        return true;
    }
}
