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

        Vector3 posicao = new Vector3(data.posX, data.posY, data.posZ);
        if (salvarPosicao && VetorFinito(posicao))
            transform.position = posicao;

        Vector3 rotacao = new Vector3(data.rotX, data.rotY, data.rotZ);
        if (salvarRotacao && VetorFinito(rotacao))
            transform.rotation = Quaternion.Euler(rotacao);

        Vector3 escala = new Vector3(data.scaleX, data.scaleY, data.scaleZ);
        if (salvarEscala && EscalaValida(escala))
            transform.localScale = escala;

        if (salvarAtivo && gameObject.activeSelf != data.ativo)
            gameObject.SetActive(data.ativo);
    }

    [ContextMenu("Gerar Novo ID")]
    public void GerarNovoId()
    {
        objectId = Guid.NewGuid().ToString("N");
    }

    private static bool EscalaValida(Vector3 escala)
    {
        const float minimo = 0.0001f;
        return VetorFinito(escala) &&
               Mathf.Abs(escala.x) > minimo &&
               Mathf.Abs(escala.y) > minimo &&
               Mathf.Abs(escala.z) > minimo;
    }

    private static bool VetorFinito(Vector3 valor)
    {
        return ValorFinito(valor.x) && ValorFinito(valor.y) && ValorFinito(valor.z);
    }

    private static bool ValorFinito(float valor)
    {
        return !float.IsNaN(valor) && !float.IsInfinity(valor);
    }

    [ContextMenu("Copiar ID no Console")]
    public void CopiarIdNoConsole()
    {
        GarantirId();
        GUIUtility.systemCopyBuffer = objectId;
    }

    private void GarantirId()
    {
        if (!string.IsNullOrWhiteSpace(objectId) || !gerarIdAutomaticoSeVazio)
            return;

        objectId = Guid.NewGuid().ToString("N");
    }
}
