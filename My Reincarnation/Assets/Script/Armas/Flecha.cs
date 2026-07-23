using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class Flecha : MonoBehaviour, IDano
{
    [Header("Dano da Flecha")]
    [SerializeField] private float dano = 10f;
    [SerializeField] private string[] tagsQueRecebemDano = { "Enemy", "Player" };
    [SerializeField] private bool causarDanoUmaVez = true;

    [Header("Ignorar Colisao")]
    [SerializeField] private string[] tagsIgnoradasColisao = { "Arch" };
    [SerializeField] private bool ignorarDonoDaFlecha = true;
    [SerializeField] private bool ignorarTerrain = true;

    [Header("Direcao Visual")]
    [SerializeField] private Transform traseiraDirecao;
    [SerializeField] private Transform pontaDirecao;

    [Header("Diagnostico Disparo")]
    [SerializeField] private bool diagnosticoTraseiraEncontrada;
    [SerializeField] private bool diagnosticoPontaEncontrada;
    [SerializeField] private Vector3 diagnosticoDirecaoVisual;
    [SerializeField] private Vector3 diagnosticoEscalaMundial;
    [SerializeField] private bool diagnosticoRigidbodyCinematico;
    [SerializeField] private int diagnosticoQuantidadeParesIgnorados;

    [Header("Tempo de Vida")]
    [SerializeField] private float tempoDeVidaAposLancada = 10f;
    [SerializeField] private bool destruirDepoisDoTempoDeVida = true;

    [Header("Audio")]
    [SerializeField] private AudioClip somColisao;
    [SerializeField] private float volumeSomColisao = 1f;

    [Header("Efeitos")]
    [SerializeField] private GameObject efeitoAoCriar;
    [SerializeField] private bool parentarEfeitoCriacaoNaFlecha = true;
    [SerializeField] private GameObject efeitoAoColidir;
    [SerializeField] private float tempoDestruirEfeitoColisao = 5f;

    [Header("Destruicao")]
    [SerializeField] private bool destruirAoColidir = true;
    [SerializeField] private float atrasoDestruirAposColisao = 0f;

    private bool jaCausouDano;
    private bool colisaoProcessada;
    private bool efeitoCriacaoInstanciado;
    private bool foiLancada;
    private bool tempoDeVidaAtivo;
    private float tempoLancamento;
    private float multiplicadorDano = 1f;
    private GameObject dono;
    private Transform raizDono;
    private readonly List<ParColisaoIgnorado> paresColisaoIgnorados = new();

    private readonly struct ParColisaoIgnorado
    {
        public readonly Collider Flecha;
        public readonly Collider Ignorado;

        public ParColisaoIgnorado(Collider flecha, Collider ignorado)
        {
            Flecha = flecha;
            Ignorado = ignorado;
        }
    }

    public float ObterDano()
    {
        return Mathf.Max(0f, dano * multiplicadorDano);
    }

    public GameObject ObterDono()
    {
        return dono;
    }

    public void DefinirDono(GameObject novoDono)
    {
        dono = novoDono;
        raizDono = dono != null && dono.transform != null ? dono.transform.root : null;
    }

    public void DefinirMultiplicadorDano(float valor)
    {
        multiplicadorDano = Mathf.Max(0f, valor);
    }

    public bool TentarObterDirecaoVisual(out Vector3 direcao)
    {
        direcao = Vector3.zero;
        diagnosticoTraseiraEncontrada = traseiraDirecao != null;
        diagnosticoPontaEncontrada = pontaDirecao != null;

        if (!diagnosticoTraseiraEncontrada || !diagnosticoPontaEncontrada || traseiraDirecao == pontaDirecao)
        {
            diagnosticoDirecaoVisual = Vector3.zero;
            return false;
        }

        Vector3 diferenca = pontaDirecao.position - traseiraDirecao.position;
        if (diferenca.sqrMagnitude < 0.000001f)
        {
            diagnosticoDirecaoVisual = Vector3.zero;
            return false;
        }

        direcao = diferenca.normalized;
        diagnosticoDirecaoVisual = direcao;
        return true;
    }

    public int ConfigurarColisoesIgnoradas(
        GameObject arco,
        GameObject donoDisparo,
        IEnumerable<Collider> collidersExtras)
    {
        RestaurarColisoesIgnoradas();
        DefinirDono(donoDisparo);

        Collider[] collidersFlecha = GetComponentsInChildren<Collider>(true);
        HashSet<Collider> collidersIgnorados = new();
        AdicionarColliders(arco, collidersIgnorados);
        AdicionarColliders(donoDisparo, collidersIgnorados);

        if (collidersExtras != null)
        {
            foreach (Collider colliderExtra in collidersExtras)
            {
                if (colliderExtra != null)
                    collidersIgnorados.Add(colliderExtra);
            }
        }

        for (int i = 0; i < collidersFlecha.Length; i++)
        {
            Collider colliderFlecha = collidersFlecha[i];
            if (colliderFlecha == null)
                continue;

            foreach (Collider colliderIgnorado in collidersIgnorados)
            {
                if (colliderIgnorado == null || colliderIgnorado == colliderFlecha ||
                    ColliderPertenceAPropriaFlecha(colliderIgnorado))
                {
                    continue;
                }

                Physics.IgnoreCollision(colliderFlecha, colliderIgnorado, true);
                paresColisaoIgnorados.Add(new ParColisaoIgnorado(colliderFlecha, colliderIgnorado));
            }
        }

        diagnosticoQuantidadeParesIgnorados = paresColisaoIgnorados.Count;
        return diagnosticoQuantidadeParesIgnorados;
    }

    public void PrepararFisicaParaDisparo(bool usarGravidade)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = GetComponentInChildren<Rigidbody>();

        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.useGravity = usarGravidade;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        AtualizarDiagnosticoFisica(rb);
    }

    public void MarcarComoLancada(GameObject donoDaFlecha)
    {
        DefinirDono(donoDaFlecha);
        foiLancada = true;
        tempoDeVidaAtivo = true;
        tempoLancamento = Time.time;
        jaCausouDano = false;
        colisaoProcessada = false;
    }

    public void ResetarEstadoParaInventarioOuPrefab()
    {
        RestaurarColisoesIgnoradas();
        foiLancada = false;
        tempoDeVidaAtivo = false;
        tempoLancamento = 0f;
        jaCausouDano = false;
        colisaoProcessada = false;
        dono = null;
        raizDono = null;
    }

    private void Awake()
    {
        InstanciarEfeitoCriacao();
        AtualizarDiagnosticoFisica(GetComponent<Rigidbody>());
    }

    private void Update()
    {
        if (!foiLancada || !tempoDeVidaAtivo || !destruirDepoisDoTempoDeVida)
            return;

        if (Time.time - tempoLancamento >= tempoDeVidaAposLancada)
            Destroy(gameObject);
    }

    public bool Disparar(Vector3 direcao, float forca)
    {
        jaCausouDano = false;
        colisaoProcessada = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = GetComponentInChildren<Rigidbody>();

        if (rb == null)
            return false;

        PrepararFisicaParaDisparo(true);
        rb.AddForce(direcao.normalized * Mathf.Max(0f, forca), ForceMode.Impulse);
        AtualizarDiagnosticoFisica(rb);

        if (!foiLancada)
            MarcarComoLancada(dono);

        return true;
    }

    private void OnValidate()
    {
        dano = Mathf.Max(0f, dano);
        multiplicadorDano = Mathf.Max(0f, multiplicadorDano);
        volumeSomColisao = Mathf.Max(0f, volumeSomColisao);
        tempoDestruirEfeitoColisao = Mathf.Max(0f, tempoDestruirEfeitoColisao);
        atrasoDestruirAposColisao = Mathf.Max(0f, atrasoDestruirAposColisao);
        tempoDeVidaAposLancada = Mathf.Max(0f, tempoDeVidaAposLancada);
        diagnosticoTraseiraEncontrada = traseiraDirecao != null;
        diagnosticoPontaEncontrada = pontaDirecao != null;
        diagnosticoEscalaMundial = transform.lossyScale;
    }

    private void OnDestroy()
    {
        paresColisaoIgnorados.Clear();
    }

    private static void AdicionarColliders(GameObject raiz, HashSet<Collider> destino)
    {
        if (raiz == null || destino == null)
            return;

        Collider[] colliders = raiz.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                destino.Add(colliders[i]);
        }
    }

    private void RestaurarColisoesIgnoradas()
    {
        for (int i = 0; i < paresColisaoIgnorados.Count; i++)
        {
            ParColisaoIgnorado par = paresColisaoIgnorados[i];
            if (par.Flecha != null && par.Ignorado != null)
                Physics.IgnoreCollision(par.Flecha, par.Ignorado, false);
        }

        paresColisaoIgnorados.Clear();
        diagnosticoQuantidadeParesIgnorados = 0;
    }

    private void AtualizarDiagnosticoFisica(Rigidbody rb)
    {
        diagnosticoEscalaMundial = transform.lossyScale;
        diagnosticoRigidbodyCinematico = rb != null && rb.isKinematic;
        TentarObterDirecaoVisual(out _);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        Vector3 ponto = transform.position;
        Vector3 normal = -transform.forward;

        if (collision.contactCount > 0)
        {
            ContactPoint contato = collision.GetContact(0);
            ponto = contato.point;
            normal = contato.normal;
        }

        ProcessarColisao(collision.collider, ponto, normal);
    }

    private void OnTriggerEnter(Collider other)
    {
        Vector3 ponto = other != null ? other.ClosestPoint(transform.position) : transform.position;
        Vector3 normal = transform.forward.sqrMagnitude > 0.0001f ? -transform.forward : Vector3.up;
        ProcessarColisao(other, ponto, normal);
    }

    private void ProcessarColisao(Collider outroCollider, Vector3 pontoColisao, Vector3 normalColisao)
    {
        if (outroCollider == null)
            return;

        if (!foiLancada)
            return;

        if (colisaoProcessada)
            return;

        if (DeveIgnorarColisao(outroCollider))
            return;

        colisaoProcessada = true;

        TocarSomColisao(pontoColisao);

        if ((!causarDanoUmaVez || !jaCausouDano) &&
            TentarResolverAlvoValido(outroCollider, out GameObject alvo))
        {
            int danoFinal = Mathf.RoundToInt(ObterDano());
            if (danoFinal > 0 && TentarAplicarDano(alvo, danoFinal))
                jaCausouDano = true;
        }

        InstanciarEfeitoColisao(pontoColisao, normalColisao);

        if (destruirAoColidir)
            Destroy(gameObject, atrasoDestruirAposColisao);
    }

    private bool TentarResolverAlvoValido(Collider outroCollider, out GameObject alvo)
    {
        alvo = null;

        if (outroCollider == null || tagsQueRecebemDano == null || tagsQueRecebemDano.Length == 0)
            return false;

        Transform transformComTag = ProcurarTransformComTagPermitida(outroCollider.transform);
        if (transformComTag == null && outroCollider.attachedRigidbody != null)
            transformComTag = ProcurarTransformComTagPermitida(outroCollider.attachedRigidbody.transform);

        if (transformComTag == null && outroCollider.transform.root != null &&
            TagEstaPermitida(outroCollider.transform.root.tag))
        {
            transformComTag = outroCollider.transform.root;
        }

        alvo = transformComTag != null ? transformComTag.gameObject : null;
        return alvo != null;
    }

    private bool TentarAplicarDano(GameObject alvo, int danoFinal)
    {
        if (alvo == null || danoFinal <= 0)
            return false;

        Component[] componentes = ColetarComponentesParaDano(alvo);
        for (int i = 0; i < componentes.Length; i++)
        {
            Component componente = componentes[i];
            if (componente == null || componente == this)
                continue;

            if (TentarChamarMetodoDano(componente, "ReceberDano", danoFinal))
                return true;

            if (TentarChamarMetodoDano(componente, "TomarDano", danoFinal))
                return true;

            if (TentarChamarMetodoDano(componente, "AplicarDano", danoFinal))
                return true;
        }

        return false;
    }

    private bool TentarChamarMetodoDano(Component componente, string nomeMetodo, int danoFinal)
    {
        Type tipo = componente.GetType();

        MethodInfo metodoIntComOrigem = tipo.GetMethod(
            nomeMetodo,
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(int), typeof(GameObject) },
            null);

        if (metodoIntComOrigem != null)
            return InvocarMetodoDano(metodoIntComOrigem, componente, danoFinal, gameObject);

        MethodInfo metodoFloatComOrigem = tipo.GetMethod(
            nomeMetodo,
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(float), typeof(GameObject) },
            null);

        if (metodoFloatComOrigem != null)
            return InvocarMetodoDano(metodoFloatComOrigem, componente, (float)danoFinal, gameObject);

        MethodInfo metodoInt = tipo.GetMethod(
            nomeMetodo,
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(int) },
            null);

        if (metodoInt != null)
            return InvocarMetodoDano(metodoInt, componente, danoFinal);

        MethodInfo metodoFloat = tipo.GetMethod(
            nomeMetodo,
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(float) },
            null);

        return metodoFloat != null && InvocarMetodoDano(metodoFloat, componente, (float)danoFinal);
    }

    private bool InvocarMetodoDano(MethodInfo metodo, Component componente, params object[] parametros)
    {
        try
        {
            metodo.Invoke(componente, parametros);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private Component[] ColetarComponentesParaDano(GameObject alvo)
    {
        List<Component> componentes = new();
        HashSet<Component> visitados = new();

        AdicionarComponentes(alvo.GetComponents<Component>(), componentes, visitados);
        AdicionarComponentes(alvo.GetComponentsInParent<Component>(true), componentes, visitados);
        AdicionarComponentes(alvo.GetComponentsInChildren<Component>(true), componentes, visitados);

        Rigidbody rb = alvo.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            AdicionarComponentes(rb.GetComponents<Component>(), componentes, visitados);
            AdicionarComponentes(rb.GetComponentsInParent<Component>(true), componentes, visitados);
            AdicionarComponentes(rb.GetComponentsInChildren<Component>(true), componentes, visitados);
        }

        return componentes.ToArray();
    }

    private void AdicionarComponentes(Component[] origem, List<Component> destino, HashSet<Component> visitados)
    {
        if (origem == null)
            return;

        for (int i = 0; i < origem.Length; i++)
        {
            Component componente = origem[i];
            if (componente == null || !visitados.Add(componente))
                continue;

            destino.Add(componente);
        }
    }

    private bool DeveIgnorarColisao(Collider outroCollider)
    {
        if (outroCollider == null)
            return true;

        if (ColliderPertenceAPropriaFlecha(outroCollider))
            return true;

        if (EhTerrain(outroCollider))
            return true;

        if (ColliderTemTagIgnorada(outroCollider))
            return true;

        return ignorarDonoDaFlecha && ColliderPertenceAoDono(outroCollider);
    }

    private bool EhTerrain(Collider outroCollider)
    {
        if (!ignorarTerrain || outroCollider == null)
            return false;

        if (outroCollider is TerrainCollider)
            return true;

        if (outroCollider.GetComponent<Terrain>() != null ||
            outroCollider.GetComponentInParent<Terrain>() != null)
        {
            return true;
        }

        return outroCollider.attachedRigidbody != null &&
               outroCollider.attachedRigidbody.GetComponentInParent<Terrain>() != null;
    }

    private bool ColliderTemTagIgnorada(Collider outroCollider)
    {
        if (outroCollider == null || tagsIgnoradasColisao == null || tagsIgnoradasColisao.Length == 0)
            return false;

        if (TransformTemTagPermitida(outroCollider.transform, tagsIgnoradasColisao))
            return true;

        if (outroCollider.attachedRigidbody != null &&
            TransformTemTagPermitida(outroCollider.attachedRigidbody.transform, tagsIgnoradasColisao))
        {
            return true;
        }

        Transform root = outroCollider.transform.root;
        return root != null && TagEstaNaLista(root.tag, tagsIgnoradasColisao);
    }

    private bool ColliderPertenceAoDono(Collider outroCollider)
    {
        if (outroCollider == null || (dono == null && raizDono == null))
            return false;

        if (TransformPertenceAoDono(outroCollider.transform))
            return true;

        return outroCollider.attachedRigidbody != null &&
               TransformPertenceAoDono(outroCollider.attachedRigidbody.transform);
    }

    private bool TransformPertenceAoDono(Transform origem)
    {
        if (origem == null || (dono == null && raizDono == null))
            return false;

        if (dono != null)
        {
            Transform donoTransform = dono.transform;
            if (origem == donoTransform || origem.IsChildOf(donoTransform))
                return true;
        }

        Transform rootOrigem = origem.root;
        return raizDono != null && rootOrigem != null && rootOrigem == raizDono;
    }

    private bool ColliderPertenceAPropriaFlecha(Collider outroCollider)
    {
        if (outroCollider == null)
            return false;

        Transform outro = outroCollider.transform;
        return outro == transform || outro.IsChildOf(transform);
    }

    private void InstanciarEfeitoCriacao()
    {
        if (efeitoCriacaoInstanciado || efeitoAoCriar == null)
            return;

        efeitoCriacaoInstanciado = true;

        Transform parent = parentarEfeitoCriacaoNaFlecha ? transform : null;
        GameObject efeito = Instantiate(efeitoAoCriar, transform.position, transform.rotation, parent);

        if (parentarEfeitoCriacaoNaFlecha && efeito != null)
        {
            efeito.transform.localPosition = Vector3.zero;
            efeito.transform.localRotation = Quaternion.identity;
        }
    }

    private void InstanciarEfeitoColisao(Vector3 ponto, Vector3 normal)
    {
        if (efeitoAoColidir == null)
            return;

        Quaternion rotacao = normal.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(normal.normalized)
            : Quaternion.identity;

        GameObject efeito = Instantiate(efeitoAoColidir, ponto, rotacao);
        if (efeito != null && tempoDestruirEfeitoColisao > 0f)
            Destroy(efeito, tempoDestruirEfeitoColisao);
    }

    private void TocarSomColisao(Vector3 ponto)
    {
        if (somColisao == null || volumeSomColisao <= 0f)
            return;

        AudioSource.PlayClipAtPoint(somColisao, ponto, volumeSomColisao);
    }

    private Transform ProcurarTransformComTagPermitida(Transform origem)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (TagEstaPermitida(atual.tag))
                return atual;

            atual = atual.parent;
        }

        return null;
    }

    private bool TransformTemTagPermitida(Transform origem, string[] tagsPermitidas)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (TagEstaNaLista(atual.tag, tagsPermitidas))
                return true;

            atual = atual.parent;
        }

        return false;
    }

    private bool TagEstaPermitida(string tagAtual)
    {
        return TagEstaNaLista(tagAtual, tagsQueRecebemDano);
    }

    private bool TagEstaNaLista(string tagAtual, string[] tagsPermitidas)
    {
        if (string.IsNullOrWhiteSpace(tagAtual) || tagsPermitidas == null)
            return false;

        for (int i = 0; i < tagsPermitidas.Length; i++)
        {
            string tagPermitida = tagsPermitidas[i];
            if (string.IsNullOrWhiteSpace(tagPermitida))
                continue;

            if (string.Equals(tagAtual, tagPermitida.Trim(), StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
