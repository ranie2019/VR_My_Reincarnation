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
            if (alvo == null) alvo = t.GetComponent<TopeiraIA>();
            if (alvo == null) alvo = t.GetComponent<RaposaIA>();
            if (alvo != null) return alvo;
        }
        return null;
    }
    internal static bool EstaMorto(Component alvo)
    {
        if (alvo is SlimeIA slime) return slime.Morto;
        if (alvo is GoblinIA goblin) return goblin.Morto;
        if (alvo is EsperitoDeFogoIA espirito) return espirito.Morto;
        if (alvo is TopeiraIA topeira) return topeira.Morto;
        if (alvo is RaposaIA raposa) return raposa.Morto;
        return true;
    }
    internal static void Aplicar(Component alvo, int dano, GameObject origem)
    {
        if (alvo is SlimeIA slime) slime.ReceberDano(dano, origem);
        else if (alvo is GoblinIA goblin) goblin.ReceberDano(dano, origem);
        else if (alvo is TopeiraIA topeira) topeira.ReceberDano(dano, origem);
        else if (alvo is RaposaIA raposa) raposa.ReceberDano(dano, origem);
        else if (alvo is EsperitoDeFogoIA espirito) espirito.ReceberDano(dano, origem);
    }
}
