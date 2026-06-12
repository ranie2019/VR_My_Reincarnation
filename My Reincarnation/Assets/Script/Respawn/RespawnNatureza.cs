using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class RespawnNatureza : MonoBehaviour
{
    public static RespawnNatureza Instancia { get; private set; }

    [Serializable]
    public class ConfiguracaoRespawnNatureza
    {
        public string idNatureza;
        public GameObject prefabRespawn;
        public float tempoRespawn = 30f;
        public Vector3 offsetRespawn = Vector3.zero;
        public bool usarRotacaoDaMorte = true;
        public bool debugRespawn = true;
    }

    [Header("Configuracoes de respawn por natureza")]
    [SerializeField] private ConfiguracaoRespawnNatureza[] configuracoesNatureza;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Debug.LogWarning(
                $"[RespawnNatureza] Existe mais de um RespawnNatureza na cena. Mantendo '{Instancia.name}' e ignorando '{name}'.",
                this);
            enabled = false;
            return;
        }

        Instancia = this;
    }

    private void OnDisable()
    {
        if (Instancia == this)
            Instancia = null;
    }

    private void OnValidate()
    {
        if (configuracoesNatureza == null)
            return;

        for (int i = 0; i < configuracoesNatureza.Length; i++)
        {
            ConfiguracaoRespawnNatureza config = configuracoesNatureza[i];
            if (config == null)
                continue;

            config.tempoRespawn = Mathf.Max(0f, config.tempoRespawn);
        }
    }

    public void AgendarRespawn(string idNatureza, Vector3 posicaoMorte, Quaternion rotacaoMorte)
    {
        string idNormalizado = idNatureza == null ? string.Empty : idNatureza.Trim();

        if (string.IsNullOrWhiteSpace(idNormalizado))
        {
            Debug.LogWarning("[RespawnNatureza] ID Natureza vazio. Respawn nao sera agendado.", this);
            return;
        }

        ConfiguracaoRespawnNatureza config = BuscarConfiguracao(idNormalizado);

        if (config == null)
        {
            Debug.LogWarning($"[RespawnNatureza] Configuracao nao encontrada para ID Natureza: '{idNatureza}'.", this);
            return;
        }

        if (config.prefabRespawn == null)
        {
            Debug.LogWarning($"[RespawnNatureza] Prefab Respawn vazio para ID Natureza: '{idNatureza}'.", this);
            return;
        }

        float tempoRespawn = Mathf.Max(0f, config.tempoRespawn);
        Vector3 posicaoFinal = posicaoMorte + config.offsetRespawn;
        Quaternion rotacaoFinal = config.usarRotacaoDaMorte
            ? rotacaoMorte
            : config.prefabRespawn.transform.rotation;

        Log(config, "AgendarRespawn chamado.");
        Log(config, $"ID Natureza: {idNormalizado}");
        Log(config, $"Posicao da morte: {posicaoMorte}");
        Log(config, $"Prefab usado: {config.prefabRespawn.name}");
        Log(config, $"Tempo respawn: {tempoRespawn}");

        StartCoroutine(RotinaRespawn(config, idNormalizado, tempoRespawn, posicaoFinal, rotacaoFinal));
    }

    private IEnumerator RotinaRespawn(
        ConfiguracaoRespawnNatureza config,
        string idNatureza,
        float tempoRespawn,
        Vector3 posicaoFinal,
        Quaternion rotacaoFinal)
    {
        if (tempoRespawn > 0f)
            yield return new WaitForSeconds(tempoRespawn);
        else
            yield return null;

        if (config.prefabRespawn == null)
        {
            Debug.LogWarning($"[RespawnNatureza] Respawn cancelado: prefabRespawn ficou null para ID '{idNatureza}'.", this);
            yield break;
        }

        GameObject novaNatureza = Instantiate(config.prefabRespawn, posicaoFinal, rotacaoFinal);

        Log(config, $"Instantiate executado: {novaNatureza.name}");
        Log(config, $"Posicao final: {posicaoFinal}");
    }

    private ConfiguracaoRespawnNatureza BuscarConfiguracao(string idNatureza)
    {
        if (string.IsNullOrWhiteSpace(idNatureza) || configuracoesNatureza == null)
            return null;

        string idNormalizado = idNatureza.Trim();

        for (int i = 0; i < configuracoesNatureza.Length; i++)
        {
            ConfiguracaoRespawnNatureza config = configuracoesNatureza[i];
            if (config == null || string.IsNullOrWhiteSpace(config.idNatureza))
                continue;

            if (string.Equals(config.idNatureza.Trim(), idNormalizado, StringComparison.Ordinal))
                return config;
        }

        return null;
    }

    private void Log(ConfiguracaoRespawnNatureza config, string mensagem)
    {
        if (config != null && config.debugRespawn)
            Debug.Log($"[RespawnNatureza] {mensagem}", this);
    }
}
