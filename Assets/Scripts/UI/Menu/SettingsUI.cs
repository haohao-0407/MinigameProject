using UnityEngine;
using UnityEngine.UI;
public class SettingsUI : UIWindow
{
    [SerializeField] private Button btnBack;
    protected override void Initialize()
    {
        base.Initialize();

        btnBack.onClick.AddListener(OnBackClicked);
    }
    private void OnBackClicked()
    {
        UIManager.Instance.Back();
    }
}