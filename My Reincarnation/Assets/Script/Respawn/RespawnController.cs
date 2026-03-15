using UnityEngine;
using System.Collections;

public class RespawnController : MonoBehaviour
{
    // Método público para iniciar o respawn de um objeto após um tempo
    public void RespawnObjectWithDelay(GameObject objectToRespawn, Vector3 respawnPosition, float delay)
    {
        StartCoroutine(RespawnAfterDelay(objectToRespawn, respawnPosition, delay));
    }

    // Coroutine para respawnar o objeto após um tempo
    private IEnumerator RespawnAfterDelay(GameObject objectToRespawn, Vector3 respawnPosition, float delay)
    {
        // Espera pelo tempo de respawn antes de criar o objeto novamente
        yield return new WaitForSeconds(delay);

        // Respawna o novo objeto na posição e rotação padrão
        Instantiate(objectToRespawn, respawnPosition, Quaternion.identity);
    }
}
