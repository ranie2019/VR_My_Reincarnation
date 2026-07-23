using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

[DisallowMultipleComponent]
public class InventarioArcoUI : MonoBehaviour
{
    private enum MaoArcoUI
    {
        Nenhuma,
        Esquerda,
        Direita
    }

    private const string CaminhoConfirmacaoFlechaPrimaryButtonEsquerdo = "<XRController>{LeftHand}/primaryButton";
    private const string CaminhoConfirmacaoFlechaPrimaryButtonDireito = "<XRController>{RightHand}/primaryButton";
    private const string CaminhoConfirmacaoFlechaPrimary2DAxisClickEsquerdo = "<XRController>{LeftHand}/primary2DAxisClick";
    private const string CaminhoConfirmacaoFlechaPrimary2DAxisClickDireito = "<XRController>{RightHand}/primary2DAxisClick";

    [Serializable]
    public class SlotFlechaVisual
    {
        public GameObject raiz;
        public GameObject prefabFlecha;
        public Image imagemFlecha;
        public TMP_Text textoQuantidade;
        public Button botaoAtivar;
        public Transform pontoPreviewPrefab;

        [NonSerialized] public GameObject previewPrefabInstanciado;
        [NonSerialized] public GameObject prefabPreviewAtual;
    }

    [Header("Referencias")]
    [SerializeField] private GameObject painelRaiz;
    [SerializeField] private InventarioFlechas inventarioFlechas;
    [SerializeField] private Arco arco;

    [Header("Slots Dinamicos")]
    [SerializeField] private Transform conteudoSlots;
    [SerializeField] private GameObject modeloSlotFlecha;
    [SerializeField] private Sprite spritePadraoFlecha;
    [SerializeField] private List<SlotFlechaVisual> slotsFlechas = new List<SlotFlechaVisual>();
    [SerializeField] private bool criarSlotsAutomaticamente = true;
    [SerializeField] private bool ocultarSlotsSemFlecha = true;

    [Header("Preview 3D das Flechas")]
    [SerializeField] private bool usarPreviewPrefab3D = true;
    [SerializeField] private Vector3 posicaoLocalPreviewPrefab = new Vector3(0f, 18f, 0f);
    [SerializeField] private Vector3 rotacaoLocalPreviewPrefab = Vector3.zero;
    [SerializeField] private float escalaBasePreviewPrefab = 1f;
    [SerializeField, Range(0.1f, 1.5f)] private float ocupacaoPreviewPrefabNoSlot = 0.95f;
    [SerializeField] private float deslocamentoFrentePreviewPrefab = -0.03f;

    [Header("Ajuste Visual Preview Flecha")]
    [SerializeField] private bool aumentarEspessuraPreviewFlecha = true;
    [SerializeField] private float multiplicadorEscalaPreviewFlecha = 1.8f;
    [SerializeField] private Vector3 escalaNaoUniformePreviewFlecha = new Vector3(1.6f, 1.6f, 1.0f);
    [SerializeField] private bool usarEscalaNaoUniformePreviewFlecha = false;
    [SerializeField] private Vector3 rotacaoExtraPreviewFlecha = new Vector3(0f, 0f, 0f);

    [Header("Entrada")]
    [SerializeField] private InputActionReference acaoAbrirInventarioArco;
    [SerializeField] private InputActionReference acaoAbrirInventarioArcoMaoEsquerda;
    [SerializeField] private InputActionReference acaoAbrirInventarioArcoMaoDireita;
    [SerializeField] private bool abrirSomenteComArcoSegurado = true;
    [SerializeField] private bool permitirTeclaXNoEditor = true;
    [SerializeField] private float intervaloMinimoAlternarUI = 0.25f;

    [Header("Confirmacao Botao Flecha")]
    [SerializeField] private InputActionReference acaoConfirmarBotaoFlecha;
    [SerializeField] private InputActionReference acaoConfirmarBotaoFlechaMaoEsquerda;
    [SerializeField] private InputActionReference acaoConfirmarBotaoFlechaMaoDireita;
    [SerializeField] private bool confirmarBotaoFlechaComAcao = true;
    [SerializeField] private bool usarSelecionadoEventSystemParaBotaoFlecha = true;
    [SerializeField] private float intervaloMinimoConfirmacaoBotaoFlecha = 0.15f;

    [Header("Seguranca UI")]
    [SerializeField] private bool desativarFisicaDaUI = true;

    [Header("Diagnostico")]
    [SerializeField] private int diagnosticoLinhasPreenchidas;
    [SerializeField] private int diagnosticoSlotsVisuaisDisponiveis;
    [SerializeField] private int diagnosticoTiposFlechaEncontrados;
    [SerializeField] private bool diagnosticoPainelAberto;
    [SerializeField] private bool diagnosticoArcoSegurado;
    [SerializeField] private bool diagnosticoInputRecebido;
    [SerializeField] private bool diagnosticoBloqueadoPorArcoNaoSegurado;
    [SerializeField] private string diagnosticoFlechaAtivaNaUI;
    [SerializeField] private string diagnosticoUltimaFlechaAtivada;
    [SerializeField] private string diagnosticoUltimoBotaoAtivado;
    [SerializeField] private bool diagnosticoUltimoCliqueRecebido;
    [SerializeField] private bool diagnosticoBotaoFlechaEmHover;
    [SerializeField] private string diagnosticoIdFlechaEmHover;
    [SerializeField] private bool diagnosticoConfirmacaoRecebida;
    [SerializeField] private bool diagnosticoBotaoRecebeuPointerEnter;
    [SerializeField] private bool diagnosticoBotaoRecebeuPointerClick;
    [SerializeField] private bool diagnosticoCanvasTemGraphicRaycaster;
    [SerializeField] private bool diagnosticoCanvasTemTrackedDeviceRaycaster;
    [SerializeField] private bool diagnosticoTrackedDeviceRaycasterAdicionado;
    [SerializeField] private string diagnosticoCanvasRaycastXR;
    [SerializeField] private int diagnosticoComponentesFisicaUIDesativados;
    [SerializeField] private int diagnosticoQuantidadePreviewsAtivos;
    [SerializeField] private Vector3 diagnosticoUltimaEscalaPreview;
    [SerializeField] private string diagnosticoUltimoPreviewCriado;
    [SerializeField] private string diagnosticoUltimoPreviewDestruido;
    [SerializeField] private bool diagnosticoAtualizandoUI;
    [SerializeField] private string diagnosticoMaoSegurandoArco;
    [SerializeField] private string diagnosticoMaoLivreArco;
    [SerializeField] private string diagnosticoUltimaAcaoEntrada;

    private readonly List<InventarioFlechas.FlechaInventarioInfo> cacheFlechas =
        new List<InventarioFlechas.FlechaInventarioInfo>();
    private readonly List<InventarioFlechas.FlechaInventarioInfo> cacheFlechasPorSlot =
        new List<InventarioFlechas.FlechaInventarioInfo>();
    private readonly Dictionary<Button, string> idsFlechaPorBotao = new Dictionary<Button, string>();
    private bool painelAberto;
    private float proximoTempoPodeAlternarUI;
    private Button botaoFlechaEmHover;
    private InputAction acaoAbrirInventarioArcoFallbackEsquerda;
    private InputAction acaoAbrirInventarioArcoFallbackDireita;
    private InputAction acaoConfirmarBotaoFlechaFallbackEsquerda;
    private InputAction acaoConfirmarBotaoFlechaFallbackDireita;
    private readonly List<InputAction> acoesHabilitadasPorEsteScript = new List<InputAction>();
    private float proximoTempoPodeConfirmarBotaoFlecha;
    private bool entradaConfirmacaoConsumidaNesteFrame;

