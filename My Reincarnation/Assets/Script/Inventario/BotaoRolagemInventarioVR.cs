using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(Collider))]
public class BotaoRolagemInventarioVR : MonoBehaviour
{
    public enum DirecaoMovimentoSlots
    {
        SubirSlots,
        DescerSlots
    }

    [SerializeField] private InventarioScrollVR inventarioScroll;
    [SerializeField, FormerlySerializedAs("direcao")] private DirecaoMovimentoSlots direcaoMovimentoSlots;
    [SerializeField] private float cooldown = 0.35f;
    [SerializeField] private bool aceitarSomenteInteractorXR = true;
    [SerializeField] private bool aceitarComponentesXRRelacionados = true;
    [SerializeField] private bool procurarInteractorXRNosFilhos = true;
    [SerializeField] private bool rolarEnquantoPermaneceEmContato = true;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip somClique;

    private float proximoToquePermitido;

    private void Reset()
    {
        inventarioScroll = GetComponentInParent<InventarioScrollVR>();
    }

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        BuscarInventarioScrollSeNecessario();
    }

    private void Start()
    {
        BuscarInventarioScrollSeNecessario();
    }

    private void OnValidate()
    {
        cooldown = Mathf.Max(0f, cooldown);
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessarContato(other, true);
    }

    private void OnTriggerStay(Collider other)
    {
        ProcessarContato(other, rolarEnquantoPermaneceEmContato);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessarContato(collision != null ? collision.collider : null, true);
    }

    private void OnCollisionStay(Collision collision)
    {
        ProcessarContato(collision != null ? collision.collider : null, rolarEnquantoPermaneceEmContato);
    }

    private void ProcessarContato(Collider other, bool podeRolar)
    {
        if (!podeRolar)
            return;

        TentarExecutarRolagem(other);
    }

    private void TentarExecutarRolagem(Collider other)
    {
        if (other == null)
            return;

        if (Time.time < proximoToquePermitido)
            return;

        if (!ColliderPassaFiltroXR(other))
            return;

        if (!BuscarInventarioScrollSeNecessario())
            return;

        proximoToquePermitido = Time.time + cooldown;
        if (ExecutarRolagem())
            TocarSom(somClique);
    }

    private bool ExecutarRolagem()
    {
        if (inventarioScroll == null)
            return false;

        int linhaInicialAntes = inventarioScroll.LinhaInicial;

        if (direcaoMovimentoSlots == DirecaoMovimentoSlots.SubirSlots)
            inventarioScroll.RolarBaixo();
        else
            inventarioScroll.RolarCima();

        return inventarioScroll.LinhaInicial != linhaInicialAntes;
    }

    private void TocarSom(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private bool BuscarInventarioScrollSeNecessario()
    {
        if (inventarioScroll != null)
            return true;

        inventarioScroll = GetComponentInParent<InventarioScrollVR>();
        return inventarioScroll != null;
    }

    private bool ColliderPassaFiltroXR(Collider other)
    {
        if (!aceitarSomenteInteractorXR)
            return true;

        if (other == null)
            return false;

        if (other.GetComponentInParent<XRBaseInteractor>() != null)
            return true;

        Rigidbody attachedRigidbody = other.attachedRigidbody;
        if (attachedRigidbody != null && attachedRigidbody.GetComponentInParent<XRBaseInteractor>() != null)
            return true;

        if (procurarInteractorXRNosFilhos)
        {
            if (other.GetComponentInChildren<XRBaseInteractor>(true) != null)
                return true;

            Transform raiz = other.transform.root;
            if (raiz != null && raiz.GetComponentInChildren<XRBaseInteractor>(true) != null)
                return true;
        }

        return aceitarComponentesXRRelacionados && BuscarComponenteXRRelacionado(other) != null;
    }

    private Component BuscarComponenteXRRelacionado(Collider other)
    {
        Component componente = BuscarComponenteXRRelacionadoEmComponentes(other.GetComponentsInParent<MonoBehaviour>(true));
        if (componente != null)
            return componente;

        componente = BuscarComponenteXRRelacionadoEmComponentes(other.GetComponentsInChildren<MonoBehaviour>(true));
        if (componente != null)
            return componente;

        Rigidbody attachedRigidbody = other.attachedRigidbody;
        if (attachedRigidbody != null)
        {
            componente = BuscarComponenteXRRelacionadoEmComponentes(attachedRigidbody.GetComponentsInParent<MonoBehaviour>(true));
            if (componente != null)
                return componente;
        }

        Transform raiz = other.transform.root;
        if (raiz != null)
            componente = BuscarComponenteXRRelacionadoEmComponentes(raiz.GetComponentsInChildren<MonoBehaviour>(true));

        return componente;
    }

    private Component BuscarComponenteXRRelacionadoEmComponentes(MonoBehaviour[] componentes)
    {
        if (componentes == null)
            return null;

        for (int i = 0; i < componentes.Length; i++)
        {
            MonoBehaviour componente = componentes[i];
            if (ComponenteEhXRRelacionado(componente))
                return componente;
        }

        return null;
    }

    private bool ComponenteEhXRRelacionado(MonoBehaviour componente)
    {
        if (componente == null)
            return false;

        System.Type tipo = componente.GetType();
        string nome = tipo.Name;
        string nomeCompleto = tipo.FullName ?? nome;

        bool namespaceXR = nomeCompleto.Contains("UnityEngine.XR") || nomeCompleto.Contains("Unity.XR") || nome.Contains("XR");
        if (!namespaceXR)
            return false;

        return nome.Contains("Interactor") ||
               nome.Contains("Controller") ||
               nome.Contains("Hand") ||
               nome.Contains("Tracked") ||
               nome.Contains("Pose") ||
               nomeCompleto.Contains("Interaction.Toolkit");
    }
}
