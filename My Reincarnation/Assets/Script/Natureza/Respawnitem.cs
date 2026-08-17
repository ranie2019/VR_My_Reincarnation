using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class Respawnitem : MonoBehaviour
{
    [Header("Respawn")]
    [SerializeField] private string respawnId = "";
    [SerializeField] private bool usarNomeDoObjetoSeIdVazio = true;

    [Header("Coleta")]
    [SerializeField] private bool considerarColetadoAoEntrarNoInventario = true;
    [SerializeField] private bool considerarColetadoAoMudarParent = true;
    [SerializeField] private bool considerarColetadoAoSairDaPosicao = true;
    [SerializeField] private bool exigirQueJogadorPegueParaRespawn = true;
    [SerializeField] private float distanciaParaConsiderarColetado = 0.25f;

    [Header("Estado")]
    [SerializeField] private bool foiPegoPeloJogador;
    [SerializeField] private bool respawnSolicitado;
    [SerializeField] private bool desarmadoComoItemColetado;

    private Vector3 posicaoOriginal;
    private Quaternion rotacaoOriginal;
    private Transform parentOriginal;
    private bool baseOriginalCapturada;
    private EstadoItemInventario estadoInventarioMonitorado;
    private XRGrabInteractable grabMonitorado;

    private void Awake()
    {
        PreencherRespawnIdSeVazio();

        if (desarmadoComoItemColetado)
        {
            enabled = false;
            return;
        }

        CapturarBaseOriginal();
        AtualizarMonitorEstadoInventario();
        AtualizarMonitorGrab();
    }

    private void Reset()
    {
        PreencherRespawnIdSeVazio();
        distanciaParaConsiderarColetado = 0.25f;
        considerarColetadoAoEntrarNoInventario = true;
        considerarColetadoAoMudarParent = true;
        considerarColetadoAoSairDaPosicao = true;
        exigirQueJogadorPegueParaRespawn = true;
    }

    private void OnEnable()
    {
        if (desarmadoComoItemColetado)
        {
            enabled = false;
            return;
        }

        AtualizarMonitorEstadoInventario();
        AtualizarMonitorGrab();
    }

    private void OnDisable()
    {
        RemoverMonitorEstadoInventario();
        RemoverMonitorGrab();
    }

    private void OnValidate()
    {
        PreencherRespawnIdSeVazio();
        distanciaParaConsiderarColetado = Mathf.Max(0.01f, distanciaParaConsiderarColetado);
    }

    private void Update()
    {
        if (desarmadoComoItemColetado)
            return;

        VerificarSeFoiColetado();
    }

    private void OnTransformParentChanged()
    {
        if (desarmadoComoItemColetado)
            return;

        VerificarSeFoiColetado();
    }

    public string ObterRespawnId()
    {
        PreencherRespawnIdSeVazio();
        return respawnId;
    }

    public bool RespawnSolicitado()
    {
        return respawnSolicitado;
    }

    public bool EstaDesarmadoComoItemColetado()
    {
        return desarmadoComoItemColetado;
    }

    public void SolicitarRespawnManual()
    {
        SolicitarRespawn();
    }

    public void DesarmarComoItemColetado()
    {
        respawnSolicitado = true;
        DesarmarInstanciaColetada();
    }

    public void CapturarBaseOriginalAtual()
    {
        CapturarBaseOriginal();
    }

    public void PrepararComoRecursoDisponivelNoMundo()
    {
        desarmadoComoItemColetado = false;
        enabled = true;
        respawnSolicitado = false;
        foiPegoPeloJogador = false;

        EstadoItemInventario estado = GetComponent<EstadoItemInventario>();
        if (estado != null && estado.estaNoInventario)
            estado.Liberar();

        ItemPersistente persistente = GetComponent<ItemPersistente>();
        if (persistente != null)
            persistente.MarcarComoSoltoNaCena();

        CapturarBaseOriginal();
        AtualizarMonitorEstadoInventario();
        AtualizarMonitorGrab();
    }

    private void PreencherRespawnIdSeVazio()
    {
        if (!usarNomeDoObjetoSeIdVazio || !string.IsNullOrWhiteSpace(respawnId))
            return;

        respawnId = gameObject.name.Replace("(Clone)", string.Empty).Trim();
    }

    private void CapturarBaseOriginal()
    {
        posicaoOriginal = transform.position;
        rotacaoOriginal = transform.rotation;
        parentOriginal = transform.parent;
        baseOriginalCapturada = true;
    }

    private void VerificarSeFoiColetado()
    {
        if (desarmadoComoItemColetado || respawnSolicitado)
            return;

        if (!baseOriginalCapturada)
            CapturarBaseOriginal();

        AtualizarMonitorEstadoInventario();

        if (considerarColetadoAoEntrarNoInventario && EstaNoInventario())
        {
            SolicitarRespawn();
            return;
        }

        if (exigirQueJogadorPegueParaRespawn && !foiPegoPeloJogador)
            return;

        if (considerarColetadoAoMudarParent && transform.parent != parentOriginal)
        {
            SolicitarRespawn();
            return;
        }

        if (considerarColetadoAoSairDaPosicao &&
            Vector3.Distance(transform.position, posicaoOriginal) >= distanciaParaConsiderarColetado)
        {
            SolicitarRespawn();
        }
    }

    private bool EstaNoInventario()
    {
        EstadoItemInventario estado = GetComponent<EstadoItemInventario>();
        if (estado != null && estado.estaNoInventario)
            return true;

        ItemPersistente persistente = GetComponent<ItemPersistente>();
        return persistente != null && persistente.EstaNoInventario();
    }

    private void SolicitarRespawn()
    {
        if (desarmadoComoItemColetado || respawnSolicitado)
            return;

        respawnSolicitado = true;
        PreencherRespawnIdSeVazio();

        if (RespawnNatureza.Instancia != null && !string.IsNullOrWhiteSpace(respawnId))
        {
            RespawnNatureza.Instancia.AgendarRespawn(
                respawnId,
                posicaoOriginal,
                rotacaoOriginal);
        }

        DesarmarInstanciaColetada();
    }

    private void DesarmarInstanciaColetada()
    {
        desarmadoComoItemColetado = true;
        RemoverMonitorEstadoInventario();
        RemoverMonitorGrab();
        enabled = false;
    }

    private void AtualizarMonitorEstadoInventario()
    {
        EstadoItemInventario estadoAtual = GetComponent<EstadoItemInventario>();
        if (estadoAtual == estadoInventarioMonitorado)
            return;

        RemoverMonitorEstadoInventario();
        estadoInventarioMonitorado = estadoAtual;

        if (estadoInventarioMonitorado != null)
            estadoInventarioMonitorado.EstadoInventarioAlterado += AoEstadoInventarioAlterado;
    }

    private void RemoverMonitorEstadoInventario()
    {
        if (estadoInventarioMonitorado == null)
            return;

        estadoInventarioMonitorado.EstadoInventarioAlterado -= AoEstadoInventarioAlterado;
        estadoInventarioMonitorado = null;
    }

    private void AoEstadoInventarioAlterado(bool estaNoInventario)
    {
        if (estaNoInventario)
            VerificarSeFoiColetado();
    }

    private void AtualizarMonitorGrab()
    {
        XRGrabInteractable grabAtual = BuscarGrabInteractable();
        if (grabAtual == grabMonitorado)
            return;

        RemoverMonitorGrab();
        grabMonitorado = grabAtual;

        if (grabMonitorado != null)
            grabMonitorado.selectEntered.AddListener(AoItemSelecionado);
    }

    private void RemoverMonitorGrab()
    {
        if (grabMonitorado == null)
            return;

        grabMonitorado.selectEntered.RemoveListener(AoItemSelecionado);
        grabMonitorado = null;
    }

    private void AoItemSelecionado(SelectEnterEventArgs args)
    {
        foiPegoPeloJogador = true;
        VerificarSeFoiColetado();
    }

    private XRGrabInteractable BuscarGrabInteractable()
    {
        XRGrabInteractable grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
            return grab;

        grab = GetComponentInParent<XRGrabInteractable>();
        if (grab != null)
            return grab;

        return GetComponentInChildren<XRGrabInteractable>(true);
    }
}
