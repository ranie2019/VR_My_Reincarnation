using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class GerenciadorEquipamentosPlayer : MonoBehaviour
{
    public static GerenciadorEquipamentosPlayer Instancia { get; private set; }

    [Header("Mãos do avatar")]
    [SerializeField] private Transform maoDireita;
    [SerializeField] private Transform maoEsquerda;
    [SerializeField] private AvatarBody avatar;
    [SerializeField] private SkinnedMeshRenderer rendererMaoDireita;
    [SerializeField] private SkinnedMeshRenderer rendererMaoEsquerda;

    [Header("Cabeça do avatar")]
    [SerializeField] private Transform cabecaAvatar;

    [Header("Inventário")]
    [SerializeField] private InventarioVR inventario;

    [Header("Luvas atuais")]
    [SerializeField] private EquiparManoplas luvaDireita;
    [SerializeField] private EquiparManoplas luvaEsquerda;

    [Header("Elmo atual")]
    [SerializeField] private EquiparElmo elmoAtual;

    public EquiparManoplas LuvaDireita => luvaDireita;
    public EquiparManoplas LuvaEsquerda => luvaEsquerda;
    public EquiparElmo ElmoAtual => elmoAtual;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            enabled = false;
            return;
        }

        Instancia = this;
        ResolverReferencias();
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }

    public bool TentarEquipar(EquiparManoplas novaLuva)
    {
        if (novaLuva == null)
            return false;

        ResolverReferencias();

        bool direita = novaLuva.LadoMao == LadoMaoEquipamento.Direita;
        Transform maoDestino = direita ? maoDireita : maoEsquerda;
        SkinnedMeshRenderer rendererDestino = direita ? rendererMaoDireita : rendererMaoEsquerda;
        EquiparManoplas luvaAtual = direita ? luvaDireita : luvaEsquerda;

        if (maoDestino == null)
            maoDestino = novaLuva.TransformMaoConfigurada;

        if (rendererDestino == null)
            rendererDestino = novaLuva.RendererMao;

        if (rendererDestino == null && luvaAtual != null)
            rendererDestino = luvaAtual.RendererMao;

        if (rendererDestino == null)
            rendererDestino = EncontrarRendererConfigurado(novaLuva.LadoMao);

        if (!novaLuva.PodeEquiparNaMao(maoDestino))
            return false;

        if (luvaAtual == novaLuva)
            return true;

        XRGrabInteractable grabNova = novaLuva.GetComponent<XRGrabInteractable>();
        EstadoItemInventario estadoNovo = grabNova != null
            ? grabNova.GetComponent<EstadoItemInventario>()
            : null;
        SlotInventario slotOrigem = estadoNovo != null && estadoNovo.estaNoInventario
            ? estadoNovo.slotAtual
            : null;

        if (estadoNovo != null && estadoNovo.estaNoInventario &&
            (slotOrigem == null || !slotOrigem.PodeRetirarItemParaEquipamento(grabNova)))
        {
            Debug.LogWarning("[GerenciadorEquipamentosPlayer] A luva precisa estar no topo do slot para ser equipada.", novaLuva);
            return false;
        }

        if (slotOrigem != null && !slotOrigem.RetirarItemParaEquipamento(grabNova))
        {
            Debug.LogWarning("[GerenciadorEquipamentosPlayer] Não foi possível retirar a luva do inventário.", novaLuva);
            return false;
        }

        if (luvaAtual != null && !TentarGuardarLuvaAtual(luvaAtual, maoDestino))
        {
            if (slotOrigem != null && slotOrigem.RestaurarItemSalvoNoSlot(grabNova, false))
                slotOrigem.FinalizarRestauracaoDoSave();
            return false;
        }

        if (!novaLuva.EquiparNaMao(maoDestino, rendererDestino))
            return false;

        if (direita)
            luvaDireita = novaLuva;
        else
            luvaEsquerda = novaLuva;

        return true;
    }

    public bool Desequipar(LadoMaoEquipamento lado)
    {
        EquiparManoplas luva = lado == LadoMaoEquipamento.Direita ? luvaDireita : luvaEsquerda;
        Transform mao = lado == LadoMaoEquipamento.Direita ? maoDireita : maoEsquerda;
        if (luva == null)
            return true;

        if (!TentarGuardarLuvaAtual(luva, mao))
            return false;

        if (lado == LadoMaoEquipamento.Direita)
            luvaDireita = null;
        else
            luvaEsquerda = null;

        return true;
    }

    public bool TentarEquipar(EquiparElmo novoElmo)
    {
        if (novoElmo == null)
            return false;

        ResolverReferencias();
        Transform cabecaDestino = cabecaAvatar != null ? cabecaAvatar : novoElmo.CabecaConfigurada;
        if (!novoElmo.PodeEquiparNaCabeca(cabecaDestino))
            return false;

        if (elmoAtual == novoElmo)
            return true;

        XRGrabInteractable grabNovo = novoElmo.GetComponent<XRGrabInteractable>();
        EstadoItemInventario estadoNovo = grabNovo != null
            ? grabNovo.GetComponent<EstadoItemInventario>()
            : null;
        SlotInventario slotOrigem = estadoNovo != null && estadoNovo.estaNoInventario
            ? estadoNovo.slotAtual
            : null;

        if (estadoNovo != null && estadoNovo.estaNoInventario &&
            (slotOrigem == null || !slotOrigem.PodeRetirarItemParaEquipamento(grabNovo)))
        {
            Debug.LogWarning("[GerenciadorEquipamentosPlayer] O elmo precisa estar no topo do slot para ser equipado.", novoElmo);
            return false;
        }

        if (slotOrigem != null && !slotOrigem.RetirarItemParaEquipamento(grabNovo))
        {
            Debug.LogWarning("[GerenciadorEquipamentosPlayer] Não foi possível retirar o elmo do inventário.", novoElmo);
            return false;
        }

        if (elmoAtual != null && !TentarGuardarElmoAtual(elmoAtual, cabecaDestino))
        {
            if (slotOrigem != null && slotOrigem.RestaurarItemSalvoNoSlot(grabNovo, false))
                slotOrigem.FinalizarRestauracaoDoSave();
            return false;
        }

        if (!novoElmo.EquiparNaCabeca(cabecaDestino))
            return false;

        elmoAtual = novoElmo;
        return true;
    }

    public bool DesequiparElmo()
    {
        if (elmoAtual == null)
            return true;

        if (!TentarGuardarElmoAtual(elmoAtual, cabecaAvatar))
            return false;

        elmoAtual = null;
        return true;
    }

    private bool TentarGuardarElmoAtual(EquiparElmo elmo, Transform cabeca)
    {
        if (elmo == null)
            return true;

        XRGrabInteractable grab = elmo.GetComponent<XRGrabInteractable>();
        if (grab == null || inventario == null)
        {
            Debug.LogWarning("[GerenciadorEquipamentosPlayer] Não foi possível devolver o elmo: inventário ou XRGrabInteractable ausente.", elmo);
            return false;
        }

        elmo.Desequipar();
        SlotInventario[] slots = inventario.ObterSlotsParaSave();
        for (int i = 0; i < slots.Length; i++)
        {
            SlotInventario slot = slots[i];
            if (slot == null || !slot.RestaurarItemSalvoNoSlot(grab, false))
                continue;

            slot.FinalizarRestauracaoDoSave();
            return true;
        }

        Transform cabecaRestaurada = cabeca != null ? cabeca : elmo.CabecaConfigurada;
        if (cabecaRestaurada != null)
            elmo.EquiparNaCabeca(cabecaRestaurada);

        Debug.LogWarning("[GerenciadorEquipamentosPlayer] Inventário sem espaço para guardar o elmo.", elmo);
        return false;
    }

    private bool TentarGuardarLuvaAtual(EquiparManoplas luva, Transform mao)
    {
        if (luva == null)
            return true;

        XRGrabInteractable grab = luva.GetComponent<XRGrabInteractable>();
        if (grab == null || inventario == null)
        {
            Debug.LogWarning("[GerenciadorEquipamentosPlayer] Não foi possível devolver a luva: inventário ou XRGrabInteractable ausente.", luva);
            return false;
        }

        luva.Desequipar();
        SlotInventario[] slots = inventario.ObterSlotsParaSave();
        for (int i = 0; i < slots.Length; i++)
        {
            SlotInventario slot = slots[i];
            if (slot == null || !slot.RestaurarItemSalvoNoSlot(grab, false))
                continue;

            slot.FinalizarRestauracaoDoSave();
            return true;
        }

        // Mantém a luva equipada se o inventário não tiver espaço compatível.
        if (mao == null)
            mao = luva.TransformMaoConfigurada;

        if (mao != null)
            luva.EquiparNaMao(mao);

        Debug.LogWarning("[GerenciadorEquipamentosPlayer] Inventário sem espaço para guardar a luva antiga.", luva);
        return false;
    }

    private void ResolverReferencias()
    {
        if (avatar == null)
            avatar = GetComponentInChildren<AvatarBody>(true);

        if (avatar == null)
            avatar = FindFirstObjectByType<AvatarBody>();

        if (cabecaAvatar == null && avatar != null)
            cabecaAvatar = avatar.cabecaAvatar;

        if (maoEsquerda == null && avatar != null)
            maoEsquerda = avatar.maoEsquerdaAvatar;

        if (maoDireita == null && avatar != null)
            maoDireita = avatar.maoDireitaAvatar;

        if (inventario == null)
            inventario = GetComponentInChildren<InventarioVR>(true);

        if (inventario == null)
            inventario = FindFirstObjectByType<InventarioVR>();
    }

    private static SkinnedMeshRenderer EncontrarRendererConfigurado(LadoMaoEquipamento lado)
    {
        EquiparManoplas[] luvas = FindObjectsByType<EquiparManoplas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < luvas.Length; i++)
        {
            if (luvas[i] != null && luvas[i].LadoMao == lado && luvas[i].RendererMao != null)
                return luvas[i].RendererMao;
        }

        return null;
    }
}
