using UnityEngine;

// Resolve somente a linhagem do collider, nunca irmaos em uma raiz compartilhada.
internal static class ReceptorDanoInimigo
{
    internal static Component Resolver(Component origem)
    {
        for (Transform t = origem != null ? origem.transform : null; t != null; t = t.parent)
        {
            Component alvo = t.GetComponent<SlimeIA>();
            if (alvo == null) alvo = t.GetComponent<GoblinIA>();
            if (alvo == null) alvo = t.GetComponent<EsperitoDeFogoIA>();
            if (alvo != null) return alvo;
        }
        return null;
    }
    internal static bool EstaMorto(Component alvo)
    {
        if (alvo is SlimeIA slime) return slime.Morto;
        if (alvo is GoblinIA goblin) return goblin.Morto;
        if (alvo is EsperitoDeFogoIA espirito) return espirito.Morto;
        return true;
    }
    internal static void Aplicar(Component alvo, int dano, GameObject origem)
    {
        if (alvo is SlimeIA slime) slime.ReceberDano(dano, origem);
        else if (alvo is GoblinIA goblin) goblin.ReceberDano(dano, origem);
        else if (alvo is EsperitoDeFogoIA espirito)
        {
            var xp = alvo.GetComponent<ExperienciaInimigo>();
            if (xp != null) xp.RegistrarPrimeiroAtacante(origem);
            espirito.ReceberDano(dano);
        }
    }
}
