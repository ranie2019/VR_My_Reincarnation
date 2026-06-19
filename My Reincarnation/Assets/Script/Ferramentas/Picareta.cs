using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class Picareta : MonoBehaviour
{
    [Header("Dano da picareta")]
    [SerializeField] private int danoPicareta = 1;

    [Header("Impacto opcional")]
    [SerializeField] private float impactForce = 0f;

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

    [Header("Tags aceitas")]
    [SerializeField] private string[] tagsAceitas = { "Rock", "Pedra", "Metal" };

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somPedra;
    [SerializeField] private AudioClip somMetal;
    [SerializeField] private AudioClip somOutros;
    [SerializeField] private AudioClip somQuebra;
    [SerializeField] private float volumePedra = 1f;
    [SerializeField] private float volumeMetal = 1f;
    [SerializeField] private float volumeOutros = 1f;
    [SerializeField] private float volumeQuebra = 1f;
    [SerializeField] private float cooldownSomColisao = 0.1f;

    private Rigidbody rb;
    private Transform donoAtualPlayer;
    private Transform raizTextoDurabilidade;
    private XRGrabInteractable grabInteractable;
    private bool quebrada;
    private float proximoSomColisaoPermitido;
    private Vector3 ultimaPosicaoAudioSegurado;
    private Quaternion ultimaRotacaoAudioSegurado;
    private float velocidadeLinearAudioSegurado;
    private float velocidadeAngularAudioSegurado;
    private float proximoAudioSeguradoPermitido;
    private bool temAmostraAudioSegurado;
    private readonly HashSet<VidaRecursoMineral> recursosDentroDoTrigger = new();
    private readonly Dictionary<VidaRecursoMineral, int> contatosPorRecurso = new();
    private readonly HashSet<Collider> contatosAudioSeguradoAtivos = new();
    private static readonly CultureInfo CulturaDurabilidade = CultureInfo.GetCultureInfo("pt-BR");
    private const float VelocidadeMinimaAudioSegurado = 0.25f;
    private const float VelocidadeAngularMinimaAudioSegurado = 45f;
    private const float CooldownMinimoAudioSegurado = 0.2f;

    public int VidaAtual => vidaAtual;
    public int VidaMaxima => vidaMaxima;
    public bool Quebrada => quebrada || vidaAtual <= 0;

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
        recursosDentroDoTrigger.Clear();
        contatosPorRecurso.Clear();
        contatosAudioSeguradoAtivos.Clear();
    }

    private void OnValidate()
    {
        danoPicareta = Mathf.Max(0, danoPicareta);
        impactForce = Mathf.Max(0f, impactForce);
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        desgastePorUso = Mathf.Max(0, desgastePorUso);
        volumePedra = Mathf.Max(0f, volumePedra);
        volumeMetal = Mathf.Max(0f, volumeMetal);
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
        AtualizarMovimentoAudioSegurado();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Quebrada || collision == null)
            return;

        Collider other = collision.collider;
        bool podeTocarSom = AudioColisaoFiltro.PodeTocarSomDeColisao(collision);
        if (podeTocarSom)
            TentarTocarAudioColisao(other, "OnCollisionEnter", EstaSendoSegurada());
        else
            LogAudioFerramenta("OnCollisionEnter", other, "bloqueado pelo filtro", false, false);

        TentarAplicarDano(other, false);

        if (impactForce > 0f && rb != null)
            AplicarForcaImpacto(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (Quebrada || collision == null || !EstaSendoSegurada())
            return;

        Collider other = collision.collider;
        if (other == null)
        {
            LogAudioFerramenta("OnCollisionStay", null, "collider nulo", false, false);
            return;
        }

        bool jaEstavaEmContato = contatosAudioSeguradoAtivos.Contains(other);
        if (jaEstavaEmContato)
        {
            LogAudioFerramenta("OnCollisionStay", other, "contato ja registrado", true, false);
            return;
        }

        if (!AudioColisaoFiltro.PodeTocarSomDeColisao(collision))
        {
            LogAudioFerramenta("OnCollisionStay", other, "bloqueado pelo filtro", false, false);
            return;
        }

        if (!DeveTocarAudioSegurado())
        {
            LogAudioFerramenta("OnCollisionStay", other, "sem movimento minimo ou em cooldown", false, false);
            return;
        }

        TentarTocarAudioColisao(other, "OnCollisionStay", true);
        proximoAudioSeguradoPermitido = Time.time + CooldownMinimoAudioSegurado;
    }

    private void OnCollisionExit(Collision collision)
    {
        Collider other = collision != null ? collision.collider : null;
        bool estavaEmContato = other != null && contatosAudioSeguradoAtivos.Contains(other);
        RemoverContatoAudioSegurado(other);
        LogAudioFerramenta("OnCollisionExit", other, estavaEmContato ? "contato liberado" : "contato nao estava registrado", estavaEmContato, false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Quebrada)
            return;

        TentarAplicarDanoPorEntradaTrigger(other);
    }

    private void OnTriggerExit(Collider other)
    {
        VidaRecursoMineral recurso = BuscarRecursoMineral(other);

        if (recurso == null)
            return;

        if (!contatosPorRecurso.TryGetValue(recurso, out int contatos))
            return;

        contatos--;

        if (contatos > 0)
        {
            contatosPorRecurso[recurso] = contatos;
            return;
        }

        contatosPorRecurso.Remove(recurso);
        recursosDentroDoTrigger.Remove(recurso);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        Transform interactorTransform = ObterTransformInteractor(args.interactorObject);
        donoAtualPlayer = EncontrarPlayerDonoAPartirDoTransform(interactorTransform);
        ResetarAmostraAudioSegurado();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        AtualizarDonoPelaSelecaoAtual();
        ResetarAmostraAudioSegurado();
    }

    private void TentarAplicarDanoPorEntradaTrigger(Collider other)
    {
        if (other == null || PertenceAoDonoAtual(other.gameObject))
            return;

        VidaRecursoMineral recurso = BuscarRecursoMineral(other);

        if (recurso == null)
            return;

        if (contatosPorRecurso.TryGetValue(recurso, out int contatos))
            contatosPorRecurso[recurso] = contatos + 1;
        else
            contatosPorRecurso.Add(recurso, 1);

        if (!recursosDentroDoTrigger.Add(recurso))
            return;

        AplicarDanoEmRecurso(recurso, false);
    }

    private void TocarSomContatoSeguradoUmaVez(Collider other)
    {
        TentarTocarAudioColisao(other, "OnCollisionStay", true);
    }

    private void TentarTocarAudioColisao(Collider other, string origem, bool controlarContatoAtivo)
    {
        if (other == null)
        {
            LogAudioFerramenta(origem, null, "collider nulo", false, false);
            return;
        }

        bool jaEstavaEmContato = contatosAudioSeguradoAtivos.Contains(other);
        if (controlarContatoAtivo && jaEstavaEmContato)
        {
            LogAudioFerramenta(origem, other, "contato ja registrado", true, false);
            return;
        }

        if (PertenceAoDonoAtual(other.gameObject))
        {
            LogAudioFerramenta(origem, other, "objeto do player ignorado", jaEstavaEmContato, false);
            return;
        }

        EscolherAudioColisao(other.gameObject, out AudioClip clip, out float volume, out string audioEscolhido);
        bool tocou = TocarSomColisao(clip, volume);

        if (controlarContatoAtivo)
            RegistrarContatoAudioSegurado(other);

        LogAudioFerramenta(origem, other, audioEscolhido, jaEstavaEmContato, tocou);
    }

    private void EscolherAudioColisao(GameObject objetoColidido, out AudioClip clip, out float volume, out string audioEscolhido)
    {
        string tagObjeto = objetoColidido != null ? objetoColidido.tag : string.Empty;
        if (string.Equals(tagObjeto, "Rock", StringComparison.Ordinal))
        {
            clip = somPedra;
            volume = volumePedra;
            audioEscolhido = "Rock";
            return;
        }

        clip = somOutros;
        volume = volumeOutros;
        audioEscolhido = "Outros";
    }

    private void RegistrarContatoAudioSegurado(Collider other)
    {
        if (other != null)
            contatosAudioSeguradoAtivos.Add(other);
    }

    private void RemoverContatoAudioSegurado(Collider other)
    {
        if (other != null)
            contatosAudioSeguradoAtivos.Remove(other);
    }

    private void TentarAplicarDano(Collider other, bool podeTocarSom = true)
    {
        if (other == null || PertenceAoDonoAtual(other.gameObject))
            return;

        VidaRecursoMineral recurso = BuscarRecursoMineral(other);

        if (recurso == null)
            return;

        AplicarDanoEmRecurso(recurso, podeTocarSom);
    }

    private bool AplicarDanoEmRecurso(VidaRecursoMineral recurso, bool podeTocarSom = true)
    {
        if (recurso == null || Quebrada)
            return false;

        if (!TentarAplicarDanoFlexivel(recurso, podeTocarSom))
            return false;

        ReduzirVidaDaPicareta();
        return true;
    }

    private VidaRecursoMineral BuscarRecursoMineral(Collider alvo)
    {
        Transform mineral = BuscarMineral(alvo);
        if (mineral == null)
            return null;

        VidaRecursoMineral recurso = mineral.GetComponentInParent<VidaRecursoMineral>();
        if (recurso != null)
            return recurso;

        recurso = mineral.GetComponentInChildren<VidaRecursoMineral>(true);
        if (recurso != null)
            return recurso;

        Transform root = mineral.root;
        return root != null ? root.GetComponentInChildren<VidaRecursoMineral>(true) : null;
    }

    private Transform BuscarMineral(Collider alvo)
    {
        if (alvo == null)
            return null;

        Transform mineral = ObterTransformComTagMineral(alvo.transform);
        if (mineral != null)
            return mineral;

        Rigidbody alvoRb = alvo.attachedRigidbody;
        if (alvoRb != null)
        {
            mineral = ObterTransformComTagMineral(alvoRb.transform);
            if (mineral != null)
                return mineral;
        }

        Transform root = alvo.transform.root;
        if (root != null && TemTagMineral(root))
            return ObterTransformComTagMineral(root);

        return null;
    }

    private bool TemTagMineral(Transform alvo)
    {
        return ObterTransformComTagMineral(alvo) != null;
    }

    private Transform ObterTransformComTagMineral(Transform alvo)
    {
        Transform atual = alvo;

        while (atual != null)
        {
            if (TemTagAceita(atual.tag))
                return atual;

            atual = atual.parent;
        }

        return null;
    }

    private bool TentarAplicarDanoFlexivel(VidaRecursoMineral recurso, bool podeTocarSom)
    {
        if (recurso == null)
            return false;

        return TentarChamarMetodoDano(recurso, podeTocarSom);
    }

    private bool TentarChamarMetodoDano(VidaRecursoMineral recurso, bool podeTocarSom)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        string[] nomesMetodos =
        {
            "ReceberDanoDePicareta",
            "ReceberDano",
            "TomarDano",
            "AplicarDano",
            "ReceberDanoDeFerramenta"
        };

        for (int i = 0; i < nomesMetodos.Length; i++)
        {
            bool metodoEncontrado = TentarChamarMetodoDano(
                recurso,
                nomesMetodos[i],
                flags,
                new[] { typeof(int), typeof(GameObject), typeof(bool) },
                new object[] { danoPicareta, gameObject, podeTocarSom },
                out bool danoAplicado);
            if (metodoEncontrado)
                return danoAplicado;

            metodoEncontrado = TentarChamarMetodoDano(
                recurso,
                nomesMetodos[i],
                flags,
                new[] { typeof(int), typeof(GameObject) },
                new object[] { danoPicareta, gameObject },
                out danoAplicado);
            if (metodoEncontrado)
                return danoAplicado;

            metodoEncontrado = TentarChamarMetodoDano(
                recurso,
                nomesMetodos[i],
                flags,
                new[] { typeof(int) },
                new object[] { danoPicareta },
                out danoAplicado);
            if (metodoEncontrado)
                return danoAplicado;

            metodoEncontrado = TentarChamarMetodoDano(
                recurso,
                nomesMetodos[i],
                flags,
                new[] { typeof(GameObject) },
                new object[] { gameObject },
                out danoAplicado);
            if (metodoEncontrado)
                return danoAplicado;

            metodoEncontrado = TentarChamarMetodoDano(
                recurso,
                nomesMetodos[i],
                flags,
                Type.EmptyTypes,
                null,
                out danoAplicado);
            if (metodoEncontrado)
                return danoAplicado;
        }

        return false;
    }

    private bool TentarChamarMetodoDano(
        VidaRecursoMineral recurso,
        string nomeMetodo,
        BindingFlags flags,
        Type[] tiposParametros,
        object[] argumentos,
        out bool danoAplicado)
    {
        danoAplicado = false;
        MethodInfo metodo = recurso.GetType().GetMethod(nomeMetodo, flags, null, tiposParametros, null);
        if (metodo == null)
            return false;

        object retorno = metodo.Invoke(recurso, argumentos);
        danoAplicado = RetornoIndicaDanoAplicado(retorno);
        return true;
    }

    private static bool RetornoIndicaDanoAplicado(object retorno)
    {
        if (retorno is bool resultado)
            return resultado;

        return true;
    }

    private static bool TagIgual(string tagAtual, string tagEsperada)
    {
        return !string.IsNullOrWhiteSpace(tagAtual) &&
               !string.IsNullOrWhiteSpace(tagEsperada) &&
               string.Equals(tagAtual, tagEsperada, StringComparison.OrdinalIgnoreCase);
    }

    private void AplicarForcaImpacto(Collision collision)
    {
        if (collision.contactCount == 0)
            return;

        Vector3 direcaoImpacto = collision.contacts[0].normal;
        rb.AddForce(-direcaoImpacto * impactForce, ForceMode.Impulse);
    }

    private bool TemTagAceita(string tagAtual)
    {
        if (tagsAceitas == null || tagsAceitas.Length == 0)
            return false;

        for (int i = 0; i < tagsAceitas.Length; i++)
        {
            if (TagIgual(tagAtual, tagsAceitas[i]))
                return true;
        }

        return false;
    }

    private void NormalizarVida()
    {
        vidaMaxima = Mathf.Max(1, vidaMaxima);

        if (vidaAtual <= 0)
            vidaAtual = vidaMaxima;

        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        quebrada = vidaAtual <= 0;
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

    private void AtualizarMovimentoAudioSegurado()
    {
        if (!EstaSendoSegurada())
        {
            ResetarAmostraAudioSegurado();
            return;
        }

        if (!temAmostraAudioSegurado || Time.deltaTime <= 0f)
        {
            ultimaPosicaoAudioSegurado = transform.position;
            ultimaRotacaoAudioSegurado = transform.rotation;
            velocidadeLinearAudioSegurado = 0f;
            velocidadeAngularAudioSegurado = 0f;
            temAmostraAudioSegurado = true;
            return;
        }

        velocidadeLinearAudioSegurado = (transform.position - ultimaPosicaoAudioSegurado).magnitude / Time.deltaTime;
        velocidadeAngularAudioSegurado = Quaternion.Angle(ultimaRotacaoAudioSegurado, transform.rotation) / Time.deltaTime;
        ultimaPosicaoAudioSegurado = transform.position;
        ultimaRotacaoAudioSegurado = transform.rotation;
    }

    private bool DeveTocarAudioSegurado()
    {
        return EstaSendoSegurada() &&
               Time.time >= proximoAudioSeguradoPermitido &&
               (velocidadeLinearAudioSegurado >= VelocidadeMinimaAudioSegurado ||
                velocidadeAngularAudioSegurado >= VelocidadeAngularMinimaAudioSegurado);
    }

    private bool EstaSendoSegurada()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable != null && grabInteractable.interactorsSelecting.Count > 0)
            return true;

        if (donoAtualPlayer == null)
            AtualizarDonoPelaSelecaoAtual();

        return donoAtualPlayer != null;
    }

    private void ResetarAmostraAudioSegurado()
    {
        temAmostraAudioSegurado = false;
        velocidadeLinearAudioSegurado = 0f;
        velocidadeAngularAudioSegurado = 0f;
        proximoAudioSeguradoPermitido = 0f;
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

    private string NormalizarNome(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        return texto.Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty);
    }

    private void ReduzirVidaDaPicareta()
    {
        if (desgastePorUso <= 0 || quebrada)
            return;

        vidaAtual = Mathf.Max(0, vidaAtual - desgastePorUso);
        AtualizarTextoDurabilidade(true);

        if (vidaAtual <= 0)
            QuebrarPicareta();
    }

    private void QuebrarPicareta()
    {
        if (quebrada)
            return;

        quebrada = true;
        vidaAtual = 0;
        AtualizarTextoDurabilidade(true);
        TocarSomQuebra();

        if (destruirQuandoVidaZerar)
            Destroy(gameObject);
    }

    private void TocarSomPorTag(GameObject objetoColidido)
    {
        if (objetoColidido == null || PertenceAoDonoAtual(objetoColidido))
            return;

        EscolherAudioColisao(objetoColidido, out AudioClip clip, out float volume, out _);
        TocarSomColisao(clip, volume);
    }

    private bool TocarSomColisao(AudioClip clip, float volume)
    {
        if (Time.time < proximoSomColisaoPermitido)
            return false;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null || clip == null)
            return false;

        proximoSomColisaoPermitido = Time.time + cooldownSomColisao;
        audioSource.PlayOneShot(clip, volume);
        return true;
    }

    private void TocarSomQuebra()
    {
        if (somQuebra != null)
            AudioSource.PlayClipAtPoint(somQuebra, transform.position, volumeQuebra);
    }

    private void LogAudioFerramenta(string origem, Collider other, string audioEscolhido, bool jaEstavaEmContato, bool tocou)
    {
    }
}
