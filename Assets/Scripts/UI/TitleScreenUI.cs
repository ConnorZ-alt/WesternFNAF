using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class TitleScreenUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button newGameButton;
    public Button continueButton;

    [Header("Transition Overlay")]
    public CanvasGroup flashOverlay;       // a full-screen white Image CanvasGroup
    public RectTransform[] bulletHoles;    // array of bullet hole Image RectTransforms

    [Header("Timing")]
    public float buttonPunchScale = 1.15f;
    public float transitionDuration = 0.6f;

    private SceneManagement _sceneManagement;

    void Start()
    {
        _sceneManagement = FindFirstObjectByType<SceneManagement>();

        // Buttons animate in on start - stagger them
        var buttons = new Button[] { newGameButton, continueButton };
        for (int i = 0; i < buttons.Length; i++)
        {
            var btn = buttons[i];
            var rt = btn.GetComponent<RectTransform>();
            rt.localScale = Vector3.zero;
            rt.DOScale(1f, 0.4f).SetDelay(0.3f + i * 0.12f).SetEase(Ease.OutBack);
        }
    }

    // Call this from the New Game button's OnClick
    public void OnNewGamePressed()
    {
        PlayButtonPunch(newGameButton.GetComponent<RectTransform>());
        PlayGunTransition(() => _sceneManagement.OnNewGameButton());
    }

    public void OnContinuePressed()
    {
        PlayButtonPunch(continueButton.GetComponent<RectTransform>());
        PlayGunTransition(() => _sceneManagement.OnNewGameButton()); // swap for save load later
    }

    private void PlayButtonPunch(RectTransform rt)
    {
        rt.DOKill();
        rt.DOPunchScale(Vector3.one * 0.2f, 0.3f, 8, 0.5f);
    }

    private void PlayGunTransition(System.Action onComplete)
    {
        var seq = DOTween.Sequence();

        // 1. Flash white (muzzle flash feel)
        seq.Append(flashOverlay.DOFade(0.85f, 0.05f));
        seq.Append(flashOverlay.DOFade(0f, 0.1f));

        // 2. Bullet holes slam in one by one
        foreach (var hole in bulletHoles)
        {
            hole.localScale = Vector3.zero;
            hole.gameObject.SetActive(true);
            seq.Append(hole.DOScale(1f, 0.06f).SetEase(Ease.OutExpo));
        }

        // 3. Short pause then fade to black
        seq.AppendInterval(0.2f);
        seq.Append(flashOverlay.DOFade(1f, transitionDuration).SetEase(Ease.InQuad)
            .OnStart(() => flashOverlay.GetComponent<Image>().color = Color.black));

        seq.OnComplete(() => onComplete?.Invoke());
    }
}