using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SceneManagement : MonoBehaviour
{
    public static event Action GameEnded;

    public bool IsJumpscared = false;

    public static bool HasGameEnded { get; private set; } = false;

    private enum GameFlowState
    {
        Running,
        Paused,
        Ended
    }

    [Header("Pause Menu")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private int optionsSceneIndex = 2;

    [Header("Game Over Scene")]
    [SerializeField] private string gameOverSceneName = "";
    [SerializeField] private int gameOverSceneIndex = 3;

    [Header("Results Scene")]
    [SerializeField] private string resultsSceneName = "";
    [SerializeField] private int resultsSceneIndex = 4;

    [Header("Title Screen")]
    [Tooltip("Build index of your title/menu scene. Used to detect when we are in a menu so the cursor is not locked.")]
    [SerializeField] private int titleSceneIndex = 0;

    public static bool isPaused;

    private GameFlowState state = GameFlowState.Running;

    public static SceneManagement Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        HasGameEnded = false;
        SetPaused(false);

        if (pauseMenu != null)
            pauseMenu.SetActive(false);

        AudioListener.pause = false;

        state = GameFlowState.Running;

        // Only lock the cursor if we are NOT on the title/menu scene.
        // The title screen manages its own cursor state.
        bool isMenuScene = SceneManager.GetActiveScene().buildIndex == titleSceneIndex
                           || SceneManager.GetActiveScene().buildIndex == optionsSceneIndex;
        if (isMenuScene)
            SetCursorForMenus();
        else
            SetCursorForGameplay();
    }

    private void Update()
    {
        if (state == GameFlowState.Ended) return;

        // Don't allow pausing on the title screen
        bool isMenuScene = SceneManager.GetActiveScene().buildIndex == titleSceneIndex || SceneManager.GetActiveScene().buildIndex == optionsSceneIndex;
        if (isMenuScene) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (state == GameFlowState.Paused) OnResumeGame();
            else OnPauseGame();
        }
    }

    // -------------------------
    // Pause / Resume
    // -------------------------

    public void OnPauseGame()
    {
        if (state == GameFlowState.Ended) return;
        if (pauseMenu == null) return;

        pauseMenu.SetActive(true);
        SetPaused(true);
        SetCursorForMenus();
        state = GameFlowState.Paused;
    }

    public void OnResumeGame()
    {
        if (state == GameFlowState.Ended) return;
        if (pauseMenu == null) return;

        pauseMenu.SetActive(false);
        SetPaused(false);
        SetCursorForGameplay();
        state = GameFlowState.Running;
    }

    public void OnResumeButton() => OnResumeGame();

    // -------------------------
    // UI Buttons (Scene Loads)
    // -------------------------

    public void OnReturnToMenuButton()
    {
        PrepareForSceneChange();
        SceneManager.LoadScene(0);
    }

    public void OnNewGameButton()
    {
        PrepareForSceneChange();
        SceneManager.LoadScene(1);
    }

    public void OnOptionsButton()
    {
        PrepareForSceneChange();
        SceneManager.LoadScene(2);
    }

    public void OnQuitGameButton()
    {
        Application.Quit();
    }

    public void OnRetryButton()
    {
        PrepareForSceneChange();
        SceneManager.LoadScene(1);
    }

    // -------------------------
    // End-of-run hooks
    // -------------------------

    public void OnGameOver()
    {
        if (state == GameFlowState.Ended) return;

        EndRunNow();
        PrepareForSceneChange();

        if (!string.IsNullOrEmpty(gameOverSceneName))
            SceneManager.LoadScene(gameOverSceneName);
        else if (gameOverSceneIndex >= 0)
            SceneManager.LoadScene(gameOverSceneIndex);
        else
            Debug.LogError("[SceneManagement] No GameOver scene set.");
    }

    public void OnShowResults()
    {
        if (state == GameFlowState.Ended) return;

        EndRunNow();
        PrepareForSceneChange();

        if (!string.IsNullOrEmpty(resultsSceneName))
            SceneManager.LoadScene(resultsSceneName);
        else if (resultsSceneIndex >= 0)
            SceneManager.LoadScene(resultsSceneIndex);
        else
            Debug.LogError("[SceneManagement] No Results scene set.");
    }

    // -------------------------
    // Helpers
    // -------------------------

    private void EndRunNow()
    {
        if (HasGameEnded) return;
        HasGameEnded = true;
        state = GameFlowState.Ended;
        GameEnded?.Invoke();
    }

    private static void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
    }

    private static void SetCursorForGameplay()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void SetCursorForMenus()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void PrepareForSceneChange()
    {
        SetPaused(false);
        SetCursorForMenus();
        AudioListener.pause = false;

        if (pauseMenu != null)
            pauseMenu.SetActive(false);
    }

    internal static void LoadScene(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }
}