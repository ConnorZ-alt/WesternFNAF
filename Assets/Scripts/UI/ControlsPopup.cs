using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ControlsPopup : MonoBehaviour
{
    // -----------------------------------------------
    // Data
    // -----------------------------------------------

    [System.Serializable]
    public class ControlHint
    {
        [Tooltip("Internal key used to mark this control as used. E.g. 'Move', 'Aim', 'Fire', 'Reload'")]
        public string controlKey;

        [Tooltip("The UI row GameObject for this hint (will fade out when used).")]
        public CanvasGroup rowCanvasGroup;

        [Tooltip("Optional: TMP text showing the hint. Leave blank if the text is a child of the row.")]
        public TextMeshProUGUI hintText;

        [HideInInspector]
        public bool hasBeenUsed = false;
    }

    // -----------------------------------------------
    // Inspector Fields
    // -----------------------------------------------

    [Header("Popup Panel")]
    [Tooltip("CanvasGroup on the root ControlsPopup panel for fade in/out.")]
    [SerializeField] private CanvasGroup popupCanvasGroup;

    [Header("Control Hints")]
    [Tooltip("One entry per control to track. Add all essential controls here.")]
    [SerializeField] private List<ControlHint> hintRows = new List<ControlHint>();

    [Header("All Done Tooltip")]
    [Tooltip("GameObject shown after all controls have been used once.")]
    [SerializeField] private CanvasGroup allDoneTooltip;

    [Header("Timing")]
    [Tooltip("Delay before the popup fades in on game start.")]
    [SerializeField] private float introDelay = 1.5f;

    [Tooltip("How long the 'all done' tooltip stays visible before fading out.")]
    [SerializeField] private float allDoneDisplayDuration = 4f;

    [Tooltip("How long individual hint rows take to fade out when used.")]
    [SerializeField] private float hintFadeDuration = 0.4f;

    // -----------------------------------------------
    // Internal State
    // -----------------------------------------------

    private Dictionary<string, ControlHint> hintLookup;
    private int usedCount = 0;
    private bool allDoneShown = false;

    // -----------------------------------------------
    // Singleton (optional — makes it easy to call from player scripts)
    // -----------------------------------------------

    public static ControlsPopup Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Build lookup so MarkControlUsed() is O(1)
        hintLookup = new Dictionary<string, ControlHint>();
        foreach (var hint in hintRows)
        {
            if (!string.IsNullOrEmpty(hint.controlKey))
                hintLookup[hint.controlKey] = hint;
        }
    }

    private void Start()
    {
        // Start fully transparent
        if (popupCanvasGroup)
        {
            popupCanvasGroup.alpha = 0f;
            popupCanvasGroup.DOFade(1f, 0.5f)
                .SetDelay(introDelay)
                .SetEase(Ease.OutQuad);
        }

        // Hide all-done tooltip initially
        if (allDoneTooltip)
        {
            allDoneTooltip.alpha = 0f;
            allDoneTooltip.gameObject.SetActive(false);
        }
    }

    // -----------------------------------------------
    // Public API
    // -----------------------------------------------

    /// <summary>
    /// Call this from player scripts the first time the player uses a control.
    /// Example: ControlsPopup.Instance?.MarkControlUsed("Fire");
    /// </summary>
    public void MarkControlUsed(string controlKey)
    {
        if (!hintLookup.TryGetValue(controlKey, out ControlHint hint)) return;
        if (hint.hasBeenUsed) return;

        hint.hasBeenUsed = true;
        usedCount++;

        // Fade out this hint row
        if (hint.rowCanvasGroup)
        {
            hint.rowCanvasGroup.DOFade(0f, hintFadeDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => hint.rowCanvasGroup.gameObject.SetActive(false));
        }

        // Check if all essential controls have been used
        if (usedCount >= hintRows.Count && !allDoneShown)
            ShowAllDoneTooltip();
    }

    // -----------------------------------------------
    // All Done Tooltip
    // -----------------------------------------------

    private void ShowAllDoneTooltip()
    {
        allDoneShown = true;

        // Fade the main popup out first
        if (popupCanvasGroup)
            popupCanvasGroup.DOFade(0f, 0.3f).SetEase(Ease.OutQuad);

        if (allDoneTooltip == null) return;

        allDoneTooltip.gameObject.SetActive(true);
        allDoneTooltip.alpha = 0f;

        var seq = DOTween.Sequence();
        seq.Append(allDoneTooltip.DOFade(1f, 0.4f).SetEase(Ease.OutQuad));
        seq.AppendInterval(allDoneDisplayDuration);
        seq.Append(allDoneTooltip.DOFade(0f, 0.5f).SetEase(Ease.InQuad));
        seq.OnComplete(() => allDoneTooltip.gameObject.SetActive(false));
    }
}