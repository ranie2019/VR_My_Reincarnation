using UnityEngine;
using System.Collections;

public class MonsterSpawnSystem : MonoBehaviour
{
    [Header("Configurações de Spawn")]
    [SerializeField] private GameObject monstroPrefab; // Prefab do monstro (ex: Slime)
    [SerializeField] private float tempoParaRespawn = 3f; // Tempo de espera antes de respawn
    private GameObject monstroAtual; // Referência para o monstro instanciado

    void Start()
    {
        // Spawn inicial do monstro
        SpawnarMonstro(transform.position);  // Usa a posição atual do objeto que contém o script para o spawn inicial
    }

    // Função chamada quando um monstro morre
    public void MonstroMorreu()
    {
        // Inicia a rotina de respawn após um tempo
        StartCoroutine(RespawnarMonstro());
    }

    // Corrotina para aguardar o tempo de respawn e instanciar o monstro novamente
    private IEnumerator RespawnarMonstro()
    {
        // Aguarda o tempo de respawn configurado
        yield return new WaitForSeconds(tempoParaRespawn);

        // Verifica se o monstro atual foi destruído corretamente e instancia o novo monstro na posição do anterior
        if (monstroAtual == null)
        {
            SpawnarMonstro(monstroAtual.transform.position);  // Usa a posição do monstro anterior como spawn
        }
    }

    // Função para instanciar o monstro
    private void SpawnarMonstro(Vector3 position)
    {
        // Verifica se o prefab do monstro foi atribuído
        if (monstroPrefab != null)
        {
            // Instancia o monstro na posição passada como parâmetro (pode ser a posição do monstro anterior)
            monstroAtual = Instantiate(monstroPrefab, position, Quaternion.identity);

            // Log de debug informando que o monstro foi instanciado
            Debug.Log($"Novo monstro instanciado na posição: {position}");
        }
        else
        {
            Debug.LogError("Prefab de monstro não atribuído!");
        }
    }
}
