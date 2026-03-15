using UnityEngine;

public class RespawnOnDestroy : MonoBehaviour
{
    private RespawnManager respawnManager;

    // Método chamado quando o objeto for destruído
    private void OnDestroy()
    {
        // Verifica se o respawnManager foi atribuído
        if (respawnManager == null)
        {
            // Tenta encontrar o RespawnManager na cena
            respawnManager = FindObjectOfType<RespawnManager>();
        }

        // Verifica se o RespawnManager foi encontrado
        if (respawnManager != null)
        {
            // Chama o método de respawn no RespawnManager
            respawnManager.RespawnObjectWithDelay(transform.position);
        }
    }
}
