using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class SceneManagement : MonoBehaviour
{
    [Header ("PauseMenu")]
    [SerializeField] private GameObject PauseMenu;
    
    public static bool isPaused;

    private void Awake()
    {
        Time.timeScale = 1f;
        isPaused = false;
        if (PauseMenu != null)
        {
            PauseMenu.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            AudioListener.pause = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AudioListener.pause = false;
        }
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                OnResumeGame();
            }
            else
            {
                OnPauseGame();
            }
        }
    }
    
    void Start()
    {
        PauseMenu.SetActive(false);
    }

    public void OnPauseGame()
    {
        // PauseMenu.SetActive(true);
        // Time.timeScale = 0f;
        // isPaused = true;
        if (PauseMenu != null)
        {
            PauseMenu.SetActive(true);
            Time.timeScale = 0f;
            isPaused = true;
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AudioListener.pause = true;
        }
    }

    public void OnResumeGame()
    {
        if (PauseMenu != null)
        {
            PauseMenu.SetActive(false);
            Time.timeScale = 1f;
            isPaused = false;
            
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            AudioListener.pause = false;
        }
    }

    public void OnResumeButton()
    {
        OnResumeGame();
    }
    
    // LOADS TO TITLE SCREEN WHEN BUTTON IS CLICKED
    public void OnReturnToMenuButton()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(0);
    }
    
    // LOADS HOUR 1/LEVEL 1 WHEN CLICKING NEW GAME
    public void OnNewGameButton()
    {
        Time.timeScale = 1f;
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
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        Time.timeScale = 1f;
        OnResumeGame();
        SceneManager.LoadScene(1);
    }
    // SEE ABOVE!!!

    // HELPS RUN SCENE MANAGEMENT!!!
    internal static void LoadScene(int sceneName)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
}
