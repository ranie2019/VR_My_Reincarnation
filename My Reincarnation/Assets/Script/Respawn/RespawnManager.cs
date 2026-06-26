using UnityEngine;
using System.Collections;

public class RespawnManager : MonoBehaviour
{
    [Header("Objeto para respawnar")]
    [Tooltip("Refer�ncia ao prefab que ser� respawnado.")]
    [SerializeField] private GameObject objectToRespawn;

    [Header("Posi��o do respawn")]
    [Tooltip("Offset da posi��o onde o novo objeto ser� respawnado, baseado na posi��o original.")]
    [SerializeField] private Vector3 respawnOffset = Vector3.zero;

    [Header("Tempo para Respawn")]
    [Tooltip("Tempo em segundos antes de o objeto ser respawnado ap�s a destrui��o.")]
    [SerializeField] public float respawnTime = 3f;

    // Verifica se o prefab est� definido ao iniciar
    private void Start()
    {
        if (objectToRespawn == null)
        {
            { }
        }
    }

    // M�todo p�blico para iniciar o respawn de um objeto ap�s um tempo
    public void RespawnObjectWithDelay(Vector3 originalPosition)
    {
        if (objectToRespawn == null)
        {
            { }
            return;
        }

        StartCoroutine(RespawnAfterDelay(originalPosition));
    }

    // Coroutine para respawnar o objeto ap�s um tempo
    private IEnumerator RespawnAfterDelay(Vector3 originalPosition)
    {
        // Espera pelo tempo de respawn antes de criar o objeto novamente
        yield return new WaitForSeconds(respawnTime);

        // Calcula a posi��o onde o objeto ser� respawnado
        Vector3 respawnPosition = originalPosition + respawnOffset;

        // Respawna o novo objeto na posi��o e rota��o padr�o
        Instantiate(objectToRespawn, respawnPosition, Quaternion.identity);
    }
}
