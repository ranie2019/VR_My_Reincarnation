using System;

[Serializable]
public class SceneObjectSaveData
{
    public bool quarentenaCogumelo; // Registro preservado, nunca instanciado automaticamente.
    public string objectId;
    public string tipoObjeto;
    public string prefabItemId;
    public string instanciaId;
    public bool ativo;
    public float posX;
    public float posY;
    public float posZ;
    public float rotX;
    public float rotY;
    public float rotZ;
    public float scaleX;
    public float scaleY;
    public float scaleZ;
    public string estadoJson;
}
