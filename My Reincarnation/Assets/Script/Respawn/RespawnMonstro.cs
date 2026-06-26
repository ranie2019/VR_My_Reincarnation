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
    }

    [Header("Configuracoes de respawn por monstro")]
    [SerializeField] private ConfiguracaoRespawnMonstro[] configuracoesMonstros;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            { }
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
        AgendarRespawn(idMonstro, posicaoMorte, rotacaoMorte, null);
    }

    public void AgendarRespawn(
        string idMonstro,
        Vector3 posicaoMorte,
        Quaternion rotacaoMorte,
        Transform[] pontosPatrulhaOriginais)
    {
        ConfiguracaoRespawnMonstro config = BuscarConfiguracao(idMonstro);

        if (config == null)
        {
            { }
            return;
        }

        if (config.prefabRespawn == null)
        {
            { }
            return;
        }

        Vector3 posicaoFinal = posicaoMorte + config.offsetRespawn;
        Quaternion rotacaoFinal = config.usarRotacaoDaMorte
            ? rotacaoMorte
            : config.prefabRespawn.transform.rotation;

        StartCoroutine(RotinaRespawn(config, posicaoFinal, rotacaoFinal, CopiarPontosPatrulha(pontosPatrulhaOriginais)));
    }

    private IEnumerator RotinaRespawn(
        ConfiguracaoRespawnMonstro config,
        Vector3 posicaoFinal,
        Quaternion rotacaoFinal,
        Transform[] pontosPatrulhaOriginais)
    {
        yield return new WaitForSeconds(config.tempoRespawn);

        if (config.prefabRespawn == null)
        {
            { }
            yield break;
        }

        GameObject novoMonstro = Instantiate(config.prefabRespawn, posicaoFinal, rotacaoFinal);
        SlimeIA slimeNovo = novoMonstro.GetComponent<SlimeIA>();

        if (slimeNovo != null)
        {
            slimeNovo.DefinirPontosPatrulha(pontosPatrulhaOriginais);
            slimeNovo.ReiniciarAposRespawn();
        }
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

    private Transform[] CopiarPontosPatrulha(Transform[] pontosPatrulhaOriginais)
    {
        if (pontosPatrulhaOriginais == null || pontosPatrulhaOriginais.Length == 0)
            return pontosPatrulhaOriginais;

        Transform[] copia = new Transform[pontosPatrulhaOriginais.Length];
        Array.Copy(pontosPatrulhaOriginais, copia, pontosPatrulhaOriginais.Length);
        return copia;
    }
}
