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

    [Header("Corpo do avatar")]
    [SerializeField] private Transform corpoAvatar;

    [Header("Inventário")]
    [SerializeField] private InventarioVR inventario;

    [Header("Luvas atuais")]
    [SerializeField] private EquiparManoplas luvaDireita;
    [SerializeField] private EquiparManoplas luvaEsquerda;

    [Header("Elmo atual")]
    [SerializeField] private EquiparElmo elmoAtual;

    [Header("Peitoral atual")]
    [SerializeField] private EquiparPeitoral peitoralAtual;
    private bool trocandoPeitoral;
    public EquiparPeitoral PeitoralAtual => peitoralAtual;

    public EquiparManoplas LuvaDireita => luvaDireita;
    public EquiparManoplas LuvaEsquerda => luvaEsquerda;
    public EquiparElmo ElmoAtual => elmoAtual;

    // Identificadores persistentes; nao dependem do nome/traducao do prefab.
    internal static string IdentificarPosicaoVestida(GameObject item)
    {
        var luva = item.GetComponent<EquiparManoplas>();
        if (luva != null && luva.JaEquipado)
            return luva.LadoMao == LadoMaoEquipamento.Direita ? "mao-direita" : "mao-esquerda";
        var elmo = item.GetComponent<EquiparElmo>();
        if (elmo != null && elmo.JaEquipado) return "cabeca";
        var peitoral = item.GetComponent<EquiparPeitoral>();
        return peitoral != null && peitoral.JaEquipado ? "peitoral" : string.Empty;
    }

    internal static bool PosicaoSaveValida(string posicao) => string.IsNullOrEmpty(posicao) ||
        posicao == "mao-direita" || posicao == "mao-esquerda" || posicao == "cabeca" || posicao == "peitoral";

    internal void ValidarEquipamentoParaCarga(GameObject item, string posicao)
    {
        ResolverReferencias();
        bool valido = false;
        var luva = item.GetComponent<EquiparManoplas>();
        int tipos = (luva != null ? 1 : 0) + (item.GetComponent<EquiparElmo>() != null ? 1 : 0) +
            (item.GetComponent<EquiparPeitoral>() != null ? 1 : 0);
        if (tipos != 1) throw new System.IO.InvalidDataException("Item sem controlador unico de equipamento.");
        switch (posicao)
        {
            case "mao-direita": valido = luva != null && luva.LadoMao == LadoMaoEquipamento.Direita && maoDireita != null; break;
            case "mao-esquerda": valido = luva != null && luva.LadoMao == LadoMaoEquipamento.Esquerda && maoEsquerda != null; break;
            case "cabeca": valido = item.GetComponent<EquiparElmo>() != null && cabecaAvatar != null; break;
            case "peitoral": valido = item.GetComponent<EquiparPeitoral>() != null && corpoAvatar != null; break;
        }
        if (!valido) throw new System.IO.InvalidDataException("Equipamento ou destino indisponivel: " + posicao);
    }

    internal void RestaurarEquipamentoVestido(ItemPersistente item, string posicao)
    {
        ValidarEquipamentoParaCarga(item.gameObject, posicao);
        bool sucesso;
        switch (posicao)
        {
            case "mao-direita":
            case "mao-esquerda": sucesso = TentarEquipar(item.GetComponent<EquiparManoplas>()); break;
            case "cabeca": sucesso = TentarEquiparElmo(item.GetComponent<EquiparElmo>(), restaurandoSave: true); break;
            default: sucesso = TentarEquiparPeitoral(item.GetComponent<EquiparPeitoral>(), restaurandoSave: true); break;
        }
        if (!sucesso || IdentificarPosicaoVestida(item.gameObject) != posicao)
            throw new System.InvalidOperationException("Nao foi possivel restaurar equipamento: " + posicao);
    }

    // Nao devolve itens ao inventario durante a carga: o save ja define seus destinos.
    internal void PrepararRestauracaoEquipamentos()
    {
        if (luvaDireita != null) luvaDireita.Desequipar();
        if (luvaEsquerda != null) luvaEsquerda.Desequipar();
        if (elmoAtual != null) elmoAtual.Desequipar();
        if (peitoralAtual != null) peitoralAtual.Desequipar();
        luvaDireita = null; luvaEsquerda = null; elmoAtual = null; peitoralAtual = null;
    }

    internal System.Action CapturarRecuperacaoEquipamentos()
    {
        var direita = luvaDireita; var esquerda = luvaEsquerda;
        var elmo = elmoAtual; var peitoral = peitoralAtual;
        var recuperarDireita = direita != null ? direita.CapturarEstadoEquipadoParaCarga() : null;
        var recuperarEsquerda = esquerda != null ? esquerda.CapturarEstadoEquipadoParaCarga() : null;
        var recuperarElmo = elmo != null ? elmo.CapturarEstadoEquipadoParaCarga() : null;
        var recuperarPeitoral = peitoral != null ? peitoral.CapturarEstadoEquipadoParaCarga() : null;
        var partes = new System.Collections.Generic.List<System.Action>();
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            var alvo = renderer; bool visivel = alvo.enabled;
            partes.Add(() => { if (alvo != null) alvo.enabled = visivel; });
        }
        return () =>
        {
            luvaDireita = direita; luvaEsquerda = esquerda; elmoAtual = elmo; peitoralAtual = peitoral;
            foreach (var restaurar in partes) restaurar();
            recuperarDireita?.Invoke(); recuperarEsquerda?.Invoke();
            recuperarElmo?.Invoke(); recuperarPeitoral?.Invoke();
        };
    }

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

    internal void NotificarElmoDestruido(EquiparElmo elmo)
    {
        if (elmoAtual == elmo) elmoAtual = null;
    }

    internal void NotificarPeitoralDestruido(EquiparPeitoral peitoral)
    {
        if (peitoralAtual == peitoral) peitoralAtual = null;
    }

    internal void NotificarManoplaDestruida(EquiparManoplas luva)
    {
        if (luvaDireita == luva) luvaDireita = null;
        if (luvaEsquerda == luva) luvaEsquerda = null;
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

    public bool TentarEquipar(EquiparElmo novoElmo) => TentarEquiparElmo(novoElmo, restaurandoSave: false);

    private bool TentarEquiparElmo(EquiparElmo novoElmo, bool restaurandoSave)
    {
        if (novoElmo == null)
            return false;

        ResolverReferencias();
        Transform cabecaDestino = cabecaAvatar != null ? cabecaAvatar : novoElmo.CabecaConfigurada;
        if (!novoElmo.PodeEquiparNaCabeca(cabecaDestino))
            return false;

        if (elmoAtual == novoElmo)
            return true;

        if (!restaurandoSave && !novoElmo.AtendeRestricao(GetComponent<StatusPlayer>()))
            return false;

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

    public bool TentarEquipar(EquiparPeitoral novoPeitoral) => TentarEquiparPeitoral(novoPeitoral, restaurandoSave: false);

    private bool TentarEquiparPeitoral(EquiparPeitoral novoPeitoral, bool restaurandoSave)
    {
        if (trocandoPeitoral) return false;
        trocandoPeitoral = true;
        try { return TentarEquiparPeitoralInterno(novoPeitoral, restaurandoSave); }
        finally { trocandoPeitoral = false; }
    }

    private bool TentarEquiparPeitoralInterno(EquiparPeitoral novoPeitoral, bool restaurandoSave)
    {
        if (novoPeitoral == null)
            return false;

        ResolverReferencias();
        Transform corpoDestino = corpoAvatar != null ? corpoAvatar : novoPeitoral.CorpoConfigurado;
        if (!novoPeitoral.PodeEquiparNoCorpo(corpoDestino))
            return false;

        if (peitoralAtual == novoPeitoral)
        {
            if (novoPeitoral.JaEquipado) return true;
            peitoralAtual = null;
        }

        if (!restaurandoSave && !novoPeitoral.AtendeRestricao(GetComponent<StatusPlayer>()))
            return false;

        XRGrabInteractable grabNovo = novoPeitoral.GetComponent<XRGrabInteractable>();
        EstadoItemInventario estadoNovo = grabNovo != null
            ? grabNovo.GetComponent<EstadoItemInventario>()
            : null;
        SlotInventario slotOrigem = estadoNovo != null && estadoNovo.estaNoInventario
            ? estadoNovo.slotAtual
            : null;

        if (estadoNovo != null && estadoNovo.estaNoInventario &&
            (slotOrigem == null || !slotOrigem.PodeRetirarItemParaEquipamento(grabNovo)))
        {
            Debug.LogWarning("[GerenciadorEquipamentosPlayer] O peitoral precisa estar no topo do slot para ser equipado.", novoPeitoral);
            return false;
        }

        if (slotOrigem != null && !slotOrigem.RetirarItemParaEquipamento(grabNovo))
        {
            Debug.LogWarning("[GerenciadorEquipamentosPlayer] Não foi possível retirar o peitoral do inventário.", novoPeitoral);
            return false;
        }

        if (peitoralAtual != null && !TentarGuardarPeitoralAtual(peitoralAtual, corpoDestino))
        {
            if (slotOrigem != null && slotOrigem.RestaurarItemSalvoNoSlot(grabNovo, false))
                slotOrigem.FinalizarRestauracaoDoSave();
            return false;
        }

        if (!novoPeitoral.EquiparNoCorpo(corpoDestino))
            return false;

        peitoralAtual = novoPeitoral;
        return true;
    }

    public bool DesequiparPeitoral()
    {
        if (trocandoPeitoral) return false;
        trocandoPeitoral = true;
        try { ResolverReferencias(); return DesequiparPeitoralInterno(); }
        finally { trocandoPeitoral = false; }
    }

    private bool DesequiparPeitoralInterno()
    {
        if (peitoralAtual == null)
            return true;

        if (!TentarGuardarPeitoralAtual(peitoralAtual, corpoAvatar))
            return false;

        peitoralAtual = null;
        return true;
    }

    private bool TentarGuardarPeitoralAtual(EquiparPeitoral peitoral, Transform corpo)
    {
        if (peitoral == null)
            return true;

        XRGrabInteractable grab = peitoral.GetComponent<XRGrabInteractable>();
        if (grab == null || inventario == null)
        {
            Debug.LogWarning("[GerenciadorEquipamentosPlayer] Não foi possível devolver o peitoral: inventário ou XRGrabInteractable ausente.", peitoral);
            return false;
        }

        peitoral.Desequipar();
        SlotInventario[] slots = inventario.ObterSlotsParaSave();
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            SlotInventario slot = slots[i];
            if (slot == null || !slot.RestaurarItemSalvoNoSlot(grab, false))
                continue;

            slot.FinalizarRestauracaoDoSave();
            return true;
        }

        Transform corpoRestaurado = corpo != null ? corpo : peitoral.CorpoConfigurado;
        if (corpoRestaurado != null)
            peitoral.EquiparNoCorpo(corpoRestaurado);

        Debug.LogWarning("[GerenciadorEquipamentosPlayer] Inventário sem espaço para guardar o peitoral.", peitoral);
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

        if (corpoAvatar == null && avatar != null)
            corpoAvatar = avatar.corpoAvatar;

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
