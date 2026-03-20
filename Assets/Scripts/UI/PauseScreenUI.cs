using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PauseScreenUI : MonoBehaviour
{
    [Header("Buttons")]
    public RectTransform resumeButton;
    public RectTransform restartButton;
    public RectTransform optionsButton;
    public RectTransform retreatButton;

    [Header("Hover and Click Animation")]
    public float hoverScale = 1.12f;
    public float hoverDuration = 0.15f;
    public float punchStrength = 0.18f;

    [Header("Pause Menu Panel")]
    [Tooltip("Drag the PauseMenu CanvasGroup here for fade-in animation.")]
    public CanvasGroup pauseCanvasGroup;

    private SceneManagement _sceneManagement;

    // -----------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------

    private void Awake()
    {
        _sceneManagement = FindFirstObjectByType<SceneManagement>();
        if (_sceneManagement == null)
            Debug.LogError("[PauseScreenUI] SceneManagement not found in scene.", this);

        FixButtonHitArea(resumeButton);
        FixButtonHitArea(restartButton);
        FixButtonHitArea(optionsButton);
        FixButtonHitArea(retreatButton);
    }

    private void OnEnable()
    {
        // Every time the pause menu opens, animate it in
        AnimatePauseMenuIn();
    }

    // -----------------------------------------------
    // Pause Menu Open Animation
    // -----------------------------------------------

    private void AnimatePauseMenuIn()
    {
        // Fade the whole panel in
        if (pauseCanvasGroup != null)
        {
            pauseCanvasGroup.alpha = 0f;
            pauseCanvasGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        // Staggered button intros — scale from zero, matching title screen timing
        AnimateButtonIntro(resumeButton,  0.10f);
        AnimateButtonIntro(restartButton, 0.18f);
        AnimateButtonIntro(optionsButton, 0.26f);
        AnimateButtonIntro(retreatButton, 0.34f);
    }

    // -----------------------------------------------
    // Hover Events
    // -----------------------------------------------

    public void OnHoverEnter_Resume()  => HoverEnter(resumeButton);
    public void OnHoverExit_Resume()   => HoverExit(resumeButton);

    public void OnHoverEnter_Restart() => HoverEnter(restartButton);
    public void OnHoverExit_Restart()  => HoverExit(restartButton);

    public void OnHoverEnter_Options() => HoverEnter(optionsButton);
    public void OnHoverExit_Options()  => HoverExit(optionsButton);

    public void OnHoverEnter_Retreat() => HoverEnter(retreatButton);
    public void OnHoverExit_Retreat()  => HoverExit(retreatButton);

    // -----------------------------------------------
    // Click Handlers
    // -----------------------------------------------

    public void OnResumePressed()
    {
        PlayPunch(resumeButton);
        // Small delay so punch is visible, then unpause
        DOVirtual.DelayedCall(0.2f, () =>
        {
            _sceneManagement?.OnResumeButton();
        }).SetUpdate(true);
    }

    public void OnRestartPressed()
    {
        PlayPunch(restartButton);
        DOVirtual.DelayedCall(0.2f, () =>
        {
            _sceneManagement?.OnRestartButton();
        }).SetUpdate(true);
    }

    public void OnOptionsPressed()
    {
        PlayPunch(optionsButton);
        DOVirtual.DelayedCall(0.2f, () =>
        {
            _sceneManagement?.OnOptionsButton();
        }).SetUpdate(true);
    }

    public void OnRetreatPressed()
    {
        PlayPunch(retreatButton);
        DOVirtual.DelayedCall(0.2f, () =>
        {
            _sceneManagement?.OnReturnToMenuButton();
        }).SetUpdate(true);
    }

    // -----------------------------------------------
    // Animation Helpers
    // -----------------------------------------------

    private void AnimateButtonIntro(RectTransform rt, float delay)
    {
        if (rt == null) return;
        rt.localScale = Vector3.zero;
        rt.DOScale(Vector3.one, 0.35f)
            .SetDelay(delay)
            .SetEase(Ease.OutBack)
            .SetUpdate(true); // SetUpdate(true) = plays even when Time.timeScale = 0 (paused)
    }

    private void HoverEnter(RectTransform rt)
    {
        if (rt == null) return;
        rt.DOKill();
        rt.DOScale(Vector3.one * hoverScale, hoverDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void HoverExit(RectTransform rt)
    {
        if (rt == null) return;
        rt.DOKill();
        rt.DOScale(Vector3.one, hoverDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void PlayPunch(RectTransform rt)
    {
        if (rt == null) return;
        rt.DOKill();
        rt.DOPunchScale(Vector3.one * punchStrength, 0.25f, 8, 0.5f)
            .SetUpdate(true);
    }

    private void FixButtonHitArea(RectTransform rt)
    {
        if (rt == null) return;

        var btn = rt.GetComponent<Button>();
        if (btn == null) return;

        var img = rt.GetComponent<Image>();
        if (img == null)
            img = rt.gameObject.AddComponent<Image>();

        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        img.sprite = null;
        img.type = Image.Type.Simple;

        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
    }
}