    private void Awake()
    {
        EncontrarReferenciasSeNecessario();
        GarantirRaycasterXRNoPainel();
        DesativarFisicaDaUISeNecessario();
        painelAberto = PainelEstaVisivel();
        GarantirSlotsConfigurados(0);
        AtualizarUI();
    }

    private void OnEnable()
    {
        EncontrarReferenciasSeNecessario();
        GarantirRaycasterXRNoPainel();
        ConfigurarAcaoConfirmarBotaoFlecha();
        AtualizarUI();
    }

    private void OnDisable()
    {
        DesabilitarAcaoConfirmarBotaoFlecha();
    }

    private void Update()
    {
        diagnosticoInputRecebido = false;
        diagnosticoBloqueadoPorArcoNaoSegurado = false;
        diagnosticoArcoSegurado = ArcoEstaSegurado(arco);
        diagnosticoConfirmacaoRecebida = false;
        entradaConfirmacaoConsumidaNesteFrame = false;
        AtualizarDiagnosticoMaosArco();

        AtualizarConfirmacaoBotaoFlecha();

        if (entradaConfirmacaoConsumidaNesteFrame)
            return;

        if (!BotaoAbrirInventarioArcoPressionado())
            return;

        diagnosticoInputRecebido = true;
        if (Time.time < proximoTempoPodeAlternarUI)
            return;

        Arco arcoSegurado = ObterArcoPermitidoParaAbrir();
        if (arcoSegurado == null)
        {
            diagnosticoBloqueadoPorArcoNaoSegurado = true;
            return;
        }

        proximoTempoPodeAlternarUI = Time.time + Mathf.Max(0f, intervaloMinimoAlternarUI);
        Alternar(arcoSegurado);
    }

    private void LateUpdate()
    {
        if (painelAberto && usarPreviewPrefab3D)
            ManterPreviewsVisiveis();
    }

    [ContextMenu("Atualizar UI Inventario Arco")]
    public void AtualizarUI()
    {
        diagnosticoAtualizandoUI = true;
        EncontrarReferenciasSeNecessario();
        cacheFlechas.Clear();

        if (inventarioFlechas != null)
            cacheFlechas.AddRange(inventarioFlechas.ObterFlechasDisponiveis());

        GarantirSlotsConfigurados(cacheFlechas.Count);

        bool[] flechasUsadas = new bool[cacheFlechas.Count];
        cacheFlechasPorSlot.Clear();
        for (int i = 0; i < slotsFlechas.Count; i++)
        {
            InventarioFlechas.FlechaInventarioInfo info = ObterInfoParaSlot(slotsFlechas[i], flechasUsadas);
            cacheFlechasPorSlot.Add(info);
            ConfigurarSlotVisual(i, info);
        }

        diagnosticoTiposFlechaEncontrados = cacheFlechas.Count;
        diagnosticoSlotsVisuaisDisponiveis = slotsFlechas.Count;
        diagnosticoLinhasPreenchidas = Mathf.Min(cacheFlechas.Count, slotsFlechas.Count);
        diagnosticoPainelAberto = painelAberto;
        diagnosticoArcoSegurado = ArcoEstaSegurado(arco);
        diagnosticoFlechaAtivaNaUI = arco != null ? arco.ObterIdTipoFlechaAtiva() : string.Empty;
        diagnosticoAtualizandoUI = false;
        AtualizarDiagnosticoPreviews();
    }

    public void AtualizarUI(Arco arcoAtual)
    {
        VincularArco(arcoAtual);
        AtualizarUI();
    }

    public void VincularArco(Arco arcoAtual)
    {
        if (arcoAtual != null)
            arco = arcoAtual;
    }

    public void Abrir()
    {
        if (ObterArcoPermitidoParaAbrir() == null)
        {
            diagnosticoBloqueadoPorArcoNaoSegurado = true;
            return;
        }

        AplicarVisibilidadePainel(true);

        AtualizarUI();
    }

    public void Abrir(Arco arcoAtual)
    {
        VincularArco(arcoAtual);
        Abrir();
    }

    public void Fechar()
    {
        AplicarVisibilidadePainel(false);

        diagnosticoPainelAberto = false;
    }

    public void Alternar()
    {
        if (painelAberto)
            Fechar();
        else
            Abrir();
    }

    public void Alternar(Arco arcoAtual)
    {
        VincularArco(arcoAtual);
        Alternar();
    }

    private void GarantirSlotsConfigurados(int quantidadeNecessaria)
    {
        RemoverSlotsNulos();

        if (slotsFlechas.Count == 0 && ModeloSlotValido())
        {
            if (conteudoSlots == null)
                conteudoSlots = modeloSlotFlecha.transform.parent;

            slotsFlechas.Add(CriarSlotPorHierarquia(modeloSlotFlecha));
        }

        if (!criarSlotsAutomaticamente || !ModeloSlotValido())
        {
            ConfigurarBotoes();
            return;
        }

        if (conteudoSlots == null)
            conteudoSlots = modeloSlotFlecha.transform.parent;

        while (slotsFlechas.Count < quantidadeNecessaria)
        {
            GameObject novoSlot = Instantiate(modeloSlotFlecha, conteudoSlots);
            novoSlot.name = modeloSlotFlecha.name + "_" + (slotsFlechas.Count + 1);
            novoSlot.SetActive(true);
            slotsFlechas.Add(CriarSlotPorHierarquia(novoSlot));
        }

        ConfigurarBotoes();
    }

    private void RemoverSlotsNulos()
    {
        for (int i = slotsFlechas.Count - 1; i >= 0; i--)
        {
            SlotFlechaVisual slot = slotsFlechas[i];
            if (slot == null || (slot.raiz == null && slot.imagemFlecha == null && slot.textoQuantidade == null && slot.botaoAtivar == null))
                slotsFlechas.RemoveAt(i);
        }
    }

    private bool ModeloSlotValido()
    {
        return modeloSlotFlecha != null &&
               modeloSlotFlecha != gameObject &&
               modeloSlotFlecha != painelRaiz &&
               !ObjetoEhArco(modeloSlotFlecha) &&
               modeloSlotFlecha.GetComponent<Canvas>() == null &&
               modeloSlotFlecha.GetComponent<InventarioArcoUI>() == null;
    }

    private static SlotFlechaVisual CriarSlotPorHierarquia(GameObject raiz)
    {
        SlotFlechaVisual slot = new SlotFlechaVisual { raiz = raiz };
        if (raiz == null)
            return slot;

        Button botao = raiz.GetComponentInChildren<Button>(true);
        slot.botaoAtivar = botao;
        slot.imagemFlecha = EncontrarImagemDoSlot(raiz, botao);
        GarantirImagemIconeSlot(slot);
        slot.textoQuantidade = EncontrarTextoQuantidadeDoSlot(raiz, botao);
        return slot;
    }

