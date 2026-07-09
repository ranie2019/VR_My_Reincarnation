using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class InventarioSaveAdapter : MonoBehaviour, IInventarioSalvavel
{
    [Header("Referencias")]
    [SerializeField] private InventarioVR inventario;
    [SerializeField] private SlotInventario[] slots;
    [SerializeField] private Transform parentItensRestaurados;

    [Header("Carregamento")]
    [SerializeField] private bool limparInventarioAntesCarregar = true;
    [SerializeField] private bool destruirOriginaisSalvosNoInventario = true;

    private void Awake()
    {
        AtualizarReferencias();
    }

    private void OnValidate()
    {
        if (inventario == null)
            inventario = GetComponent<InventarioVR>();

        if (inventario != null)
            slots = NormalizarSlots(inventario.ObterSlotsParaSave());
        else
            slots = NormalizarSlots(slots);
    }

    public List<InventorySaveData> SalvarInventario()
    {
        AtualizarReferencias();

        List<InventorySaveData> resultado = new List<InventorySaveData>();

        if (slots == null)
            return resultado;

        for (int indiceSlot = 0; indiceSlot < slots.Length; indiceSlot++)
        {
            SlotInventario slot = slots[indiceSlot];
            if (slot == null)
                continue;

            int quantidade = slot.ObterQuantidadeRealParaSave();
            if (quantidade <= 0)
                continue;

            XRGrabInteractable itemRepresentante = slot.ObterItemRepresentanteParaSave();
            if (itemRepresentante == null)
                continue;

            ItemPersistente persistenteRepresentante = itemRepresentante.GetComponent<ItemPersistente>();
            if (persistenteRepresentante != null && !persistenteRepresentante.SalvarNoInventario)
                continue;

            if (persistenteRepresentante != null)
                persistenteRepresentante.MarcarComoNoInventario();

            InventorySaveData data = persistenteRepresentante != null
                ? CriarSaveComItemPersistente(persistenteRepresentante, indiceSlot, quantidade)
                : CriarSaveFallback(itemRepresentante, indiceSlot, quantidade);

            if (data == null || string.IsNullOrWhiteSpace(data.itemId))
            {
                { }
                continue;
            }

            List<XRGrabInteractable> itens = slot.ObterItensParaSave();
            AplicarInstanciaIdsDaPilha(data, itens);

            for (int i = 0; i < itens.Count; i++)
            {
                XRGrabInteractable item = itens[i];
                if (item == null)
                    continue;

                ItemPersistente persistente = item.GetComponent<ItemPersistente>();
                if (persistente != null)
                    persistente.MarcarComoNoInventario();
            }

            resultado.Add(data);
        }

        return resultado;
    }

    public void CarregarInventario(List<InventorySaveData> itens)
    {
        AtualizarReferencias();

        if (itens == null)
            return;

        if (limparInventarioAntesCarregar)
            LimparSlots();

        List<ItemPersistente> originaisCena = ObterItensPersistentesSoltosCena();
        HashSet<ItemPersistente> originaisUsados = new HashSet<ItemPersistente>();

        HashSet<string> instanciaIdsCarregados = new HashSet<string>();
        HashSet<SlotInventario> slotsRestaurados = new HashSet<SlotInventario>();

        for (int i = 0; i < itens.Count; i++)
        {
            InventorySaveData data = itens[i];
            if (data == null || string.IsNullOrWhiteSpace(data.itemId))
                continue;

            if (data.slot < 0 || slots == null || data.slot >= slots.Length || slots[data.slot] == null)
            {
                { }
                continue;
            }

            SlotInventario slotDestino = slots[data.slot];
            int quantidade = Mathf.Max(1, data.quantidade);
            List<string> instanciaIds = ObterInstanciaIdsParaRestaurar(data);
            int quantidadeComIdSalvo = instanciaIds.Count;

            for (int quantidadeIndex = 0; quantidadeIndex < quantidade; quantidadeIndex++)
            {
                string instanciaId = ObterOuCriarInstanciaIdParaRestaurar(
                    instanciaIds,
                    quantidadeIndex,
                    data,
                    instanciaIdsCarregados);

                if (!instanciaIdsCarregados.Add(instanciaId))
                {
                    { }
                    continue;
                }

                InventorySaveData dataInstancia = CriarDataParaInstancia(data, instanciaId);
                bool esconderNaPilha = quantidadeIndex < quantidade - 1;
                bool instanciaTinhaIdSalvo = quantidadeIndex < quantidadeComIdSalvo;
                int candidatosRuntime = 0;

                ItemPersistente originalCena = instanciaTinhaIdSalvo
                    ? EncontrarOriginalParaRestaurarNoInventario(
                        originaisCena,
                        dataInstancia.itemId,
                        dataInstancia.instanciaId,
                        originaisUsados,
                        out candidatosRuntime)
                    : null;

                if (originalCena != null)
                {
                    originaisUsados.Add(originalCena);
                    if (RestaurarItemExistente(dataInstancia, originalCena, slotDestino, esconderNaPilha))
                        slotsRestaurados.Add(slotDestino);

                    continue;
                }

                if (instanciaTinhaIdSalvo && candidatosRuntime > 1)
                {
                    { }
                    continue;
                }

                if (instanciaTinhaIdSalvo && ExisteOriginalMesmoTipoNaoUsado(originaisCena, dataInstancia.itemId, originaisUsados))
                {
                    { }
                    continue;
                }

                if (CriarERestaurarItemNoInventario(dataInstancia, slotDestino, esconderNaPilha))
                    slotsRestaurados.Add(slotDestino);
            }
        }

        FinalizarRestauracaoDosSlots();
        FinalizarVisualAposLoadDosSlots(slotsRestaurados);

        if (slotsRestaurados.Count > 0 && isActiveAndEnabled)
            StartCoroutine(FinalizarVisualAposLoadNosProximosFrames(new List<SlotInventario>(slotsRestaurados)));
    }

    private void FinalizarRestauracaoDosSlots()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].FinalizarRestauracaoDoSave();
        }
    }

    private void FinalizarVisualAposLoadDosSlots(IEnumerable<SlotInventario> slotsRestaurados)
    {
        if (slotsRestaurados == null)
            return;

        foreach (SlotInventario slot in slotsRestaurados)
        {
            if (slot != null)
                slot.ForcarRecalculoVisualAposLoad();
        }
    }

    private IEnumerator FinalizarVisualAposLoadNosProximosFrames(List<SlotInventario> slotsRestaurados)
    {
        if (slotsRestaurados == null || slotsRestaurados.Count == 0)
            yield break;

        yield return null;
        FinalizarVisualAposLoadDosSlots(slotsRestaurados);

        yield return null;
        FinalizarVisualAposLoadDosSlots(slotsRestaurados);
    }

    private bool RestaurarItemExistente(InventorySaveData data, ItemPersistente persistente, SlotInventario slot, bool esconderNaPilha)
    {
        if (data == null || persistente == null || slot == null)
            return false;

        GameObject instancia = persistente.gameObject;
        instancia.name = SlotInventario.LimparNomeItem(instancia.name);
        instancia.SetActive(true);

        persistente.AplicarSaveData(data);

        XRGrabInteractable grab = instancia.GetComponent<XRGrabInteractable>();
        if (grab == null)
        {
            { }
            persistente.MarcarComoSoltoNaCena();
            return false;
        }

        persistente.MarcarComoNoInventario();

        if (!slot.RestaurarItemSalvoNoSlot(grab, esconderNaPilha))
        {
            { }
            persistente.MarcarComoSoltoNaCena();
            return false;
        }

        persistente.MarcarComoNoInventario();
        return true;
    }

    private bool CriarERestaurarItemNoInventario(InventorySaveData data, SlotInventario slot, bool esconderNaPilha)
    {
        if (data == null || slot == null)
            return false;

        GameObject prefab = ObterPrefabParaRestaurar(data);
        if (prefab == null)
            return false;

        Transform parent = parentItensRestaurados != null ? parentItensRestaurados : transform;
        GameObject instancia = Instantiate(prefab, parent);
        instancia.name = SlotInventario.LimparNomeItem(prefab.name);
        instancia.SetActive(true);

        ItemPersistente persistente = instancia.GetComponent<ItemPersistente>();
        if (persistente == null)
            persistente = instancia.AddComponent<ItemPersistente>();

        XRGrabInteractable grab = instancia.GetComponent<XRGrabInteractable>();
        if (grab == null)
        {
            Destroy(instancia);
            return false;
        }

        persistente.AplicarSaveData(data);
        persistente.MarcarComoNoInventario();

        if (!slot.RestaurarItemSalvoNoSlot(grab, esconderNaPilha))
        {
            Destroy(instancia);
            return false;
        }

        persistente.MarcarComoNoInventario();
        return true;
    }

    private List<ItemPersistente> ObterItensPersistentesSoltosCena()
    {
        ItemPersistente[] itens = FindObjectsByType<ItemPersistente>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        List<ItemPersistente> resultado = new List<ItemPersistente>();
        for (int i = 0; i < itens.Length; i++)
        {
            ItemPersistente item = itens[i];
            if (item == null)
                continue;

            if (item.EstaNoInventario() || item.GetComponentInParent<SlotInventario>(true) != null)
                continue;

            resultado.Add(item);
        }

        return resultado;
    }

    private ItemPersistente EncontrarOriginalParaRestaurarNoInventario(
        List<ItemPersistente> originaisCena,
        string itemId,
        string instanciaId,
        HashSet<ItemPersistente> usados,
        out int candidatosRuntime)
    {
        candidatosRuntime = 0;

        if (originaisCena == null || string.IsNullOrWhiteSpace(instanciaId))
            return null;

        string instanciaIdNormalizado = instanciaId.Trim();

        for (int i = 0; i < originaisCena.Count; i++)
        {
            ItemPersistente item = originaisCena[i];
            if (item == null || usados.Contains(item))
                continue;

            if (string.Equals(item.ObterInstanciaIdSemGerar(), instanciaIdNormalizado, System.StringComparison.Ordinal))
                return item;
        }

        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        string itemIdNormalizado = itemId.Trim();
        ItemPersistente encontrado = null;

        for (int i = 0; i < originaisCena.Count; i++)
        {
            ItemPersistente item = originaisCena[i];
            if (item == null || usados.Contains(item))
                continue;

            if (!item.InstanciaIdFoiGeradoEmRuntime())
                continue;

            if (!string.Equals(item.ObterItemIdSemFallback(), itemIdNormalizado, System.StringComparison.Ordinal))
                continue;

            candidatosRuntime++;
            encontrado = item;
        }

        return candidatosRuntime == 1 ? encontrado : null;
    }

    private bool ExisteOriginalMesmoTipoNaoUsado(
        List<ItemPersistente> originaisCena,
        string itemId,
        HashSet<ItemPersistente> usados)
    {
        if (originaisCena == null || string.IsNullOrWhiteSpace(itemId))
            return false;

        string itemIdNormalizado = itemId.Trim();
        for (int i = 0; i < originaisCena.Count; i++)
        {
            ItemPersistente item = originaisCena[i];
            if (item == null || usados.Contains(item))
                continue;

            if (string.Equals(item.ObterItemIdSemFallback(), itemIdNormalizado, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void LimparSlots()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].LimparItensSalvosDoSlot(true);
        }
    }

    private void DestruirOriginaisSalvos(List<InventorySaveData> itens)
    {
        HashSet<string> instanciaIdsSalvos = ExtrairInstanciaIdsSalvos(itens);
        if (instanciaIdsSalvos.Count == 0)
            return;

        ItemPersistente[] itensCena = FindObjectsByType<ItemPersistente>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        HashSet<string> instanciaIdsEncontrados = new HashSet<string>();
        HashSet<ItemPersistente> removidos = new HashSet<ItemPersistente>();

        for (int i = 0; i < itensCena.Length; i++)
        {
            ItemPersistente item = itensCena[i];
            if (item == null || !item.DestruirOriginalAoCarregarSeEstiverNoInventario)
                continue;

            if (item.EstaNoInventario() || item.GetComponentInParent<SlotInventario>(true) != null)
                continue;

            string instanciaId = item.ObterInstanciaId();
            if (string.IsNullOrWhiteSpace(instanciaId) || !instanciaIdsSalvos.Contains(instanciaId.Trim()))
                continue;

            instanciaIdsEncontrados.Add(instanciaId.Trim());
            removidos.Add(item);
            Destroy(item.gameObject);
        }

        RemoverOriginaisComIdRuntimePorItemId(itens, itensCena, instanciaIdsEncontrados, removidos);
    }

    private void RemoverOriginaisComIdRuntimePorItemId(
        List<InventorySaveData> itens,
        ItemPersistente[] itensCena,
        HashSet<string> instanciaIdsEncontrados,
        HashSet<ItemPersistente> removidos)
    {
        if (itens == null || itensCena == null)
            return;

        for (int i = 0; i < itens.Count; i++)
        {
            InventorySaveData data = itens[i];
            if (data == null || string.IsNullOrWhiteSpace(data.itemId))
                continue;

            List<string> instanciaIds = ObterInstanciaIdsParaRestaurar(data);
            for (int j = 0; j < instanciaIds.Count; j++)
            {
                string instanciaId = instanciaIds[j];
                if (instanciaIdsEncontrados.Contains(instanciaId))
                    continue;

                ItemPersistente candidato = EncontrarUnicoOriginalRuntimePorItemId(
                    itensCena,
                    data.itemId,
                    removidos,
                    out int totalCandidatos);

                if (candidato == null)
                {
                    if (totalCandidatos > 1)
                    {
                        { }
                    }

                    continue;
                }

                removidos.Add(candidato);
                instanciaIdsEncontrados.Add(instanciaId);
                { }
                Destroy(candidato.gameObject);
            }
        }
    }

    private ItemPersistente EncontrarUnicoOriginalRuntimePorItemId(
        ItemPersistente[] itensCena,
        string itemId,
        HashSet<ItemPersistente> ignorar,
        out int totalCandidatos)
    {
        totalCandidatos = 0;
        if (itensCena == null || string.IsNullOrWhiteSpace(itemId))
            return null;

        string itemIdNormalizado = itemId.Trim();
        ItemPersistente encontrado = null;

        for (int i = 0; i < itensCena.Length; i++)
        {
            ItemPersistente item = itensCena[i];
            if (item == null || ignorar.Contains(item))
                continue;

            if (item.EstaNoInventario() || item.GetComponentInParent<SlotInventario>(true) != null)
                continue;

            if (!item.InstanciaIdFoiGeradoEmRuntime())
                continue;

            string itemIdCena = item.ObterItemIdSemFallback();
            if (!string.Equals(itemIdCena, itemIdNormalizado, System.StringComparison.Ordinal))
                continue;

            totalCandidatos++;
            encontrado = item;
        }

        return totalCandidatos == 1 ? encontrado : null;
    }

    private HashSet<string> ExtrairInstanciaIdsSalvos(List<InventorySaveData> itens)
    {
        HashSet<string> instanciaIds = new HashSet<string>();
        if (itens == null)
            return instanciaIds;

        for (int i = 0; i < itens.Count; i++)
        {
            InventorySaveData item = itens[i];
            if (item == null)
                continue;

            if (!string.IsNullOrWhiteSpace(item.instanciaId))
                instanciaIds.Add(item.instanciaId.Trim());

            if (item.instanciaIds == null)
                continue;

            for (int j = 0; j < item.instanciaIds.Count; j++)
            {
                string instanciaId = item.instanciaIds[j];
                if (!string.IsNullOrWhiteSpace(instanciaId))
                    instanciaIds.Add(instanciaId.Trim());
            }
        }

        return instanciaIds;
    }

    private List<string> ObterInstanciaIdsParaRestaurar(InventorySaveData data)
    {
        List<string> ids = new List<string>();
        if (data == null)
            return ids;

        AdicionarInstanciaIdUnico(ids, data.instanciaId);

        if (data.instanciaIds == null)
            return ids;

        for (int i = 0; i < data.instanciaIds.Count; i++)
            AdicionarInstanciaIdUnico(ids, data.instanciaIds[i]);

        return ids;
    }

    private string ObterOuCriarInstanciaIdParaRestaurar(
        List<string> ids,
        int indice,
        InventorySaveData data,
        HashSet<string> idsJaUsados)
    {
        if (ids == null)
            ids = new List<string>();

        if (indice < ids.Count && !string.IsNullOrWhiteSpace(ids[indice]))
        {
            string idSalvo = ids[indice].Trim();
            if (idsJaUsados == null || !idsJaUsados.Contains(idSalvo))
                return idSalvo;
        }

        string novoId;
        do
        {
            novoId = CriarInstanciaIdGeradoParaStack(data);
        }
        while ((idsJaUsados != null && idsJaUsados.Contains(novoId)) || ids.Contains(novoId));

        ids.Add(novoId);
        return novoId;
    }

    private string CriarInstanciaIdGeradoParaStack(InventorySaveData data)
    {
        string prefixo = data != null && !string.IsNullOrWhiteSpace(data.itemId)
            ? data.itemId.Trim()
            : "item";

        return $"{prefixo}_stack_{System.Guid.NewGuid():N}";
    }

    private GameObject ObterPrefabParaRestaurar(InventorySaveData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.itemId))
            return null;

        ItemDatabaseLocal database = ItemDatabaseLocal.Instancia != null
            ? ItemDatabaseLocal.Instancia
            : FindFirstObjectByType<ItemDatabaseLocal>();

        return database != null ? database.ObterPrefab(data.itemId) : null;
    }

    private void AdicionarInstanciaIdUnico(List<string> ids, string instanciaId)
    {
        if (ids == null || string.IsNullOrWhiteSpace(instanciaId))
            return;

        string id = instanciaId.Trim();
        if (!ids.Contains(id))
            ids.Add(id);
    }

    private InventorySaveData CriarDataParaInstancia(InventorySaveData origem, string instanciaId)
    {
        return new InventorySaveData
        {
            itemId = origem.itemId,
            nomeItem = origem.nomeItem,
            instanciaId = instanciaId,
            instanciaIds = new List<string> { instanciaId },
            quantidade = 1,
            slot = origem.slot,
            estaNoInventario = true,
            durabilidade = origem.durabilidade,
            equipado = origem.equipado,
            dadosExtrasJson = origem.dadosExtrasJson
        };
    }

    private void AplicarInstanciaIdsDaPilha(InventorySaveData data, List<XRGrabInteractable> itens)
    {
        if (data == null)
            return;

        data.instanciaIds = new List<string>();

        if (itens != null)
        {
            for (int i = 0; i < itens.Count; i++)
            {
                XRGrabInteractable item = itens[i];
                if (item == null)
                    continue;

                ItemPersistente persistente = item.GetComponent<ItemPersistente>();
                if (persistente == null)
                    continue;

                AdicionarInstanciaIdUnico(data.instanciaIds, persistente.ObterInstanciaId());
            }
        }

        if (data.instanciaIds.Count == 0)
            AdicionarInstanciaIdUnico(data.instanciaIds, data.instanciaId);

        if (data.instanciaIds.Count > 0)
            data.instanciaId = data.instanciaIds[0];
    }

    private InventorySaveData CriarSaveComItemPersistente(ItemPersistente item, int indiceSlot, int quantidade)
    {
        InventorySaveData data = item.CriarSaveData(indiceSlot, Mathf.Max(1, quantidade), false);
        data.itemId = item.ObterItemId();
        data.nomeItem = item.ObterNomeItem();
        data.quantidade = Mathf.Max(1, quantidade);
        return data;
    }

    private InventorySaveData CriarSaveFallback(XRGrabInteractable item, int indiceSlot, int quantidade)
    {
        string nome = item.gameObject.name.Trim();
        { }
        return new InventorySaveData
        {
            itemId = nome,
            nomeItem = nome,
            instanciaId = string.Empty,
            instanciaIds = new List<string>(),
            quantidade = Mathf.Max(1, quantidade),
            slot = indiceSlot,
            estaNoInventario = true,
            durabilidade = -1f,
            equipado = false,
            dadosExtrasJson = string.Empty
        };
    }

    private void AtualizarReferencias()
    {
        if (inventario == null)
            inventario = GetComponent<InventarioVR>();

        if (inventario == null)
            inventario = FindFirstObjectByType<InventarioVR>();

        if (inventario != null)
            slots = NormalizarSlots(inventario.ObterSlotsParaSave());

        if ((slots == null || slots.Length == 0))
            slots = NormalizarSlots(FindObjectsByType<SlotInventario>(FindObjectsInactive.Include, FindObjectsSortMode.None));

        slots = NormalizarSlots(slots);
    }

    private SlotInventario[] NormalizarSlots(SlotInventario[] origem)
    {
        if (origem == null || origem.Length == 0)
            return new SlotInventario[0];

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

}
