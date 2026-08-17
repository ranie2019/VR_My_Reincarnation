public enum RaridadeItem
{
    Normal = 0,
    Incomum = 1,
    Raro = 2,
    Epico = 3,
    Unico = 4,
    Lendario = 5,
    Divino = 6
}

public static class RaridadeItemUtil
{
    public static int ObterMultiplicadorProgressivo(RaridadeItem raridade)
    {
        int indice = (int)raridade;

        if (indice < 0)
            indice = 0;
        else if (indice > (int)RaridadeItem.Divino)
            indice = (int)RaridadeItem.Divino;

        return indice + 1;
    }

    public static int CalcularQuantidadePorRaridade(int quantidadeBase, RaridadeItem raridade)
    {
        int baseSegura = quantidadeBase < 1 ? 1 : quantidadeBase;
        return baseSegura * ObterMultiplicadorProgressivo(raridade);
    }
}
