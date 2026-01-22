using UnityEngine;

public class SpawnOnDestroy : MonoBehaviour
{
    [Header("Objeto para spawnar")]
    [Tooltip("Referência ao objeto que será spawnado quando este objeto for destruído.")]
    [SerializeField] private GameObject objectToSpawn;

    [Header("Posição do spawn")]
    [Tooltip("Offset da posição onde o novo objeto será spawnado, baseado na posição do objeto atual.")]
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;

    [Header("Rotação do spawn")]
    [Tooltip("Rotação do objeto que será spawnado.")]
    [SerializeField] private Quaternion spawnRotation = Quaternion.identity;

    [Header("Chance de Spawn")]
    [Tooltip("Porcentagem de 0.001 a 100 que determina a chance do objeto ser spawnado.")]
    [SerializeField, Range(0.001f, 100f)] private float spawnChance = 100f;

    // Método chamado quando este objeto for destruído
    private void OnDestroy()
    {
        // Verifica se o objeto a ser spawnado está definido
        if (objectToSpawn != null)
        {
            // Gera um valor aleatório entre 0 e 100
            float randomValue = Random.Range(0f, 100f);

            // Verifica se o valor aleatório é menor ou igual à chance de spawn
            if (randomValue <= spawnChance)
            {
                // Calcula a posição onde o objeto será spawnado
                Vector3 spawnPosition = transform.position + spawnOffset;

                // Spawna o novo objeto na posição e rotação especificadas
                Instantiate(objectToSpawn, spawnPosition, spawnRotation);
            }
        }
    }
}
