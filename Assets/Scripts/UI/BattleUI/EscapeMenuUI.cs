using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ESC pause menu. Toggles the menu panel with the Escape key, pauses the game
/// while it is open (Time.timeScale = 0), wires the Exit button to quit the game
/// and the volume slider to the overall game volume (AudioListener.volume).
/// Attach to the menu panel (must have a CanvasGroup). Other buttons are left unwired.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class EscapeMenuUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private CanvasGroup panel;

    [Header("Buttons")]
    [SerializeField] private Button exitButton;

    [Header("Volume")]
    [SerializeField] private Slider volumeSlider;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (panel == null)
            panel = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        // Sync the slider to the current global volume, then listen for changes.
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(AudioListener.volume);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);

        // Start hidden and unpaused.
        SetOpen(false);
    }

    private void Update()
    {
        if (WasEscapePressedThisFrame())
            Toggle();
    }

    /// <summary>
    /// Reads the Escape key regardless of which input backend is active. The project
    /// has "Both" input handling with the new Input System package present, where the
    /// legacy Input class can be unreliable, so we prefer the new API when available.
    /// </summary>
    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
            return keyboard.escapeKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    private void Toggle()
    {
        SetOpen(!IsOpen);
    }

    private void SetOpen(bool open)
    {
        IsOpen = open;

        if (panel != null)
        {
            panel.alpha = open ? 1f : 0f;
            panel.interactable = open;
            panel.blocksRaycasts = open;
        }

        // Pause gameplay while the menu is open. The turn loop's AI coroutines
        // use WaitForSeconds, which respects timeScale, so this halts them too.
        Time.timeScale = open ? 0f : 1f;
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
    }

    private void OnExitClicked()
    {
        // Make sure we do not leave the game paused if this runs in the editor.
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDisable()
    {
        // Restore time scale if this object is disabled while the menu is open.
        if (IsOpen)
            Time.timeScale = 1f;
    }
}
