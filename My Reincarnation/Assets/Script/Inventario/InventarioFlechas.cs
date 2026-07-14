using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class InventarioFlechas : MonoBehaviour
{
    [Serializable]
    public class FlechaInventarioInfo
    {
        public string idTipoFlecha;
        public string nomeExibicao;
        public int quantidade;
        public GameObject prefabFlecha;
        public Sprite icone;
        public SlotInventario slotOrigem;
    }

    [Header("Referencias")]
    [SerializeField] private bool atualizarSlotsAutomaticamente = true;
    [SerializeField] private InventarioVR inventario;
    [SerializeField] private SlotInventario[] slots;
    [SerializeField] private SlotInventario[] slotsComFlechas;

    [Header("Diagnostico")]
    [SerializeField] private int quantidadeSlotsEncontrados;
    [SerializeField] private int quantidadeSlotsComFlechas;
    [SerializeField] private int quantidadeTotalFlechasDisponiveis;
    [SerializeField] private string ultimaFlechaConsumida;
    [SerializeField] private int quantidadeUltimaFlechaConsumida;
    [SerializeField] private int tiposFlechaDisponiveis;

    private readonly List<FlechaInventarioInfo> cacheFlechas = new List<FlechaInventarioInfo>();
    private readonly List<SlotInventario> cacheSlotsComFlechas = new List<SlotInventario>();

    private void Awake()
    {
        AtualizarReferencias();
    }

    private void OnEnable()
    {
        AtualizarReferencias();
    }

    private void OnValidate()
    {
        AtualizarReferencias();
    }

    [ContextMenu("Atualizar Slots Automaticamente")]
    private void AtualizarSlotsAutomaticamenteNoInspector()
    {
        AtualizarReferencias();
    }

    public List<FlechaInventarioInfo> ObterFlechasDisponiveis()
    {
        AtualizarReferencias();
        cacheFlechas.Clear();

        if (slots == null)
        {
            tiposFlechaDisponiveis = 0;
            quantidadeSlotsComFlechas = 0;
            quantidadeTotalFlechasDisponiveis = 0;
            slotsComFlechas = Array.Empty<SlotInventario>();
            return cacheFlechas;
        }

        Dictionary<string, FlechaInventarioInfo> porId = new Dictionary<string, FlechaInventarioInfo>();
        cacheSlotsComFlechas.Clear();
        quantidadeTotalFlechasDisponiveis = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            SlotInventario slot = slots[i];
            if (slot == null || !slot.PossuiItem())
                continue;

            if (!TentarObterFlechaDoSlot(slot, out XRGrabInteractable item, out ItemInventarioDados dados))
                continue;

            string id = NormalizarId(ObterIdTipoFlecha(item, dados));
            int quantidade = slot.ObterQuantidadeAtual();
            if (string.IsNullOrWhiteSpace(id) || quantidade <= 0)
                continue;

            if (!cacheSlotsComFlechas.Contains(slot))
                cacheSlotsComFlechas.Add(slot);

            quantidadeTotalFlechasDisponiveis += quantidade;

            if (!porId.TryGetValue(id, out FlechaInventarioInfo info))
            {
                info = new FlechaInventarioInfo
                {
                    idTipoFlecha = id,
                    nomeExibicao = ObterNomeExibicaoFlecha(item, dados),
                    quantidade = 0,
                    prefabFlecha = ObterPrefabFlecha(item, dados),
                    icone = dados != null ? dados.ObterIconeFlecha() : null,
                    slotOrigem = slot
                };

                porId.Add(id, info);
                cacheFlechas.Add(info);
            }

            info.quantidade += quantidade;

            if (info.prefabFlecha == null)
                info.prefabFlecha = ObterPrefabFlecha(item, dados);

            if (info.icone == null && dados != null)
                info.icone = dados.ObterIconeFlecha();
        }

        slotsComFlechas = cacheSlotsComFlechas.ToArray();
        quantidadeSlotsComFlechas = slotsComFlechas.Length;
        tiposFlechaDisponiveis = cacheFlechas.Count;
        return cacheFlechas;
    }

    public int ObterQuantidadeTotal(string idTipoFlecha)
    {
        string id = NormalizarId(idTipoFlecha);
        if (string.IsNullOrWhiteSpace(id))
            return 0;

        List<FlechaInventarioInfo> flechas = ObterFlechasDisponiveis();
        for (int i = 0; i < flechas.Count; i++)
        {
            if (string.Equals(flechas[i].idTipoFlecha, id, StringComparison.Ordinal))
                return flechas[i].quantidade;
        }

        return 0;
    }

    public bool TemFlecha(string idTipoFlecha)
    {
        return ObterQuantidadeTotal(idTipoFlecha) > 0;
    }

    public bool TentarObterPrefabFlecha(string idTipoFlecha, out GameObject prefabFlecha)
    {
        prefabFlecha = null;
        string id = NormalizarId(idTipoFlecha);
        if (string.IsNullOrWhiteSpace(id))
            return false;

        List<FlechaInventarioInfo> flechas = ObterFlechasDisponiveis();
        for (int i = 0; i < flechas.Count; i++)
        {
            FlechaInventarioInfo info = flechas[i];
            if (!string.Equals(info.idTipoFlecha, id, StringComparison.Ordinal))
                continue;

            prefabFlecha = info.prefabFlecha;
            return prefabFlecha != null;
        }

        return false;
    }

    public bool TentarEquiparPrimeiraFlechaDisponivel(out string idTipoFlecha, out GameObject prefabFlecha)
    {
        idTipoFlecha = string.Empty;
        prefabFlecha = null;

        List<FlechaInventarioInfo> flechas = ObterFlechasDisponiveis();
        if (flechas.Count == 0)
            return false;

        FlechaInventarioInfo info = flechas[0];
        idTipoFlecha = info.idTipoFlecha;
        prefabFlecha = info.prefabFlecha;
        return prefabFlecha != null;
    }

    public bool ConsumirUmaFlecha(string idTipoFlecha)
    {
        AtualizarReferencias();

        string id = NormalizarId(idTipoFlecha);
        if (string.IsNullOrWhiteSpace(id) || slots == null)
            return false;

        SlotInventario slotEscolhido = null;
        int menorQuantidade = int.MaxValue;

        for (int i = 0; i < slots.Length; i++)
        {
            SlotInventario slot = slots[i];
            if (slot == null || !slot.PossuiItem())
                continue;

            if (!TentarObterFlechaDoSlot(slot, out XRGrabInteractable item, out ItemInventarioDados dados))
                continue;

            if (!string.Equals(NormalizarId(ObterIdTipoFlecha(item, dados)), id, StringComparison.Ordinal))
                continue;

            int quantidade = slot.ObterQuantidadeAtual();
            if (quantidade <= 0 || quantidade >= menorQuantidade)
                continue;

            menorQuantidade = quantidade;
            slotEscolhido = slot;
        }

        if (slotEscolhido == null)
            return false;

        bool consumiu = slotEscolhido.ConsumirUmaUnidade(out ItemInventarioDados itemConsumido);
        if (!consumiu)
            return false;

        ultimaFlechaConsumida = itemConsumido != null ? itemConsumido.NomeItem : id;
        quantidadeUltimaFlechaConsumida = Mathf.Max(0, menorQuantidade - 1);
        ObterFlechasDisponiveis();
        return true;
    }

    private static bool TentarObterFlechaDoSlot(
        SlotInventario slot,
        out XRGrabInteractable item,
        out ItemInventarioDados dados)
    {
        item = null;
        dados = null;

        if (slot == null)
            return false;

        item = slot.ObterItemRepresentanteParaSave();
        if (item != null)
            dados = item.GetComponent<ItemInventarioDados>();

        if (dados == null)
            dados = slot.ObterItemRepresentante();

        return ItemEhFlecha(item, dados);
    }

    private static bool ItemEhFlecha(XRGrabInteractable item, ItemInventarioDados dados)
    {
        if (dados != null && dados.EhFlecha())
            return true;

        if (dados != null && NomePareceFlecha(dados.NomeItem))
            return true;

        if (item == null)
            return false;

        GameObject objeto = item.gameObject;
        if (objeto != null && string.Equals(objeto.tag, "Arrow", StringComparison.Ordinal))
            return true;

        if (item.GetComponentInChildren<Flecha>(true) != null)
            return true;

        return NomePareceFlecha(item.name) ||
               (item.transform != null && NomePareceFlecha(item.transform.name));
    }

    private static string ObterIdTipoFlecha(XRGrabInteractable item, ItemInventarioDados dados)
    {
        if (dados != null)
        {
            string idDados = NormalizarId(dados.ObterIdTipoFlecha());
            if (!string.IsNullOrWhiteSpace(idDados))
                return idDados;
        }

        if (item != null)
            return LimparNomeFlecha(item.gameObject.name);

        return string.Empty;
    }

    private static string ObterNomeExibicaoFlecha(XRGrabInteractable item, ItemInventarioDados dados)
    {
        if (dados != null && !string.IsNullOrWhiteSpace(dados.NomeItem))
            return dados.NomeItem;

        return item != null ? LimparNomeFlecha(item.gameObject.name) : "Flecha";
    }

    private static GameObject ObterPrefabFlecha(XRGrabInteractable item, ItemInventarioDados dados)
    {
        string id = ObterIdTipoFlecha(item, dados);
        GameObject prefabBanco = ObterPrefabPeloBanco(id);
        if (prefabBanco != null)
            return prefabBanco;

        if (dados != null)
            return dados.ObterPrefabFlechaParaDisparo();

        if (item == null)
            return null;

        ItemPersistente persistente = item.GetComponent<ItemPersistente>();
        if (persistente != null && persistente.PrefabReferencia != null)
            return persistente.PrefabReferencia;

        return item.gameObject;
    }

    private static GameObject ObterPrefabPeloBanco(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        ItemDatabaseLocal database = ItemDatabaseLocal.Instancia != null
            ? ItemDatabaseLocal.Instancia
            : FindFirstObjectByType<ItemDatabaseLocal>();

        return database != null ? database.ObterPrefab(itemId) : null;
    }

    private static bool NomePareceFlecha(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return false;

        return nome.IndexOf("Flexa", StringComparison.OrdinalIgnoreCase) >= 0 ||
               nome.IndexOf("Flecha", StringComparison.OrdinalIgnoreCase) >= 0 ||
               nome.IndexOf("Arrow", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string LimparNomeFlecha(string nome)
    {
        string limpo = SlotInventario.LimparNomeItem(nome);
        if (string.IsNullOrWhiteSpace(limpo) || !limpo.EndsWith(")", StringComparison.Ordinal))
            return limpo;

        int inicioSufixo = limpo.LastIndexOf("(", StringComparison.Ordinal);
        if (inicioSufixo < 0 || inicioSufixo >= limpo.Length - 2)
            return limpo;

        for (int i = inicioSufixo + 1; i < limpo.Length - 1; i++)
        {
            if (!char.IsDigit(limpo[i]))
                return limpo;
        }

        return limpo.Substring(0, inicioSufixo).TrimEnd();
    }

    private void AtualizarReferencias()
    {
        if (inventario == null)
            inventario = GetComponent<InventarioVR>();

        if (inventario == null)
            inventario = GetComponentInParent<InventarioVR>(true);

        if (inventario == null)
            inventario = EncontrarInventarioComSlots();

        SlotInventario[] slotsEncontrados = null;
        if (inventario != null)
            slotsEncontrados = inventario.ObterSlotsParaSave();

        if (slotsEncontrados == null || slotsEncontrados.Length == 0)
        {
            slotsEncontrados = FindObjectsByType<SlotInventario>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        }

        if (atualizarSlotsAutomaticamente || slots == null || slots.Length == 0)
            slots = NormalizarSlots(slotsEncontrados);
        else
            slots = NormalizarSlots(slots);

        quantidadeSlotsEncontrados = slots != null ? slots.Length : 0;
        AtualizarSlotsComFlechasParaInspector();
    }

    private void AtualizarSlotsComFlechasParaInspector()
    {
        cacheSlotsComFlechas.Clear();

        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                SlotInventario slot = slots[i];
                if (slot == null || !slot.PossuiItem())
                    continue;

                if (TentarObterFlechaDoSlot(slot, out _, out _) && !cacheSlotsComFlechas.Contains(slot))
                    cacheSlotsComFlechas.Add(slot);
            }
        }

        slotsComFlechas = cacheSlotsComFlechas.ToArray();
        quantidadeSlotsComFlechas = slotsComFlechas.Length;
    }

    private static InventarioVR EncontrarInventarioComSlots()
    {
        InventarioVR[] inventarios = FindObjectsByType<InventarioVR>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (inventarios == null || inventarios.Length == 0)
            return null;

        InventarioVR primeiroValido = null;
        for (int i = 0; i < inventarios.Length; i++)
        {
            InventarioVR candidato = inventarios[i];
            if (candidato == null)
                continue;

            if (primeiroValido == null)
                primeiroValido = candidato;

            SlotInventario[] slotsInventario = candidato.ObterSlotsParaSave();
            if (slotsInventario != null && slotsInventario.Length > 0)
                return candidato;
        }

        return primeiroValido;
    }

    private static SlotInventario[] NormalizarSlots(SlotInventario[] origem)
    {
        if (origem == null || origem.Length == 0)
            return Array.Empty<SlotInventario>();

        List<SlotInventario> resultado = new List<SlotInventario>();
        HashSet<SlotInventario> vistos = new HashSet<SlotInventario>();

        for (int i = 0; i < origem.Length; i++)
        {
            SlotInventario slot = origem[i];
            if (slot == null || !vistos.Add(slot))
                continue;

            resultado.Add(slot);
        }

        return resultado.ToArray();
    }

    private static string NormalizarId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }
}
