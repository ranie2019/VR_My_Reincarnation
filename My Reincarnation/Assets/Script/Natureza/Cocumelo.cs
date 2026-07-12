using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class Cocumelo : MonoBehaviour
{
    [Header("Respawn")]
    [FormerlySerializedAs("idCocumelo")]
    [SerializeField] private string respawnId = "";

    private const float DistanciaParaSolicitarRespawn = 0.25f;

    private Vector3 posicaoOriginal;
    private Quaternion rotacaoOriginal;
    private Transform parentOriginal;
    private bool respawnSolicitado;
    private bool baseOriginalCapturada;

    private void Awake()
    {
        PreencherRespawnIdSeVazio();
        CapturarBaseOriginal();
    }

    private void Reset()
    {
        PreencherRespawnIdSeVazio();
    }

    private void Update()
    {
        VerificarSaidaDaPosicaoOriginal();
    }

    private void OnTransformParentChanged()
    {
        VerificarSaidaDaPosicaoOriginal();
    }

    private void OnValidate()
    {
        PreencherRespawnIdSeVazio();
    }

    public string ObterRespawnId()
    {
        PreencherRespawnIdSeVazio();
        return respawnId;
    }

    public string ObterIdCocumelo()
    {
        return ObterRespawnId();
    }

    private void PreencherRespawnIdSeVazio()
    {
        if (string.IsNullOrWhiteSpace(respawnId))
            respawnId = gameObject.name;
    }

    private void CapturarBaseOriginal()
    {
        posicaoOriginal = transform.position;
        rotacaoOriginal = transform.rotation;
        parentOriginal = transform.parent;
        baseOriginalCapturada = true;
    }

    private void VerificarSaidaDaPosicaoOriginal()
    {
        if (respawnSolicitado)
            return;

        if (!baseOriginalCapturada)
            CapturarBaseOriginal();

        bool mudouDeParent = transform.parent != parentOriginal;
        bool saiuDaPosicao =
            Vector3.Distance(transform.position, posicaoOriginal) >= DistanciaParaSolicitarRespawn;

        if (mudouDeParent || saiuDaPosicao)
            SolicitarRespawn();
    }

    private void SolicitarRespawn()
    {
        if (respawnSolicitado)
            return;

        respawnSolicitado = true;
        PreencherRespawnIdSeVazio();

        if (RespawnNatureza.Instancia != null && !string.IsNullOrWhiteSpace(respawnId))
        {
            RespawnNatureza.Instancia.AgendarRespawn(
                respawnId,
                posicaoOriginal,
                rotacaoOriginal);
        }
        else if (RespawnNatureza.Instancia == null)
        {
            { }
        }
        else if (string.IsNullOrWhiteSpace(respawnId))
        {
            { }
        }
    }
}
