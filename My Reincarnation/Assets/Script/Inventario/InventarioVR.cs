using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class InventarioVR : MonoBehaviour
{
    [SerializeField] private XRInputButtonReader botaoInventario =
        new XRInputButtonReader("Inventario");

    [SerializeField] private GameObject painelInventario;
    [SerializeField] private Transform inventarioRoot;
    [SerializeField] private float yInventarioFechado = -100f;
    [SerializeField] private SlotInventario[] slots;
    [SerializeField] private Collider[] collidersBotoesRolagem;
    [SerializeField] private BotaoRolagemInventarioVR[] botoesRolagem;

    private bool aberto = false;
    private readonly Dictionary<Collider, bool> estadosColliders = new();
    private readonly Dictionary<XRSocketInteractor, bool> estadosSockets = new();
    private readonly Dictionary<XRGrabInteractable, bool> estadosGrabInteractables = new();
    private readonly Dictionary<Rigidbody, EstadoRigidbodyInventario> estadosRigidbodies = new();
    private Vector3 posicaoInventarioAberto;
    private Collider[] collidersInventario;
    private bool posicaoInventarioCapturada;

    private void Awake()
    {
        CapturarConfiguracaoInventario();
    }

    private void OnEnable()
    {
        botaoInventario.EnableDirectActionIfModeUsed();
    }

    private void OnDisable()
    {
        botaoInventario.DisableDirectActionIfModeUsed();

        if (painelInventario != null)
        {
            aberto = false;
            AtualizarSlots(false);
            SetPainelVisivel(false);
            AtualizarBotoesRolagem(false);
            DefinirFisicaInventario(false);
            PosicionarInventarioFechado();
        }
    }

    private void Start()
    {
        CapturarConfiguracaoInventario();
        SetEstado(false);
        StartCoroutine(ReaplicarFisicaFechadaAposInicializacao());
    }

    private void Update()
    {
        if (botaoInventario.ReadWasPerformedThisFrame())
        {
            SetEstado(!aberto);
        }

        // Apenas para testar no Editor com teclado
        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            SetEstado(!aberto);
        }
    }

    private void SetEstado(bool estado)
    {
        aberto = estado;

        if (painelInventario != null && !painelInventario.activeSelf)
            painelInventario.SetActive(true);

        if (aberto)
        {
            RestaurarPosicaoInventarioAberto();
            DefinirFisicaInventario(true);
            AtualizarBotoesRolagem(true);
            SetPainelVisivel(true);
            AtualizarSlots(true);
            return;
        }

        AtualizarSlots(false);
        SetPainelVisivel(false);
        AtualizarBotoesRolagem(false);
        DefinirFisicaInventario(false);
        PosicionarInventarioFechado();
    }

    private IEnumerator ReaplicarFisicaFechadaAposInicializacao()
    {
        // O save pode restaurar itens nos slots depois do Start deste componente.
        // Reaplicar por poucos frames inclui esses objetos sem manter busca por frame em VR.
        const int framesVerificacao = 5;
        for (int i = 0; i < framesVerificacao; i++)
        {
            yield return null;

            if (!aberto)
                DefinirFisicaInventario(false);
        }
    }

    private void DefinirFisicaInventario(bool ativo)
    {
        GameObject raizFisica = ObterRaizFisicaInventario();
        if (raizFisica == null)
            return;

        collidersInventario = raizFisica.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < collidersInventario.Length; i++)
        {
            Collider colliderAtual = collidersInventario[i];
            if (colliderAtual == null)
                continue;

            if (ativo)
            {
                if (estadosColliders.TryGetValue(colliderAtual, out bool estavaAtivo))
                    colliderAtual.enabled = estavaAtivo;
            }
            else
            {
                if (!estadosColliders.ContainsKey(colliderAtual))
                    estadosColliders.Add(colliderAtual, colliderAtual.enabled);

                colliderAtual.enabled = false;
            }
        }

        XRSocketInteractor[] sockets = raizFisica.GetComponentsInChildren<XRSocketInteractor>(true);
        for (int i = 0; i < sockets.Length; i++)
        {
            XRSocketInteractor socketAtual = sockets[i];
            if (socketAtual == null)
                continue;

            if (ativo)
            {
                if (estadosSockets.TryGetValue(socketAtual, out bool estavaAtivo))
                    socketAtual.enabled = estavaAtivo;
            }
            else
            {
                if (!estadosSockets.ContainsKey(socketAtual))
                    estadosSockets.Add(socketAtual, socketAtual.enabled);

                socketAtual.enabled = false;
            }
        }

        XRGrabInteractable[] grabs = raizFisica.GetComponentsInChildren<XRGrabInteractable>(true);
        for (int i = 0; i < grabs.Length; i++)
        {
            XRGrabInteractable grabAtual = grabs[i];
            if (grabAtual == null || EhItemGuardadoNoInventario(grabAtual))
                continue;

            if (ativo)
            {
                if (estadosGrabInteractables.TryGetValue(grabAtual, out bool estavaAtivo))
                    grabAtual.enabled = estavaAtivo;
            }
            else
            {
                if (!estadosGrabInteractables.ContainsKey(grabAtual))
                    estadosGrabInteractables.Add(grabAtual, grabAtual.enabled);

                grabAtual.enabled = false;
            }
        }

        Rigidbody[] rigidbodies = raizFisica.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rigidbodyAtual = rigidbodies[i];
            if (rigidbodyAtual == null)
                continue;

            if (ativo)
            {
                if (estadosRigidbodies.TryGetValue(rigidbodyAtual, out EstadoRigidbodyInventario estado))
                {
                    rigidbodyAtual.isKinematic = estado.IsKinematic;
                    rigidbodyAtual.detectCollisions = estado.DetectCollisions;
                }
            }
            else
            {
                if (!estadosRigidbodies.ContainsKey(rigidbodyAtual))
                {
                    estadosRigidbodies.Add(
                        rigidbodyAtual,
                        new EstadoRigidbodyInventario(rigidbodyAtual.isKinematic, rigidbodyAtual.detectCollisions));
                }

                rigidbodyAtual.isKinematic = true;
                rigidbodyAtual.detectCollisions = false;
            }
        }

        if (ativo)
        {
            estadosColliders.Clear();
            estadosSockets.Clear();
            estadosGrabInteractables.Clear();
            estadosRigidbodies.Clear();
        }

        Physics.SyncTransforms();
    }

    private void CapturarConfiguracaoInventario()
    {
        if (inventarioRoot == null && painelInventario != null)
            inventarioRoot = painelInventario.transform;

        if (inventarioRoot == null)
            return;

        if (!posicaoInventarioCapturada)
        {
            posicaoInventarioAberto = inventarioRoot.localPosition;
            posicaoInventarioCapturada = true;
        }

        collidersInventario = inventarioRoot.GetComponentsInChildren<Collider>(true);
    }

    private GameObject ObterRaizFisicaInventario()
    {
        if (inventarioRoot != null)
            return inventarioRoot.gameObject;

        return painelInventario;
    }

    private void PosicionarInventarioFechado()
    {
        if (inventarioRoot == null || !posicaoInventarioCapturada)
            return;

        inventarioRoot.localPosition = new Vector3(
            posicaoInventarioAberto.x,
            yInventarioFechado,
            posicaoInventarioAberto.z);
    }

    private void RestaurarPosicaoInventarioAberto()
    {
        if (inventarioRoot == null || !posicaoInventarioCapturada)
            return;

        inventarioRoot.localPosition = posicaoInventarioAberto;
    }

    private static bool EhItemGuardadoNoInventario(XRGrabInteractable grabInteractable)
    {
        if (grabInteractable == null)
            return false;

        EstadoItemInventario estado = grabInteractable.GetComponent<EstadoItemInventario>();
        return estado != null && estado.estaNoInventario;
    }

    private void SetPainelVisivel(bool visivel)
    {
        if (painelInventario == null)
            return;

        foreach (var canvas in painelInventario.GetComponentsInChildren<Canvas>(true))
            canvas.enabled = visivel;

        foreach (var graphic in painelInventario.GetComponentsInChildren<Graphic>(true))
            graphic.enabled = visivel;

        foreach (var renderer in painelInventario.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = visivel;
    }

    private void AtualizarSlots(bool inventarioAberto)
    {
        if (slots == null)
            return;

        foreach (var slot in slots)
        {
            if (slot != null)
                slot.SetInventarioAberto(inventarioAberto);
        }
    }

    private void AtualizarBotoesRolagem(bool ativo)
    {
        if (collidersBotoesRolagem != null)
        {
            foreach (var colliderBotao in collidersBotoesRolagem)
            {
                if (colliderBotao != null)
                    colliderBotao.enabled = ativo;
            }
        }

        if (botoesRolagem == null)
            return;

        foreach (var botaoRolagem in botoesRolagem)
        {
            if (botaoRolagem != null)
                botaoRolagem.SetBotaoAtivo(ativo);
        }
    }

    private readonly struct EstadoRigidbodyInventario
    {
        public readonly bool IsKinematic;
        public readonly bool DetectCollisions;

        public EstadoRigidbodyInventario(bool isKinematic, bool detectCollisions)
        {
            IsKinematic = isKinematic;
            DetectCollisions = detectCollisions;
        }
    }
}
