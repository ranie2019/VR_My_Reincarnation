using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(Collider))]
public class BotaoRolagemInventarioVR : MonoBehaviour
{
    public enum DirecaoRolagem
    {
        Cima,
        Baixo
    }

    [SerializeField] private InventarioScrollVR inventarioScroll;
    [SerializeField] private DirecaoRolagem direcao;
    [SerializeField] private float cooldown = 0.35f;
    [SerializeField] private bool aceitarSomenteInteractorXR = true;

    private float proximoToquePermitido;

    private void Reset()
    {
        GarantirColliderTrigger();
        inventarioScroll = GetComponentInParent<InventarioScrollVR>();
    }

    private void Awake()
    {
        GarantirColliderTrigger();
        BuscarInventarioScrollSeNecessario();
    }

    private void OnValidate()
    {
        cooldown = Mathf.Max(0f, cooldown);
        GarantirColliderTrigger();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < proximoToquePermitido)
            return;

        if (aceitarSomenteInteractorXR && !ColliderEhInteractorXR(other))
            return;

        if (!BuscarInventarioScrollSeNecessario())
            return;

        proximoToquePermitido = Time.time + cooldown;

        if (direcao == DirecaoRolagem.Cima)
            inventarioScroll.RolarCima();
        else
            inventarioScroll.RolarBaixo();
    }

    private bool BuscarInventarioScrollSeNecessario()
    {
        if (inventarioScroll != null)
            return true;

        inventarioScroll = GetComponentInParent<InventarioScrollVR>();
        if (inventarioScroll != null)
            return true;

        Debug.LogWarning(
            $"[{nameof(BotaoRolagemInventarioVR)}] InventarioScrollVR nao encontrado no objeto '{name}' nem nos pais.",
            this);
        return false;
    }

    private bool ColliderEhInteractorXR(Collider other)
    {
        if (other == null)
            return false;

        return other.GetComponentInParent<XRBaseInteractor>() != null;
    }

    private void GarantirColliderTrigger()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    // Para OnTriggerEnter funcionar, o botao pode ter Rigidbody isKinematic = true,
    // ou a mao/controlador XR precisa ter Rigidbody/Collider configurados.
}
