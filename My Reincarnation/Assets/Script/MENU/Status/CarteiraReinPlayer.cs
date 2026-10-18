using System;
using System.Globalization;
using UnityEngine;

[DisallowMultipleComponent]
public class CarteiraReinPlayer : MonoBehaviour
{
    public const long FATOR_REIN = 100000;

    [SerializeField] private long reinUnidades;

    public event Action<long> AoSaldoReinAlterado;

    public long ReinUnidades => reinUnidades;

    public void AdicionarReinUnidades(long unidades)
    {
        if (unidades <= 0)
            return;

        long novoSaldo = reinUnidades > long.MaxValue - unidades
            ? long.MaxValue
            : reinUnidades + unidades;

        DefinirSaldoPorUnidades(novoSaldo);
    }

    public bool TentarRemoverReinUnidades(long unidades)
    {
        if (unidades <= 0)
            return false;

        if (reinUnidades < unidades)
            return false;

        DefinirSaldoPorUnidades(reinUnidades - unidades);
        return true;
    }

    public void DefinirSaldoPorUnidades(long unidades)
    {
        long saldoNormalizado = Math.Max(0L, unidades);
        if (reinUnidades == saldoNormalizado)
            return;

        reinUnidades = saldoNormalizado;
        AoSaldoReinAlterado?.Invoke(reinUnidades);
    }

    public string ObterSaldoFormatado()
    {
        return FormatarUnidades(reinUnidades);
    }

    public static long ConverterDecimalParaUnidades(decimal valor)
    {
        if (valor <= 0m)
            return 0L;

        decimal unidades = decimal.Round(valor * FATOR_REIN, 0, MidpointRounding.AwayFromZero);
        return unidades >= long.MaxValue ? long.MaxValue : (long)unidades;
    }

    public static decimal ConverterUnidadesParaDecimal(long unidades)
    {
        if (unidades <= 0)
            return 0m;

        return unidades / (decimal)FATOR_REIN;
    }

    public static string FormatarUnidades(long unidades)
    {
        decimal valor = ConverterUnidadesParaDecimal(unidades);
        return valor.ToString("0.#####", CultureInfo.InvariantCulture);
    }
}
