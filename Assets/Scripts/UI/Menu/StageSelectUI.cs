using System;
using UnityEngine;
using UnityEngine.UI;

public class StageSelectUI : UIWindow
{
    [SerializeField] private Dropdown StageList1;
    [SerializeField] private Dropdown StageList2;
    [SerializeField] private Button btnStart;
    [SerializeField] private Button btnBack;

    protected override void Initialize()
    {
        base.Initialize();

        btnStart.onClick.AddListener(OnStartClicked);

        btnBack.onClick.AddListener(OnBackClicked);
    }

    private void OnStartClicked()
    {
        Debug.Log("Start Battle");

        string sceneName = ResolveSelectedStage();
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[StageSelectUI] No stage selected.");
            return;
        }

        SceneLoader.Instance.LoadSceneAsync(sceneName);
    }

    private string ResolveSelectedStage()
    {
        // TODO: read dropdown values and map to scene names.
        return "SampleScene";
    }

    private void OnBackClicked()
    {
        UIManager.Instance.Back();
    }
}
