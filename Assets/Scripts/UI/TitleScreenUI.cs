using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title screen button animations and scene transition.
///
/// MANUAL SETUP IN INSPECTOR:
/// 1. Assign all four button RectTransforms to the matching fields.
/// 2. Wire each button's OnClick to this script's methods:
///      NewGameButton  -> OnNewGamePressed
///      ContinueButton -> OnContinuePressed
///      OptionsButton  -> OnOptionsPressed
///      QuitGameButton -> OnLeavePressed
/// 3. Wire hover via EventTrigger on each button:
///      PointerEnter -> OnHoverEnter_X
///      PointerExit  -> OnHoverExit_X
/// 4. Optionally assign FlashOverlay and BulletHoles for the transition effect.
/// </summary>
public class TitleScreenUI : MonoBehaviour
{
    [Header("Buttons")]
    public RectTransform newGameButton;
    public RectTransform continueButton;
    public RectTransform optionsButton;
    public RectTransform leaveButton;

    [Header("Hover and Click Animation")]
    public float hoverScale = 1.12f;
    public float hoverDuration = 0.15f;
    public float punchStrength = 0.18f;

    [Header("Gunshot Transition (optional — leave empty to skip)")]
    public CanvasGroup flashOverlay;
    public RectTransform[] bulletHoles;
    public float transitionDuration = 0.55f;

    private SceneManagement _sceneManagement;

    // -----------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------

    private void Awake()
    {
        _sceneManagement = FindFirstObjectByType<SceneManagement>();
        if (_sceneManagement == null)
            Debug.LogError("[TitleScreenUI] SceneManagement not found in scene.", this);

        // Fix the hit areas on all buttons so hover and clicks work across the full button rect
        FixButtonHitArea(newGameButton);
        FixButtonHitArea(continueButton);
        FixButtonHitArea(optionsButton);
        FixButtonHitArea(leaveButton);
    }

    private void Start()
    {
        // Make sure cursor is visible and unlocked on the title screen
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Staggered intro — buttons scale from zero on load
        AnimateButtonIntro(newGameButton,  0.20f);
        AnimateButtonIntro(continueButton, 0.30f);
        AnimateButtonIntro(optionsButton,  0.40f);
        AnimateButtonIntro(leaveButton,    0.50f);
    }

    // -----------------------------------------------
    // Hover Events
    // Wire via EventTrigger on each button:
    //   PointerEnter -> OnHoverEnter_X
    //   PointerExit  -> OnHoverExit_X
    // -----------------------------------------------

    public void OnHoverEnter_NewGame()  => HoverEnter(newGameButton);
    public void OnHoverExit_NewGame()   => HoverExit(newGameButton);

    public void OnHoverEnter_Continue() => HoverEnter(continueButton);
    public void OnHoverExit_Continue()  => HoverExit(continueButton);

    public void OnHoverEnter_Options()  => HoverEnter(optionsButton);
    public void OnHoverExit_Options()   => HoverExit(optionsButton);

    public void OnHoverEnter_Leave()    => HoverEnter(leaveButton);
    public void OnHoverExit_Leave()     => HoverExit(leaveButton);

    // -----------------------------------------------
    // Click Handlers
    // Wire to each button's OnClick in the Inspector
    // -----------------------------------------------

    public void OnNewGamePressed()
    {
        PlayPunch(newGameButton);
        PlayTransition(() => _sceneManagement?.OnNewGameButton());
    }

    public void OnContinuePressed()
    {
        PlayPunch(continueButton);
        PlayTransition(() => _sceneManagement?.OnNewGameButton());
    }

    public void OnOptionsPressed()
    {
        PlayPunch(optionsButton);
        PlayTransition(() => _sceneManagement?.OnOptionsButton());
    }

    public void OnLeavePressed()
    {
        PlayPunch(leaveButton);
        PlayTransition(() => _sceneManagement?.OnQuitGameButton());
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
            .SetEase(Ease.OutBack);
    }

    private void HoverEnter(RectTransform rt)
    {
        if (rt == null) return;
        rt.DOKill();
        rt.DOScale(Vector3.one * hoverScale, hoverDuration).SetEase(Ease.OutQuad);
    }

    private void HoverExit(RectTransform rt)
    {
        if (rt == null) return;
        rt.DOKill();
        rt.DOScale(Vector3.one, hoverDuration).SetEase(Ease.OutQuad);
    }

    private void PlayPunch(RectTransform rt)
    {
        if (rt == null) return;
        rt.DOKill();
        rt.DOPunchScale(Vector3.one * punchStrength, 0.25f, 8, 0.5f);
    }

    private void PlayTransition(System.Action onComplete)
    {
        if (flashOverlay == null)
        {
            onComplete?.Invoke();
            return;
        }

        var seq = DOTween.Sequence();

        seq.Append(flashOverlay.DOFade(0.9f, 0.04f));
        seq.Append(flashOverlay.DOFade(0f,   0.08f));

        if (bulletHoles != null)
        {
            foreach (var hole in bulletHoles)
            {
                if (hole == null) continue;
                hole.localScale = Vector3.zero;
                hole.gameObject.SetActive(true);
                seq.Append(hole.DOScale(1f, 0.05f).SetEase(Ease.OutExpo));
            }
        }

        seq.AppendInterval(0.15f);
        seq.AppendCallback(() =>
        {
            var img = flashOverlay.GetComponent<Image>();
            if (img != null) img.color = Color.black;
        });
        seq.Append(flashOverlay.DOFade(1f, transitionDuration).SetEase(Ease.InQuad));
        seq.OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>
    /// Ensures the button has a full-size transparent Image that covers its entire
    /// RectTransform, so hover and click detection work across the whole button area
    /// and not just the tiny default sprite bounds.
    /// </summary>
    private void FixButtonHitArea(RectTransform rt)
    {
        if (rt == null) return;

        var btn = rt.GetComponent<Button>();
        if (btn == null) return;

        var img = rt.GetComponent<Image>();
        if (img == null)
            img = rt.gameObject.AddComponent<Image>();

        // Fully transparent — invisible but catches raycasts across the full rect
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;

        // Sprite None + Simple type = fills the full RectTransform, no 32x32 sprite bounds
        img.sprite = null;
        img.type = Image.Type.Simple;

        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
    }
}