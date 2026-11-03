using UnityEngine;
using UnityEngine.SceneManagement;

public class loadSync : MonoBehaviour
{
    public string sceneName;
    public void LoadSceneByName()
    {
        SceneManager.LoadSceneAsync(sceneName);
    }
}
