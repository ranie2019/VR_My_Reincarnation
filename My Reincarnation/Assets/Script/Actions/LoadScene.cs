using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Load scene using name, or reload the active scene
/// </summary>
public class LoadScene : MonoBehaviour
{
    // Carrega uma cena usando o nome fornecido
    public void LoadSceneUsingName(string sceneName)
    {
        if (SceneExists(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError($"Scene '{sceneName}' does not exist in the Build Settings.");
        }
    }

    // Recarrega a cena atual
    public void ReloadCurrentScene()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(currentSceneName))
        {
            SceneManager.LoadScene(currentSceneName);
        }
        else
        {
            Debug.LogError("Current scene name is null or empty!");
        }
    }

    // Verifica se a cena existe na lista de Build Settings
    private bool SceneExists(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameFromPath = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (sceneNameFromPath.Equals(sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
