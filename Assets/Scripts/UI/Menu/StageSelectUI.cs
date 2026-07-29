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
    }
    private void OnBackClicked()
    {
        UIManager.Instance.Back();
    }

}