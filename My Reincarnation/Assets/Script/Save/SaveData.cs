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
    public const string VersaoAtual = "0.8-migracao-cogumelos";
    public string versaoSave = VersaoAtual;
    public string dataSave;
    public PlayerSaveData player = new PlayerSaveData();
    public List<InventorySaveData> inventario = new List<InventorySaveData>();
    public List<SceneObjectSaveData> objetosCena = new List<SceneObjectSaveData>();
    public List<RespawnNatureza.EstadoPontoCogumelo> cogumelosPendentes = new List<RespawnNatureza.EstadoPontoCogumelo>();
    public List<string> itensRemovidosPermanentemente = new List<string>();
    public List<MissionSaveData> missoes = new List<MissionSaveData>();
}
