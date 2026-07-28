using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Fica em cada botão "Create".
///
/// Responsabilidade deste script:
/// - Detectar hover do laser (XRSimpleInteractable).
/// - Com o laser em cima do botão, esperar o jogador apertar o botão do
///   controle (mão esquerda OU direita) pra confirmar.
/// - Ao confirmar, perguntar pro CriarItem (único na cena) se este botão
///   específico tem recurso suficiente, mandar consumir, e instanciar o
///   prefab no ponto de spawn.
///
/// Requisito: o objeto precisa ter um Collider (BoxCollider, Is Trigger) do tamanho do botao.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class BotaoCriarItem : MonoBehaviour
{
    [Header("Item a ser criado")]
    [Tooltip("Prefab que será instanciado quando a criação for confirmada.")]
    [SerializeField] private GameObject prefab;

    [Tooltip("Ponto fixo (ex: bancada do Ferreiro) onde o prefab criado será instanciado.")]
    [SerializeField] private Transform pontoSpawn;

    [Header("Referências")]
    [Tooltip("Se vazio, tenta encontrar automaticamente na cena.")]
    [SerializeField] private CriarItem criarItem;

    [Tooltip("Se vazio, pega o Button do próprio objeto.")]
    [SerializeField] private Button botaoUI;

    [Header("Confirmação (Hover + Botão do Controle)")]
    [SerializeField] private InputActionReference acaoConfirmarMaoEsquerda;
    [SerializeField] private InputActionReference acaoConfirmarMaoDireita;

    private XRSimpleInteractable interactable;
    private bool botaoEmHover;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        if (criarItem == null)
            criarItem = FindFirstObjectByType<CriarItem>();

        if (botaoUI == null)
            botaoUI = GetComponent<Button>();
    }

    private void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);

        HabilitarAcao(acaoConfirmarMaoEsquerda);
        HabilitarAcao(acaoConfirmarMaoDireita);
    }

    private void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);

        DesabilitarAcao(acaoConfirmarMaoEsquerda);
        DesabilitarAcao(acaoConfirmarMaoDireita);

        botaoEmHover = false;
    }

    private void Update()
    {
        VerificarConfirmacao();
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        botaoEmHover = true;
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        botaoEmHover = false;
    }

    private void VerificarConfirmacao()
    {
        if (!botaoEmHover)
            return;

        if (!BotaoConfirmarPressionado())
            return;

        TentarCriarItem();
    }

    private void TentarCriarItem()
    {
        if (criarItem == null || botaoUI == null || prefab == null)
        {
            Debug.LogWarning("[BotaoCriarItem] Faltando CriarItem, Button ou Prefab em: " + gameObject.name);
            return;
        }

        if (!criarItem.PossuiRecursosSuficientes(botaoUI))
            return;

        if (!criarItem.ConsumirRecursos(botaoUI))
            return;

        Vector3 posicao = pontoSpawn != null ? pontoSpawn.position : transform.position;
        Quaternion rotacao = pontoSpawn != null ? pontoSpawn.rotation : Quaternion.identity;

        Instantiate(prefab, posicao, rotacao);
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
}