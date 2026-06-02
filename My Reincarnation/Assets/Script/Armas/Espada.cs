using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class Espada : MonoBehaviour
{
    [Header("Dano")]
    [SerializeField] private int danoEspada = 1;

    [Header("Vida / Durabilidade")]
    [SerializeField] private int vidaMaxima = 20;
    [SerializeField] private int vidaAtual = 20;
    [SerializeField] private int desgastePorDanoCausado = 1;
    [SerializeField] private bool destruirQuandoVidaZerar = true;

    [Header("Tags que recebem dano")]
    [SerializeField] private string[] tagsAlvoDano;

    [Header("Cooldown")]
    [SerializeField] private float cooldownDanoMesmoAlvo = 0.35f;

    private Transform donoAtualPlayer;
    private XRGrabInteractable grabInteractable;
    private bool quebrada;
    private readonly Dictionary<int, float> proximoDanoPermitidoPorAlvo = new();

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        NormalizarVida();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        donoAtualPlayer = null;
        proximoDanoPermitidoPorAlvo.Clear();
    }

    private void OnValidate()
    {
        danoEspada = Mathf.Max(0, danoEspada);
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        desgastePorDanoCausado = Mathf.Max(0, desgastePorDanoCausado);
        cooldownDanoMesmoAlvo = Mathf.Max(0f, cooldownDanoMesmoAlvo);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        ProcessarPossivelDano(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessarPossivelDano(other);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        Transform interactorTransform = ObterTransformInteractor(args.interactorObject);
        Transform novoDono = EncontrarPlayerDonoAPartirDoTransform(interactorTransform);
        donoAtualPlayer = novoDono;
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        AtualizarDonoPelaSelecaoAtual();
    }

    private void ProcessarPossivelDano(Collider outroCollider)
    {
        if (outroCollider == null || quebrada || vidaAtual <= 0)
            return;

        GameObject objetoTocado = outroCollider.gameObject;
        if (EhParteDaPropriaEspada(objetoTocado))
            return;

        if (!TagEhAlvoValido(objetoTocado, out GameObject alvoResolvido))
            return;

        if (alvoResolvido == null || EhParteDaPropriaEspada(alvoResolvido))
            return;

        if (PertenceAoDonoAtual(alvoResolvido) || EstaAcopladoAoMesmoDono(alvoResolvido))
            return;

        if (!PodeAplicarDanoAgora(alvoResolvido))
            return;

        bool danoAplicadoOuAlvoPlayerValido = TentarAplicarDano(alvoResolvido, danoEspada);
        if (!danoAplicadoOuAlvoPlayerValido)
            return;

        ReduzirVidaDaEspada();
    }

    private void NormalizarVida()
    {
        vidaMaxima = Mathf.Max(1, vidaMaxima);

        if (vidaAtual <= 0)
            vidaAtual = vidaMaxima;

        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
    }

    private bool PodeAplicarDanoAgora(GameObject alvo)
    {
        if (cooldownDanoMesmoAlvo <= 0f)
            return true;

        int idAlvo = ObterIdCooldown(alvo);
        if (proximoDanoPermitidoPorAlvo.TryGetValue(idAlvo, out float proximoTempo) && Time.time < proximoTempo)
            return false;

        proximoDanoPermitidoPorAlvo[idAlvo] = Time.time + cooldownDanoMesmoAlvo;
        return true;
    }

    private int ObterIdCooldown(GameObject alvo)
    {
        if (alvo == null)
            return 0;

        Rigidbody rb = alvo.GetComponentInParent<Rigidbody>();
        if (rb != null)
            return rb.GetInstanceID();

        return alvo.transform.root != null
            ? alvo.transform.root.gameObject.GetInstanceID()
            : alvo.GetInstanceID();
    }

    private bool EhParteDaPropriaEspada(GameObject obj)
    {
        if (obj == null)
            return false;

        Transform t = obj.transform;
        return t == transform || t.IsChildOf(transform);
    }

    private Transform ObterTransformInteractor(IXRSelectInteractor interactor)
    {
        return (interactor as MonoBehaviour)?.transform;
    }

    private void AtualizarDonoPelaSelecaoAtual()
    {
        donoAtualPlayer = null;

        if (grabInteractable == null || grabInteractable.interactorsSelecting.Count == 0)
            return;

        for (int i = 0; i < grabInteractable.interactorsSelecting.Count; i++)
        {
            Transform interactorTransform = ObterTransformInteractor(grabInteractable.interactorsSelecting[i]);
            Transform player = EncontrarPlayerDonoAPartirDoTransform(interactorTransform);

            if (player == null)
                continue;

            donoAtualPlayer = player;
            return;
        }
    }

    private bool TagEhAlvoValido(GameObject obj, out GameObject alvoResolvido)
    {
        alvoResolvido = null;

        if (obj == null || tagsAlvoDano == null || tagsAlvoDano.Length == 0)
            return false;

        Transform alvoComTag = EncontrarTransformComTagAlvo(obj.transform);
        if (alvoComTag != null)
        {
            alvoResolvido = alvoComTag.gameObject;
            return true;
        }

        Rigidbody rb = obj.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            alvoComTag = EncontrarTransformComTagAlvo(rb.transform);
            if (alvoComTag != null)
            {
                alvoResolvido = alvoComTag.gameObject;
                return true;
            }
        }

        if (obj.transform.root != null)
        {
            alvoComTag = EncontrarTransformComTagAlvo(obj.transform.root);
            if (alvoComTag != null)
            {
                alvoResolvido = alvoComTag.gameObject;
                return true;
            }
        }

        return false;
    }

    private Transform EncontrarTransformComTagAlvo(Transform origem)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (TagConfigurada(atual.gameObject))
                return atual;

            atual = atual.parent;
        }

        return null;
    }

    private bool TagConfigurada(GameObject obj)
    {
        if (obj == null || tagsAlvoDano == null)
            return false;

        string tagObjeto = obj.tag;
        for (int i = 0; i < tagsAlvoDano.Length; i++)
        {
            string tagPermitida = tagsAlvoDano[i];
            if (string.IsNullOrWhiteSpace(tagPermitida))
                continue;

            if (string.Equals(tagObjeto, tagPermitida.Trim(), StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private bool PertenceAoDonoAtual(GameObject obj)
    {
        if (donoAtualPlayer == null || obj == null)
            return false;

        Transform alvo = obj.transform;
        if (alvo == donoAtualPlayer || alvo.IsChildOf(donoAtualPlayer))
            return true;

        Transform playerDoAlvo = EncontrarPlayerDonoAPartirDoTransform(alvo);
        return playerDoAlvo == donoAtualPlayer;
    }

    private bool EstaAcopladoAoMesmoDono(GameObject obj)
    {
        if (donoAtualPlayer == null || obj == null)
            return false;

        if (InteractableEstaSelecionadoPeloDono(obj.GetComponentInParent<XRGrabInteractable>()))
            return true;

        XRGrabInteractable[] interactablesFilhos = obj.GetComponentsInChildren<XRGrabInteractable>(true);
        for (int i = 0; i < interactablesFilhos.Length; i++)
        {
            if (InteractableEstaSelecionadoPeloDono(interactablesFilhos[i]))
                return true;
        }

        return false;
    }

    private bool InteractableEstaSelecionadoPeloDono(XRGrabInteractable interactable)
    {
        if (interactable == null || interactable == grabInteractable)
            return false;

        for (int i = 0; i < interactable.interactorsSelecting.Count; i++)
        {
            Transform interactorTransform = ObterTransformInteractor(interactable.interactorsSelecting[i]);
            Transform player = EncontrarPlayerDonoAPartirDoTransform(interactorTransform);

            if (player == donoAtualPlayer)
                return true;
        }

        return false;
    }

    private bool TentarAplicarDano(GameObject alvo, int dano)
    {
        if (alvo == null || dano <= 0)
            return false;

        if (TentarChamarReceberDano(alvo, dano))
            return true;

        if (EhPlayer(alvo))
        {
            Debug.LogWarning($"[Espada] Alvo Player '{alvo.name}' foi atingido, mas nao possui metodo publico ReceberDano(int).");
            return true;
        }

        return false;
    }

    private bool TentarChamarReceberDano(GameObject alvo, int dano)
    {
        Component[] componentes = ColetarComponentesParaDano(alvo);
        for (int i = 0; i < componentes.Length; i++)
        {
            Component componente = componentes[i];
            if (componente == null || componente == this)
                continue;

            MethodInfo metodo = componente.GetType().GetMethod(
                "ReceberDano",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(int) },
                null);

            if (metodo == null)
                continue;

            try
            {
                metodo.Invoke(componente, new object[] { dano });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Espada] Falha ao aplicar dano em '{componente.name}' via ReceberDano(int): {e.Message}");
                return false;
            }
        }

        return false;
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

    private bool EhPlayer(GameObject obj)
    {
        return EncontrarPlayerDonoAPartirDoTransform(obj != null ? obj.transform : null) != null;
    }

    private Transform EncontrarPlayerDonoAPartirDoTransform(Transform origem)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (string.Equals(atual.tag, "Player", StringComparison.Ordinal))
                return atual;

            atual = atual.parent;
        }

        return null;
    }

    private void ReduzirVidaDaEspada()
    {
        if (desgastePorDanoCausado <= 0 || quebrada)
            return;

        vidaAtual = Mathf.Max(0, vidaAtual - desgastePorDanoCausado);

        if (vidaAtual <= 0)
            QuebrarEspada();
    }

    private void QuebrarEspada()
    {
        if (quebrada)
            return;

        quebrada = true;
        vidaAtual = 0;

        if (destruirQuandoVidaZerar)
            Destroy(gameObject);
    }
}
