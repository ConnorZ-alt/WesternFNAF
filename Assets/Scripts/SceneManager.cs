using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManager : MonoBehaviour
{
    // LOADS TO TITLE SCREEN WHEN BUTTON IS CLICKED
    public void OnReturnToMenuButton()
    {
        SceneManager.LoadScene(0);
    }
    
    // LOADS HOUR 1/LEVEL 1 WHEN CLICKING NEW GAME
    public void OnNewGameButton()
    {
        SceneManager.LoadScene(1);
    }

    // COME BACK TO THIS FOR SAVING PLAYER SPOT WHEN CONTINUING A PLAYTHROUGH!!!
    
    // public void OnContinueGameButton()
    // {
    //     SceneManager.LoadScene(1);
    // }

    // LOADS TO OPTIONS MENU (CHANGE LATER WHEN ADDING NEW LEVELS)
    public void OnOptionsButton()
    {
        SceneManager.LoadScene(2);
    }

    // QUITS THE GAME WHEN CLICKED
    public void OnQuitGameButton()
    {
        Application.Quit();
        
        // CODE GIVEN BY IRENE TO QUIT THE GAME (LIKE IN 245 PROJECT)
// #if UNITY_EDITOR
//         UnityEditor.EditorApplication.isPlaying = false;
// #endif
    }

    // COME BACK TO THESE WHEN ADDING NEW LEVELS!!!
    
    // public void OnNextHourButton()
    // {
    //     SceneManager.LoadScene(2);
    // }
    
    public void OnRetryButton()
    {
        SceneManager.LoadScene(1);
    }
    // SEE ABOVE!!!

    // HELPS RUN SCENE MANAGEMENT!!!
    internal static void LoadScene(int sceneName)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
}
