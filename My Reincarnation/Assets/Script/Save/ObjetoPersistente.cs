using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ObjetoPersistente : MonoBehaviour, ISalvavel
{
    [Header("Identificacao")]
    [SerializeField] private string objectId;
    [SerializeField] private string tipoObjeto;

    [Header("Salvar")]
    [SerializeField] private bool salvarAtivo = true;
    [SerializeField] private bool salvarPosicao = true;
    [SerializeField] private bool salvarRotacao = true;
    [SerializeField] private bool salvarEscala = false;

    [Header("Estado Generico")]
    [SerializeField] private string estadoJson;

    [Header("Gerar ID")]
    [SerializeField] private bool gerarIdAutomaticoSeVazio = true;

    private void Reset()
    {
        if (string.IsNullOrWhiteSpace(tipoObjeto))
            tipoObjeto = gameObject.name;

        GarantirId();
    }

    private void Awake()
    {
        GarantirId();
    }

    private void OnEnable()
    {
        GarantirId();

        if (SaveManager.Instancia != null)
            SaveManager.Instancia.RegistrarObjeto(this);
    }

    private void OnDisable()
    {
        if (SaveManager.Instancia != null)
            SaveManager.Instancia.RemoverObjeto(this);
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(tipoObjeto))
            tipoObjeto = gameObject.name;

        GarantirId();
    }

    public string ObterId()
    {
        GarantirId();
        return objectId;
    }

    public SceneObjectSaveData SalvarEstado()
    {
        GarantirId();

        ItemPersistente itemPersistente = GetComponent<ItemPersistente>();
        if (itemPersistente != null)
            return null;

        EstadoItemInventario estadoInventario = GetComponent<EstadoItemInventario>();
        if (estadoInventario != null && estadoInventario.estaNoInventario)
            return null;

        Vector3 posicao = transform.position;
        Vector3 rotacao = transform.eulerAngles;
        Vector3 escala = transform.localScale;

        return new SceneObjectSaveData
        {
            objectId = objectId,
            tipoObjeto = string.IsNullOrWhiteSpace(tipoObjeto) ? gameObject.name : tipoObjeto,
            ativo = gameObject.activeSelf,
            posX = posicao.x,
            posY = posicao.y,
            posZ = posicao.z,
            rotX = rotacao.x,
            rotY = rotacao.y,
            rotZ = rotacao.z,
            scaleX = escala.x,
            scaleY = escala.y,
            scaleZ = escala.z,
            estadoJson = estadoJson
        };
    }

    public void CarregarEstado(SceneObjectSaveData data)
    {
        if (data == null)
            return;

        estadoJson = data.estadoJson;

        if (salvarPosicao)
            transform.position = new Vector3(data.posX, data.posY, data.posZ);

        if (salvarRotacao)
            transform.rotation = Quaternion.Euler(data.rotX, data.rotY, data.rotZ);

        if (salvarEscala)
            transform.localScale = new Vector3(data.scaleX, data.scaleY, data.scaleZ);

        if (salvarAtivo && gameObject.activeSelf != data.ativo)
            gameObject.SetActive(data.ativo);
    }

    [ContextMenu("Gerar Novo ID")]
    public void GerarNovoId()
    {
        objectId = Guid.NewGuid().ToString("N");
        Debug.Log($"[ObjetoPersistente] Novo ID gerado para '{name}': {objectId}", this);
    }

    [ContextMenu("Copiar ID no Console")]
    public void CopiarIdNoConsole()
    {
        GarantirId();
        GUIUtility.systemCopyBuffer = objectId;
        Debug.Log($"[ObjetoPersistente] ID copiado: {objectId}", this);
    }

    private void GarantirId()
    {
        if (!string.IsNullOrWhiteSpace(objectId) || !gerarIdAutomaticoSeVazio)
            return;

        objectId = Guid.NewGuid().ToString("N");
    }
}
