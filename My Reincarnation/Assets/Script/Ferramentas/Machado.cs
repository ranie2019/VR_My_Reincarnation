using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class Machado : MonoBehaviour
{
    [Header("Impacto (opcional)")]
    [SerializeField] private float impactForce = 0f; // 0 = nao aplica forca extra

    [Header("Vida / Durabilidade")]
    [SerializeField] private int vidaMaxima = 100;
    [SerializeField] private int vidaAtual = 100;
    [SerializeField] private int desgastePorUso = 1;
    [SerializeField] private bool destruirQuandoVidaZerar = false;

    [Header("Texto Durabilidade")]
    public TMP_Text textoValorAtual;
    public TMP_Text textoValorTotal;

    [Header("Efeito LED Texto")]
    public Color corTextoA = Color.white;
    public Color corTextoB = Color.cyan;
    public float velocidadePiscarTexto = 2f;
    public bool usarEfeitoLedTexto = true;

    [Header("Tags")]
    [SerializeField] private string[] tagsQuePodemReceberDano = { "Tree", "Arvore", "Madeira" };

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [FormerlySerializedAs("somHit")]
    [SerializeField] private AudioClip somCorte;
    [SerializeField] private AudioClip somOutros;
    [SerializeField] private AudioClip somQuebra;
    [FormerlySerializedAs("volumeHit")]
    [SerializeField] private float volumeCorte = 1f;
    [SerializeField] private float volumeOutros = 1f;
    [SerializeField] private float volumeQuebra = 1f;
    [FormerlySerializedAs("cooldownSomHit")]
    [SerializeField] private float cooldownSomColisao = 0.1f;

    private Transform donoAtualPlayer;
    private Transform raizTextoDurabilidade;
    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;
    private bool quebrado;
    private float proximoSomColisaoPermitido;
    private readonly HashSet<VidaArvore> arvoresDentroDoTrigger = new();
    private readonly Dictionary<VidaArvore, int> contatosPorArvore = new();
    private static readonly CultureInfo CulturaDurabilidade = CultureInfo.GetCultureInfo("pt-BR");

    public int VidaAtual => vidaAtual;
    public int VidaMaxima => vidaMaxima;
    public bool Quebrado => quebrado || vidaAtual <= 0;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
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
        arvoresDentroDoTrigger.Clear();
        contatosPorArvore.Clear();
    }

    private void OnValidate()
    {
        impactForce = Mathf.Max(0f, impactForce);
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        desgastePorUso = Mathf.Max(0, desgastePorUso);
        volumeCorte = Mathf.Max(0f, volumeCorte);
        volumeOutros = Mathf.Max(0f, volumeOutros);
        volumeQuebra = Mathf.Max(0f, volumeQuebra);
        cooldownSomColisao = Mathf.Max(0f, cooldownSomColisao);
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
        if (Quebrado || collision == null)
            return;

        TentarAplicarDanoPorContato(collision.collider);

        if (impactForce > 0f && rb != null && TagPodeReceberDano(collision.collider))
            ApplyImpactForce(collision);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Quebrado)
            return;

        TentarAplicarDanoPorEntradaTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Sem dano continuo: um novo hit so e liberado depois de OnTriggerExit.
    }

    private void OnTriggerExit(Collider other)
    {
        VidaArvore arvore = BuscarVidaArvore(other);
        if (arvore == null)
            return;

        if (!contatosPorArvore.TryGetValue(arvore, out int contatos))
            return;

        contatos--;

        if (contatos > 0)
        {
            contatosPorArvore[arvore] = contatos;
            return;
        }

        contatosPorArvore.Remove(arvore);
        arvoresDentroDoTrigger.Remove(arvore);
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

    private void ApplyImpactForce(Collision collision)
    {
        if (collision.contactCount == 0)
            return;

        Vector3 impactDirection = collision.contacts[0].normal;
        rb.AddForce(-impactDirection * impactForce, ForceMode.Impulse);
    }

    private void TentarAplicarDanoPorEntradaTrigger(Collider other)
    {
        TentarAplicarDanoPorContato(other, true);
    }

    private void TentarAplicarDanoPorContato(Collider other, bool controlarEntradaTrigger = false)
    {
        if (other == null || DeveIgnorarObjeto(other))
            return;

        if (!TagPodeReceberDano(other))
        {
            TocarSomOutros();
            return;
        }

        VidaArvore arvore = BuscarVidaArvore(other);
        if (arvore == null)
        {
            TocarSomOutros();
            return;
        }

        if (controlarEntradaTrigger)
        {
            if (contatosPorArvore.TryGetValue(arvore, out int contatos))
                contatosPorArvore[arvore] = contatos + 1;
            else
                contatosPorArvore.Add(arvore, 1);

            if (!arvoresDentroDoTrigger.Add(arvore))
                return;
        }

        if (!arvore.ReceberDanoDeMachado(gameObject))
        {
            TocarSomOutros();
            return;
        }

        TocarSomCorte();
        ReduzirVidaDoMachado();
    }

    private void NormalizarVida()
    {
        vidaMaxima = Mathf.Max(1, vidaMaxima);

        if (vidaAtual <= 0)
            vidaAtual = vidaMaxima;

        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        quebrado = vidaAtual <= 0;
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

    private string NormalizarNome(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        return texto.Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty);
    }

    private void ReduzirVidaDoMachado()
    {
        if (desgastePorUso <= 0 || quebrado)
            return;

        vidaAtual = Mathf.Max(0, vidaAtual - desgastePorUso);
        AtualizarTextoDurabilidade(true);

        if (vidaAtual <= 0)
            QuebrarMachado();
    }

    private void QuebrarMachado()
    {
        if (quebrado)
            return;

        quebrado = true;
        vidaAtual = 0;
        AtualizarTextoDurabilidade(true);
        TocarSomQuebra();

        if (destruirQuandoVidaZerar)
            Destroy(gameObject);
    }

    private void TocarSomCorte()
    {
        TocarSomColisao(somCorte, volumeCorte);
    }

    private void TocarSomOutros()
    {
        TocarSomColisao(somOutros, volumeOutros);
    }

    private void TocarSomColisao(AudioClip clip, float volume)
    {
        if (Time.time < proximoSomColisaoPermitido)
            return;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null || clip == null)
            return;

        proximoSomColisaoPermitido = Time.time + cooldownSomColisao;
        audioSource.PlayOneShot(clip, volume);
    }

    private void TocarSomQuebra()
    {
        if (somQuebra != null)
            AudioSource.PlayClipAtPoint(somQuebra, transform.position, volumeQuebra);
    }

    private VidaArvore BuscarVidaArvore(Collider alvo)
    {
        if (alvo == null)
            return null;

        VidaArvore vida = alvo.GetComponentInParent<VidaArvore>();
        if (vida != null)
            return vida;

        Rigidbody alvoRb = alvo.attachedRigidbody;
        if (alvoRb != null)
        {
            vida = alvoRb.GetComponentInParent<VidaArvore>();
            if (vida != null)
                return vida;
        }

        Transform root = alvo.transform.root;
        return root != null ? root.GetComponentInChildren<VidaArvore>() : null;
    }

    private bool TagPodeReceberDano(Collider alvo)
    {
        if (alvo == null)
            return false;

        if (TransformTemTagPermitida(alvo.transform))
            return true;

        Rigidbody alvoRb = alvo.attachedRigidbody;
        return alvoRb != null && TransformTemTagPermitida(alvoRb.transform);
    }

    private bool TransformTemTagPermitida(Transform origem)
    {
        Transform atual = origem;

        while (atual != null)
        {
            if (TagPodeReceberDano(atual.gameObject))
                return true;

            atual = atual.parent;
        }

        return false;
    }

    private bool TagPodeReceberDano(GameObject objeto)
    {
        if (objeto == null || tagsQuePodemReceberDano == null)
            return false;

        string tagObjeto = objeto.tag;
        for (int i = 0; i < tagsQuePodemReceberDano.Length; i++)
        {
            string tagPermitida = tagsQuePodemReceberDano[i];
            if (!string.IsNullOrWhiteSpace(tagPermitida) && string.Equals(tagObjeto, tagPermitida, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private bool DeveIgnorarObjeto(Collider alvo)
    {
        if (alvo == null)
            return true;

        if (DeveIgnorarObjeto(alvo.gameObject))
            return true;

        Rigidbody alvoRb = alvo.attachedRigidbody;
        return alvoRb != null && DeveIgnorarObjeto(alvoRb.gameObject);
    }

    private bool DeveIgnorarObjeto(GameObject objeto)
    {
        if (objeto == null)
            return true;

        Transform alvo = objeto.transform;
        if (alvo == transform || alvo.IsChildOf(transform))
            return true;

        if (donoAtualPlayer == null)
            AtualizarDonoPelaSelecaoAtual();

        if (donoAtualPlayer == null)
            return false;

        if (alvo == donoAtualPlayer || alvo.IsChildOf(donoAtualPlayer))
            return true;

        Transform playerDoAlvo = EncontrarPlayerDonoAPartirDoTransform(alvo);
        return playerDoAlvo == donoAtualPlayer;
    }
}
