using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OptionsScreenUI : MonoBehaviour
{
    // -----------------------------------------------
    // Navigation
    // -----------------------------------------------

    [Header("Return Button")]
    public RectTransform returnToTitleButton;

    [Header("Tab Buttons")]
    public RectTransform audioTabButton;
    public RectTransform displayTabButton;
    public RectTransform controlsTabButton;

    [Header("Panels")]
    [Tooltip("Parent object containing all Audio settings UI.")]
    public GameObject audioPanel;
    [Tooltip("Parent object containing all Display settings UI.")]
    public GameObject displayPanel;
    [Tooltip("Parent object containing Controls list UI.")]
    public GameObject controlsPanel;

    // -----------------------------------------------
    // Audio Settings
    // -----------------------------------------------

    [Header("Audio Settings")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    // -----------------------------------------------
    // Display Settings
    // -----------------------------------------------

    [Header("Display Settings")]
    public Slider brightnessSlider;
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    // -----------------------------------------------
    // Controls Display
    // -----------------------------------------------

    [Header("Controls")]
    [Tooltip("TMP text that displays the keybind list.")]
    public TextMeshProUGUI controlsListText;

    // -----------------------------------------------
    // Animation
    // -----------------------------------------------

    [Header("Animation")]
    public float hoverScale = 1.12f;
    public float hoverDuration = 0.15f;
    public float punchStrength = 0.18f;

    private SceneManagement _sceneManagement;
    private Resolution[] availableResolutions;

    // -----------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------

    private void Awake()
    {
        _sceneManagement = FindFirstObjectByType<SceneManagement>();

        FixButtonHitArea(returnToTitleButton);
        FixButtonHitArea(audioTabButton);
        FixButtonHitArea(displayTabButton);
        FixButtonHitArea(controlsTabButton);
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetupResolutionDropdown();
        SetupSliderListeners();
        LoadSavedSettings();
        PopulateControlsList();

        // Start on Audio tab
        ShowPanel(audioPanel);

        // Staggered intro
        AnimateButtonIntro(returnToTitleButton, 0.10f);
        AnimateButtonIntro(audioTabButton,      0.18f);
        AnimateButtonIntro(displayTabButton,    0.26f);
        AnimateButtonIntro(controlsTabButton,   0.34f);
    }

    // -----------------------------------------------
    // Tab Switching
    // -----------------------------------------------

    public void OnAudioTabPressed()
    {
        PlayPunch(audioTabButton);
        ShowPanel(audioPanel);
    }

    public void OnDisplayTabPressed()
    {
        PlayPunch(displayTabButton);
        ShowPanel(displayPanel);
    }

    public void OnControlsTabPressed()
    {
        PlayPunch(controlsTabButton);
        ShowPanel(controlsPanel);
    }

    private void ShowPanel(GameObject panelToShow)
    {
        if (audioPanel)   audioPanel.SetActive(audioPanel     == panelToShow);
        if (displayPanel) displayPanel.SetActive(displayPanel == panelToShow);
        if (controlsPanel) controlsPanel.SetActive(controlsPanel == panelToShow);
    }

    // -----------------------------------------------
    // Audio Settings
    // -----------------------------------------------

    private void SetupSliderListeners()
    {
        if (masterVolumeSlider)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (musicVolumeSlider)
        {
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider)
        {
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        if (brightnessSlider)
        {
            brightnessSlider.minValue = 0f;
            brightnessSlider.maxValue = 1f;
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        }
    }

    private void OnMasterVolumeChanged(float value)
    {
        if (AudioManager.Instance) AudioManager.Instance.SetMasterVolume(value);
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance) AudioManager.Instance.SetMusicVolume(value);
        PlayerPrefs.SetFloat("MusicVolume", value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance) AudioManager.Instance.SetSfxVolume(value);
        PlayerPrefs.SetFloat("SFXVolume", value);
    }

    // -----------------------------------------------
    // Display Settings
    // -----------------------------------------------

    private void SetupResolutionDropdown()
    {
        if (!resolutionDropdown) return;

        availableResolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        int currentResolutionIndex = 0;
        var options = new System.Collections.Generic.List<string>();

        for (int i = 0; i < availableResolutions.Length; i++)
        {
            string option = availableResolutions[i].width + " x " + availableResolutions[i].height;
            options.Add(option);

            if (availableResolutions[i].width == Screen.currentResolution.width &&
                availableResolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void OnResolutionChanged(int index)
    {
        if (availableResolutions == null || index >= availableResolutions.Length) return;
        Resolution res = availableResolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        PlayerPrefs.SetInt("ResolutionIndex", index);
    }

    public void OnFullscreenToggleChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
    }

    private void OnBrightnessChanged(float value)
    {
        // Brightness is controlled via a global CanvasGroup or post-processing overlay.
        // For now we store the value — wire to your post-process volume or a dark overlay CanvasGroup.
        PlayerPrefs.SetFloat("Brightness", value);

        // Example: if you have a dark overlay CanvasGroup in your scene:
        // brightnessOverlay.alpha = 1f - value;
    }

    // -----------------------------------------------
    // Controls List
    // -----------------------------------------------

    private void PopulateControlsList()
    {
        if (!controlsListText) return;

        controlsListText.text =
            "<b>Movement</b>\n" +
            "W / A / S / D  —  Move\n" +
            "\n" +
            "<b>Combat</b>\n" +
            "Right Mouse Button  —  Aim\n" +
            "Left Mouse Button   —  Fire\n" +
            "R                   —  Reload\n" +
            "\n" +
            "<b>Interaction</b>\n" +
            "E                   —  Interact / Pick Up\n" +
            "\n" +
            "<b>Menu</b>\n" +
            "Escape              —  Pause\n";
    }

    // -----------------------------------------------
    // Save / Load Settings
    // -----------------------------------------------

    private void LoadSavedSettings()
    {
        float master = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float music  = PlayerPrefs.GetFloat("MusicVolume",  0.6f);
        float sfx    = PlayerPrefs.GetFloat("SFXVolume",    1f);
        float brightness = PlayerPrefs.GetFloat("Brightness", 1f);
        bool fullscreen  = PlayerPrefs.GetInt("Fullscreen", 1) == 1;

        if (masterVolumeSlider) masterVolumeSlider.value = master;
        if (musicVolumeSlider)  musicVolumeSlider.value  = music;
        if (sfxVolumeSlider)    sfxVolumeSlider.value    = sfx;
        if (brightnessSlider)   brightnessSlider.value   = brightness;
        if (fullscreenToggle)   fullscreenToggle.isOn    = fullscreen;

        // Apply loaded values immediately
        if (AudioManager.Instance)
        {
            AudioManager.Instance.SetMasterVolume(master);
            AudioManager.Instance.SetMusicVolume(music);
            AudioManager.Instance.SetSfxVolume(sfx);
        }
    }

    // -----------------------------------------------
    // Return Button
    // -----------------------------------------------

    public void OnHoverEnter_Return() => HoverEnter(returnToTitleButton);
    public void OnHoverExit_Return()  => HoverExit(returnToTitleButton);

    public void OnHoverEnter_Audio()   => HoverEnter(audioTabButton);
    public void OnHoverExit_Audio()    => HoverExit(audioTabButton);

    public void OnHoverEnter_Display() => HoverEnter(displayTabButton);
    public void OnHoverExit_Display()  => HoverExit(displayTabButton);

    public void OnHoverEnter_Controls() => HoverEnter(controlsTabButton);
    public void OnHoverExit_Controls()  => HoverExit(controlsTabButton);

    public void OnReturnToTitlePressed()
    {
        PlayerPrefs.Save();
        PlayPunch(returnToTitleButton);
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