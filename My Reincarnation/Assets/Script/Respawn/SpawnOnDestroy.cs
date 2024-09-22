using UnityEngine;

public class SpawnOnDestroy : MonoBehaviour
{
    [Header("Objeto para spawnar")]
    [Tooltip("Refer�ncia ao objeto que ser� spawnado quando este objeto for destru�do.")]
    [SerializeField] private GameObject objectToSpawn;

    [Header("Posi��o do spawn")]
    [Tooltip("Offset da posi��o onde o novo objeto ser� spawnado, baseado na posi��o do objeto atual.")]
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;

    [Header("Rota��o do spawn")]
    [Tooltip("Rota��o do objeto que ser� spawnado.")]
    [SerializeField] private Quaternion spawnRotation = Quaternion.identity;

    [Header("Chance de Spawn")]
    [Tooltip("Porcentagem de 0.001 a 100 que determina a chance do objeto ser spawnado.")]
    [SerializeField, Range(0.001f, 100f)] private float spawnChance = 100f;

    // M�todo chamado quando este objeto for destru�do
    private void OnDestroy()
    {
        // Verifica se o objeto a ser spawnado est� definido
        if (objectToSpawn != null)
        {
            // Gera um valor aleat�rio entre 0 e 100
            float randomValue = Random.Range(0f, 100f);

            // Verifica se o valor aleat�rio � menor ou igual � chance de spawn
            if (randomValue <= spawnChance)
            {
                // Calcula a posi��o onde o objeto ser� spawnado
                Vector3 spawnPosition = transform.position + spawnOffset;

                // Spawna o novo objeto na posi��o e rota��o especificadas
                Instantiate(objectToSpawn, spawnPosition, spawnRotation);
            }
        }
        else
        {
            Debug.LogWarning("Nenhum objeto definido para ser spawnado!");
        }
    }
}
