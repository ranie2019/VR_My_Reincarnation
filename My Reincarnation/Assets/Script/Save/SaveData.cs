using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public string versaoSave = "0.2-status-player";
    public string dataSave;
    public PlayerSaveData player = new PlayerSaveData();
    public List<InventorySaveData> inventario = new List<InventorySaveData>();
    public List<SceneObjectSaveData> objetosCena = new List<SceneObjectSaveData>();
}
