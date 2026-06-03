using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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

    [Header("Texto Durabilidade")]
    public TMP_Text textoValorAtual;
    public TMP_Text textoValorTotal;

    [Header("Efeito LED Texto")]
    public Color corTextoA = Color.white;
    public Color corTextoB = Color.cyan;
    public float velocidadePiscarTexto = 2f;
    public bool usarEfeitoLedTexto = true;

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
    private Transform raizTextoDurabilidade;
    private XRGrabInteractable grabInteractable;
    private bool quebrado;
    private readonly Dictionary<int, float> proximoBloqueioPermitidoPorObjeto = new();
    private static readonly CultureInfo CulturaDurabilidade = CultureInfo.GetCultureInfo("pt-BR");

    public int VidaAtual => vidaAtual;
    public int VidaMaxima => vidaMaxima;
    public bool Quebrado => quebrado || vidaAtual <= 0;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        NormalizarVida();
        EncontrarTextosDurabilidadeSeNecessario(true);
        AtualizarTextoDurabilidade(true);
        AplicarCorTexto(corTextoA);
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
        velocidadePiscarTexto = Mathf.Max(0f, velocidadePiscarTexto);
        AtualizarTextoDurabilidade(false);
    }

    private void LateUpdate()
    {
        RotacionarTextoParaCamera();
        AtualizarEfeitoLedTexto();
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
        AtualizarTextoDurabilidade(false);
    }

    private void EncontrarTextosDurabilidadeSeNecessario(bool criarSeNecessario = false)
    {
        if (textoValorAtual != null && textoValorTotal != null)
            return;

        TMP_Text[] textos = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textos.Length; i++)
        {
            TMP_Text texto = textos[i];
            if (texto == null)
                continue;

            string nome = NormalizarNome(texto.gameObject.name);

            if (textoValorAtual == null && (nome == "valoratual" || nome == "atual"))
                textoValorAtual = texto;

            if (textoValorTotal == null && (nome == "valortotal" || nome == "total"))
                textoValorTotal = texto;
        }

        if (criarSeNecessario && Application.isPlaying && (textoValorAtual == null || textoValorTotal == null))
            CriarTextosDurabilidadeSeNecessario();
    }

    public void AtualizarDurabilidadeVisual()
    {
        AtualizarTextoDurabilidade(true);
    }

    private void AtualizarTextoDurabilidade(bool criarSeNecessario = false)
    {
        EncontrarTextosDurabilidadeSeNecessario(criarSeNecessario);

        if (textoValorAtual != null)
            textoValorAtual.text = FormatarValorDurabilidade(vidaAtual);

        if (textoValorTotal != null)
            textoValorTotal.text = FormatarValorDurabilidade(vidaMaxima);
    }

    private string FormatarValorDurabilidade(int valor)
    {
        return Mathf.Max(0, valor).ToString("N0", CulturaDurabilidade);
    }

    private void AtualizarEfeitoLedTexto()
    {
        EncontrarTextosDurabilidadeSeNecessario(true);

        Color cor = corTextoA;
        if (usarEfeitoLedTexto && velocidadePiscarTexto > 0f)
        {
            float t = Mathf.PingPong(Time.time * velocidadePiscarTexto, 1f);
            cor = Color.Lerp(corTextoA, corTextoB, t);
        }

        AplicarCorTexto(cor);
    }

    private void CriarTextosDurabilidadeSeNecessario()
    {
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            canvas = CriarCanvasDurabilidade();

        Transform raiz = canvas.transform;

        if (textoValorAtual == null)
            textoValorAtual = CriarTextoDurabilidade(raiz, "Valor Atual", new Vector2(-45f, 0f), TextAlignmentOptions.Right);

        if (!ExisteSeparadorDurabilidade(raiz))
            CriarTextoDurabilidade(raiz, "Separador", Vector2.zero, TextAlignmentOptions.Center).text = "/";

        if (textoValorTotal == null)
            textoValorTotal = CriarTextoDurabilidade(raiz, "Valor Total", new Vector2(45f, 0f), TextAlignmentOptions.Left);

        raizTextoDurabilidade = raiz;
    }

    private Canvas CriarCanvasDurabilidade()
    {
        GameObject canvasObj = new GameObject("Canvas Durabilidade", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObj.transform.SetParent(transform, false);

        RectTransform rect = canvasObj.GetComponent<RectTransform>();
        rect.localPosition = new Vector3(0f, 0.55f, -0.35f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.01f;
        rect.sizeDelta = new Vector2(160f, 40f);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = ObterCameraTextoDurabilidade();
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        scaler.referencePixelsPerUnit = 100f;

        return canvas;
    }

    private TMP_Text CriarTextoDurabilidade(Transform parent, string nome, Vector2 posicao, TextAlignmentOptions alinhamento)
    {
        GameObject textoObj = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textoObj.transform.SetParent(parent, false);

        RectTransform rect = textoObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = new Vector2(80f, 30f);

        TextMeshProUGUI texto = textoObj.GetComponent<TextMeshProUGUI>();
        texto.text = "0";
        texto.fontSize = 24f;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 10f;
        texto.fontSizeMax = 26f;
        texto.alignment = alinhamento;
        texto.raycastTarget = false;
        texto.color = corTextoA;

        return texto;
    }

    private bool ExisteSeparadorDurabilidade(Transform raiz)
    {
        TMP_Text[] textos = raiz.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textos.Length; i++)
        {
            TMP_Text texto = textos[i];
            if (texto == null)
                continue;

            string nome = NormalizarNome(texto.gameObject.name);
            if (nome == "separador" || texto.text.Trim() == "/")
                return true;
        }

        return false;
    }

    private void AplicarCorTexto(Color cor)
    {
        if (textoValorAtual != null)
            textoValorAtual.color = cor;

        if (textoValorTotal != null)
            textoValorTotal.color = cor;
    }

    private void RotacionarTextoParaCamera()
    {
        Transform raizTexto = ObterRaizTextoDurabilidade();
        if (raizTexto == null)
            return;

        Camera cameraAlvo = ObterCameraTextoDurabilidade();
        if (cameraAlvo == null)
            return;

        Transform cameraTransform = cameraAlvo.transform;
        raizTexto.LookAt(
            raizTexto.position + cameraTransform.rotation * Vector3.forward,
            cameraTransform.rotation * Vector3.up);
    }

    private Transform ObterRaizTextoDurabilidade()
    {
        if (raizTextoDurabilidade != null)
            return raizTextoDurabilidade;

        EncontrarTextosDurabilidadeSeNecessario(true);

        if (textoValorAtual != null && textoValorAtual.transform.parent != null)
            raizTextoDurabilidade = textoValorAtual.transform.parent;
        else if (textoValorTotal != null && textoValorTotal.transform.parent != null)
            raizTextoDurabilidade = textoValorTotal.transform.parent;

        return raizTextoDurabilidade;
    }

    private Camera ObterCameraTextoDurabilidade()
    {
        if (donoAtualPlayer == null)
            AtualizarDonoPelaSelecaoAtual();

        if (donoAtualPlayer != null)
        {
            Camera cameraDoDono = donoAtualPlayer.GetComponentInChildren<Camera>(true);
            if (cameraDoDono != null)
                return cameraDoDono;
        }

        if (Camera.main != null)
            return Camera.main;

        return FindFirstObjectByType<Camera>();
    }

    private string NormalizarNome(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        return texto.Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty);
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
        AtualizarTextoDurabilidade(true);

        if (vidaAtual <= 0)
            QuebrarEscudo();
    }

    private void QuebrarEscudo()
    {
        if (quebrado)
            return;

        quebrado = true;
        vidaAtual = 0;
        AtualizarTextoDurabilidade(true);
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
