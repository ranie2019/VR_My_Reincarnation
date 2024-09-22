using UnityEngine;
using System.Collections;

public class RespawnManager : MonoBehaviour
{
    [Header("Objeto para respawnar")]
    [Tooltip("Referência ao prefab que será respawnado.")]
    [SerializeField] private GameObject objectToRespawn;

    [Header("Posição do respawn")]
    [Tooltip("Offset da posição onde o novo objeto será respawnado, baseado na posição original.")]
    [SerializeField] private Vector3 respawnOffset = Vector3.zero;

    [Header("Tempo para Respawn")]
    [Tooltip("Tempo em segundos antes de o objeto ser respawnado após a destruição.")]
    [SerializeField] public float respawnTime = 3f;

    // Método público para iniciar o respawn de um objeto após um tempo
    public void RespawnObjectWithDelay(Vector3 originalPosition)
    {
        if (objectToRespawn == null)
        {
            Debug.LogError("O prefab objectToRespawn não está definido!");
            return;
        }

        StartCoroutine(RespawnAfterDelay(originalPosition));
    }

    // Coroutine para respawnar o objeto após um tempo
    private IEnumerator RespawnAfterDelay(Vector3 originalPosition)
    {
        // Espera pelo tempo de respawn antes de criar o objeto novamente
        yield return new WaitForSeconds(respawnTime);

        // Calcula a posição onde o objeto será respawnado
        Vector3 respawnPosition = originalPosition + respawnOffset;

        // Respawna o novo objeto na posição e rotação padrão
        Instantiate(objectToRespawn, respawnPosition, Quaternion.identity);
    }
}
