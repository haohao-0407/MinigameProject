using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : UIWindow
{
    [SerializeField] private Button btnStart;
    [SerializeField] private Button btnSettings;
    [SerializeField] private Button btnQuit;

    protected override void Initialize()
    {
        Debug.Log("MainMenu Initialize");

        Debug.Log(btnStart);
        Debug.Log(btnSettings);
        Debug.Log(btnQuit);

        base.Initialize();

        btnStart.onClick.AddListener(OnStartClicked);

        btnSettings.onClick.AddListener(OnSettingsClicked);

        btnQuit.onClick.AddListener(OnQuitClicked);
    }

    public void OnStartClicked()
    {
        Debug.Log("Start Click");
        UIManager.Instance.Open(UIPanelType.StageSelect);
    }

    private void OnSettingsClicked()
    {
        UIManager.Instance.Open(UIPanelType.Settings);
    }

    private void OnQuitClicked()
    {
        Application.Quit();
    }

    public override void CloseImmediate() //初始化专用。
    {
        return;
    }
}