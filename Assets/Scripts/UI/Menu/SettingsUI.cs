using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsUI : UIWindow
{
    [Header("Audio")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text musicLabel;
    [SerializeField] private TMP_Text sfxLabel;

    [Header("Display")]
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Text fullscreenLabel;

    [Header("Buttons")]
    [SerializeField] private Button btnSave;
    [SerializeField] private Button btnBack;

    private const string PREFS_MUSIC = "MusicVolume";
    private const string PREFS_SFX = "SfxVolume";
    private const string PREFS_FULLSCREEN = "Fullscreen";

    protected override void Initialize()
    {
        base.Initialize();

        LoadSettings();
        WireCallbacks();
    }

    private void LoadSettings()
    {
        float musicVol = PlayerPrefs.GetFloat(PREFS_MUSIC, 0.75f);
        float sfxVol = PlayerPrefs.GetFloat(PREFS_SFX, 0.75f);
        bool fs = PlayerPrefs.GetInt(PREFS_FULLSCREEN, 1) == 1;

        if (musicSlider != null) musicSlider.value = musicVol;
        if (sfxSlider != null) sfxSlider.value = sfxVol;
        if (fullscreenToggle != null) fullscreenToggle.isOn = fs;

        RefreshLabels();
        ApplyAudio();
        ApplyFullscreen();
    }

    private void WireCallbacks()
    {
        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(_ => RefreshLabels());
        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(_ => RefreshLabels());
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(_ =>
            {
                RefreshLabels();
                ApplyFullscreen();
            });
        if (btnSave != null)
            btnSave.onClick.AddListener(OnSaveClicked);
        if (btnBack != null)
            btnBack.onClick.AddListener(OnBackClicked);
    }

    private void RefreshLabels()
    {
        if (musicLabel != null && musicSlider != null)
            musicLabel.text = $"Music  {Mathf.RoundToInt(musicSlider.value * 100)}%";
        if (sfxLabel != null && sfxSlider != null)
            sfxLabel.text = $"SFX    {Mathf.RoundToInt(sfxSlider.value * 100)}%";
        if (fullscreenLabel != null && fullscreenToggle != null)
            fullscreenLabel.text = fullscreenToggle.isOn
                ? "Fullscreen  ON"
                : "Fullscreen  OFF";
    }

    private void ApplyAudio()
    {
        // TODO: route through AudioManager when it is implemented.
    }

    private void ApplyFullscreen()
    {
        if (fullscreenToggle != null)
            Screen.fullScreen = fullscreenToggle.isOn;
    }

    private void OnSaveClicked()
    {
        if (musicSlider != null)
            PlayerPrefs.SetFloat(PREFS_MUSIC, musicSlider.value);
        if (sfxSlider != null)
            PlayerPrefs.SetFloat(PREFS_SFX, sfxSlider.value);
        if (fullscreenToggle != null)
            PlayerPrefs.SetInt(PREFS_FULLSCREEN, fullscreenToggle.isOn ? 1 : 0);

        PlayerPrefs.Save();
        ApplyAudio();
        ApplyFullscreen();

        Debug.Log("[SettingsUI] Saved.");
    }

    private void OnBackClicked()
    {
        // In MainMenu scene: pop the UIManager page stack.
        if (UIManager.Instance != null)
            UIManager.Instance.Back();

        // Close ourselves. Works whether UIManager handled it or not.
        if (IsOpen)
            Close();
    }
}
