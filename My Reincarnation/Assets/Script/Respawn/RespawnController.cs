using UnityEngine;
using System.Collections;

public class RespawnController : MonoBehaviour
{
    // M�todo p�blico para iniciar o respawn de um objeto ap�s um tempo
    public void RespawnObjectWithDelay(GameObject objectToRespawn, Vector3 respawnPosition, float delay)
    {
        StartCoroutine(RespawnAfterDelay(objectToRespawn, respawnPosition, delay));
    }

    // Coroutine para respawnar o objeto ap�s um tempo
    private IEnumerator RespawnAfterDelay(GameObject objectToRespawn, Vector3 respawnPosition, float delay)
    {
        // Espera pelo tempo de respawn antes de criar o objeto novamente
        yield return new WaitForSeconds(delay);

        // Respawna o novo objeto na posi��o e rota��o padr�o
        Instantiate(objectToRespawn, respawnPosition, Quaternion.identity);
    }
}
