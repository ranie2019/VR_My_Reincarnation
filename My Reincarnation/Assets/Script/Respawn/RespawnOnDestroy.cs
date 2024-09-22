using UnityEngine;

public class RespawnOnDestroy : MonoBehaviour
{
    private RespawnManager respawnManager;

    // M�todo chamado quando o objeto for destru�do
    private void OnDestroy()
    {
        // Verifica se o respawnManager foi atribu�do
        if (respawnManager == null)
        {
            // Tenta encontrar o RespawnManager na cena
            respawnManager = FindObjectOfType<RespawnManager>();
        }

        // Chama o m�todo de respawn no RespawnManager
        respawnManager.RespawnObjectWithDelay(transform.position);
    }
}
