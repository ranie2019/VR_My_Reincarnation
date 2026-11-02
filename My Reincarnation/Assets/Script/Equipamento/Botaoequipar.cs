using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class BotaoEquipar : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Se vazio, tenta encontrar automaticamente subindo pelos pais.")]
    [SerializeField] private EquiparManoplas equiparManoplas;
    [SerializeField] private EquiparElmo equiparElmo;
    [SerializeField] private EquiparPeitoral equiparPeitoral;

    [Header("Confirmação")]
    [SerializeField] private InputActionReference acaoConfirmarMaoEsquerda;
    [SerializeField] private InputActionReference acaoConfirmarMaoDireita;

    private XRSimpleInteractable interactable;
    private bool botaoEmHover;

    internal void DefinirElmoDoPopup(EquiparElmo elmo)
    {
        // O popup pode ficar fora da hierarquia do equipamento enquanto acompanha a camera.
        equiparElmo = elmo;
        equiparManoplas = null;
        equiparPeitoral = null;
    }

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        if (equiparManoplas == null)
            equiparManoplas = GetComponentInParent<EquiparManoplas>(true);

        if (equiparPeitoral == null)
            equiparPeitoral = GetComponentInParent<EquiparPeitoral>(true);

        if (equiparElmo == null)
            equiparElmo = GetComponentInParent<EquiparElmo>(true);
    }

    private void OnEnable()
    {
        if (interactable == null)
            return;

        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);

        HabilitarAcao(acaoConfirmarMaoEsquerda);
        HabilitarAcao(acaoConfirmarMaoDireita);
    }

    private void OnDisable()
    {
        UsoAcaoCompartilhada.LiberarTodas(this);
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

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
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

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

        if (equiparPeitoral != null)
            equiparPeitoral.Equipar();
        else if (equiparElmo != null)
            equiparElmo.Equipar();
        else if (equiparManoplas != null)
            equiparManoplas.Equipar();
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

    private void HabilitarAcao(InputActionReference acaoRef)
    {
        if (acaoRef != null && acaoRef.action != null)
            UsoAcaoCompartilhada.Adquirir(acaoRef.action, this);
    }

    private void DesabilitarAcao(InputActionReference acaoRef)
    {
        if (acaoRef != null && acaoRef.action != null)
            UsoAcaoCompartilhada.Liberar(acaoRef.action, this);
    }
}
