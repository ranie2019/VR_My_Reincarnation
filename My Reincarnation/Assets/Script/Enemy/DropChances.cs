using System;
using UnityEngine;

/// <summary>
/// Calculo de chances de drop compartilhado pelos monstros (padrao dos monstros).
/// O ultimo elemento do vetor representa "Nada". NaN identifica uma entrada sem prefab.
/// Mesma regra usada pelo SlimeIA: os valores sempre somam 100.
/// </summary>
public static class DropChances
{
    public static float Sanitizar(float chance)
    {
        return float.IsNaN(chance) || float.IsInfinity(chance) ? 0f : Mathf.Clamp(chance, 0f, 100f);
    }

    public static int Sortear(float[] chances)
    {
        Distribuir(chances);
        double sorteio = UnityEngine.Random.Range(0, 16777216) * (100d / 16777216d);
        double acumulado = 0d;
        for (int i = 0; i < chances.Length - 1; i++)
        {
            acumulado += chances[i];
            if (sorteio < acumulado)
                return i;
        }
        return -1; // Nada
    }

    public static void Distribuir(float[] chances, int indiceEditado = -1)
    {
        int indiceNada = chances.Length - 1;
        if (chances[indiceNada] < 0f)
        {
            double somaItens = 0;
            for (int i = 0; i < indiceNada; i++)
                somaItens += Sanitizar(chances[i]);
            chances[indiceNada] = (float)Math.Max(0d, 100d - somaItens);
        }

        chances[indiceNada] = Sanitizar(chances[indiceNada]);

        double total = 0;
        int destinoSaldo = -1;
        for (int i = 0; i < chances.Length; i++)
        {
            if (i == indiceEditado || float.IsNaN(chances[i]))
                continue;

            chances[i] = Sanitizar(chances[i]);
            total += chances[i];
            if (destinoSaldo < 0 || chances[i] > chances[destinoSaldo] ||
                (i == indiceNada && chances[i] == chances[destinoSaldo]))
                destinoSaldo = i;
        }

        if (destinoSaldo < 0)
        {
            chances[indiceNada] = 100f;
        }
        else
        {
            double restante = 100d - (indiceEditado >= 0 ? chances[indiceEditado] : 0f);
            double saldo = restante;
            for (int i = 0; i < chances.Length; i++)
            {
                if (i == indiceEditado || i == destinoSaldo || float.IsNaN(chances[i]))
                    continue;
                chances[i] = total > 0d ? (float)(restante * chances[i] / total) : 0f;
                saldo -= chances[i];
            }
            chances[destinoSaldo] = (float)Math.Max(0d, saldo);
        }

        for (int i = 0; i < indiceNada; i++)
            if (float.IsNaN(chances[i]))
                chances[i] = 0f;
    }
}
