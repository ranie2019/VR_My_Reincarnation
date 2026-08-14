using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Ativa um popup de interação quando o laser do controle VR (XR Ray Interactor)
/// passa sobre o NPC. O popup contém um botão "Yes" que abre o Inventário do
/// Ferreiro, e um botão X para fechar.
///
/// O botão "Yes" é filho do Canvas popup, então aparece automaticamente
/// junto com o popup - não depende de nenhuma função estar associada a ele.
/// A confirmação (hover + botão do controle) só decide QUANDO a ação de
/// abrir o inventário acontece.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class NPCPopup : MonoBehaviour
{
    [Header("Referências do Popup")]
    [Tooltip("Canvas do popup, deve começar desativado na cena.")]
    [SerializeField] private GameObject popupCanvas;

    [Tooltip("Canvas do Inventário do Ferreiro, deve começar desativado na cena.")]
    [SerializeField] private GameObject inventarioFerreiroCanvas;

    [Header("Confirmação do Botão Yes (Hover + Botão do Controle)")]
    [Tooltip("Ação do controle esquerdo que confirma o botão em hover.")]
    [SerializeField] private InputActionReference acaoConfirmarMaoEsquerda;

    [Tooltip("Ação do controle direito que confirma o botão em hover.")]
    [SerializeField] private InputActionReference acaoConfirmarMaoDireita;

    [Header("Configurações")]
    [Tooltip("Tempo (em segundos) sem interação até o popup/inventário fecharem sozinhos.")]
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
    /// Chamado pelo BotaoYesHover quando o laser entra/sai do botão Yes.
    /// </summary>
    public void DefinirBotaoYesEmHover(bool emHover)
    {
        botaoYesEmHover = emHover;
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        Debug.Log("[NPCPopup] Hover ENTROU no NPC. PopupAtivo antes: " + (popupCanvas != null && popupCanvas.activeSelf) +
                   " | InventarioAtivo: " + InventarioEstaAberto());
        AbrirPopup();
    }

    /// <summary>
    /// Abre o popup de interação com o NPC.
    /// Não abre se o Inventário do Ferreiro já estiver ativo.
    /// </summary>
    public void AbrirPopup()
    {
        if (InventarioEstaAberto())
        {
            Debug.Log("[NPCPopup] AbrirPopup bloqueado: inventário ainda está ativo.");
            return;
        }

        if (popupCanvas != null && !popupCanvas.activeSelf)
        {
            popupCanvas.SetActive(true);
            ultimaInteracaoPopup = Time.time;
            Debug.Log("[NPCPopup] Popup ABERTO com sucesso.");
        }
        else
        {
            Debug.Log("[NPCPopup] Popup NÃO abriu. popupCanvas nulo? " + (popupCanvas == null) +
                       " | já estava ativo? " + (popupCanvas != null && popupCanvas.activeSelf));
        }
    }

    private bool InventarioEstaAberto()
    {
        return inventarioFerreiroCanvas != null && inventarioFerreiroCanvas.activeSelf;
    }

    /// <summary>
    /// Fecha o popup. Chamado pelo botão X.
    /// </summary>
    public void FecharPopup()
    {
        if (popupCanvas != null && popupCanvas.activeSelf)
            popupCanvas.SetActive(false);
    }

    /// <summary>
    /// Fecha o Canvas do Inventário do Ferreiro. Chamado pelo botão X do inventário.
    /// </summary>
    public void FecharInventario()
    {
        if (inventarioFerreiroCanvas != null && inventarioFerreiroCanvas.activeSelf)
            inventarioFerreiroCanvas.SetActive(false);
    }

    /// <summary>
    /// Chamado pelo botão "Yes" do popup. Fecha o popup e abre
    /// o Canvas do Inventário do Ferreiro.
    /// </summary>
    public void AbrirInventario()
    {
        FecharPopup();

        if (inventarioFerreiroCanvas != null)
            inventarioFerreiroCanvas.SetActive(true);
    }
}