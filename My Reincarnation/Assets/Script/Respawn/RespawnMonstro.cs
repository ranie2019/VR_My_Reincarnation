using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class RespawnMonstro : MonoBehaviour
{
    public static RespawnMonstro Instancia { get; private set; }

    [Serializable]
    public class ConfiguracaoRespawnMonstro
    {
        public string idMonstro;
        public GameObject prefabRespawn;
        public float tempoRespawn = 10f;
        public Vector3 offsetRespawn = Vector3.zero;
        public bool usarRotacaoDaMorte = true;
        public bool debugRespawn = true;
    }

    [Header("Configuracoes de respawn por monstro")]
    [SerializeField] private ConfiguracaoRespawnMonstro[] configuracoesMonstros;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Debug.LogWarning(
                $"[RespawnMonstro] Existe mais de um RespawnMonstro na cena. Mantendo '{Instancia.name}' e ignorando '{name}'.",
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
        if (configuracoesMonstros == null)
            return;

        for (int i = 0; i < configuracoesMonstros.Length; i++)
        {
            ConfiguracaoRespawnMonstro config = configuracoesMonstros[i];
            if (config == null)
                continue;

            config.tempoRespawn = Mathf.Max(0f, config.tempoRespawn);
        }
    }

    public void AgendarRespawn(string idMonstro, Vector3 posicaoMorte, Quaternion rotacaoMorte)
    {
        ConfiguracaoRespawnMonstro config = BuscarConfiguracao(idMonstro);

        if (config == null)
        {
            Debug.LogWarning($"[RespawnMonstro] Configuracao nao encontrada para ID Monstro: '{idMonstro}'.", this);
            return;
        }

        if (config.prefabRespawn == null)
        {
            Debug.LogWarning($"[RespawnMonstro] Prefab Respawn vazio para ID Monstro: '{idMonstro}'.", this);
            return;
        }

        Vector3 posicaoFinal = posicaoMorte + config.offsetRespawn;
        Quaternion rotacaoFinal = config.usarRotacaoDaMorte
            ? rotacaoMorte
            : config.prefabRespawn.transform.rotation;

        Log(config, "AgendarRespawn chamado.");
        Log(config, $"ID Monstro: {idMonstro}");
        Log(config, $"Posicao da morte: {posicaoMorte}");
        Log(config, $"Prefab usado: {config.prefabRespawn.name}");
        Log(config, $"Tempo respawn: {config.tempoRespawn}");

        StartCoroutine(RotinaRespawn(config, posicaoFinal, rotacaoFinal));
    }

    private IEnumerator RotinaRespawn(ConfiguracaoRespawnMonstro config, Vector3 posicaoFinal, Quaternion rotacaoFinal)
    {
        yield return new WaitForSeconds(config.tempoRespawn);

        if (config.prefabRespawn == null)
        {
            Debug.LogWarning($"[RespawnMonstro] Respawn cancelado: prefabRespawn ficou null para ID '{config.idMonstro}'.", this);
            yield break;
        }

        GameObject novoMonstro = Instantiate(config.prefabRespawn, posicaoFinal, rotacaoFinal);

        Log(config, $"Instantiate executado: {novoMonstro.name}");
        Log(config, $"Posicao final: {posicaoFinal}");
    }

    private ConfiguracaoRespawnMonstro BuscarConfiguracao(string idMonstro)
    {
        if (string.IsNullOrWhiteSpace(idMonstro) || configuracoesMonstros == null)
            return null;

        string idNormalizado = idMonstro.Trim();

        for (int i = 0; i < configuracoesMonstros.Length; i++)
        {
            ConfiguracaoRespawnMonstro config = configuracoesMonstros[i];
            if (config == null || string.IsNullOrWhiteSpace(config.idMonstro))
                continue;

            if (string.Equals(config.idMonstro.Trim(), idNormalizado, StringComparison.Ordinal))
                return config;
        }

        return null;
    }

    private void Log(ConfiguracaoRespawnMonstro config, string mensagem)
    {
        if (config != null && config.debugRespawn)
            Debug.Log($"[RespawnMonstro] {mensagem}", this);
    }
}
