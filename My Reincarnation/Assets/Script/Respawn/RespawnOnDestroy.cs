using UnityEngine;

public class RespawnOnDestroy : MonoBehaviour
{
    private RespawnManager respawnManager;

    // M�todo chamado quando o objeto for destru�do
    private bool encerrandoAplicacao;
    private void OnApplicationQuit() { encerrandoAplicacao = true; }

    private void OnDestroy()
    {
        if (!Application.isPlaying || encerrandoAplicacao || !gameObject.scene.isLoaded) return;
        // Descarte de itens de inventario e limpeza de carga nao sao uma morte no mundo.
        var item = GetComponent<ItemPersistente>();
        if (item != null && item.EstaNoInventario()) return;
        if (SaveManager.Instancia != null &&
            SaveManager.Instancia.EstadoCarregamento != SaveManager.EstadoCarga.Concluido &&
            SaveManager.Instancia.EstadoCarregamento != SaveManager.EstadoCarga.SessaoNova) return;
        // Verifica se o respawnManager foi atribu�do
        if (respawnManager == null)
        {
            // Tenta encontrar o RespawnManager na cena
            respawnManager = FindFirstObjectByType<RespawnManager>();
        }

        // Verifica se o RespawnManager foi encontrado
        if (respawnManager != null)
        {
            // Chama o m�todo de respawn no RespawnManager
            respawnManager.RespawnObjectWithDelay(transform.position);
        }
    }
}
