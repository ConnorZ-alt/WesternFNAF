using UnityEngine;
using UnityEngine.SceneManagement;

public class ShowMouseCursor : MonoBehaviour
{

    private static ShowMouseCursor instance;
    
    void Awake()
    {

        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;

        ShowCursor();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        DontDestroyOnLoad(this);
    }

    private void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
        ShowCursor();
    }

    private static void ShowCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
