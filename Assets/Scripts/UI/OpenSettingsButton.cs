using UnityEngine;
using UnityEngine.UI;

// Attach to any Button to open the SettingsUI panel.
// Works with UIManager if present; falls back to direct toggle otherwise.
[RequireComponent(typeof(Button))]
public class OpenSettingsButton : MonoBehaviour
{
    [SerializeField] private SettingsUI targetSettings;

    private void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(OpenSettings);
    }

    private void OpenSettings()
    {
        // Try UIManager first (MainMenu scene).
        if (UIManager.Instance != null)
        {
            UIManager.Instance.Open(UIPanelType.Settings);
            return;
        }

        // Fallback: direct toggle (battle scene).
        if (targetSettings == null)
            targetSettings = FindObjectOfType<SettingsUI>(true);

        if (targetSettings != null)
            targetSettings.Open();
    }
}
