using System;
using System.Collections;
using System.Collections.Generic;
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

    private sealed class PendenteVerde
    {
        public ConfiguracaoRespawnMonstro config;
        public Vector3 posicao; public Quaternion rotacao; public Transform[] patrulha;
        public float instante; public Coroutine rotina;
        public bool volumeVermelho; public float raio, altura; public Vector3 centro;
    }
    private readonly Dictionary<int, PendenteVerde> pendentesVerdes = new Dictionary<int, PendenteVerde>();
    private readonly RaycastHit[] soloVerde = new RaycastHit[24];
    private void OnEnable()
    {
        if (Instancia != null && Instancia != this) return;
        Instancia = this;
        foreach (var par in pendentesVerdes)
            if (par.Value.rotina == null) par.Value.rotina = StartCoroutine(RespawnVerde(par.Key, par.Value));
    }
    public void AgendarRespawnVerde(string id, Vector3 morte, Quaternion rotacao, Transform[] patrulha, int identidade)
    {
        if (pendentesVerdes.ContainsKey(identidade)) return;
        var config = BuscarConfiguracao(id);
        if (config == null || config.prefabRespawn == null) { Debug.LogError("[RespawnMonstro] Slime Verde sem prefab/configuracao: " + id, this); return; }
        var pedido = new PendenteVerde { config = config, posicao = morte + config.offsetRespawn,
            rotacao = config.usarRotacaoDaMorte ? rotacao : config.prefabRespawn.transform.rotation,
            patrulha = CopiarPontosPatrulha(patrulha), instante = Time.time + Mathf.Max(0f, config.tempoRespawn) };
        pendentesVerdes.Add(identidade, pedido);
        if (isActiveAndEnabled) pedido.rotina = StartCoroutine(RespawnVerde(identidade, pedido));
    }
    public void AgendarRespawnVermelho(string id, Vector3 morte, Quaternion rotacao, Transform[] patrulha, int identidade,
        float raio, float altura, Vector3 centro)
    {
        // Reutiliza a fila e sua deduplicacao; a primeira execucao sempre cede um frame.
        AgendarRespawnVerde(id, morte, rotacao, patrulha, identidade);
        if (!pendentesVerdes.TryGetValue(identidade, out var pedido)) return;
        pedido.volumeVermelho = true;
        pedido.raio = Mathf.Max(0.01f, raio * 0.95f);
        pedido.altura = Mathf.Max(pedido.raio * 2f, altura);
        pedido.centro = centro;
    }
    private bool LocalVerdeLivre(Vector3 pedido, out Vector3 local, PendenteVerde volume = null)
    {
        local = pedido;
        int n = Physics.RaycastNonAlloc(pedido + Vector3.up * 0.5f, Vector3.down, soloVerde, 2.5f, ~0, QueryTriggerInteraction.Ignore);
        if (n == soloVerde.Length) return false;
        float menor = float.PositiveInfinity; bool achou = false; float soloMaisAlto = 0f;
        for (int i = 0; i < n; i++)
        {
            var hit = soloVerde[i];
            if (hit.collider == null || hit.collider.GetComponentInParent<SlimeIA>() != null || hit.collider.GetComponentInParent<StatusPlayer>() != null) continue;
            if (hit.distance < menor && SlimeMovimento.SoloTransitavel(hit.normal, 45f))
            { menor = hit.distance; soloMaisAlto = hit.point.y; local.y = Mathf.Max(pedido.y, hit.point.y + 0.04f); achou = true; }
        }
        if (volume != null && volume.volumeVermelho && achou)
        {
            // Volume real do Vermelho, sem assumir as dimensoes menores do Verde.
            local.y = Mathf.Max(local.y, soloMaisAlto + volume.altura * 0.5f - volume.centro.y + 0.04f);
            Vector3 centro = local + volume.centro;
            float meiaParteReta = Mathf.Max(0f, volume.altura * 0.5f - volume.raio);
            return !Physics.CheckCapsule(centro - Vector3.up * meiaParteReta, centro + Vector3.up * meiaParteReta,
                volume.raio, ~0, QueryTriggerInteraction.Ignore);
        }
        return achou && !Physics.CheckSphere(local + Vector3.up * 0.3f, 0.25f, ~0, QueryTriggerInteraction.Ignore);
    }
    private bool TentarLocalVerdeProximo(Vector3 pedido, out Vector3 local, PendenteVerde volume = null)
    {
        if (LocalVerdeLivre(pedido, out local, volume)) return true;
        // Correcao limitada a 30 cm, usada apenas quando o ponto solicitado esta ocupado.
        if (LocalVerdeLivre(pedido + Vector3.right * 0.3f, out local, volume)) return true;
        if (LocalVerdeLivre(pedido - Vector3.right * 0.3f, out local, volume)) return true;
        if (LocalVerdeLivre(pedido + Vector3.forward * 0.3f, out local, volume)) return true;
        return LocalVerdeLivre(pedido - Vector3.forward * 0.3f, out local, volume);
    }
    private IEnumerator RespawnVerde(int id, PendenteVerde pedido)
    {
        // Sempre cede um frame, inclusive quando o tempo configurado e zero.
        yield return null;
        while (Time.time < pedido.instante) yield return null;
        Vector3 local;
        while (!TentarLocalVerdeProximo(pedido.posicao, out local, pedido)) yield return new WaitForSeconds(1f);
        try
        {
            if (pedido.config.prefabRespawn == null) yield break;
            var novo = Instantiate(pedido.config.prefabRespawn, local, pedido.rotacao);
            var slime = novo.GetComponent<SlimeIA>();
            if (slime != null) { slime.DefinirPontosPatrulha(pedido.patrulha); slime.ReiniciarAposRespawn(); }
        }
        finally { pendentesVerdes.Remove(id); }
    }

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
        foreach (var pedido in pendentesVerdes.Values)
        {
            if (pedido.rotina != null) StopCoroutine(pedido.rotina);
            pedido.rotina = null;
        }
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
