using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("Diagnostico Load Inventario")]
    [SerializeField] private string ultimoItemIdCarregado;
    [SerializeField] private bool ultimoPrefabEncontrado;
    [SerializeField] private int ultimaQuantidadeCarregada;
    [SerializeField] private bool ultimaFalhaPrefabAusente;
    [SerializeField] private bool ultimaFalhaItemIdVazio;
    [SerializeField] private bool ultimoLoadDiretoPorPrefab;

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

            List<XRGrabInteractable> itens = slot.ObterItensParaSave();
            if (itens == null)
                itens = new List<XRGrabInteractable>();

            int quantidade = itens.Count;
            if (quantidade <= 0)
                continue;

            XRGrabInteractable itemRepresentante = slot.ObterItemRepresentanteParaSave();
            if (itemRepresentante == null)
                continue;

            GarantirPersistenciaDosItensDaPilha(itens);

            ItemPersistente persistenteRepresentante = GarantirItemPersistenteParaSave(itemRepresentante);
            if (persistenteRepresentante != null && !persistenteRepresentante.SalvarNoInventario)
                continue;

            if (persistenteRepresentante != null)
                persistenteRepresentante.MarcarComoNoInventario();

            InventorySaveData data = persistenteRepresentante != null
                ? CriarSaveComItemPersistente(persistenteRepresentante, indiceSlot, quantidade)
                : CriarSaveFallback(itemRepresentante, indiceSlot, quantidade);

            if (DataInventarioSemIdentificacao(data))
            {
                { }
                continue;
            }

            NormalizarNomeItemSalvo(data, itemRepresentante);
            MarcarOrigemRuntimeNoSave(data, itens);
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
            if (DataInventarioSemIdentificacao(data))
            {
                RegistrarFalhaItemIdVazio();
                continue;
            }

            if (data.slot < 0 || slots == null || data.slot >= slots.Length || slots[data.slot] == null)
            {
                { }
                continue;
            }

            SlotInventario slotDestino = slots[data.slot];
            int quantidade = Mathf.Max(1, data.quantidade);
            GameObject prefabParaRestaurar = ObterPrefabParaRestaurar(data);
            bool restaurarDiretoPorPrefab = DeveRestaurarDiretoPorPrefab(data, prefabParaRestaurar);
            AtualizarDiagnosticoLoad(data, prefabParaRestaurar, quantidade, restaurarDiretoPorPrefab);

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

                if (restaurarDiretoPorPrefab)
                {
                    if (CriarERestaurarItemNoInventario(dataInstancia, slotDestino, esconderNaPilha))
                    {
                        slotsRestaurados.Add(slotDestino);
                        RemoverOriginalSoltoCorrespondenteAoItemRestauradoDireto(
                            originaisCena,
                            dataInstancia,
                            originaisUsados);
                    }

                    continue;
                }

                ItemPersistente originalCena = instanciaTinhaIdSalvo
                    ? EncontrarOriginalParaRestaurarNoInventario(
                        originaisCena,
                        dataInstancia.itemId,
                        dataInstancia.instanciaId,
                        originaisUsados,
                        out candidatosRuntime)
                    : null;

                bool podeCriarInstanciaSalvaPorPrefab = PodeCriarInstanciaSalvaPorPrefab(
                    prefabParaRestaurar,
                    quantidade,
                    dataInstancia,
                    candidatosRuntime);

                if (originalCena != null)
                {
                    originaisUsados.Add(originalCena);
                    if (RestaurarItemExistente(dataInstancia, originalCena, slotDestino, esconderNaPilha))
                        slotsRestaurados.Add(slotDestino);

                    continue;
                }

                if (instanciaTinhaIdSalvo && candidatosRuntime > 1 && !podeCriarInstanciaSalvaPorPrefab)
                {
                    { }
                    continue;
                }

                if (instanciaTinhaIdSalvo &&
                    !podeCriarInstanciaSalvaPorPrefab &&
                    ExisteOriginalMesmoTipoNaoUsado(originaisCena, dataInstancia.itemId, originaisUsados))
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

    private void RemoverOriginalSoltoCorrespondenteAoItemRestauradoDireto(
        List<ItemPersistente> originaisCena,
        InventorySaveData data,
        HashSet<ItemPersistente> originaisUsados)
    {
        if (!destruirOriginaisSalvosNoInventario || originaisCena == null || data == null)
            return;

        ItemPersistente original = EncontrarOriginalSoltoPorInstanciaId(
            originaisCena,
            data.instanciaId,
            originaisUsados);

        if (original == null)
        {
            original = EncontrarOriginalSoltoPorItemIdParaRestauracaoDireta(
                originaisCena,
                data.itemId,
                originaisUsados);
        }

        if (original == null)
            return;

        originaisUsados?.Add(original);
        originaisCena.Remove(original);

        if (original.gameObject != null)
        {
            original.gameObject.SetActive(false);
            Destroy(original.gameObject);
        }
    }

    private ItemPersistente EncontrarOriginalSoltoPorInstanciaId(
        List<ItemPersistente> originaisCena,
        string instanciaId,
        HashSet<ItemPersistente> originaisUsados)
    {
        if (originaisCena == null || string.IsNullOrWhiteSpace(instanciaId))
            return null;

        string instanciaIdNormalizado = instanciaId.Trim();
        for (int i = 0; i < originaisCena.Count; i++)
        {
            ItemPersistente item = originaisCena[i];
            if (item == null || (originaisUsados != null && originaisUsados.Contains(item)))
                continue;

            if (item.EstaNoInventario() || item.GetComponentInParent<SlotInventario>(true) != null)
                continue;

            if (string.Equals(item.ObterInstanciaIdSemGerar(), instanciaIdNormalizado, System.StringComparison.Ordinal))
                return item;
        }

        return null;
    }

    private ItemPersistente EncontrarOriginalSoltoPorItemIdParaRestauracaoDireta(
        List<ItemPersistente> originaisCena,
        string itemId,
        HashSet<ItemPersistente> originaisUsados)
    {
        ItemPersistente originalComIdPersistente = EncontrarOriginalSoltoPorItemId(
            originaisCena,
            itemId,
            originaisUsados,
            true);

        if (originalComIdPersistente != null)
            return originalComIdPersistente;

        return EncontrarOriginalSoltoPorItemId(
            originaisCena,
            itemId,
            originaisUsados,
            false);
    }

    private ItemPersistente EncontrarOriginalSoltoPorItemId(
        List<ItemPersistente> originaisCena,
        string itemId,
        HashSet<ItemPersistente> originaisUsados,
        bool apenasIdRuntime)
    {
        if (originaisCena == null || string.IsNullOrWhiteSpace(itemId))
            return null;

        string itemIdNormalizado = itemId.Trim();
        for (int i = 0; i < originaisCena.Count; i++)
        {
            ItemPersistente item = originaisCena[i];
            if (item == null || (originaisUsados != null && originaisUsados.Contains(item)))
                continue;

            if (item.EstaNoInventario() || item.GetComponentInParent<SlotInventario>(true) != null)
                continue;

            if (apenasIdRuntime && !item.InstanciaIdFoiGeradoEmRuntime())
                continue;

            if (item.CorrespondeAoItemId(itemIdNormalizado))
                return item;
        }

        return null;
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

            if (!item.CorrespondeAoItemId(itemIdNormalizado))
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

            if (item.CorrespondeAoItemId(itemIdNormalizado))
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

            if (!item.CorrespondeAoItemId(itemIdNormalizado))
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

            if (ItemPersistente.InstanciaIdEhGuidValido(item.instanciaId))
                instanciaIds.Add(item.instanciaId.Trim());

            if (item.instanciaIds == null)
                continue;

            for (int j = 0; j < item.instanciaIds.Count; j++)
            {
                string instanciaId = item.instanciaIds[j];
                if (ItemPersistente.InstanciaIdEhGuidValido(instanciaId))
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
        return System.Guid.NewGuid().ToString("N");
    }

    private GameObject ObterPrefabParaRestaurar(InventorySaveData data)
    {
        if (data == null ||
            (string.IsNullOrWhiteSpace(data.itemId) && string.IsNullOrWhiteSpace(data.nomeItem)))
        {
            return null;
        }

        ItemDatabaseLocal database = ItemDatabaseLocal.Instancia != null
            ? ItemDatabaseLocal.Instancia
            : FindFirstObjectByType<ItemDatabaseLocal>();

        if (database == null)
            return ObterPrefabFallbackSemDatabase(data);

        GameObject prefab = ObterPrefabPorPossiveisIds(database, data.itemId, data.nomeItem);
        return prefab != null ? prefab : ObterPrefabFallbackSemDatabase(data);
    }

    private bool DeveRestaurarDiretoPorPrefab(InventorySaveData data, GameObject prefab)
    {
        if (data == null)
            return false;

        if (prefab == null)
        {
            if (ItemIdPareceFlecha(data.itemId) || ItemIdPareceFlecha(data.nomeItem))
                ultimaFalhaPrefabAusente = true;

            return false;
        }

        return prefab.GetComponent<Flecha>() != null ||
               prefab.GetComponentInChildren<Flecha>(true) != null;
    }

    private static bool ItemIdPareceFlecha(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        return itemId.IndexOf("Flexa", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               itemId.IndexOf("Flecha", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               itemId.IndexOf("Arrow", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void AtualizarDiagnosticoLoad(InventorySaveData data, GameObject prefab, int quantidade, bool loadDiretoPorPrefab)
    {
        ultimoItemIdCarregado = data != null ? data.itemId : string.Empty;
        ultimoPrefabEncontrado = prefab != null;
        ultimaQuantidadeCarregada = Mathf.Max(0, quantidade);
        ultimaFalhaPrefabAusente = prefab == null &&
                                   data != null &&
                                   (ItemIdPareceFlecha(data.itemId) || ItemIdPareceFlecha(data.nomeItem));
        ultimaFalhaItemIdVazio = DataInventarioSemIdentificacao(data);
        ultimoLoadDiretoPorPrefab = loadDiretoPorPrefab;
    }

    private void RegistrarFalhaItemIdVazio()
    {
        ultimoItemIdCarregado = string.Empty;
        ultimoPrefabEncontrado = false;
        ultimaQuantidadeCarregada = 0;
        ultimaFalhaPrefabAusente = false;
        ultimaFalhaItemIdVazio = true;
        ultimoLoadDiretoPorPrefab = false;
    }

    private bool DataInventarioSemIdentificacao(InventorySaveData data)
    {
        return data == null ||
               (string.IsNullOrWhiteSpace(data.itemId) && string.IsNullOrWhiteSpace(data.nomeItem));
    }

    private void AdicionarInstanciaIdUnico(List<string> ids, string instanciaId)
    {
        if (ids == null || !ItemPersistente.InstanciaIdEhGuidValido(instanciaId))
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
            instanciaCriadaEmRuntime = origem.instanciaCriadaEmRuntime,
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

                ItemPersistente persistente = GarantirItemPersistenteParaSave(item);
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

    private void MarcarOrigemRuntimeNoSave(InventorySaveData data, List<XRGrabInteractable> itens)
    {
        if (data == null)
            return;

        data.instanciaCriadaEmRuntime = false;

        if (itens == null || itens.Count != 1 || itens[0] == null)
            return;

        ItemPersistente persistente = GarantirItemPersistenteParaSave(itens[0]);
        data.instanciaCriadaEmRuntime = persistente != null && persistente.InstanciaIdFoiGeradoEmRuntime();
    }

    private InventorySaveData CriarSaveComItemPersistente(ItemPersistente item, int indiceSlot, int quantidade)
    {
        return item.CriarSaveData(indiceSlot, Mathf.Max(1, quantidade), false);
    }

    private void GarantirPersistenciaDosItensDaPilha(List<XRGrabInteractable> itens)
    {
        if (itens == null)
            return;

        for (int i = 0; i < itens.Count; i++)
            GarantirItemPersistenteParaSave(itens[i]);
    }

    private ItemPersistente GarantirItemPersistenteParaSave(XRGrabInteractable item)
    {
        if (item == null)
            return null;

        ItemPersistente persistente = item.GetComponent<ItemPersistente>();
        if (persistente == null)
            persistente = item.gameObject.AddComponent<ItemPersistente>();

        persistente.MarcarComoNoInventario();
        return persistente;
    }

    private void NormalizarNomeItemSalvo(InventorySaveData data, XRGrabInteractable representante)
    {
        if (data == null)
            return;

        ItemInventarioDados dados = representante != null
            ? representante.GetComponent<ItemInventarioDados>()
            : null;

        if (dados != null && dados.PrefabParaStack != null)
        {
            string nomePrefabStack = LimparNomeParaBuscaPrefab(dados.PrefabParaStack.name);
            if (!string.IsNullOrWhiteSpace(nomePrefabStack))
            {
                data.nomeItem = nomePrefabStack;
                return;
            }
        }

        string nomeAtual = !string.IsNullOrWhiteSpace(data.nomeItem)
            ? data.nomeItem
            : representante != null ? representante.gameObject.name : string.Empty;

        data.nomeItem = LimparNomeParaBuscaPrefab(nomeAtual);
    }

    private GameObject ObterPrefabPorPossiveisIds(ItemDatabaseLocal database, string itemId, string nomeItem)
    {
        GameObject prefab = ObterPrefabPorId(database, itemId);
        if (prefab != null)
            return prefab;

        prefab = ObterPrefabPorId(database, nomeItem);
        if (prefab != null)
            return prefab;

        prefab = ObterPrefabPorId(database, LimparNomeParaBuscaPrefab(itemId));
        if (prefab != null)
            return prefab;

        return ObterPrefabPorId(database, LimparNomeParaBuscaPrefab(nomeItem));
    }

    private GameObject ObterPrefabFallbackSemDatabase(InventorySaveData data)
    {
        GameObject prefab = ObterPrefabPorReferenciasDaCena(data);
        if (prefab != null)
            return prefab;

#if UNITY_EDITOR
        prefab = ObterPrefabPorGuidNoEditor(data != null ? data.itemId : null);
        if (prefab != null)
            return prefab;

        return ObterPrefabPorGuidNoEditor(data != null ? data.nomeItem : null);
#else
        return null;
#endif
    }

    private GameObject ObterPrefabPorReferenciasDaCena(InventorySaveData data)
    {
        if (DataInventarioSemIdentificacao(data))
            return null;

        ItemInventarioDados[] dadosItens = FindObjectsByType<ItemInventarioDados>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < dadosItens.Length; i++)
        {
            ItemInventarioDados dados = dadosItens[i];
            if (dados == null)
                continue;

            GameObject prefab = dados.PrefabParaStack;
            if (prefab == null)
                continue;

            if (ReferenciaCorrespondeAoItemSalvo(data, dados.gameObject, dados, prefab))
                return prefab;
        }

        ItemPersistente[] persistentes = FindObjectsByType<ItemPersistente>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < persistentes.Length; i++)
        {
            ItemPersistente persistente = persistentes[i];
            if (persistente == null)
                continue;

            GameObject prefab = persistente.PrefabReferencia;
            if (prefab == null)
            {
                ItemInventarioDados dados = persistente.GetComponent<ItemInventarioDados>();
                prefab = dados != null ? dados.PrefabParaStack : null;
            }

            if (prefab == null)
                continue;

            if (ReferenciaCorrespondeAoItemSalvo(data, persistente.gameObject, persistente.GetComponent<ItemInventarioDados>(), prefab))
                return prefab;
        }

        return null;
    }

    private bool ReferenciaCorrespondeAoItemSalvo(
        InventorySaveData data,
        GameObject referencia,
        ItemInventarioDados dados,
        GameObject prefab)
    {
        if (data == null)
            return false;

        if (TextoCorrespondeAoItemSalvo(data, referencia != null ? referencia.name : null))
            return true;

        if (TextoCorrespondeAoItemSalvo(data, prefab != null ? prefab.name : null))
            return true;

        if (dados != null && TextoCorrespondeAoItemSalvo(data, dados.NomeItem))
            return true;

        ItemPersistente persistenteReferencia = referencia != null ? referencia.GetComponent<ItemPersistente>() : null;
        if (PersistenteCorrespondeAoItemSalvo(data, persistenteReferencia))
            return true;

        ItemPersistente persistentePrefab = prefab != null ? prefab.GetComponentInChildren<ItemPersistente>(true) : null;
        return PersistenteCorrespondeAoItemSalvo(data, persistentePrefab);
    }

    private bool PersistenteCorrespondeAoItemSalvo(InventorySaveData data, ItemPersistente persistente)
    {
        if (data == null || persistente == null)
            return false;

        return persistente.CorrespondeAoItemId(data.itemId) ||
               persistente.CorrespondeAoItemId(data.nomeItem) ||
               TextoCorrespondeAoItemSalvo(data, persistente.ObterNomeItem());
    }

    private bool TextoCorrespondeAoItemSalvo(InventorySaveData data, string texto)
    {
        if (data == null || string.IsNullOrWhiteSpace(texto))
            return false;

        string textoLimpo = LimparNomeParaBuscaPrefab(texto);
        string itemIdLimpo = LimparNomeParaBuscaPrefab(data.itemId);
        string nomeLimpo = LimparNomeParaBuscaPrefab(data.nomeItem);

        return (!string.IsNullOrWhiteSpace(data.itemId) && string.Equals(texto.Trim(), data.itemId.Trim(), System.StringComparison.Ordinal)) ||
               (!string.IsNullOrWhiteSpace(data.nomeItem) && string.Equals(texto.Trim(), data.nomeItem.Trim(), System.StringComparison.Ordinal)) ||
               (!string.IsNullOrWhiteSpace(itemIdLimpo) && string.Equals(textoLimpo, itemIdLimpo, System.StringComparison.Ordinal)) ||
               (!string.IsNullOrWhiteSpace(nomeLimpo) && string.Equals(textoLimpo, nomeLimpo, System.StringComparison.Ordinal));
    }

    private bool PodeCriarInstanciaSalvaPorPrefab(
        GameObject prefab,
        int quantidade,
        InventorySaveData dataInstancia,
        int candidatosRuntime)
    {
        if (prefab == null)
            return false;

        return quantidade > 1 ||
               (dataInstancia != null && dataInstancia.instanciaCriadaEmRuntime);
    }

#if UNITY_EDITOR
    private GameObject ObterPrefabPorGuidNoEditor(string possivelGuid)
    {
        if (string.IsNullOrWhiteSpace(possivelGuid))
            return null;

        string caminho = AssetDatabase.GUIDToAssetPath(possivelGuid.Trim());
        return string.IsNullOrWhiteSpace(caminho)
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
    }
#endif

    private GameObject ObterPrefabPorId(ItemDatabaseLocal database, string id)
    {
        if (database == null || string.IsNullOrWhiteSpace(id))
            return null;

        return database.ObterPrefab(id.Trim());
    }

    private string LimparNomeParaBuscaPrefab(string nome)
    {
        string limpo = SlotInventario.LimparNomeItem(nome);
        if (string.IsNullOrWhiteSpace(limpo))
            return string.Empty;

        int abre = limpo.LastIndexOf('(');
        bool terminaComParenteses = limpo.EndsWith(")");
        if (abre > 0 && terminaComParenteses)
        {
            string conteudo = limpo.Substring(abre + 1, limpo.Length - abre - 2).Trim();
            if (int.TryParse(conteudo, out _))
                limpo = limpo.Substring(0, abre).TrimEnd();
        }

        return limpo.Trim();
    }

    private InventorySaveData CriarSaveFallback(XRGrabInteractable item, int indiceSlot, int quantidade)
    {
        string nome = LimparNomeParaBuscaPrefab(item.gameObject.name);
        { }
        return new InventorySaveData
        {
            itemId = nome,
            nomeItem = nome,
            instanciaId = string.Empty,
            instanciaIds = new List<string>(),
            instanciaCriadaEmRuntime = true,
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
