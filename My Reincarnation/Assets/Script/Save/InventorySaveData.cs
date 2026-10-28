using System;
using System.Collections.Generic;

[Serializable]
public class InventorySaveData
{
    public string itemId;
    public string nomeItem;
    public string instanciaId;
    public List<string> instanciaIds = new List<string>();
    // 0 = legado representativo; 1 = estado individual associado por GUID.
    public int versaoEstadosIndividuais;
    public List<InventoryItemState> estadosIndividuais;
    public bool possuiDurabilidadeMaxima;
    public float durabilidadeMaxima;
    public bool instanciaCriadaEmRuntime;
    public int quantidade;
    public int slot;
    public bool estaNoInventario;
    public float durabilidade;
    public bool equipado;
    public string dadosExtrasJson;
    public void ValidarEstadosIndividuais()
    {
        bool temEstados = estadosIndividuais != null && estadosIndividuais.Count > 0;
        if (versaoEstadosIndividuais == 0 && !temEstados) return;
        if (versaoEstadosIndividuais != 1 || !temEstados || quantidade < 1 || quantidade > 99 ||
            estadosIndividuais.Count != quantidade || instanciaIds == null || instanciaIds.Count != quantidade ||
            instanciaId != instanciaIds[0])
            throw new System.IO.InvalidDataException("Estados individuais incompletos no slot " + slot);
        var esperados = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in instanciaIds)
            if (!Guid.TryParseExact(id, "N", out _) || !esperados.Add(id))
                throw new System.IO.InvalidDataException("ID individual invalido ou repetido: " + id);
        foreach (InventoryItemState estado in estadosIndividuais)
        {
            if (estado == null || !esperados.Remove(estado.instanciaId ?? "") ||
                string.IsNullOrWhiteSpace(estado.itemId) ||
                float.IsNaN(estado.durabilidade) || float.IsInfinity(estado.durabilidade) ||
                (estado.durabilidade < 0f && estado.durabilidade != -1f) ||
                (estado.possuiDurabilidadeMaxima && (float.IsNaN(estado.durabilidadeMaxima) ||
                 float.IsInfinity(estado.durabilidadeMaxima) || estado.durabilidadeMaxima <= 0f ||
                 estado.durabilidade < 0f || estado.durabilidade > estado.durabilidadeMaxima)))
                throw new System.IO.InvalidDataException("Estado individual invalido no slot " + slot);
        }
    }

    public InventorySaveData CriarDataParaInstancia(string id)
    {
        InventoryItemState estado = null;
        if (versaoEstadosIndividuais != 0 || (estadosIndividuais != null && estadosIndividuais.Count > 0))
        {
            ValidarEstadosIndividuais();
            estado = estadosIndividuais.Find(e => e.instanciaId == id);
            if (estado == null) throw new System.IO.InvalidDataException("Estado individual ausente: " + id);
        }
        return new InventorySaveData
        {
            itemId = estado != null ? estado.itemId : itemId,
            nomeItem = estado != null ? estado.nomeItem : nomeItem,
            instanciaId = id,
            instanciaIds = new List<string> { id },
            instanciaCriadaEmRuntime = estado != null ? estado.instanciaCriadaEmRuntime : instanciaCriadaEmRuntime,
            quantidade = 1, slot = slot, estaNoInventario = true,
            durabilidade = estado != null ? estado.durabilidade : durabilidade,
            possuiDurabilidadeMaxima = estado != null ? estado.possuiDurabilidadeMaxima : possuiDurabilidadeMaxima,
            durabilidadeMaxima = estado != null ? estado.durabilidadeMaxima : durabilidadeMaxima,
            equipado = estado != null ? estado.equipado : equipado,
            dadosExtrasJson = estado != null ? estado.dadosExtrasJson : dadosExtrasJson
        };
    }
}

[Serializable]
public class InventoryItemState
{
    public string instanciaId;
    public string itemId;
    public string nomeItem;
    public float durabilidade;
    public bool possuiDurabilidadeMaxima;
    public float durabilidadeMaxima;
    public bool instanciaCriadaEmRuntime;
    public bool equipado;
    public string dadosExtrasJson;
}
