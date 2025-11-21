using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CrosshairUI : MonoBehaviour
{
    [SerializeField] private ItemController revolverItemController;
    [SerializeField] private Image crosshairImage;
    [SerializeField] private float fadeLerp = 12f;

    [SerializeField] private Color normalColor = new Color(1, 1, 1, 0.9f);

    private CanvasGroup canvasGroup;

    void Awake()
    {
        if (!revolverItemController) revolverItemController = FindObjectOfType<ItemController>();
        if (!crosshairImage) crosshairImage = GetComponent<Image>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (crosshairImage) crosshairImage.raycastTarget = false;
        canvasGroup.alpha = 0f; // start hidden
    }

    void Update()
    {
        bool shouldShowCrosshair = revolverItemController && revolverItemController.IsAiming && !SceneManagement.isPaused;
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, shouldShowCrosshair ? 1f : 0f, Time.deltaTime * fadeLerp);

        // always use the same color
        if (crosshairImage) crosshairImage.color = normalColor;
    }
}