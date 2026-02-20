using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SceneManagement : MonoBehaviour
{
    /// <summary>
    /// This event fires exactly once when the run ends (Game Over OR Results).
    /// Other scripts can subscribe so they can stop spawning enemies, stop timers, etc.
    /// </summary>
    public static event Action GameEnded;

    public bool IsJumpscared = false;

    /// <summary>
    /// After this becomes true, the run is “over” and gameplay systems should stop doing stuff.
    /// </summary>
    public static bool HasGameEnded { get; private set; } = false;

    private enum GameFlowState
    {
        Running, // normal gameplay
        Paused,  // pause menu up
        Ended    // game over/results triggered
    }

    [Header("Pause Menu")]
    [SerializeField] private GameObject pauseMenu;

    [Header("Game Over Scene")]
    [Tooltip("If set, this name will be used; otherwise index is used.")]
    [SerializeField] private string gameOverSceneName = "";
    [SerializeField] private int gameOverSceneIndex = 3;

    [Header("Results Scene")]
    [Tooltip("If set, this name will be used; otherwise index is used.")]
    [SerializeField] private string resultsSceneName = "";
    [SerializeField] private int resultsSceneIndex = 4;

    // Keeping this because other scripts already reference it.
    public static bool isPaused;

    private GameFlowState state = GameFlowState.Running;
    
    public static SceneManagement Instance; //makes sure there is only one

    private void Awake()
    {
        //Checks if there is another SceneManagement and if so destroy this one
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // When the scene starts, we assume we are in gameplay and not paused.
        // Also reset global end-of-run flag in case this scene is loaded again.
        HasGameEnded = false;

        SetPaused(false);
        SetCursorForGameplay();

        // Pause menu should start hidden.
        if (pauseMenu != null)
            pauseMenu.SetActive(false);

        // Make sure sound is not stuck paused.
        AudioListener.pause = false;

        state = GameFlowState.Running;
    }

    private void Update()
    {
        // If the run already ended, do not allow pause toggling anymore.
        if (state == GameFlowState.Ended) return;

        // Escape toggles pause.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (state == GameFlowState.Paused) OnResumeGame();
            else OnPauseGame();
        }
    }

    // -------------------------
    // Pause / Resume
    // -------------------------

    /// <summary>
    /// Pauses the game: time stops, pause menu shows, cursor unlocks.
    /// </summary>
    public void OnPauseGame()
    {
        // If the run is over, you should not pause anymore.
        if (state == GameFlowState.Ended) return;

        if (pauseMenu == null) return;

        pauseMenu.SetActive(true);
        SetPaused(true);
        SetCursorForMenus();

        state = GameFlowState.Paused;
    }

    /// <summary>
    /// Resumes the game: time continues, pause menu hides, cursor locks.
    /// </summary>
    public void OnResumeGame()
    {
        // If the run is over, you should not resume gameplay.
        if (state == GameFlowState.Ended) return;

        if (pauseMenu == null) return;

        pauseMenu.SetActive(false);
        SetPaused(false);
        SetCursorForGameplay();

        state = GameFlowState.Running;
    }

    /// <summary>
    /// This is just a helper for UI buttons that call “Resume”.
    /// </summary>
    public void OnResumeButton() => OnResumeGame();

    // -------------------------
    // UI Buttons (Scene Loads)
    // -------------------------

    /// <summary>
    /// Loads the title/menu scene (build index 0).
    /// </summary>
    public void OnReturnToMenuButton()
    {
        PrepareForSceneChange();
        SceneManager.LoadScene(0);
    }

    /// <summary>
    /// Starts the main gameplay scene (build index 1).
    /// </summary>
    public void OnNewGameButton()
    {
        PrepareForSceneChange();
        SceneManager.LoadScene(1);
    }

    /// <summary>
    /// Loads your options scene (build index 2).
    /// </summary>
    public void OnOptionsButton()
    {
        PrepareForSceneChange();
        SceneManager.LoadScene(2);
    }

    /// <summary>
    /// Quits the game application (does nothing in editor).
    /// </summary>
    public void OnQuitGameButton()
    {
        Application.Quit();
    }

    /// <summary>
    /// Retries the current level (your build uses HourOne at index 1).
    /// </summary>
    public void OnRetryButton()
    {
        PrepareForSceneChange();
        SceneManager.LoadScene(1);
    }

    // -------------------------
    // End-of-run hooks (called by other scripts)
    // -------------------------

    /// <summary>
    /// Call this when the player loses (like from an explosion damage event).
    /// It ends the run and loads the Game Over scene.
    /// </summary>
    public void OnGameOver()
    {
        if (state == GameFlowState.Ended) return;

        EndRunNow(); // fires GameEnded once and locks state

        PrepareForSceneChange();

        if (!string.IsNullOrEmpty(gameOverSceneName))
        {
            SceneManager.LoadScene(gameOverSceneName);
        }
        else if (gameOverSceneIndex >= 0)
        {
            SceneManager.LoadScene(gameOverSceneIndex);
        }
        else
            Debug.LogError("[SceneManagement] No GameOver scene set (name or index).");
    }

    /// <summary>
    /// Call this when the train reaches the goal.
    /// It ends the run and loads the Results scene.
    /// </summary>
    public void OnShowResults()
    {
        if (state == GameFlowState.Ended) return;

        EndRunNow(); // fires GameEnded once and locks state

        PrepareForSceneChange();

        if (!string.IsNullOrEmpty(resultsSceneName))
            SceneManager.LoadScene(resultsSceneName);
        else if (resultsSceneIndex >= 0)
            SceneManager.LoadScene(resultsSceneIndex);
        else
            Debug.LogError("[SceneManagement] No Results scene set (name or index).");
    }

    // -------------------------
    // Helpers
    // -------------------------

    /// <summary>
    /// Ends the run exactly once and notifies listeners.
    /// This is the “one true place” where GameEnded happens.
    /// </summary>
    private void EndRunNow()
    {
        if (HasGameEnded) return;

        HasGameEnded = true;
        state = GameFlowState.Ended;

        // Notifying observers: spawners, AI, etc. can stop safely.
        GameEnded?.Invoke();
    }

    /// <summary>
    /// Sets Time.timeScale and audio pause to match the pause state.
    /// </summary>
    private static void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        // In pause menu we usually pause audio too.
        AudioListener.pause = paused;
    }

    /// <summary>
    /// Cursor locked/hidden is what you want during FPS gameplay.
    /// </summary>
    private static void SetCursorForGameplay()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// Cursor unlocked/visible is what you want in menus.
    /// </summary>
    private static void SetCursorForMenus()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// This is called right before a scene load.
    /// We make sure timeScale is normal so the next scene doesn’t load “frozen”.
    /// </summary>
    private void PrepareForSceneChange()
    {
        // Always unpause when switching scenes.
        SetPaused(false);
        SetCursorForMenus();
        AudioListener.pause = false;

        // Also hide pause menu so it won’t flash if this object persists.
        if (pauseMenu != null)
            pauseMenu.SetActive(false);
    }

    /// <summary>
    /// Utility wrapper you had before. Keeps the same signature.
    /// </summary>
    internal static void LoadScene(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }
}
