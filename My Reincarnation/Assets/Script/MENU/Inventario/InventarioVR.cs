using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class InventarioVR : MonoBehaviour
{
    [SerializeField] private GameObject painelInventario;
    [SerializeField] private Transform inventarioRoot;

    [SerializeField] private SlotInventario[] slots;
    [SerializeField] private Collider[] collidersBotoesRolagem;
    [SerializeField] private BotaoRolagemInventarioVR[] botoesRolagem;

    private bool aberto = false;
    private readonly Dictionary<Collider, bool> estadosColliders = new();
    private readonly Dictionary<XRSocketInteractor, bool> estadosSockets = new();
    private readonly Dictionary<XRGrabInteractable, bool> estadosGrabInteractables = new();
    private readonly Dictionary<Rigidbody, EstadoRigidbodyInventario> estadosRigidbodies = new();

    private Collider[] collidersInventario;

    private void Awake()
    {
        if (inventarioRoot == null && painelInventario != null)
            inventarioRoot = painelInventario.transform;

        if (inventarioRoot != null)
            collidersInventario = inventarioRoot.GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        // Começa fechado (MyMenu controla a abertura)
        SetEstado(false);
        StartCoroutine(ReaplicarFisicaFechadaAposInicializacao());
    }

    // ─── API PÚBLICA (MyMenu deve chamar estas) ──────────────────────────────

    public void AbrirInventario() => SetEstado(true);
    public void FecharInventario() => SetEstado(false);
    public void AlternarInventario() => SetEstado(!aberto);
    public bool EstaAberto() => aberto;

    public bool ControlaPainel(GameObject painel)
    {
        if (painel == null || painelInventario == null)
            return false;

        return painel == painelInventario;
    }

    // ─── LÓGICA INTERNA ───────────────────────────────────────────────────────

    private void SetEstado(bool estado)
    {
        aberto = estado;

        if (aberto)
        {
            if (painelInventario != null)
                painelInventario.SetActive(true);

            SetFilhosAtivos(true);
            DefinirFisicaInventario(true);
            AtualizarBotoesRolagem(true);
            AtualizarSlots(true);
            return;
        }

        // Fechando
        AtualizarSlots(false);
        SetFilhosAtivos(false);
        AtualizarBotoesRolagem(false);
        DefinirFisicaInventario(false);

        // NÃO move mais a posição — o painel fica onde está no editor
    }

    private void SetFilhosAtivos(bool ativo)
    {
        if (painelInventario == null)
            return;

        // 1. Ativa/desativa os GameObjects filhos primeiro (inclui Titulo)
        Transform[] filhos = painelInventario.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < filhos.Length; i++)
        {
            if (filhos[i] == null || filhos[i] == painelInventario.transform) continue;
            if (ComponentePertenceAItemInventario(filhos[i])) continue;
            if (filhos[i].GetComponent<SlotInventario>() != null) continue;

            filhos[i].gameObject.SetActive(ativo);
        }

        // 2. Garante componentes visuais ligados
        Canvas[] canvases = painelInventario.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] == null) continue;
            if (ComponentePertenceAItemInventario(canvases[i])) continue;
            canvases[i].enabled = ativo;
        }

        Graphic[] graphics = painelInventario.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null) continue;
            if (ComponentePertenceAItemInventario(graphics[i])) continue;
            graphics[i].enabled = ativo;
        }

        Renderer[] renderers = painelInventario.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (ComponentePertenceAItemInventario(renderers[i])) continue;
            renderers[i].enabled = ativo;
        }
    }

    private IEnumerator ReaplicarFisicaFechadaAposInicializacao()
    {
        for (int i = 0; i < 5; i++)
        {
            yield return null;
            if (!aberto)
                DefinirFisicaInventario(false);
        }
    }

    private void DefinirFisicaInventario(bool ativo)
    {
        GameObject raizFisica = inventarioRoot != null
            ? inventarioRoot.gameObject
            : painelInventario;

        if (raizFisica == null)
            return;

        if (inventarioRoot != null)
            collidersInventario = raizFisica.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < (collidersInventario?.Length ?? 0); i++)
        {
            Collider c = collidersInventario[i];
            if (c == null || ComponentePertenceAItemInventario(c)) continue;

            if (ativo)
            {
                if (estadosColliders.TryGetValue(c, out bool era)) c.enabled = era;
            }
            else
            {
                if (!estadosColliders.ContainsKey(c)) estadosColliders.Add(c, c.enabled);
                c.enabled = false;
            }
        }

        XRSocketInteractor[] sockets = raizFisica.GetComponentsInChildren<XRSocketInteractor>(true);
        for (int i = 0; i < sockets.Length; i++)
        {
            XRSocketInteractor s = sockets[i];
            if (s == null) continue;

            if (ativo)
            {
                if (estadosSockets.TryGetValue(s, out bool era)) s.enabled = era;
            }
            else
            {
                if (!estadosSockets.ContainsKey(s)) estadosSockets.Add(s, s.enabled);
                s.enabled = false;
            }
        }

        XRGrabInteractable[] grabs = raizFisica.GetComponentsInChildren<XRGrabInteractable>(true);
        for (int i = 0; i < grabs.Length; i++)
        {
            XRGrabInteractable g = grabs[i];
            if (g == null || EhItemGuardadoNoInventario(g)) continue;

            if (ativo)
            {
                if (estadosGrabInteractables.TryGetValue(g, out bool era)) g.enabled = era;
            }
            else
            {
                if (!estadosGrabInteractables.ContainsKey(g)) estadosGrabInteractables.Add(g, g.enabled);
                g.enabled = false;
            }
        }

        Rigidbody[] rbs = raizFisica.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            Rigidbody rb = rbs[i];
            if (rb == null || ComponentePertenceAItemInventario(rb)) continue;

            if (ativo)
            {
                if (estadosRigidbodies.TryGetValue(rb, out EstadoRigidbodyInventario est))
                {
                    rb.isKinematic = est.IsKinematic;
                    rb.detectCollisions = est.DetectCollisions;
                }
            }
            else
            {
                if (!estadosRigidbodies.ContainsKey(rb))
                    estadosRigidbodies.Add(rb, new EstadoRigidbodyInventario(rb.isKinematic, rb.detectCollisions));
                rb.isKinematic = true;
                rb.detectCollisions = false;
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

    private static bool EhItemGuardadoNoInventario(XRGrabInteractable g)
    {
        if (g == null) return false;
        EstadoItemInventario e = g.GetComponent<EstadoItemInventario>();
        return e != null && e.estaNoInventario;
    }

    private static bool ComponentePertenceAItemInventario(Component c)
    {
        if (c == null) return false;
        EstadoItemInventario e = c.GetComponentInParent<EstadoItemInventario>(true);
        return e != null && e.estaNoInventario;
    }

    private void AtualizarSlots(bool inventarioAberto)
    {
        if (slots == null) return;
        foreach (var slot in slots)
            if (slot != null) slot.SetInventarioAberto(inventarioAberto);
    }

    public SlotInventario[] ObterSlotsParaSave()
    {
        if (slots != null && slots.Length > 0) return slots;
        return GetComponentsInChildren<SlotInventario>(true);
    }

    private void AtualizarBotoesRolagem(bool ativo)
    {
        if (collidersBotoesRolagem != null)
            foreach (var c in collidersBotoesRolagem)
                if (c != null) c.enabled = ativo;

        if (botoesRolagem == null) return;
        foreach (var b in botoesRolagem)
            if (b != null) b.SetBotaoAtivo(ativo);
    }

    private readonly struct EstadoRigidbodyInventario
    {
        public readonly bool IsKinematic;
        public readonly bool DetectCollisions;
        public EstadoRigidbodyInventario(bool k, bool d) { IsKinematic = k; DetectCollisions = d; }
    }
}
