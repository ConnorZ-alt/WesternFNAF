using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class CrosshairUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ItemController revolverItemController; // the gun that tells us if we are aiming
    [SerializeField] private Image crosshairImage;                  // the UI image for the crosshair

    [Header("Fade Settings")]
    [SerializeField] private float fadeLerp = 12f;                  // higher = fades faster

    [Header("Look")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.9f);

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // Awake runs when this UI object is created.
        // We use it to grab components and set default settings.

        if (!revolverItemController)
            revolverItemController = FindObjectOfType<ItemController>();

        if (!crosshairImage)
            crosshairImage = GetComponent<Image>();

        canvasGroup = GetComponent<CanvasGroup>();

        // Make sure the crosshair doesn’t block clicks (it’s just a picture).
        if (crosshairImage)
        {
            crosshairImage.raycastTarget = false;
            crosshairImage.color = normalColor; // set once here (not every frame)
        }

        // Start hidden so it doesn’t pop in at the beginning.
        canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        // Update runs every frame.
        // We decide whether we *should* show the crosshair, then smoothly fade to that value.

        float targetAlpha = ShouldShowCrosshair() ? 1f : 0f;
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeLerp);

        // If you ever want to change crosshair color based on state later,
        // this is where you would do it.
    }

    /// <summary>
    /// Returns true when the crosshair should be visible.
    /// </summary>
    private bool ShouldShowCrosshair()
    {
        // Crosshair should NOT show if the game is paused.
        if (SceneManagement.isPaused) return false;

        // We need a gun reference, and we only show when the gun says we are aiming.
        if (!revolverItemController) return false;

        return revolverItemController.IsAiming;
    }
}
