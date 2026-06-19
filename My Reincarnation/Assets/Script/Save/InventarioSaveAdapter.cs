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
    [SerializeField] private bool debugInventarioSave = true;

    private void Awake()
    {
        AtualizarReferencias();
    }

    private void OnValidate()
    {
        if (inventario == null)
            inventario = GetComponent<InventarioVR>();

        if ((slots == null || slots.Length == 0) && inventario != null)
            slots = inventario.GetComponentsInChildren<SlotInventario>(true);
    }

    public List<InventorySaveData> SalvarInventario()
    {
        AtualizarReferencias();

        List<InventorySaveData> resultado = new List<InventorySaveData>();

        if (slots == null)
            return resultado;

        Dictionary<string, InventorySaveData> agrupados = new Dictionary<string, InventorySaveData>();

        for (int indiceSlot = 0; indiceSlot < slots.Length; indiceSlot++)
        {
            SlotInventario slot = slots[indiceSlot];
            if (slot == null)
                continue;

            List<XRGrabInteractable> itens = slot.ObterItensParaSave();

            for (int i = 0; i < itens.Count; i++)
            {
                XRGrabInteractable item = itens[i];
                if (item == null)
                    continue;

                ItemPersistente persistente = item.GetComponent<ItemPersistente>();
                if (persistente != null && !persistente.SalvarNoInventario)
                    continue;

                if (persistente != null)
                    persistente.MarcarComoNoInventario();

                InventorySaveData data = persistente != null
                    ? CriarSaveComItemPersistente(persistente, indiceSlot)
                    : CriarSaveFallback(item, indiceSlot);

                if (data == null || string.IsNullOrWhiteSpace(data.itemId))
                {
                    Debug.LogWarning($"[InventarioSaveAdapter] Item sem itemId valido nao foi salvo: {item.gameObject.name}", this);
                    continue;
                }

                string instanciaLog = string.IsNullOrWhiteSpace(data.instanciaId) ? "(sem instanciaId)" : data.instanciaId;
                Log($"Item detectado no inventario para save: {data.itemId} | instancia {instanciaLog} | inventario? true | pos atual {item.transform.position}");

                string chave = CriarChaveAgrupamento(data);
                if (agrupados.TryGetValue(chave, out InventorySaveData existente))
                {
                    existente.quantidade += 1;
                    AdicionarInstanciaIds(existente, data);
                    continue;
                }

                agrupados.Add(chave, data);
            }
        }

        foreach (InventorySaveData data in agrupados.Values)
        {
            resultado.Add(data);
            string instanciaPrincipal = string.IsNullOrWhiteSpace(data.instanciaId) ? "(sem instanciaId)" : data.instanciaId;
            Log($"Item salvo no inventario: {data.itemId} | instancia {instanciaPrincipal} | inventario? true | slot {data.slot} | qtd {data.quantidade}");
        }

        Log($"Total de itens salvos no inventario: {resultado.Count}");
        return resultado;
    }

    public void CarregarInventario(List<InventorySaveData> itens)
    {
        AtualizarReferencias();

        if (itens == null)
        {
            Log("Lista de inventario vazia no save.");
            return;
        }

        if (limparInventarioAntesCarregar)
            LimparSlots();

        List<ItemPersistente> originaisCena = ObterItensPersistentesSoltosCena();
        HashSet<ItemPersistente> originaisUsados = new HashSet<ItemPersistente>();

        int itensCarregados = 0;
        HashSet<string> instanciaIdsCarregados = new HashSet<string>();

        for (int i = 0; i < itens.Count; i++)
        {
            InventorySaveData data = itens[i];
            if (data == null || string.IsNullOrWhiteSpace(data.itemId))
                continue;

            if (data.slot < 0 || slots == null || data.slot >= slots.Length || slots[data.slot] == null)
            {
                Debug.LogWarning($"[InventarioSaveAdapter] Slot invalido para item '{data.itemId}': {data.slot}", this);
                continue;
            }

            int quantidade = Mathf.Max(1, data.quantidade);
            List<string> instanciaIds = ObterInstanciaIdsParaRestaurar(data);
            if (instanciaIds.Count == 0)
            {
                Debug.LogWarning($"[InventarioSaveAdapter] Item de inventario ignorado por falta de instanciaId confiavel. itemId: {data.itemId}. Save antigo/contaminado pode causar duplicacao; crie um save novo depois de validar os IDs.", this);
                continue;
            }

            int quantidadeParaRestaurar = Mathf.Min(quantidade, instanciaIds.Count);
            if (quantidadeParaRestaurar < quantidade)
            {
                Debug.LogWarning($"[InventarioSaveAdapter] Quantidade salva maior que a quantidade de instanciaIds confiaveis para itemId: {data.itemId}. Restaurando {quantidadeParaRestaurar} de {quantidade}.", this);
            }

            for (int quantidadeIndex = 0; quantidadeIndex < quantidadeParaRestaurar; quantidadeIndex++)
            {
                string instanciaId = instanciaIds[quantidadeIndex];
                if (!instanciaIdsCarregados.Add(instanciaId))
                {
                    Debug.LogWarning($"[InventarioSaveAdapter] instanciaId duplicado no inventario salvo ignorado: {instanciaId} | itemId: {data.itemId}", this);
                    continue;
                }

                InventorySaveData dataInstancia = CriarDataParaInstancia(data, instanciaId);
                bool esconderNaPilha = quantidadeIndex < quantidadeParaRestaurar - 1;

                ItemPersistente originalCena = EncontrarOriginalParaRestaurarNoInventario(
                    originaisCena,
                    dataInstancia.itemId,
                    dataInstancia.instanciaId,
                    originaisUsados,
                    out int candidatosRuntime);

                if (originalCena != null)
                {
                    originaisUsados.Add(originalCena);
                    if (RestaurarItemExistente(dataInstancia, originalCena, slots[data.slot], esconderNaPilha))
                        itensCarregados++;

                    continue;
                }

                if (candidatosRuntime > 1)
                {
                    Debug.LogWarning($"[InventarioSaveAdapter] Item de inventario nao restaurado porque existem {candidatosRuntime} originais com ID temporario e mesmo itemId na cena. itemId: {dataInstancia.itemId} | instanciaId salvo: {dataInstancia.instanciaId}. Gere IDs persistentes na cena e crie um save novo.", this);
                    continue;
                }

                if (ExisteOriginalMesmoTipoNaoUsado(originaisCena, dataInstancia.itemId, originaisUsados))
                {
                    Debug.LogWarning($"[InventarioSaveAdapter] Item de inventario nao restaurado para evitar duplicacao. Existe original de cena do mesmo itemId, mas o instanciaId nao confere. itemId: {dataInstancia.itemId} | instanciaId salvo: {dataInstancia.instanciaId}. Save antigo/contaminado; gere IDs e crie um save novo.", this);
                    continue;
                }

                Debug.LogWarning($"[InventarioSaveAdapter] Item de inventario nao restaurado porque a instancia original nao foi encontrada. itemId: {dataInstancia.itemId} | instanciaId salvo: {dataInstancia.instanciaId}. Nao sera instanciado prefab novo por seguranca.", this);
            }
        }

        FinalizarRestauracaoDosSlots();
        Log($"Itens carregados no inventario: {itensCarregados}");
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
            Debug.LogWarning($"[InventarioSaveAdapter] Item existente '{data.itemId}' nao possui XRGrabInteractable. Ele nao pode ser restaurado no inventario.", persistente);
            persistente.MarcarComoSoltoNaCena();
            return false;
        }

        persistente.MarcarComoNoInventario();

        if (!slot.RestaurarItemSalvoNoSlot(grab, esconderNaPilha))
        {
            Debug.LogWarning($"[InventarioSaveAdapter] Falha ao restaurar item existente '{data.itemId}' no slot {data.slot}.", persistente);
            persistente.MarcarComoSoltoNaCena();
            return false;
        }

        persistente.MarcarComoNoInventario();
        Log($"Item original da cena restaurado no inventario: {data.itemId} | instancia {data.instanciaId} | inventario? true | slot {data.slot}");
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
        {
            Log("Nenhum instanciaId confiavel no inventario salvo. Originais soltos nao serao destruidos por seguranca.");
            return;
        }

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
            Log($"Removendo original da cena para item salvo no inventario: {item.ObterItemId()} | instancia {instanciaId}");
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
                        Debug.LogWarning($"[InventarioSaveAdapter] Original da cena nao removido para evitar apagar item errado. Existem {totalCandidatos} itens com instanciaId temporario e itemId '{data.itemId}'. Rode Tools/Save/Gerar IDs dos Itens Persistentes da Cena e crie um save novo.", this);
                    }

                    continue;
                }

                removidos.Add(candidato);
                instanciaIdsEncontrados.Add(instanciaId);
                Debug.LogWarning($"[InventarioSaveAdapter] Removendo original com instanciaId temporario porque este item foi salvo no inventario. itemId: {data.itemId} | instanciaId salvo: {instanciaId}", candidato);
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

    private void AdicionarInstanciaIds(InventorySaveData destino, InventorySaveData origem)
    {
        if (destino == null || origem == null)
            return;

        if (destino.instanciaIds == null)
            destino.instanciaIds = new List<string>();

        if (!string.IsNullOrWhiteSpace(destino.instanciaId) && !destino.instanciaIds.Contains(destino.instanciaId.Trim()))
            destino.instanciaIds.Add(destino.instanciaId.Trim());

        if (!string.IsNullOrWhiteSpace(origem.instanciaId) && !destino.instanciaIds.Contains(origem.instanciaId.Trim()))
            destino.instanciaIds.Add(origem.instanciaId.Trim());

        if (origem.instanciaIds == null)
            return;

        for (int i = 0; i < origem.instanciaIds.Count; i++)
        {
            string instanciaId = origem.instanciaIds[i];
            if (string.IsNullOrWhiteSpace(instanciaId) || destino.instanciaIds.Contains(instanciaId.Trim()))
                continue;

            destino.instanciaIds.Add(instanciaId.Trim());
        }
    }

    private InventorySaveData CriarSaveComItemPersistente(ItemPersistente item, int indiceSlot)
    {
        InventorySaveData data = item.CriarSaveData(indiceSlot, 1, false);
        data.itemId = item.ObterItemId();
        data.nomeItem = item.ObterNomeItem();
        return data;
    }

    private InventorySaveData CriarSaveFallback(XRGrabInteractable item, int indiceSlot)
    {
        string nome = item.gameObject.name.Trim();
        Debug.LogWarning($"[InventarioSaveAdapter] Item sem ItemPersistente. Usando gameObject.name como itemId: {nome}", this);

        return new InventorySaveData
        {
            itemId = nome,
            nomeItem = nome,
            instanciaId = string.Empty,
            instanciaIds = new List<string>(),
            quantidade = 1,
            slot = indiceSlot,
            estaNoInventario = true,
            durabilidade = -1f,
            equipado = false,
            dadosExtrasJson = string.Empty
        };
    }

    private string CriarChaveAgrupamento(InventorySaveData data)
    {
        return $"{data.slot}|{data.itemId}|{data.durabilidade}|{data.equipado}|{data.dadosExtrasJson}";
    }

    private void AtualizarReferencias()
    {
        if (inventario == null)
            inventario = GetComponent<InventarioVR>();

        if (inventario == null)
            inventario = FindFirstObjectByType<InventarioVR>();

        if ((slots == null || slots.Length == 0) && inventario != null)
            slots = inventario.GetComponentsInChildren<SlotInventario>(true);

        if ((slots == null || slots.Length == 0))
            slots = FindObjectsByType<SlotInventario>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void Log(string mensagem)
    {
    }
}
