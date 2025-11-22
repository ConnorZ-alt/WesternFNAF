using System;                               // NEW: for Action
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class SceneManagement : MonoBehaviour
{
    // ===================== NEW: global game-flow signal =====================
    /// <summary>Raised exactly once when the run ends (Game Over or Results).</summary>
    public static event Action GameEnded;

    /// <summary>True after the run has ended (prevents further spawns, etc.).</summary>
    public static bool HasGameEnded { get; private set; } = false;

    private static void FireGameEndedOnce()
    {
        if (HasGameEnded) return;
        HasGameEnded = true;
        GameEnded?.Invoke();
    }
    // =======================================================================

    [Header("PauseMenu")]
    [SerializeField] private GameObject PauseMenu;

    [Header("Game Over")]
    [Tooltip("If set, this name will be used; otherwise the index is used.")]
    [SerializeField] private string gameOverSceneName = "";   // e.g., "Scenes/GameOverScreen"
    [SerializeField] private int    gameOverSceneIndex = 3;   // matches your build list
    private bool gameOver;

    [Header("Results")]
    [Tooltip("If set, this name will be used; otherwise the index is used.")]
    [SerializeField] private string resultsSceneName = "";    // e.g., "Scenes/ResultsScreen"
    [SerializeField] private int    resultsSceneIndex  = 4;   // matches your build list

    public static bool isPaused;

    private void Awake()
    {
        Time.timeScale = 1f;
        isPaused = false;
        gameOver = false;

        // Reset end-of-run flag when a gameplay scene starts
        HasGameEnded = false;

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
        // Block pause/resume after game over/results
        if (gameOver) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) OnResumeGame();
            else          OnPauseGame();
        }
    }

    void Start()
    {
        if (PauseMenu != null) PauseMenu.SetActive(false);
    }

    public void OnPauseGame()
    {
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

    public void OnResumeButton() => OnResumeGame();

    // Title Screen
    public void OnReturnToMenuButton()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(0);
    }

    // Start Level 1
    public void OnNewGameButton()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(1);
    }

    // Options
    public void OnOptionsButton()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(2);
    }

    // Quit
    public void OnQuitGameButton()
    {
        Application.Quit();
    }

    // Retry current level (your build uses HourOne at index 1)
    public void OnRetryButton()
    {
        Time.timeScale = 1f;
        OnResumeGame();
        SceneManager.LoadScene(1);
    }

    // ======== Game flow hooks you can call from other scripts ========

    // Called when the player dies (e.g., Dynamite.OnExplode -> UnityEvent)
    public void OnGameOver()
    {
        if (gameOver) return;
        gameOver = true;

        // NEW: mark & notify BEFORE scene load so listeners can stop spawning
        FireGameEndedOnce();

        Time.timeScale = 1f;
        isPaused = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AudioListener.pause = false;

        if (!string.IsNullOrEmpty(gameOverSceneName))
            SceneManager.LoadScene(gameOverSceneName);
        else if (gameOverSceneIndex >= 0)
            SceneManager.LoadScene(gameOverSceneIndex);
        else
            Debug.LogError("[SceneManagement] No GameOver scene set (name or index).");
    }

    // Called when train reaches goal (e.g., TrainController → this)
    public void OnShowResults()
    {
        if (gameOver) return;
        gameOver = true;

        // NEW: mark & notify BEFORE scene load so listeners can stop spawning
        FireGameEndedOnce();

        Time.timeScale = 1f;
        isPaused = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AudioListener.pause = false;

        if (!string.IsNullOrEmpty(resultsSceneName))
            SceneManager.LoadScene(resultsSceneName);
        else if (resultsSceneIndex >= 0)
            SceneManager.LoadScene(resultsSceneIndex);
        else
            Debug.LogError("[SceneManagement] No Results scene set (name or index).");
    }

    // Utility
    internal static void LoadScene(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }
}