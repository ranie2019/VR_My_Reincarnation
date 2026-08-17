using System;
using System.Collections;
using System.Collections.Generic;
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
    }

    [Header("Configuracoes de respawn por natureza")]
    [SerializeField] private ConfiguracaoRespawnNatureza[] configuracoesNatureza;

    private readonly HashSet<string> respawnsAgendados = new();

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            enabled = false;
            return;
        }

        Instancia = this;
    }

    private void OnDisable()
    {
        if (Instancia == this)
            Instancia = null;

        respawnsAgendados.Clear();
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
            return;

        ConfiguracaoRespawnNatureza config = BuscarConfiguracao(idNormalizado);

        if (config == null)
            return;

        if (config.prefabRespawn == null)
            return;

        float tempoRespawn = Mathf.Max(0f, config.tempoRespawn);
        Vector3 posicaoFinal = posicaoMorte + config.offsetRespawn;
        Quaternion rotacaoFinal = config.usarRotacaoDaMorte
            ? rotacaoMorte
            : config.prefabRespawn.transform.rotation;

        string chaveRespawn = CriarChaveRespawn(idNormalizado, posicaoFinal);
        if (!respawnsAgendados.Add(chaveRespawn))
            return;

        StartCoroutine(RotinaRespawn(config, tempoRespawn, posicaoFinal, rotacaoFinal, chaveRespawn));
    }

    private IEnumerator RotinaRespawn(
        ConfiguracaoRespawnNatureza config,
        float tempoRespawn,
        Vector3 posicaoFinal,
        Quaternion rotacaoFinal,
        string chaveRespawn)
    {
        if (tempoRespawn > 0f)
            yield return new WaitForSeconds(tempoRespawn);
        else
            yield return null;

        if (config.prefabRespawn == null)
        {
            respawnsAgendados.Remove(chaveRespawn);
            yield break;
        }

        GameObject instancia = Instantiate(config.prefabRespawn, posicaoFinal, rotacaoFinal);
        PrepararItensRespawnados(instancia);
        respawnsAgendados.Remove(chaveRespawn);
    }

    private void PrepararItensRespawnados(GameObject instancia)
    {
        if (instancia == null)
            return;

        Respawnitem[] itens = instancia.GetComponentsInChildren<Respawnitem>(true);
        for (int i = 0; i < itens.Length; i++)
        {
            if (itens[i] != null)
                itens[i].PrepararComoRecursoDisponivelNoMundo();
        }
    }

    private static string CriarChaveRespawn(string idNatureza, Vector3 posicao)
    {
        int x = Mathf.RoundToInt(posicao.x * 1000f);
        int y = Mathf.RoundToInt(posicao.y * 1000f);
        int z = Mathf.RoundToInt(posicao.z * 1000f);
        return idNatureza + "|" + x + "|" + y + "|" + z;
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
}
