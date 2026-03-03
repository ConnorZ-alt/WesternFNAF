using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Options screen button animations.
///
/// MANUAL SETUP IN INSPECTOR:
/// 1. Attach this to a new empty GameObject called "OptionsScreenManager".
/// 2. Assign the ReturnToTitleButton RectTransform.
/// 3. Wire the button's OnClick to: OnReturnToTitlePressed
/// 4. Wire hover via EventTrigger on the button:
///      PointerEnter -> OnHoverEnter_Return
///      PointerExit  -> OnHoverExit_Return
/// </summary>
public class OptionsScreenUI : MonoBehaviour
{
    [Header("Buttons")]
    public RectTransform returnToTitleButton;

    [Header("Animation")]
    public float hoverScale = 1.12f;
    public float hoverDuration = 0.15f;
    public float punchStrength = 0.18f;

    private SceneManagement _sceneManagement;

    private void Awake()
    {
        _sceneManagement = FindFirstObjectByType<SceneManagement>();
        if (_sceneManagement == null)
            Debug.LogError("[OptionsScreenUI] SceneManagement not found in scene.", this);

        FixButtonHitArea(returnToTitleButton);
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AnimateButtonIntro(returnToTitleButton, 0.2f);
    }

    // -----------------------------------------------
    // Hover Events
    // -----------------------------------------------

    public void OnHoverEnter_Return() => HoverEnter(returnToTitleButton);
    public void OnHoverExit_Return()  => HoverExit(returnToTitleButton);

    // -----------------------------------------------
    // Click Handler
    // -----------------------------------------------

    public void OnReturnToTitlePressed()
    {
        PlayPunch(returnToTitleButton);
        // Small delay so the punch animation is visible before the scene loads
        DOVirtual.DelayedCall(0.25f, () => _sceneManagement?.OnReturnToMenuButton());
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