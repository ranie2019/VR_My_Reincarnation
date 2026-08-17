using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Ativa um popup de intera��o quando o laser do controle VR (XR Ray Interactor)
/// passa sobre o NPC. O popup cont�m um bot�o "Yes" que abre o Invent�rio do
/// Ferreiro, e um bot�o X para fechar.
///
/// O bot�o "Yes" � filho do Canvas popup, ent�o aparece automaticamente
/// junto com o popup - n�o depende de nenhuma fun��o estar associada a ele.
/// A confirma��o (hover + bot�o do controle) s� decide QUANDO a a��o de
/// abrir o invent�rio acontece.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class NPCPopup : MonoBehaviour
{
    [Header("Refer�ncias do Popup")]
    [Tooltip("Canvas do popup, deve come�ar desativado na cena.")]
    [SerializeField] private GameObject popupCanvas;

    [Tooltip("Canvas do Invent�rio do Ferreiro, deve come�ar desativado na cena.")]
    [SerializeField] private GameObject inventarioFerreiroCanvas;

    [Header("Confirma��o do Bot�o Yes (Hover + Bot�o do Controle)")]
    [Tooltip("A��o do controle esquerdo que confirma o bot�o em hover.")]
    [SerializeField] private InputActionReference acaoConfirmarMaoEsquerda;

    [Tooltip("A��o do controle direito que confirma o bot�o em hover.")]
    [SerializeField] private InputActionReference acaoConfirmarMaoDireita;

    [Header("Configura��es")]
    [Tooltip("Tempo (em segundos) sem intera��o at� o popup/invent�rio fecharem sozinhos.")]
    [SerializeField] private float tempoParaFecharPorInatividade = 30f;

    private XRSimpleInteractable interactable;
    private bool botaoYesEmHover;
    private float ultimaInteracaoPopup;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        if (popupCanvas != null)
            popupCanvas.SetActive(false);

        if (inventarioFerreiroCanvas != null)
            inventarioFerreiroCanvas.SetActive(false);
    }

    private void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);

        HabilitarAcao(acaoConfirmarMaoEsquerda);
        HabilitarAcao(acaoConfirmarMaoDireita);
    }

    private void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);

        DesabilitarAcao(acaoConfirmarMaoEsquerda);
        DesabilitarAcao(acaoConfirmarMaoDireita);
    }

    private static void HabilitarAcao(InputActionReference acaoRef)
    {
        if (acaoRef != null && acaoRef.action != null)
            acaoRef.action.Enable();
    }

    private static void DesabilitarAcao(InputActionReference acaoRef)
    {
        if (acaoRef != null && acaoRef.action != null)
            acaoRef.action.Disable();
    }

    private void Update()
    {
        VerificarConfirmacaoBotaoYes();
        VerificarFechamentoPorInatividade();
    }

    private void VerificarFechamentoPorInatividade()
    {
        if (popupCanvas == null || !popupCanvas.activeSelf)
            return;

        if (botaoYesEmHover)
            ultimaInteracaoPopup = Time.time;

        if (Time.time - ultimaInteracaoPopup >= tempoParaFecharPorInatividade)
            FecharPopup();
    }

    private void VerificarConfirmacaoBotaoYes()
    {
        if (!botaoYesEmHover)
            return;

        if (!BotaoConfirmarPressionado())
            return;

        AbrirInventario();
    }

    private bool BotaoConfirmarPressionado()
    {
        return AcaoFoiPressionada(acaoConfirmarMaoEsquerda) ||
               AcaoFoiPressionada(acaoConfirmarMaoDireita);
    }

    private static bool AcaoFoiPressionada(InputActionReference acaoRef)
    {
        if (acaoRef == null || acaoRef.action == null)
            return false;

        try
        {
            return acaoRef.action.WasPressedThisFrame();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Chamado pelo BotaoYesHover quando o laser entra/sai do bot�o Yes.
    /// </summary>
    public void DefinirBotaoYesEmHover(bool emHover)
    {
        botaoYesEmHover = emHover;
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        AbrirPopup();
    }

    /// <summary>
    /// Abre o popup de intera��o com o NPC.
    /// N�o abre se o Invent�rio do Ferreiro j� estiver ativo.
    /// </summary>
    public void AbrirPopup()
    {
        if (InventarioEstaAberto())
        {
            return;
        }

        if (popupCanvas != null && !popupCanvas.activeSelf)
        {
            popupCanvas.SetActive(true);
            ultimaInteracaoPopup = Time.time;
        }
    }

    private bool InventarioEstaAberto()
    {
        return inventarioFerreiroCanvas != null && inventarioFerreiroCanvas.activeSelf;
    }

    /// <summary>
    /// Fecha o popup. Chamado pelo bot�o X.
    /// </summary>
    public void FecharPopup()
    {
        if (popupCanvas != null && popupCanvas.activeSelf)
            popupCanvas.SetActive(false);
    }

    /// <summary>
    /// Fecha o Canvas do Invent�rio do Ferreiro. Chamado pelo bot�o X do invent�rio.
    /// </summary>
    public void FecharInventario()
    {
        if (inventarioFerreiroCanvas != null && inventarioFerreiroCanvas.activeSelf)
            inventarioFerreiroCanvas.SetActive(false);
    }

    /// <summary>
    /// Chamado pelo bot�o "Yes" do popup. Fecha o popup e abre
    /// o Canvas do Invent�rio do Ferreiro.
    /// </summary>
    public void AbrirInventario()
    {
        FecharPopup();

        if (inventarioFerreiroCanvas != null)
            inventarioFerreiroCanvas.SetActive(true);
    }
}