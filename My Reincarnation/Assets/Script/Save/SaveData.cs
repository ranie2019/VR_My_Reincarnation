using System;
using System.Collections.Generic;

[Serializable]
public class MissionSaveData
{
    public string gerenciadorId;
    public string idMissao;
    public int estado;
    public int quantidadeAtualObjetivo;
}

[Serializable]
public class SaveData
{
    public string versaoSave = "0.3-missoes-persistentes";
    public string dataSave;
    public PlayerSaveData player = new PlayerSaveData();
    public List<InventorySaveData> inventario = new List<InventorySaveData>();
    public List<SceneObjectSaveData> objetosCena = new List<SceneObjectSaveData>();
    public List<MissionSaveData> missoes = new List<MissionSaveData>();
}
