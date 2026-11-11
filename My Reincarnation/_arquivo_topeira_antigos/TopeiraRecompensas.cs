using System;
using UnityEngine;

// =====================================================================
// TopeiraRecompensas
// Tudo o que acontece UMA vez quando a Topeira morre: experiencia, REIN,
// progresso de missao e drops. Cada etapa e isolada: uma falha nao impede
// as demais, e nenhuma recompensa e repetida.
// =====================================================================
[DisallowMultipleComponent, RequireComponent(typeof(ExperienciaInimigo))]
public class TopeiraRecompensas : MonoBehaviour
{
    #region Configuracao
    [Serializable]
    public class DropConfig
    {
        public GameObject prefab;
        [Range(0f, 100f)] public float chance = 50f;
        [Min(0)] public int quantidadeMinima = 1;
        [Min(0)] public int quantidadeMaxima = 1;
        public Vector3 offsetSpawn = new Vector3(0f, 0.5f, 0f);
    }

    [Header("REIN")]
    [SerializeField] private bool darReinAoMorrer = true;
    [SerializeField] private string reinMinimo = "0.01";
    [SerializeField] private string reinMaximo = "0.05";
    [SerializeField, Range(0, 5)] private int casasDecimaisSorteio = 5;

    [Header("Drops (um unico sorteio; soma das chances <= 100, o resto e 'nada')")]
    [SerializeField] private DropConfig[] drops;
    #endregion

    #region Estado
    private bool concedido;
    private ExperienciaInimigo experiencia;
    #endregion

    #region API
    /// <param name="ultimaOrigemDano">Quem deu o ultimo golpe (fallback do primeiro atacante).</param>
    /// <param name="idMonstro">Mesmo id do respawn, usado pelas missoes de matar inimigos.</param>
    public void Conceder(GameObject ultimaOrigemDano, string idMonstro)
    {
        if (concedido) return;
        concedido = true;

        Etapa(EntregarExperiencia);
        Etapa(() => PagarRein(ultimaOrigemDano));
        Etapa(() => NotificarMissao(idMonstro));
        Etapa(SortearDrop);
    }

    private static void Etapa(Action etapa)
    {
        try { etapa(); } catch (Exception erro) { Debug.LogException(erro); }
    }
    #endregion

    #region Experiencia
    private void EntregarExperiencia()
    {
        if (experiencia == null) experiencia = GetComponent<ExperienciaInimigo>();
        if (experiencia != null) experiencia.EntregarExperiencia();
    }
    #endregion

    #region REIN
    private void PagarRein(GameObject ultimaOrigemDano)
    {
        if (!darReinAoMorrer) return;

        if (experiencia == null) experiencia = GetComponent<ExperienciaInimigo>();
        StatusPlayer status = experiencia != null ? experiencia.PrimeiroAtacante : null;
        if (status == null && ultimaOrigemDano != null)
            status = ultimaOrigemDano.GetComponentInParent<StatusPlayer>();
        if (status == null) status = FindFirstObjectByType<StatusPlayer>();

        CarteiraReinPlayer carteira = status != null ? status.ObterCarteiraRein() : null;
        if (carteira == null) return;

        long unidades = SortearUnidades();
        if (unidades > 0) carteira.AdicionarReinUnidades(unidades);
    }

    private long SortearUnidades()
    {
        long minimo = Converter(reinMinimo);
        long maximo = Converter(reinMaximo);
        if (maximo < minimo) { long t = minimo; minimo = maximo; maximo = t; }

        // Passo: 1 REIN = 100.000 unidades; 5 casas decimais => passo 1.
        long passo = 1;
        for (int i = casasDecimaisSorteio; i < 5; i++) passo *= 10;

        minimo = ((minimo + passo - 1) / passo) * passo;
        maximo = (maximo / passo) * passo;
        if (maximo <= minimo) return minimo;

        long passos = ((maximo - minimo) / passo) + 1;
        long indice = Math.Min(passos - 1, (long)Math.Floor(UnityEngine.Random.value * passos));
        return minimo + indice * passo;
    }

    private static long Converter(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return 0;
        string normalizado = texto.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalizado, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out decimal valor)) return 0;
        return CarteiraReinPlayer.ConverterDecimalParaUnidades(valor);
    }
    #endregion

    #region Missao
    private void NotificarMissao(string idMonstro)
    {
        GerenciadorMissoes gerenciador = FindFirstObjectByType<GerenciadorMissoes>();
        if (gerenciador != null) gerenciador.NotificarInimigoMorto(idMonstro);
    }
    #endregion

    #region Drops
    private void SortearDrop()
    {
        if (drops == null || drops.Length == 0) return;

        float sorteio = UnityEngine.Random.Range(0f, 100f);
        float acumulado = 0f;
        for (int i = 0; i < drops.Length; i++)
        {
            DropConfig d = drops[i];
            if (d == null || d.prefab == null || d.chance <= 0f) continue;

            acumulado += d.chance;
            if (sorteio >= acumulado) continue;

            int minimo = Mathf.Max(0, d.quantidadeMinima);
            int quantidade = UnityEngine.Random.Range(minimo, Mathf.Max(minimo, d.quantidadeMaxima) + 1);
            for (int q = 0; q < quantidade; q++)
                Instantiate(d.prefab, transform.position + d.offsetSpawn, Quaternion.identity);
            return;
        }
        // Restante do intervalo: nenhum drop.
    }
    #endregion
}
