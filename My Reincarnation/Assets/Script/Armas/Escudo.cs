using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class Escudo : MonoBehaviour
{
    [Header("Vida / Durabilidade")]
    [SerializeField] private int vidaMaxima = 30;
    [SerializeField] private int vidaAtual = 30;
    [SerializeField] private int desgastePorBloqueio = 1;
    [SerializeField] private bool destruirQuandoVidaZerar = true;

    [Header("Tags que desgastam o escudo")]
    [SerializeField] private string[] tagsQueDesgastamEscudo;

    [Header("Cooldown")]
    [SerializeField] private float cooldownBloqueioMesmoObjeto = 0.25f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somBloqueio;
    [SerializeField] private AudioClip somQuebrar;
    [SerializeField] private float volumeBloqueio = 1f;
    [SerializeField] private float volumeQuebrar = 1f;

    private Transform donoAtualPlayer;
    private XRGrabInteractable grabInteractable;
    private bool quebrado;
    private readonly Dictionary<int, float> proximoBloqueioPermitidoPorObjeto = new();

    public int VidaAtual => vidaAtual;
    public int VidaMaxima => vidaMaxima;
    public bool Quebrado => quebrado || vidaAtual <= 0;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        NormalizarVida();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

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
        proximoBloqueioPermitidoPorObjeto.Clear();
    }

    private void OnValidate()
    {
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        desgastePorBloqueio = Mathf.Max(0, desgastePorBloqueio);
        cooldownBloqueioMesmoObjeto = Mathf.Max(0f, cooldownBloqueioMesmoObjeto);
        volumeBloqueio = Mathf.Max(0f, volumeBloqueio);
        volumeQuebrar = Mathf.Max(0f, volumeQuebrar);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null)
            return;

        ProcessarBloqueio(collision.collider.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        ProcessarBloqueio(other.gameObject);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        Transform interactorTransform = ObterTransformInteractor(args.interactorObject);
        donoAtualPlayer = EncontrarPlayerDonoAPartirDoTransform(interactorTransform);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        AtualizarDonoPelaSelecaoAtual();
    }

    public bool EstaProtegendoPlayer(Transform player)
    {
        if (Quebrado || player == null || donoAtualPlayer == null)
            return false;

        return player == donoAtualPlayer || player.IsChildOf(donoAtualPlayer);
    }

    public bool BloqueiaDanoDe(GameObject origemDano, Transform playerAlvo)
    {
        if (!EstaProtegendoPlayer(playerAlvo) || origemDano == null)
            return false;

        if (EhParteDoProprioEscudo(origemDano))
            return false;

        if (!TagPodeDesgastarEscudo(origemDano, out GameObject origemResolvida))
            return false;

        if (PertenceAoDonoAtual(origemResolvida) || EstaAcopladoAoMesmoDono(origemResolvida))
            return false;

        // TODO futuro:
        // if (escudo != null && escudo.BloqueiaDanoDe(origemDano, playerTransform))
        //     return; // dano bloqueado
        return true;
    }

    public bool RegistrarBloqueio(GameObject origemDano)
    {
        if (Quebrado || origemDano == null)
            return false;

        if (EhParteDoProprioEscudo(origemDano))
            return false;

        if (!TagPodeDesgastarEscudo(origemDano, out GameObject origemResolvida))
            return false;

        if (origemResolvida == null || EhParteDoProprioEscudo(origemResolvida))
            return false;

        if (PertenceAoDonoAtual(origemResolvida) || EstaAcopladoAoMesmoDono(origemResolvida))
            return false;

        if (!PodeBloquearAgora(origemResolvida))
            return false;

        TocarSomBloqueio();
        ReduzirVidaDoEscudo();
        return true;
    }

    private void ProcessarBloqueio(GameObject objetoColidido)
    {
        RegistrarBloqueio(objetoColidido);
    }

    private void NormalizarVida()
    {
        vidaMaxima = Mathf.Max(1, vidaMaxima);

        if (vidaAtual <= 0)
            vidaAtual = vidaMaxima;

        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
    }

    private bool PodeBloquearAgora(GameObject origem)
    {
        if (cooldownBloqueioMesmoObjeto <= 0f)
            return true;

        int idOrigem = ObterIdCooldown(origem);
        if (proximoBloqueioPermitidoPorObjeto.TryGetValue(idOrigem, out float proximoTempo) && Time.time < proximoTempo)
            return false;

        proximoBloqueioPermitidoPorObjeto[idOrigem] = Time.time + cooldownBloqueioMesmoObjeto;
        return true;
    }

    private int ObterIdCooldown(GameObject origem)
    {
        if (origem == null)
            return 0;

        Rigidbody rb = origem.GetComponentInParent<Rigidbody>();
        if (rb != null)
            return rb.GetInstanceID();

        return origem.transform.root != null
            ? origem.transform.root.gameObject.GetInstanceID()
            : origem.GetInstanceID();
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

    private bool TagPodeDesgastarEscudo(GameObject obj, out GameObject origemResolvida)
    {
        origemResolvida = null;

        if (obj == null || tagsQueDesgastamEscudo == null || tagsQueDesgastamEscudo.Length == 0)
            return false;

        Transform origemComTag = EncontrarTransformComTagPermitida(obj.transform);
        if (origemComTag != null)
        {
            origemResolvida = origemComTag.gameObject;
            return true;
        }

        Rigidbody rb = obj.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            origemComTag = EncontrarTransformComTagPermitida(rb.transform);
            if (origemComTag != null)
            {
                origemResolvida = origemComTag.gameObject;
                return true;
            }
        }

        if (obj.transform.root != null)
        {
            origemComTag = EncontrarTransformComTagPermitida(obj.transform.root);
            if (origemComTag != null)
            {
                origemResolvida = origemComTag.gameObject;
                return true;
            }
        }

        return false;
    }

    private Transform EncontrarTransformComTagPermitida(Transform origem)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (TagEstaConfigurada(atual.gameObject))
                return atual;

            atual = atual.parent;
        }

        return null;
    }

    private bool TagEstaConfigurada(GameObject obj)
    {
        if (obj == null || tagsQueDesgastamEscudo == null)
            return false;

        string tagObjeto = obj.tag;
        for (int i = 0; i < tagsQueDesgastamEscudo.Length; i++)
        {
            string tagPermitida = tagsQueDesgastamEscudo[i];
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

        Transform donoDoObjeto = ObterDonoAtualDoObjeto(obj);
        return donoDoObjeto == donoAtualPlayer;
    }

    private bool EstaAcopladoAoMesmoDono(GameObject obj)
    {
        if (donoAtualPlayer == null || obj == null)
            return false;

        Transform donoDoObjeto = ObterDonoAtualDoObjeto(obj);
        return donoDoObjeto == donoAtualPlayer;
    }

    private Transform ObterDonoAtualDoObjeto(GameObject obj)
    {
        if (obj == null)
            return null;

        Transform playerNaHierarquia = EncontrarPlayerDonoAPartirDoTransform(obj.transform);
        if (playerNaHierarquia != null)
            return playerNaHierarquia;

        XRGrabInteractable interactable = obj.GetComponentInParent<XRGrabInteractable>();
        Transform dono = ObterDonoDeInteractable(interactable);
        if (dono != null)
            return dono;

        XRGrabInteractable[] interactablesFilhos = obj.GetComponentsInChildren<XRGrabInteractable>(true);
        for (int i = 0; i < interactablesFilhos.Length; i++)
        {
            dono = ObterDonoDeInteractable(interactablesFilhos[i]);
            if (dono != null)
                return dono;
        }

        Rigidbody rb = obj.GetComponentInParent<Rigidbody>();
        if (rb != null && rb.gameObject != obj)
            return ObterDonoAtualDoObjeto(rb.gameObject);

        return null;
    }

    private Transform ObterDonoDeInteractable(XRGrabInteractable interactable)
    {
        if (interactable == null)
            return null;

        for (int i = 0; i < interactable.interactorsSelecting.Count; i++)
        {
            Transform interactorTransform = ObterTransformInteractor(interactable.interactorsSelecting[i]);
            Transform player = EncontrarPlayerDonoAPartirDoTransform(interactorTransform);

            if (player != null)
                return player;
        }

        return null;
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

    private bool EhParteDoProprioEscudo(GameObject obj)
    {
        if (obj == null)
            return false;

        Transform t = obj.transform;
        return t == transform || t.IsChildOf(transform);
    }

    private void ReduzirVidaDoEscudo()
    {
        if (desgastePorBloqueio <= 0 || quebrado)
            return;

        vidaAtual = Mathf.Max(0, vidaAtual - desgastePorBloqueio);

        if (vidaAtual <= 0)
            QuebrarEscudo();
    }

    private void QuebrarEscudo()
    {
        if (quebrado)
            return;

        quebrado = true;
        vidaAtual = 0;
        TocarSomQuebrar();

        if (destruirQuandoVidaZerar)
            Destroy(gameObject);
    }

    private void TocarSomBloqueio()
    {
        if (audioSource != null && somBloqueio != null)
            audioSource.PlayOneShot(somBloqueio, volumeBloqueio);
    }

    private void TocarSomQuebrar()
    {
        if (somQuebrar != null)
            AudioSource.PlayClipAtPoint(somQuebrar, transform.position, volumeQuebrar);
    }
}
