using System;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class Equipamento : MonoBehaviour, IDano
{
    public enum TipoEquipamento
    {
        Arma,
        Escudo
    }

    [Header("Tipo")]
    [SerializeField] private TipoEquipamento tipo = TipoEquipamento.Arma;

    [Header("Dano (Arma)")]
    [SerializeField] private int dano = 3;
    [SerializeField] private int desgastePorDanoCausado = 1;
    [SerializeField] private string[] tagsAlvoDano;
    [SerializeField] private float cooldownDanoMesmoAlvo = 0.35f;

    [Header("Bloqueio (Escudo)")]
    [SerializeField] private int desgastePorBloqueio = 1;
    [SerializeField] private string[] tagsQueDesgastamEscudo;
    [SerializeField] private float cooldownBloqueioMesmoObjeto = 0.25f;

    [Header("Vida / Durabilidade")]
    [SerializeField] private int vidaMaxima = 100;
    [SerializeField] private int vidaAtual = 100;
    [SerializeField] private bool destruirQuandoVidaZerar = true;

    [Header("Texto Durabilidade")]
    public TMP_Text textoValorAtual;
    public TMP_Text textoValorTotal;

    [Header("Efeito LED Texto")]
    public Color corTextoA = Color.white;
    public Color corTextoB = Color.cyan;
    public float velocidadePiscarTexto = 2f;
    public bool usarEfeitoLedTexto = true;

    [Header("Dono / XR")]
    [SerializeField] private float tempoIgnorarDonoAposSoltar = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somHit;
    [SerializeField] private AudioClip somBloqueio;
    [SerializeField] private AudioClip somQuebra;
    [SerializeField] private float volumeHit = 1f;
    [SerializeField] private float volumeBloqueio = 1f;
    [SerializeField] private float volumeQuebra = 1f;
    [SerializeField] private float cooldownSomHit = 0.1f;

    // ===== Propriedades p�blicas (usadas pelo Reparar) =====
    public int VidaAtual => vidaAtual;
    public int VidaMaxima => vidaMaxima;
    public float VidaFaltando => Mathf.Max(0, vidaMaxima - vidaAtual);
    public bool EstaQuebrado => quebrado || vidaAtual <= 0;
    public TipoEquipamento Tipo => tipo;

    // ===== Internos =====
    private Transform donoAtualPlayer;
    private Transform ultimoDonoPlayer;
    private Transform raizTextoDurabilidade;
    private XRGrabInteractable grabInteractable;
    private bool quebrado;
    private float ignorarUltimoDonoAte;
    private float proximoSomHitPermitido;
    private readonly Dictionary<int, float> proximoDanoPermitidoPorAlvo = new();
    private readonly Dictionary<int, float> proximoBloqueioPermitidoPorObjeto = new();
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    // ===================== CICLO DE VIDA =====================

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        NormalizarVida();
        EncontrarTextosDurabilidadeSeNecessario(true);
        AtualizarTextoDurabilidade(true);
        AplicarCorTexto(corTextoA);
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
        ultimoDonoPlayer = null;
        ignorarUltimoDonoAte = 0f;
        proximoDanoPermitidoPorAlvo.Clear();
        proximoBloqueioPermitidoPorObjeto.Clear();
    }

    private void OnValidate()
    {
        dano = Mathf.Max(0, dano);
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        desgastePorDanoCausado = Mathf.Max(0, desgastePorDanoCausado);
        desgastePorBloqueio = Mathf.Max(0, desgastePorBloqueio);
        cooldownDanoMesmoAlvo = Mathf.Max(0f, cooldownDanoMesmoAlvo);
        cooldownBloqueioMesmoObjeto = Mathf.Max(0f, cooldownBloqueioMesmoObjeto);
        tempoIgnorarDonoAposSoltar = Mathf.Max(0f, tempoIgnorarDonoAposSoltar);
        volumeHit = Mathf.Max(0f, volumeHit);
        volumeBloqueio = Mathf.Max(0f, volumeBloqueio);
        volumeQuebra = Mathf.Max(0f, volumeQuebra);
        cooldownSomHit = Mathf.Max(0f, cooldownSomHit);
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
        if (collision == null) return;

        if (tipo == TipoEquipamento.Arma)
            ProcessarPossivelDano(collision.collider);
        else
            ProcessarBloqueio(collision.collider != null ? collision.collider.gameObject : null, true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        if (tipo == TipoEquipamento.Arma)
            ProcessarPossivelDano(other);
        else
            ProcessarBloqueio(other.gameObject, false);
    }

    // ===================== IDano =====================

    public float ObterDano()
    {
        return tipo == TipoEquipamento.Arma ? Mathf.Max(0, dano) : 0f;
    }

    public GameObject ObterDono()
    {
        return GetDonoAtual();
    }

    public GameObject GetDonoAtual()
    {
        if (donoAtualPlayer == null)
            AtualizarDonoPelaSelecaoAtual();

        return donoAtualPlayer != null ? donoAtualPlayer.gameObject : null;
    }

    // ===================== REPARO (usado pelo script Reparar) =====================

    public void RepararCompleto()
    {
        quebrado = false;
        vidaAtual = vidaMaxima;
        AtualizarTextoDurabilidade(true);
    }

    public void DefinirVida(int atual, int maxima)
    {
        vidaMaxima = Mathf.Max(1, maxima);
        vidaAtual = Mathf.Clamp(atual, 0, vidaMaxima);
        quebrado = vidaAtual <= 0;
        AtualizarTextoDurabilidade(true);
    }

    // ===================== DONO XR =====================

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        Transform interactorTransform = ObterTransformInteractor(args.interactorObject);
        Transform novoDono = EncontrarPlayerDonoAPartirDoTransform(interactorTransform);
        donoAtualPlayer = novoDono;
        ultimoDonoPlayer = novoDono;
        ignorarUltimoDonoAte = 0f;
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        Transform donoAntesDeSoltar = donoAtualPlayer;
        AtualizarDonoPelaSelecaoAtual();

        if (donoAtualPlayer != null)
        {
            ultimoDonoPlayer = donoAtualPlayer;
            ignorarUltimoDonoAte = 0f;
            return;
        }

        if (donoAntesDeSoltar != null && tempoIgnorarDonoAposSoltar > 0f)
        {
            ultimoDonoPlayer = donoAntesDeSoltar;
            ignorarUltimoDonoAte = Time.time + tempoIgnorarDonoAposSoltar;
        }
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
            if (player == null) continue;

            donoAtualPlayer = player;
            return;
        }
    }

    private Transform ObterTransformInteractor(IXRSelectInteractor interactor)
    {
        return (interactor as MonoBehaviour)?.transform;
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

    // ===================== L�GICA DE ARMA (dano) =====================

    private void ProcessarPossivelDano(Collider outroCollider)
    {
        if (outroCollider == null || quebrado || vidaAtual <= 0) return;

        if (EstadoItemInventario.EstaNoInventario(this) || EstadoItemInventario.EstaNoInventario(outroCollider))
            return;

        GameObject objetoTocado = outroCollider.gameObject;
        if (EhParteDoProprioEquipamento(objetoTocado)) return;

        if (!TagEhAlvoValido(objetoTocado, out GameObject alvoResolvido)) return;
        if (alvoResolvido == null || EhParteDoProprioEquipamento(alvoResolvido)) return;

        if (PertenceAoDonoAtual(alvoResolvido) || PertenceAoDonoRecente(alvoResolvido) ||
            EstaAcopladoAoMesmoDono(alvoResolvido) || EstaAcopladoAoDonoRecente(alvoResolvido))
            return;

        if (!PodeAplicarDanoAgora(alvoResolvido)) return;

        bool danoAplicado = TentarAplicarDano(alvoResolvido, dano);
        if (!danoAplicado) return;

        TocarSomHit();
        ReduzirVida(desgastePorDanoCausado);
    }

    private bool TagEhAlvoValido(GameObject obj, out GameObject alvoResolvido)
    {
        alvoResolvido = null;
        if (obj == null || tagsAlvoDano == null || tagsAlvoDano.Length == 0) return false;

        Transform alvoComTag = EncontrarTransformComTag(obj.transform, tagsAlvoDano);
        if (alvoComTag != null)
        {
            alvoResolvido = alvoComTag.gameObject;
            return true;
        }

        Rigidbody rb = obj.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            alvoComTag = EncontrarTransformComTag(rb.transform, tagsAlvoDano);
            if (alvoComTag != null)
            {
                alvoResolvido = alvoComTag.gameObject;
                return true;
            }
        }

        if (obj.transform.root != null)
        {
            alvoComTag = EncontrarTransformComTag(obj.transform.root, tagsAlvoDano);
            if (alvoComTag != null)
            {
                alvoResolvido = alvoComTag.gameObject;
                return true;
            }
        }

        return false;
    }

    private bool PodeAplicarDanoAgora(GameObject alvo)
    {
        if (cooldownDanoMesmoAlvo <= 0f) return true;

        int id = ObterIdStable(alvo);
        if (proximoDanoPermitidoPorAlvo.TryGetValue(id, out float t) && Time.time < t)
            return false;

        proximoDanoPermitidoPorAlvo[id] = Time.time + cooldownDanoMesmoAlvo;
        return true;
    }

    private bool TentarAplicarDano(GameObject alvo, int valorDano)
    {
        if (alvo == null || valorDano <= 0) return false;

        Component[] componentes = ColetarComponentes(alvo);
        for (int i = 0; i < componentes.Length; i++)
        {
            Component c = componentes[i];
            if (c == null || c == this) continue;

            // ReceberDano(int, GameObject)
            MethodInfo m2 = c.GetType().GetMethod("ReceberDano", BindingFlags.Instance | BindingFlags.Public, null,
                new[] { typeof(int), typeof(GameObject) }, null);
            if (m2 != null)
            {
                try { m2.Invoke(c, new object[] { valorDano, gameObject }); return true; }
                catch { return false; }
            }

            // ReceberDano(int)
            MethodInfo m1 = c.GetType().GetMethod("ReceberDano", BindingFlags.Instance | BindingFlags.Public, null,
                new[] { typeof(int) }, null);
            if (m1 != null)
            {
                try { m1.Invoke(c, new object[] { valorDano }); return true; }
                catch { return false; }
            }

            // ReceberDano(float, GameObject) - StatusPlayer
            MethodInfo m3 = c.GetType().GetMethod("ReceberDano", BindingFlags.Instance | BindingFlags.Public, null,
                new[] { typeof(float), typeof(GameObject) }, null);
            if (m3 != null)
            {
                try { m3.Invoke(c, new object[] { (float)valorDano, gameObject }); return true; }
                catch { return false; }
            }
        }

        return false;
    }

    // ===================== L�GICA DE ESCUDO (bloqueio) =====================

    public bool EstaProtegendoPlayer(Transform player)
    {
        if (tipo != TipoEquipamento.Escudo || EstaQuebrado || player == null || donoAtualPlayer == null)
            return false;

        return player == donoAtualPlayer || player.IsChildOf(donoAtualPlayer);
    }

    public bool BloqueiaDanoDe(GameObject origemDano, Transform playerAlvo)
    {
        if (tipo != TipoEquipamento.Escudo || !EstaProtegendoPlayer(playerAlvo) || origemDano == null)
            return false;

        if (EstadoItemInventario.EstaNoInventario(this) || EstadoItemInventario.EstaNoInventario(origemDano))
            return false;

        if (EhParteDoProprioEquipamento(origemDano)) return false;
        if (!TagPodeDesgastarEscudo(origemDano, out GameObject origemResolvida)) return false;
        if (PertenceAoDonoAtual(origemResolvida) || PertenceAoDonoRecente(origemResolvida) ||
            EstaAcopladoAoMesmoDono(origemResolvida) || EstaAcopladoAoDonoRecente(origemResolvida))
            return false;

        return true;
    }

    public bool RegistrarBloqueio(GameObject origemDano, bool tocarSom = true)
    {
        if (tipo != TipoEquipamento.Escudo || EstaQuebrado || origemDano == null) return false;

        if (EstadoItemInventario.EstaNoInventario(this) || EstadoItemInventario.EstaNoInventario(origemDano))
            return false;

        if (EhParteDoProprioEquipamento(origemDano)) return false;
        if (!TagPodeDesgastarEscudo(origemDano, out GameObject origemResolvida)) return false;
        if (origemResolvida == null || EhParteDoProprioEquipamento(origemResolvida)) return false;
        if (PertenceAoDonoAtual(origemResolvida) || PertenceAoDonoRecente(origemResolvida) ||
            EstaAcopladoAoMesmoDono(origemResolvida) || EstaAcopladoAoDonoRecente(origemResolvida))
            return false;
        if (!PodeBloquearAgora(origemResolvida)) return false;

        if (tocarSom) TocarSomBloqueio();
        ReduzirVida(desgastePorBloqueio);
        return true;
    }

    private void ProcessarBloqueio(GameObject objetoColidido, bool tocarSom)
    {
        RegistrarBloqueio(objetoColidido, tocarSom);
    }

    private bool TagPodeDesgastarEscudo(GameObject obj, out GameObject origemResolvida)
    {
        origemResolvida = null;
        if (obj == null || tagsQueDesgastamEscudo == null || tagsQueDesgastamEscudo.Length == 0)
            return false;

        Transform t = EncontrarTransformComTag(obj.transform, tagsQueDesgastamEscudo);
        if (t != null) { origemResolvida = t.gameObject; return true; }

        Rigidbody rb = obj.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            t = EncontrarTransformComTag(rb.transform, tagsQueDesgastamEscudo);
            if (t != null) { origemResolvida = t.gameObject; return true; }
        }

        if (obj.transform.root != null)
        {
            t = EncontrarTransformComTag(obj.transform.root, tagsQueDesgastamEscudo);
            if (t != null) { origemResolvida = t.gameObject; return true; }
        }

        return false;
    }

    private bool PodeBloquearAgora(GameObject origem)
    {
        if (cooldownBloqueioMesmoObjeto <= 0f) return true;

        int id = ObterIdStable(origem);
        if (proximoBloqueioPermitidoPorObjeto.TryGetValue(id, out float t) && Time.time < t)
            return false;

        proximoBloqueioPermitidoPorObjeto[id] = Time.time + cooldownBloqueioMesmoObjeto;
        return true;
    }

    // ===================== VIDA =====================

    private void NormalizarVida()
    {
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        if (vidaAtual <= 0) vidaAtual = vidaMaxima;
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        AtualizarTextoDurabilidade(false);
    }

    private void ReduzirVida(int quantidade)
    {
        if (quantidade <= 0 || quebrado) return;

        vidaAtual = Mathf.Max(0, vidaAtual - quantidade);
        AtualizarTextoDurabilidade(true);

        if (vidaAtual <= 0)
            Quebrar();
    }

    private void Quebrar()
    {
        if (quebrado) return;

        quebrado = true;
        vidaAtual = 0;
        AtualizarTextoDurabilidade(true);
        TocarSomQuebra();

        if (destruirQuandoVidaZerar)
            Destroy(gameObject);
    }

    // ===================== TEXTO / LED =====================

    public void AtualizarDurabilidadeVisual()
    {
        AtualizarTextoDurabilidade(true);
    }

    private void EncontrarTextosDurabilidadeSeNecessario(bool criarSeNecessario = false)
    {
        if (textoValorAtual != null && textoValorTotal != null) return;

        TMP_Text[] textos = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textos.Length; i++)
        {
            TMP_Text texto = textos[i];
            if (texto == null) continue;

            string nome = NormalizarNome(texto.gameObject.name);
            if (textoValorAtual == null && (nome == "valoratual" || nome == "atual"))
                textoValorAtual = texto;
            if (textoValorTotal == null && (nome == "valortotal" || nome == "total"))
                textoValorTotal = texto;
        }

        if (criarSeNecessario && Application.isPlaying && (textoValorAtual == null || textoValorTotal == null))
            CriarTextosDurabilidadeSeNecessario();
    }

    private void AtualizarTextoDurabilidade(bool criarSeNecessario = false)
    {
        EncontrarTextosDurabilidadeSeNecessario(criarSeNecessario);

        if (textoValorAtual != null)
            textoValorAtual.text = Formatar(vidaAtual);
        if (textoValorTotal != null)
            textoValorTotal.text = Formatar(vidaMaxima);
    }

    private string Formatar(int valor)
    {
        return Mathf.Max(0, valor).ToString("N0", Cultura);
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

    private void AplicarCorTexto(Color cor)
    {
        if (textoValorAtual != null) textoValorAtual.color = cor;
        if (textoValorTotal != null) textoValorTotal.color = cor;
    }

    private void CriarTextosDurabilidadeSeNecessario()
    {
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) canvas = CriarCanvasDurabilidade();

        Transform raiz = canvas.transform;
        if (textoValorAtual == null)
            textoValorAtual = CriarTexto(raiz, "Valor Atual", new Vector2(-45f, 0f), TextAlignmentOptions.Right);
        if (!ExisteSeparador(raiz))
            CriarTexto(raiz, "Separador", Vector2.zero, TextAlignmentOptions.Center).text = "/";
        if (textoValorTotal == null)
            textoValorTotal = CriarTexto(raiz, "Valor Total", new Vector2(45f, 0f), TextAlignmentOptions.Left);

        raizTextoDurabilidade = raiz;
    }

    private Canvas CriarCanvasDurabilidade()
    {
        GameObject go = new GameObject("Canvas Durabilidade", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.localPosition = new Vector3(0f, 0.55f, -0.35f);
        rect.localScale = Vector3.one * 0.01f;
        rect.sizeDelta = new Vector2(160f, 40f);

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = ObterCamera();
        canvas.sortingOrder = 10;

        return canvas;
    }

    private TMP_Text CriarTexto(Transform parent, string nome, Vector2 pos, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(80f, 30f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = "0";
        tmp.fontSize = 24f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 10f;
        tmp.fontSizeMax = 26f;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.color = corTextoA;
        return tmp;
    }

    private bool ExisteSeparador(Transform raiz)
    {
        foreach (var t in raiz.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null) continue;
            string n = NormalizarNome(t.gameObject.name);
            if (n == "separador" || t.text.Trim() == "/") return true;
        }
        return false;
    }

    private void RotacionarTextoParaCamera()
    {
        Transform raiz = ObterRaizTexto();
        if (raiz == null) return;

        Camera cam = ObterCamera();
        if (cam == null) return;

        Transform ct = cam.transform;
        raiz.LookAt(raiz.position + ct.rotation * Vector3.forward, ct.rotation * Vector3.up);
    }

    private Transform ObterRaizTexto()
    {
        if (raizTextoDurabilidade != null) return raizTextoDurabilidade;

        EncontrarTextosDurabilidadeSeNecessario(true);
        if (textoValorAtual != null && textoValorAtual.transform.parent != null)
            raizTextoDurabilidade = textoValorAtual.transform.parent;
        else if (textoValorTotal != null && textoValorTotal.transform.parent != null)
            raizTextoDurabilidade = textoValorTotal.transform.parent;

        return raizTextoDurabilidade;
    }

    private Camera ObterCamera()
    {
        if (donoAtualPlayer == null) AtualizarDonoPelaSelecaoAtual();
        if (donoAtualPlayer != null)
        {
            Camera c = donoAtualPlayer.GetComponentInChildren<Camera>(true);
            if (c != null) return c;
        }
        if (Camera.main != null) return Camera.main;
        return FindFirstObjectByType<Camera>();
    }

    // ===================== �UDIO =====================

    private void TocarSomHit()
    {
        if (Time.time < proximoSomHitPermitido) return;
        proximoSomHitPermitido = Time.time + cooldownSomHit;
        if (audioSource != null && somHit != null)
            audioSource.PlayOneShot(somHit, volumeHit);
    }

    private void TocarSomBloqueio()
    {
        if (audioSource != null && somBloqueio != null)
            audioSource.PlayOneShot(somBloqueio, volumeBloqueio);
    }

    private void TocarSomQuebra()
    {
        if (somQuebra != null)
            AudioSource.PlayClipAtPoint(somQuebra, transform.position, volumeQuebra);
    }

    // ===================== UTILIT�RIOS =====================

    private bool EhParteDoProprioEquipamento(GameObject obj)
    {
        if (obj == null) return false;
        Transform t = obj.transform;
        return t == transform || t.IsChildOf(transform);
    }

    private bool PertenceAoDonoAtual(GameObject obj)
    {
        if (donoAtualPlayer == null || obj == null) return false;
        Transform alvo = obj.transform;
        if (alvo == donoAtualPlayer || alvo.IsChildOf(donoAtualPlayer)) return true;
        return EncontrarPlayerDonoAPartirDoTransform(alvo) == donoAtualPlayer;
    }

    private bool PertenceAoDonoRecente(GameObject obj)
    {
        if (!DeveIgnorarUltimoDono() || obj == null) return false;
        Transform alvo = obj.transform;
        if (alvo == ultimoDonoPlayer || alvo.IsChildOf(ultimoDonoPlayer)) return true;
        return EncontrarPlayerDonoAPartirDoTransform(alvo) == ultimoDonoPlayer;
    }

    private bool EstaAcopladoAoMesmoDono(GameObject obj)
    {
        if (donoAtualPlayer == null || obj == null) return false;
        if (InteractableSelecionadoPeloDono(obj.GetComponentInParent<XRGrabInteractable>(), donoAtualPlayer))
            return true;

        var filhos = obj.GetComponentsInChildren<XRGrabInteractable>(true);
        for (int i = 0; i < filhos.Length; i++)
            if (InteractableSelecionadoPeloDono(filhos[i], donoAtualPlayer)) return true;

        return false;
    }

    private bool EstaAcopladoAoDonoRecente(GameObject obj)
    {
        if (!DeveIgnorarUltimoDono() || obj == null) return false;
        if (InteractableSelecionadoPeloDono(obj.GetComponentInParent<XRGrabInteractable>(), ultimoDonoPlayer))
            return true;

        var filhos = obj.GetComponentsInChildren<XRGrabInteractable>(true);
        for (int i = 0; i < filhos.Length; i++)
            if (InteractableSelecionadoPeloDono(filhos[i], ultimoDonoPlayer)) return true;

        return false;
    }

    private bool DeveIgnorarUltimoDono()
    {
        return ultimoDonoPlayer != null && Time.time <= ignorarUltimoDonoAte;
    }

    private bool InteractableSelecionadoPeloDono(XRGrabInteractable interactable, Transform dono)
    {
        if (interactable == null || interactable == grabInteractable || dono == null) return false;

        for (int i = 0; i < interactable.interactorsSelecting.Count; i++)
        {
            Transform it = ObterTransformInteractor(interactable.interactorsSelecting[i]);
            if (EncontrarPlayerDonoAPartirDoTransform(it) == dono) return true;
        }
        return false;
    }

    private Transform EncontrarTransformComTag(Transform origem, string[] tags)
    {
        Transform atual = origem;
        while (atual != null)
        {
            if (TagConfigurada(atual.gameObject, tags)) return atual;
            atual = atual.parent;
        }
        return null;
    }

    private bool TagConfigurada(GameObject obj, string[] tags)
    {
        if (obj == null || tags == null) return false;
        string tagObj = obj.tag;
        for (int i = 0; i < tags.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(tags[i])) continue;
            if (string.Equals(tagObj, tags[i].Trim(), StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private int ObterIdStable(GameObject obj)
    {
        if (obj == null) return 0;
        Rigidbody rb = obj.GetComponentInParent<Rigidbody>();
        if (rb != null) return rb.GetInstanceID();
        return obj.transform.root != null ? obj.transform.root.gameObject.GetInstanceID() : obj.GetInstanceID();
    }

    private Component[] ColetarComponentes(GameObject alvo)
    {
        List<Component> lista = new();
        HashSet<Component> visitados = new();

        void Add(Component[] arr)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null && visitados.Add(arr[i]))
                    lista.Add(arr[i]);
            }
        }

        Add(alvo.GetComponents<Component>());
        Add(alvo.GetComponentsInParent<Component>(true));
        Add(alvo.GetComponentsInChildren<Component>(true));

        Rigidbody rb = alvo.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            Add(rb.GetComponents<Component>());
            Add(rb.GetComponentsInParent<Component>(true));
            Add(rb.GetComponentsInChildren<Component>(true));
        }

        return lista.ToArray();
    }

    private string NormalizarNome(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        return texto.Trim().ToLowerInvariant()
            .Replace(" ", "").Replace("_", "").Replace("-", "");
    }
}