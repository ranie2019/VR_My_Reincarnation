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
public class Espada : MonoBehaviour
{
    [Header("Dano")]
    [SerializeField] private int danoEspada = 1;

    [Header("Vida / Durabilidade")]
    [SerializeField] private int vidaMaxima = 20;
    [SerializeField] private int vidaAtual = 20;
    [SerializeField] private int desgastePorDanoCausado = 1;
    [SerializeField] private bool destruirQuandoVidaZerar = true;

    [Header("Texto Durabilidade")]
    public TMP_Text textoValorAtual;
    public TMP_Text textoValorTotal;

    [Header("Efeito LED Texto")]
    public Color corTextoA = Color.white;
    public Color corTextoB = Color.cyan;
    public float velocidadePiscarTexto = 2f;
    public bool usarEfeitoLedTexto = true;

    [Header("Tags que recebem dano")]
    [SerializeField] private string[] tagsAlvoDano;

    [Header("Cooldown")]
    [SerializeField] private float cooldownDanoMesmoAlvo = 0.35f;

    private Transform donoAtualPlayer;
    private Transform raizTextoDurabilidade;
    private XRGrabInteractable grabInteractable;
    private bool quebrada;
    private readonly Dictionary<int, float> proximoDanoPermitidoPorAlvo = new();
    private static readonly CultureInfo CulturaDurabilidade = CultureInfo.GetCultureInfo("pt-BR");

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
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
        proximoDanoPermitidoPorAlvo.Clear();
    }

    private void OnValidate()
    {
        danoEspada = Mathf.Max(0, danoEspada);
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = Mathf.Clamp(vidaAtual, 0, vidaMaxima);
        desgastePorDanoCausado = Mathf.Max(0, desgastePorDanoCausado);
        cooldownDanoMesmoAlvo = Mathf.Max(0f, cooldownDanoMesmoAlvo);
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

    public GameObject GetDonoAtual()
    {
        if (donoAtualPlayer == null)
            AtualizarDonoPelaSelecaoAtual();

        return donoAtualPlayer != null ? donoAtualPlayer.gameObject : null;
    }

    public StatusPlayer GetStatusPlayerDonoAtual()
    {
        GameObject dono = GetDonoAtual();
        if (dono == null)
            return null;

        StatusPlayer statusPlayer = dono.GetComponent<StatusPlayer>();
        if (statusPlayer != null)
            return statusPlayer;

        statusPlayer = dono.GetComponentInParent<StatusPlayer>();
        if (statusPlayer != null)
            return statusPlayer;

        return dono.transform.root != null
            ? dono.transform.root.GetComponentInChildren<StatusPlayer>(true)
            : null;
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

            MethodInfo metodoComOrigem = componente.GetType().GetMethod(
                "ReceberDano",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(int), typeof(GameObject) },
                null);

            if (metodoComOrigem != null)
            {
                try
                {
                    metodoComOrigem.Invoke(componente, new object[] { dano, gameObject });
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Espada] Falha ao aplicar dano em '{componente.name}' via ReceberDano(int, GameObject): {e.Message}");
                    return false;
                }
            }

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
        AtualizarTextoDurabilidade(true);

        if (vidaAtual <= 0)
            QuebrarEspada();
    }

    private void QuebrarEspada()
    {
        if (quebrada)
            return;

        quebrada = true;
        vidaAtual = 0;
        AtualizarTextoDurabilidade(true);

        if (destruirQuandoVidaZerar)
            Destroy(gameObject);
    }
}
