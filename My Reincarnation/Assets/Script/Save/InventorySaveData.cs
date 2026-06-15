using System;
using System.Collections.Generic;

[Serializable]
public class InventorySaveData
{
    public string itemId;
    public string nomeItem;
    public string instanciaId;
    public List<string> instanciaIds = new List<string>();
    public int quantidade;
    public int slot;
    public bool estaNoInventario;
    public float durabilidade;
    public bool equipado;
    public string dadosExtrasJson;
}