    private static Image EncontrarImagemDoSlot(GameObject raiz, Button botao)
    {
        Image[] imagens = raiz.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < imagens.Length; i++)
        {
            if (imagens[i].gameObject == raiz)
                continue;

            if (botao != null && imagens[i].GetComponentInParent<Button>(true) == botao)
                continue;

            string nome = imagens[i].name.ToLowerInvariant();
            if (nome.Contains("flecha") || nome.Contains("flexa") || nome.Contains("icone") || nome.Contains("icon"))
                return imagens[i];
        }

        for (int i = 0; i < imagens.Length; i++)
        {
            if (imagens[i].gameObject == raiz)
                continue;

            if (botao == null || imagens[i].GetComponentInParent<Button>(true) != botao)
                return imagens[i];
        }

        return null;
    }

    private static void GarantirImagemIconeSlot(SlotFlechaVisual slot)
    {
        if (slot == null || slot.raiz == null)
            return;

        if (ImagemIconeValida(slot))
        {
            ConfigurarImagemIcone(slot.imagemFlecha);
            return;
        }

        Transform imagemExistente = slot.raiz.transform.Find("Imagem Flecha");
        if (imagemExistente == null)
            imagemExistente = slot.raiz.transform.Find("Icone Flecha");

        if (imagemExistente != null)
            slot.imagemFlecha = imagemExistente.GetComponent<Image>();

        if (slot.imagemFlecha == null)
        {
            GameObject objetoImagem = new GameObject("Imagem Flecha", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            objetoImagem.layer = slot.raiz.layer;
            objetoImagem.transform.SetParent(slot.raiz.transform, false);

            RectTransform rect = objetoImagem.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 12f);
            rect.sizeDelta = new Vector2(58f, 58f);

            slot.imagemFlecha = objetoImagem.GetComponent<Image>();
        }

        ConfigurarImagemIcone(slot.imagemFlecha);
    }

    private static bool ImagemIconeValida(SlotFlechaVisual slot)
    {
        if (slot == null || slot.raiz == null || slot.imagemFlecha == null)
            return false;

        if (slot.imagemFlecha.gameObject == slot.raiz)
            return false;

        if (slot.botaoAtivar != null && slot.imagemFlecha.GetComponentInParent<Button>(true) == slot.botaoAtivar)
            return false;

        return true;
    }

    private static void ConfigurarImagemIcone(Image imagem)
    {
        if (imagem == null)
            return;

        imagem.raycastTarget = false;
        imagem.preserveAspect = true;
        imagem.type = Image.Type.Simple;
    }

    private static TMP_Text EncontrarTextoQuantidadeDoSlot(GameObject raiz, Button botao)
    {
        TMP_Text[] textos = raiz.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textos.Length; i++)
        {
            if (botao != null && textos[i].GetComponentInParent<Button>(true) == botao)
                continue;

            string nome = textos[i].name.ToLowerInvariant();
            if (nome.Contains("quantidade") || nome.Contains("qtd") || nome.Contains("count"))
                return textos[i];
        }

        for (int i = 0; i < textos.Length; i++)
        {
            if (botao == null || textos[i].GetComponentInParent<Button>(true) != botao)
                return textos[i];
        }

        return textos.Length > 0 ? textos[0] : null;
    }

    private void ConfigurarBotoes()
    {
        for (int i = 0; i < slotsFlechas.Count; i++)
        {
            SlotFlechaVisual slot = slotsFlechas[i];
            if (slot == null || slot.botaoAtivar == null)
                continue;

            slot.botaoAtivar.onClick.RemoveAllListeners();
            idsFlechaPorBotao.Remove(slot.botaoAtivar);
        }
    }

    private void ConfigurarBotaoAtivarSlot(SlotFlechaVisual slot, InventarioFlechas.FlechaInventarioInfo info, bool ativo)
    {
        if (slot == null || slot.botaoAtivar == null)
            return;

        Button botao = slot.botaoAtivar;
        botao.gameObject.SetActive(true);
        botao.onClick.RemoveAllListeners();

        string idLocal = info != null ? info.idTipoFlecha : string.Empty;
        if (string.IsNullOrWhiteSpace(idLocal))
            idsFlechaPorBotao.Remove(botao);
        else
            idsFlechaPorBotao[botao] = idLocal;

        botao.interactable = info != null && info.quantidadeTotal > 0 && !string.IsNullOrWhiteSpace(idLocal);
        DefinirTextoBotao(botao, ativo ? "ACTIVE" : "ACTIVATE");
        ConfigurarRaycastSlotParaBotao(slot, botao);
        GarantirRaycastBotao(botao);
        ConfigurarHoverBotaoFlecha(botao, idLocal);

        if (!string.IsNullOrWhiteSpace(idLocal))
            botao.onClick.AddListener(() => AtivarFlechaDoSlot(idLocal));
    }

    private void AtivarFlechaDoSlot(string idTipoFlecha)
    {
        diagnosticoUltimoCliqueRecebido = true;
        diagnosticoUltimoBotaoAtivado = idTipoFlecha;

        if (arco == null || string.IsNullOrWhiteSpace(idTipoFlecha))
            return;

        if (inventarioFlechas != null && inventarioFlechas.ObterQuantidadeTotal(idTipoFlecha) <= 0)
            return;

        arco.AtivarTipoFlecha(idTipoFlecha);
        diagnosticoUltimaFlechaAtivada = idTipoFlecha;
        diagnosticoFlechaAtivaNaUI = arco.ObterIdTipoFlechaAtiva();
        AtualizarUI();
    }

    private void AtualizarConfirmacaoBotaoFlecha()
    {
        if (!painelAberto || !confirmarBotaoFlechaComAcao)
            return;

        Button botao = ObterBotaoFlechaParaConfirmar();
        AtualizarDiagnosticoBotaoFlechaHover(botao);

        if (!BotaoFlechaValido(botao))
            return;

        if (!BotaoConfirmarFlechaPressionado())
            return;

        if (Time.time < proximoTempoPodeConfirmarBotaoFlecha)
            return;

        proximoTempoPodeConfirmarBotaoFlecha = Time.time + Mathf.Max(0f, intervaloMinimoConfirmacaoBotaoFlecha);
        diagnosticoConfirmacaoRecebida = true;
        entradaConfirmacaoConsumidaNesteFrame = true;
        botao.onClick.Invoke();
    }

    private Button ObterBotaoFlechaParaConfirmar()
    {
        if (BotaoFlechaValido(botaoFlechaEmHover))
            return botaoFlechaEmHover;

        if (!usarSelecionadoEventSystemParaBotaoFlecha || EventSystem.current == null)
            return null;

        GameObject selecionado = EventSystem.current.currentSelectedGameObject;
        if (selecionado == null)
            return null;

        Button botaoSelecionado = selecionado.GetComponent<Button>();
        if (botaoSelecionado == null)
            botaoSelecionado = selecionado.GetComponentInParent<Button>();

        return BotaoFlechaValido(botaoSelecionado) ? botaoSelecionado : null;
    }

    private bool BotaoFlechaValido(Button botao)
    {
        return botao != null &&
               botao.gameObject.activeInHierarchy &&
               botao.interactable &&
               idsFlechaPorBotao.ContainsKey(botao);
    }

    private void AtualizarDiagnosticoBotaoFlechaHover(Button botao)
    {
        diagnosticoBotaoFlechaEmHover = BotaoFlechaValido(botao);
        diagnosticoIdFlechaEmHover = diagnosticoBotaoFlechaEmHover && idsFlechaPorBotao.TryGetValue(botao, out string idTipoFlecha)
            ? idTipoFlecha
            : string.Empty;
    }

    internal void DefinirBotaoFlechaEmHover(Button botao, string idTipoFlecha)
    {
        if (string.IsNullOrWhiteSpace(idTipoFlecha) || !BotaoFlechaValido(botao))
            return;

        botaoFlechaEmHover = botao;
        diagnosticoBotaoFlechaEmHover = true;
        diagnosticoIdFlechaEmHover = idTipoFlecha;
    }

    internal void RegistrarPointerEnterBotaoFlecha(Button botao, string idTipoFlecha)
    {
        diagnosticoBotaoRecebeuPointerEnter = true;
        DefinirBotaoFlechaEmHover(botao, idTipoFlecha);
    }

    internal void RegistrarPointerClickBotaoFlecha(Button botao, string idTipoFlecha)
    {
        diagnosticoBotaoRecebeuPointerClick = true;
        DefinirBotaoFlechaEmHover(botao, idTipoFlecha);
    }

    internal void LimparBotaoFlechaEmHover(Button botao)
    {
        if (botaoFlechaEmHover != botao)
            return;

        botaoFlechaEmHover = null;
        diagnosticoBotaoFlechaEmHover = false;
        diagnosticoIdFlechaEmHover = string.Empty;
    }

    private void ConfigurarHoverBotaoFlecha(Button botao, string idTipoFlecha)
    {
        if (botao == null)
            return;

        BotaoFlechaInventarioArco hover = botao.GetComponent<BotaoFlechaInventarioArco>();
        if (hover == null)
            hover = botao.gameObject.AddComponent<BotaoFlechaInventarioArco>();

        hover.Configurar(this, botao, idTipoFlecha);
    }

    private bool BotaoConfirmarFlechaPressionado()
    {
        return EntradaMaoLivrePressionada(
            acaoConfirmarBotaoFlechaMaoEsquerda,
            acaoConfirmarBotaoFlechaMaoDireita,
            acaoConfirmarBotaoFlecha,
            ObterAcaoConfirmarBotaoFlechaFallbackEsquerda(),
            ObterAcaoConfirmarBotaoFlechaFallbackDireita(),
            "Confirmar flecha"
        );
    }

    private InputAction ObterAcaoAbrirInventarioArcoFallbackEsquerda()
    {
        if (acaoAbrirInventarioArcoFallbackEsquerda != null)
            return acaoAbrirInventarioArcoFallbackEsquerda;

        acaoAbrirInventarioArcoFallbackEsquerda = new InputAction("Abrir Inventario Arco Esquerda", InputActionType.Button);
        acaoAbrirInventarioArcoFallbackEsquerda.AddBinding(CaminhoConfirmacaoFlechaPrimaryButtonEsquerdo);
        return acaoAbrirInventarioArcoFallbackEsquerda;
    }

    private InputAction ObterAcaoAbrirInventarioArcoFallbackDireita()
    {
        if (acaoAbrirInventarioArcoFallbackDireita != null)
            return acaoAbrirInventarioArcoFallbackDireita;

        acaoAbrirInventarioArcoFallbackDireita = new InputAction("Abrir Inventario Arco Direita", InputActionType.Button);
        acaoAbrirInventarioArcoFallbackDireita.AddBinding(CaminhoConfirmacaoFlechaPrimaryButtonDireito);
        return acaoAbrirInventarioArcoFallbackDireita;
    }

    private InputAction ObterAcaoConfirmarBotaoFlechaFallbackEsquerda()
    {
        if (acaoConfirmarBotaoFlechaFallbackEsquerda != null)
            return acaoConfirmarBotaoFlechaFallbackEsquerda;

        acaoConfirmarBotaoFlechaFallbackEsquerda = new InputAction("Confirmar Botao Flecha Esquerda", InputActionType.Button);
        acaoConfirmarBotaoFlechaFallbackEsquerda.AddBinding(CaminhoConfirmacaoFlechaPrimaryButtonEsquerdo);
        acaoConfirmarBotaoFlechaFallbackEsquerda.AddBinding(CaminhoConfirmacaoFlechaPrimary2DAxisClickEsquerdo);
        return acaoConfirmarBotaoFlechaFallbackEsquerda;
    }

    private InputAction ObterAcaoConfirmarBotaoFlechaFallbackDireita()
    {
        if (acaoConfirmarBotaoFlechaFallbackDireita != null)
            return acaoConfirmarBotaoFlechaFallbackDireita;

        acaoConfirmarBotaoFlechaFallbackDireita = new InputAction("Confirmar Botao Flecha Direita", InputActionType.Button);
        acaoConfirmarBotaoFlechaFallbackDireita.AddBinding(CaminhoConfirmacaoFlechaPrimaryButtonDireito);
        acaoConfirmarBotaoFlechaFallbackDireita.AddBinding(CaminhoConfirmacaoFlechaPrimary2DAxisClickDireito);
        return acaoConfirmarBotaoFlechaFallbackDireita;
    }

    private void ConfigurarAcaoConfirmarBotaoFlecha()
    {
        ObterAcaoConfirmarBotaoFlechaFallbackEsquerda();
        ObterAcaoConfirmarBotaoFlechaFallbackDireita();
    }

    private void DesabilitarAcaoConfirmarBotaoFlecha()
    {
        for (int i = 0; i < acoesHabilitadasPorEsteScript.Count; i++)
        {
            InputAction acao = acoesHabilitadasPorEsteScript[i];
            if (acao != null)
                acao.Disable();
        }

        acoesHabilitadasPorEsteScript.Clear();
    }

    private bool EntradaMaoLivrePressionada(
        InputActionReference acaoEsquerda,
        InputActionReference acaoDireita,
        InputActionReference acaoGenerica,
        InputAction fallbackEsquerdo,
        InputAction fallbackDireito,
        string nomeEntrada)
    {
        MaoArcoUI maoLivre = ObterMaoLivreArco(arco);

        if (maoLivre == MaoArcoUI.Esquerda)
        {
            return AcaoPressionada(acaoEsquerda, nomeEntrada + " esquerda") ||
                   (acaoEsquerda == null && AcaoPressionada(acaoGenerica, nomeEntrada + " generica")) ||
                   AcaoPressionada(fallbackEsquerdo, nomeEntrada + " fallback esquerda");
        }

        if (maoLivre == MaoArcoUI.Direita)
        {
            return AcaoPressionada(acaoDireita, nomeEntrada + " direita") ||
                   (acaoDireita == null && AcaoPressionada(acaoGenerica, nomeEntrada + " generica")) ||
                   AcaoPressionada(fallbackDireito, nomeEntrada + " fallback direita");
        }

        return AcaoPressionada(acaoGenerica, nomeEntrada + " generica") ||
               AcaoPressionada(acaoEsquerda, nomeEntrada + " esquerda") ||
               AcaoPressionada(acaoDireita, nomeEntrada + " direita") ||
               AcaoPressionada(fallbackEsquerdo, nomeEntrada + " fallback esquerda") ||
               AcaoPressionada(fallbackDireito, nomeEntrada + " fallback direita");
    }

    private bool EntradaMaoSegurandoPressionada(
        InputActionReference acaoEsquerda,
        InputActionReference acaoDireita,
        InputActionReference acaoGenerica,
        InputAction fallbackEsquerdo,
        InputAction fallbackDireito,
        string nomeEntrada)
    {
        MaoArcoUI maoSegurando = ObterMaoSegurandoArco(arco);

        if (maoSegurando == MaoArcoUI.Esquerda)
        {
            return AcaoPressionada(acaoEsquerda, nomeEntrada + " esquerda segurando") ||
                   (acaoEsquerda == null && AcaoPressionada(acaoGenerica, nomeEntrada + " generica")) ||
                   AcaoPressionada(fallbackEsquerdo, nomeEntrada + " fallback esquerda segurando");
        }

        if (maoSegurando == MaoArcoUI.Direita)
        {
            return AcaoPressionada(acaoDireita, nomeEntrada + " direita segurando") ||
                   (acaoDireita == null && AcaoPressionada(acaoGenerica, nomeEntrada + " generica")) ||
                   AcaoPressionada(fallbackDireito, nomeEntrada + " fallback direita segurando");
        }

        return AcaoPressionada(acaoGenerica, nomeEntrada + " generica") ||
               AcaoPressionada(acaoEsquerda, nomeEntrada + " esquerda") ||
               AcaoPressionada(acaoDireita, nomeEntrada + " direita") ||
               AcaoPressionada(fallbackEsquerdo, nomeEntrada + " fallback esquerda") ||
               AcaoPressionada(fallbackDireito, nomeEntrada + " fallback direita");
    }

    private bool AcaoPressionada(InputActionReference referencia, string nomeEntrada)
    {
        return referencia != null && AcaoPressionada(referencia.action, nomeEntrada);
    }

    private bool AcaoPressionada(InputAction acao, string nomeEntrada)
    {
        if (acao == null)
            return false;

        try
        {
            GarantirAcaoHabilitada(acao);
            bool pressionou = acao.WasPressedThisFrame();

            if (pressionou)
                diagnosticoUltimaAcaoEntrada = nomeEntrada;

            return pressionou;
        }
        catch
        {
            return false;
        }
    }

    private void GarantirAcaoHabilitada(InputAction acao)
    {
        if (acao == null || acao.enabled)
            return;

        acao.Enable();

        if (!acoesHabilitadasPorEsteScript.Contains(acao))
            acoesHabilitadasPorEsteScript.Add(acao);
    }

    private MaoArcoUI ObterMaoSegurandoArco(Arco arcoCandidato)
    {
        if (arcoCandidato == null)
            return MaoArcoUI.Nenhuma;

        ArmaAttachDuasMao attach = arcoCandidato.GetComponent<ArmaAttachDuasMao>();
        if (attach == null || !attach.ArcoEstaSegurado())
            return MaoArcoUI.Nenhuma;

        if (attach.EstaSeguradoPelaDireita())
            return MaoArcoUI.Direita;

        if (attach.EstaSeguradoPelaEsquerda())
            return MaoArcoUI.Esquerda;

        return MaoArcoUI.Nenhuma;
    }

    private MaoArcoUI ObterMaoLivreArco(Arco arcoCandidato)
    {
        MaoArcoUI maoSegurando = ObterMaoSegurandoArco(arcoCandidato);

        if (maoSegurando == MaoArcoUI.Direita)
            return MaoArcoUI.Esquerda;

        if (maoSegurando == MaoArcoUI.Esquerda)
            return MaoArcoUI.Direita;

        return MaoArcoUI.Nenhuma;
    }

    private void AtualizarDiagnosticoMaosArco()
    {
        diagnosticoMaoSegurandoArco = ObterMaoSegurandoArco(arco).ToString();
        diagnosticoMaoLivreArco = ObterMaoLivreArco(arco).ToString();
    }

    private InventarioFlechas.FlechaInventarioInfo ObterInfoParaSlot(SlotFlechaVisual slot, bool[] flechasUsadas)
    {
        if (slot != null && slot.prefabFlecha != null)
        {
            string idSlot = ObterIdFlechaDoPrefab(slot.prefabFlecha);

            for (int i = 0; i < cacheFlechas.Count; i++)
            {
                InventarioFlechas.FlechaInventarioInfo info = cacheFlechas[i];
                if (info == null || info.prefabFlecha == null)
                    continue;

                if (InfoCorrespondeAoSlot(info, slot.prefabFlecha, idSlot))
                {
                    if (flechasUsadas != null && i < flechasUsadas.Length)
                        flechasUsadas[i] = true;

                    return info;
                }
            }

            return null;
        }

        for (int i = 0; i < cacheFlechas.Count; i++)
        {
            if (flechasUsadas != null && i < flechasUsadas.Length && flechasUsadas[i])
                continue;

            if (flechasUsadas != null && i < flechasUsadas.Length)
                flechasUsadas[i] = true;

            return cacheFlechas[i];
        }

        return null;
    }

    private static bool InfoCorrespondeAoSlot(
        InventarioFlechas.FlechaInventarioInfo info,
        GameObject prefabSlot,
        string idSlot)
    {
        if (info == null || prefabSlot == null)
            return false;

        if (info.prefabFlecha == prefabSlot)
            return true;

        if (IdsFlechaIguais(info.idTipoFlecha, idSlot))
            return true;

        if (info.prefabFlecha != null && IdsFlechaIguais(ObterIdFlechaDoPrefab(info.prefabFlecha), idSlot))
            return true;

        return IdsFlechaIguais(info.nomeExibicao, idSlot);
    }

    private static string ObterIdFlechaDoPrefab(GameObject prefabFlecha)
    {
        if (prefabFlecha == null)
            return string.Empty;

        ItemInventarioDados dados = prefabFlecha.GetComponent<ItemInventarioDados>();
        if (dados != null)
        {
            string idDados = NormalizarIdFlechaUI(dados.ObterIdTipoFlecha());
            if (!string.IsNullOrWhiteSpace(idDados))
                return idDados;
        }

        return NormalizarIdFlechaUI(prefabFlecha.name);
    }

    private static bool IdsFlechaIguais(string a, string b)
    {
        string idA = NormalizarIdFlechaUI(a);
        string idB = NormalizarIdFlechaUI(b);

        return !string.IsNullOrWhiteSpace(idA) &&
               !string.IsNullOrWhiteSpace(idB) &&
               string.Equals(idA, idB, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizarIdFlechaUI(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? string.Empty : SlotInventario.LimparNomeItem(id).Trim();
    }

    private void ConfigurarSlotVisual(int indice, InventarioFlechas.FlechaInventarioInfo info)
    {
        if (indice < 0 || indice >= slotsFlechas.Count)
            return;

        SlotFlechaVisual slot = slotsFlechas[indice];
        if (slot == null)
            return;

        if (info == null)
        {
            LimparSlotVisual(slot);
            return;
        }

        if (slot.raiz != null)
            slot.raiz.SetActive(true);

        if (usarPreviewPrefab3D)
        {
            OcultarImagemFlecha(slot);
            AtualizarPreviewPrefabSlot(slot, info);
        }
        else
        {
            OcultarPreviewPrefabSlot(slot);
            GarantirImagemIconeSlot(slot);

            if (slot.imagemFlecha != null)
            {
                Sprite sprite = info.icone != null ? info.icone : spritePadraoFlecha;
                slot.imagemFlecha.sprite = sprite;
                slot.imagemFlecha.enabled = sprite != null;
            }
        }

        if (slot.textoQuantidade != null)
            slot.textoQuantidade.text = info.quantidadeTotal.ToString();

        bool ativo = arco != null &&
                     string.Equals(arco.ObterIdTipoFlechaAtiva(), info.idTipoFlecha, StringComparison.Ordinal);

        ConfigurarBotaoAtivarSlot(slot, info, ativo);
    }

    private void LimparSlotVisual(SlotFlechaVisual slot)
    {
        if (slot.botaoAtivar != null)
        {
            slot.botaoAtivar.onClick.RemoveAllListeners();
            slot.botaoAtivar.interactable = false;
            idsFlechaPorBotao.Remove(slot.botaoAtivar);
            LimparBotaoFlechaEmHover(slot.botaoAtivar);
            ConfigurarHoverBotaoFlecha(slot.botaoAtivar, string.Empty);
            DefinirTextoBotao(slot.botaoAtivar, "ACTIVATE");
        }

        if (slot.raiz != null && ocultarSlotsSemFlecha)
        {
            slot.raiz.SetActive(false);
            return;
        }

        if (slot.raiz != null)
            slot.raiz.SetActive(true);

        if (slot.imagemFlecha != null)
        {
            slot.imagemFlecha.sprite = null;
            slot.imagemFlecha.enabled = false;
        }

        OcultarPreviewPrefabSlot(slot);

        if (slot.textoQuantidade != null)
            slot.textoQuantidade.text = "0";
    }

    private static void DefinirTextoBotao(Button botao, string texto)
    {
        if (botao == null)
            return;

        TMP_Text textoTMP = botao.GetComponentInChildren<TMP_Text>(true);
        if (textoTMP != null)
            textoTMP.text = texto;
    }

    private static void ConfigurarRaycastSlotParaBotao(SlotFlechaVisual slot, Button botao)
    {
        if (slot == null || slot.raiz == null || botao == null)
            return;

        Graphic alvoBotao = botao.targetGraphic;
        Graphic[] graficosSlot = slot.raiz.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graficosSlot.Length; i++)
        {
            if (graficosSlot[i] == null)
                continue;

            graficosSlot[i].raycastTarget = graficosSlot[i] == alvoBotao;
        }

        TMP_Text[] textosBotao = botao.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textosBotao.Length; i++)
        {
            if (textosBotao[i] != null)
                textosBotao[i].raycastTarget = false;
        }

        Navigation navegacao = botao.navigation;
        navegacao.mode = Navigation.Mode.None;
        botao.navigation = navegacao;
    }

    private static void GarantirRaycastBotao(Button botao)
    {
        if (botao == null)
            return;

        if (botao.targetGraphic != null)
            botao.targetGraphic.raycastTarget = true;

        Image imagemBotao = botao.GetComponent<Image>();
        if (imagemBotao != null)
            imagemBotao.raycastTarget = true;
    }

    private void AtualizarPreviewPrefabSlot(SlotFlechaVisual slot, InventarioFlechas.FlechaInventarioInfo info)
    {
        if (slot == null || slot.raiz == null || info == null || info.prefabFlecha == null)
        {
            OcultarPreviewPrefabSlot(slot);
            return;
        }

        if (slot.previewPrefabInstanciado == null || slot.prefabPreviewAtual != info.prefabFlecha)
        {
            DestruirPreviewPrefabSlot(slot);
            slot.previewPrefabInstanciado = Instantiate(info.prefabFlecha, ObterParentPreviewPrefab(slot));
            slot.previewPrefabInstanciado.name = "Preview_" + info.prefabFlecha.name;
            slot.prefabPreviewAtual = info.prefabFlecha;
            diagnosticoUltimoPreviewCriado = slot.previewPrefabInstanciado.name;
            PrepararObjetoPreview(slot.previewPrefabInstanciado);
        }

        slot.previewPrefabInstanciado.SetActive(true);
        ForcarPreviewVisivel(slot.previewPrefabInstanciado);
        AjustarPreviewPrefabNoSlot(slot);
        ForcarPreviewVisivel(slot.previewPrefabInstanciado);
        AtualizarDiagnosticoPreviews();
    }

    private Transform ObterParentPreviewPrefab(SlotFlechaVisual slot)
    {
        if (slot != null && slot.pontoPreviewPrefab != null)
            return slot.pontoPreviewPrefab;

        if (slot == null || slot.raiz == null)
            return transform;

        Transform pontoExistente = slot.raiz.transform.Find("Ponto Preview 3D");
        if (pontoExistente != null)
        {
            slot.pontoPreviewPrefab = pontoExistente;
            return pontoExistente;
        }

        GameObject ponto = new GameObject("Ponto Preview 3D", typeof(RectTransform));
        ponto.layer = slot.raiz.layer;
        ponto.transform.SetParent(slot.raiz.transform, false);

        RectTransform rect = ponto.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(posicaoLocalPreviewPrefab.x, posicaoLocalPreviewPrefab.y);
        rect.sizeDelta = Vector2.zero;

        slot.pontoPreviewPrefab = ponto.transform;
        return slot.pontoPreviewPrefab;
    }

    private void PrepararObjetoPreview(GameObject preview)
    {
        if (preview == null)
            return;

        RemoverTagsDoPreview(preview.transform);

        Collider[] colliders = preview.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                DestruirComponenteSeguro(colliders[i]);
        }

        Rigidbody[] rigidbodies = preview.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];
            if (rb == null)
                continue;

            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            if (rb.useGravity)
                rb.useGravity = false;

            if (rb.detectCollisions)
                rb.detectCollisions = false;
        }

        AudioSource[] audios = preview.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audios.Length; i++)
        {
            if (audios[i] == null)
                continue;

            audios[i].Stop();
            DestruirComponenteSeguro(audios[i]);
        }

        MonoBehaviour[] comportamentos = preview.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < comportamentos.Length; i++)
        {
            if (comportamentos[i] == null)
                continue;

            comportamentos[i].enabled = false;
            DestruirComponenteSeguro(comportamentos[i]);
        }

        ForcarPreviewVisivel(preview);
    }

    private static void RemoverTagsDoPreview(Transform raiz)
    {
        if (raiz == null)
            return;

        raiz.gameObject.tag = "Untagged";
        for (int i = 0; i < raiz.childCount; i++)
            RemoverTagsDoPreview(raiz.GetChild(i));
    }

    private static void DestruirComponenteSeguro(Component componente)
    {
        if (componente == null)
            return;

        if (Application.isPlaying)
            Destroy(componente);
        else
            DestroyImmediate(componente);
    }

    private void AjustarPreviewPrefabNoSlot(SlotFlechaVisual slot)
    {
        if (slot == null || slot.raiz == null || slot.previewPrefabInstanciado == null)
            return;

        Transform parentPreview = ObterParentPreviewPrefab(slot);
        Transform preview = slot.previewPrefabInstanciado.transform;
        preview.SetParent(parentPreview, false);
        preview.localPosition = posicaoLocalPreviewPrefab;
        preview.localRotation = Quaternion.Euler(rotacaoLocalPreviewPrefab + rotacaoExtraPreviewFlecha);
        preview.localScale = Vector3.one * Mathf.Max(0.0001f, escalaBasePreviewPrefab);

        if (!TentarObterBoundsRenderers(slot.previewPrefabInstanciado, out Bounds bounds))
        {
            AplicarAjusteVisualPreviewFlecha(slot.previewPrefabInstanciado);
            diagnosticoUltimaEscalaPreview = preview.localScale;
            return;
        }

        float tamanhoAlvo = CalcularTamanhoAlvoPreview(slot);
        float maiorTamanho = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (tamanhoAlvo > 0f && maiorTamanho > 0.0001f)
        {
            float fator = tamanhoAlvo / maiorTamanho;
            preview.localScale *= fator;

            if (!TentarObterBoundsRenderers(slot.previewPrefabInstanciado, out bounds))
                return;
        }

        Vector3 centroDesejado = parentPreview.TransformPoint(posicaoLocalPreviewPrefab) +
                                 parentPreview.forward * deslocamentoFrentePreviewPrefab;
        preview.position += centroDesejado - bounds.center;
        AplicarAjusteVisualPreviewFlecha(slot.previewPrefabInstanciado);

        if (TentarObterBoundsRenderers(slot.previewPrefabInstanciado, out bounds))
            preview.position += centroDesejado - bounds.center;

        diagnosticoUltimaEscalaPreview = preview.localScale;
    }

    private void AplicarAjusteVisualPreviewFlecha(GameObject preview)
    {
        if (preview == null)
            return;

        Vector3 escala = preview.transform.localScale;

        if (aumentarEspessuraPreviewFlecha)
            escala *= Mathf.Max(0.0001f, multiplicadorEscalaPreviewFlecha);

        if (usarEscalaNaoUniformePreviewFlecha)
            escala = Vector3.Scale(escala, escalaNaoUniformePreviewFlecha);

        preview.transform.localScale = escala;
    }

    private void FixarPreviewPrefabNoSlot(SlotFlechaVisual slot)
    {
        if (slot == null || slot.raiz == null || slot.previewPrefabInstanciado == null)
            return;

        Transform parentPreview = ObterParentPreviewPrefab(slot);
        Transform preview = slot.previewPrefabInstanciado.transform;

        if (preview.parent != parentPreview)
            preview.SetParent(parentPreview, false);

        preview.localPosition = posicaoLocalPreviewPrefab;
        preview.localRotation = Quaternion.Euler(rotacaoLocalPreviewPrefab + rotacaoExtraPreviewFlecha);

        if (TentarObterBoundsRenderers(slot.previewPrefabInstanciado, out Bounds bounds))
        {
            Vector3 centroDesejado = parentPreview.TransformPoint(posicaoLocalPreviewPrefab) +
                                     parentPreview.forward * deslocamentoFrentePreviewPrefab;
            preview.position += centroDesejado - bounds.center;
        }

        diagnosticoUltimaEscalaPreview = preview.localScale;
    }

    private void ManterPreviewsVisiveis()
    {
        for (int i = 0; i < slotsFlechas.Count; i++)
        {
            SlotFlechaVisual slot = slotsFlechas[i];
            if (slot == null || slot.raiz == null || !slot.raiz.activeInHierarchy)
                continue;

            InventarioFlechas.FlechaInventarioInfo info = i < cacheFlechasPorSlot.Count
                ? cacheFlechasPorSlot[i]
                : null;

            if (info == null || info.prefabFlecha == null)
                continue;

            if (slot.previewPrefabInstanciado == null || slot.prefabPreviewAtual != info.prefabFlecha)
                AtualizarPreviewPrefabSlot(slot, info);

            if (slot.previewPrefabInstanciado == null)
                continue;

            slot.previewPrefabInstanciado.SetActive(true);
            ForcarPreviewVisivel(slot.previewPrefabInstanciado);
            FixarPreviewPrefabNoSlot(slot);
            ForcarPreviewVisivel(slot.previewPrefabInstanciado);
        }

        AtualizarDiagnosticoPreviews();
    }

    private float CalcularTamanhoAlvoPreview(SlotFlechaVisual slot)
    {
        RectTransform rect = slot != null ? slot.raiz != null ? slot.raiz.transform as RectTransform : null : null;
        if (rect == null)
            return 0.2f * Mathf.Clamp(ocupacaoPreviewPrefabNoSlot, 0.1f, 1.5f);

        Vector3[] cantos = new Vector3[4];
        rect.GetWorldCorners(cantos);
        float largura = Vector3.Distance(cantos[0], cantos[3]);
        float altura = Vector3.Distance(cantos[0], cantos[1]);
        float menor = Mathf.Min(largura, altura);
        return menor * Mathf.Clamp(ocupacaoPreviewPrefabNoSlot, 0.1f, 1.5f);
    }

    private static bool TentarObterBoundsRenderers(GameObject objeto, out Bounds bounds)
    {
        bounds = new Bounds();
        if (objeto == null)
            return false;

        Renderer[] renderers = objeto.GetComponentsInChildren<Renderer>(true);
        bool encontrou = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererAtual = renderers[i];
            if (rendererAtual == null || !rendererAtual.enabled)
                continue;

            if (!encontrou)
            {
                bounds = rendererAtual.bounds;
                encontrou = true;
            }
            else
            {
                bounds.Encapsulate(rendererAtual.bounds);
            }
        }

        return encontrou;
    }

    private static void ForcarPreviewVisivel(GameObject preview)
    {
        if (preview == null)
            return;

        preview.SetActive(true);

        Renderer[] renderers = preview.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererAtual = renderers[i];
            if (rendererAtual == null)
                continue;

            rendererAtual.gameObject.SetActive(true);
            rendererAtual.enabled = true;
        }
    }

    private static void OcultarImagemFlecha(SlotFlechaVisual slot)
    {
        if (slot == null || slot.imagemFlecha == null)
            return;

        slot.imagemFlecha.sprite = null;
        slot.imagemFlecha.enabled = false;
    }

    private void OcultarPreviewPrefabSlot(SlotFlechaVisual slot)
    {
        if (slot == null || slot.previewPrefabInstanciado == null)
            return;

        slot.previewPrefabInstanciado.SetActive(false);
        AtualizarDiagnosticoPreviews();
    }

    private void DestruirPreviewPrefabSlot(SlotFlechaVisual slot)
    {
        if (slot == null || slot.previewPrefabInstanciado == null)
            return;

        GameObject preview = slot.previewPrefabInstanciado;
        diagnosticoUltimoPreviewDestruido = preview != null ? preview.name : string.Empty;
        slot.previewPrefabInstanciado = null;
        slot.prefabPreviewAtual = null;

        if (Application.isPlaying)
            Destroy(preview);
        else
            DestroyImmediate(preview);

        AtualizarDiagnosticoPreviews();
    }

    private void AtualizarDiagnosticoPreviews()
    {
        diagnosticoQuantidadePreviewsAtivos = 0;
        for (int i = 0; i < slotsFlechas.Count; i++)
        {
            SlotFlechaVisual slot = slotsFlechas[i];
            if (slot != null &&
                slot.previewPrefabInstanciado != null &&
                slot.previewPrefabInstanciado.activeInHierarchy &&
                PreviewPossuiRendererVisivel(slot.previewPrefabInstanciado))
            {
                diagnosticoQuantidadePreviewsAtivos++;
            }
        }
    }

    private static bool PreviewPossuiRendererVisivel(GameObject preview)
    {
        if (preview == null)
            return false;

        Renderer[] renderers = preview.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled && renderers[i].gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    private bool BotaoAbrirInventarioArcoPressionado()
    {
        if (EntradaMaoSegurandoPressionada(
                acaoAbrirInventarioArcoMaoEsquerda,
                acaoAbrirInventarioArcoMaoDireita,
                acaoAbrirInventarioArco,
                ObterAcaoAbrirInventarioArcoFallbackEsquerda(),
                ObterAcaoAbrirInventarioArcoFallbackDireita(),
                "Abrir inventario arco"))
        {
            return true;
        }

        return permitirTeclaXNoEditor &&
               Application.isEditor &&
               Keyboard.current != null &&
               Keyboard.current.xKey.wasPressedThisFrame;
    }

    private Arco ObterArcoPermitidoParaAbrir()
    {
        if (ArcoPodeAbrirInventario(arco))
            return arco;

        return null;
    }

    private bool ArcoPodeAbrirInventario(Arco arcoCandidato)
    {
        if (arcoCandidato == null || arcoCandidato.Quebrado)
            return false;

        if (!abrirSomenteComArcoSegurado)
            return true;

        ArmaAttachDuasMao attach = arcoCandidato.GetComponent<ArmaAttachDuasMao>();
        return attach != null && attach.ArcoEstaSegurado();
    }

    private bool ArcoEstaSegurado(Arco arcoCandidato)
    {
        if (arcoCandidato == null)
            return false;

        ArmaAttachDuasMao attach = arcoCandidato.GetComponent<ArmaAttachDuasMao>();
        return attach != null && attach.ArcoEstaSegurado();
    }

    private void EncontrarReferenciasSeNecessario()
    {
        if (arco == null)
            arco = GetComponentInParent<Arco>(true);

        if (painelRaiz == null || ObjetoEhArco(painelRaiz))
        {
            GameObject painelFilho = EncontrarPainelInventarioArco();
            if (painelFilho != null)
                painelRaiz = painelFilho;
            else if (painelRaiz == null && !ObjetoEhArco(gameObject))
                painelRaiz = gameObject;
        }

        if (inventarioFlechas == null)
        {
            if (arco != null)
                inventarioFlechas = arco.GetComponent<InventarioFlechas>();

            if (inventarioFlechas != null)
                return;

            InventarioFlechas[] inventarios = FindObjectsByType<InventarioFlechas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            if (inventarios != null && inventarios.Length > 0)
                inventarioFlechas = inventarios[0];
        }
    }

    private void GarantirRaycasterXRNoPainel()
    {
        GameObject painel = ObterPainelControlavel();
        diagnosticoCanvasTemGraphicRaycaster = false;
        diagnosticoCanvasTemTrackedDeviceRaycaster = false;
        diagnosticoTrackedDeviceRaycasterAdicionado = false;
        diagnosticoCanvasRaycastXR = string.Empty;

        if (painel == null)
            return;

        Canvas canvas = painel.GetComponent<Canvas>();
        if (canvas == null)
            return;

        diagnosticoCanvasRaycastXR = painel.name;
        diagnosticoCanvasTemGraphicRaycaster = painel.GetComponent<GraphicRaycaster>() != null;

        TrackedDeviceGraphicRaycaster raycasterXR = painel.GetComponent<TrackedDeviceGraphicRaycaster>();
        if (raycasterXR == null)
        {
            raycasterXR = painel.AddComponent<TrackedDeviceGraphicRaycaster>();
            diagnosticoTrackedDeviceRaycasterAdicionado = raycasterXR != null;
        }

        if (raycasterXR == null)
            return;

        raycasterXR.ignoreReversedGraphics = false;
        raycasterXR.checkFor2DOcclusion = false;
        raycasterXR.checkFor3DOcclusion = false;
        raycasterXR.blockingMask = ~0;
        raycasterXR.raycastTriggerInteraction = QueryTriggerInteraction.Ignore;
        raycasterXR.enabled = true;
        diagnosticoCanvasTemTrackedDeviceRaycaster = true;
    }

    private GameObject EncontrarPainelInventarioArco()
    {
        Transform raizBusca = arco != null ? arco.transform : transform;
        Canvas[] canvases = raizBusca.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] == null)
                continue;

            GameObject candidato = canvases[i].gameObject;
            if (candidato == gameObject || ObjetoEhArco(candidato))
                continue;

            string nome = candidato.name.ToLowerInvariant();
            if (nome.Contains("inventario") && nome.Contains("arco"))
                return candidato;
        }

        return null;
    }

    private GameObject ObterPainelControlavel()
    {
        if (painelRaiz != null && !ObjetoEhArco(painelRaiz))
            return painelRaiz;

        if (!ObjetoEhArco(gameObject))
            return gameObject;

        return null;
    }

    private bool PainelEstaVisivel()
    {
        GameObject painel = ObterPainelControlavel();
        if (painel == null)
            return false;

        if (painel != gameObject)
            return painel.activeSelf;

        Canvas canvas = painel.GetComponent<Canvas>();
        return canvas == null ? painel.activeSelf : canvas.enabled;
    }

    private void AplicarVisibilidadePainel(bool visivel)
    {
        GameObject painel = ObterPainelControlavel();
        painelAberto = visivel;

        if (painel == null)
            return;

        if (painel != gameObject)
        {
            painel.SetActive(visivel);
            return;
        }

        Canvas[] canvases = painel.GetComponents<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
            canvases[i].enabled = visivel;

        GraphicRaycaster[] raycasters = painel.GetComponents<GraphicRaycaster>();
        for (int i = 0; i < raycasters.Length; i++)
            raycasters[i].enabled = visivel;

        TrackedDeviceGraphicRaycaster[] raycastersXR = painel.GetComponents<TrackedDeviceGraphicRaycaster>();
        for (int i = 0; i < raycastersXR.Length; i++)
            raycastersXR[i].enabled = visivel;

        CanvasGroup grupo = painel.GetComponent<CanvasGroup>();
        if (grupo != null)
        {
            grupo.alpha = visivel ? 1f : 0f;
            grupo.interactable = visivel;
            grupo.blocksRaycasts = visivel;
        }
    }

    private void DesativarFisicaDaUISeNecessario()
    {
        diagnosticoComponentesFisicaUIDesativados = 0;
        if (!desativarFisicaDaUI)
            return;

        GameObject painel = ObterPainelControlavel();
        if (painel == null || ObjetoEhArco(painel))
            return;

        Collider[] colliders = painel.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
                continue;

            if (colliders[i].enabled)
            {
                colliders[i].enabled = false;
                diagnosticoComponentesFisicaUIDesativados++;
            }
        }

        Rigidbody[] rigidbodies = painel.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (rigidbodies[i] == null)
                continue;

            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
            diagnosticoComponentesFisicaUIDesativados++;
        }

        XRGrabInteractable[] grabs = painel.GetComponentsInChildren<XRGrabInteractable>(true);
        for (int i = 0; i < grabs.Length; i++)
        {
            if (grabs[i] != null && grabs[i].enabled)
            {
                grabs[i].enabled = false;
                diagnosticoComponentesFisicaUIDesativados++;
            }
        }
    }

    private bool ObjetoEhArco(GameObject objeto)
    {
        if (objeto == null)
            return false;

        if (arco != null && objeto == arco.gameObject)
            return true;

        return objeto.GetComponent<Arco>() != null;
    }

}
