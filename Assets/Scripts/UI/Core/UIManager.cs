using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private Dictionary<UIPanelType, UIWindow> windows = new();

    private readonly Stack<UIPanelType> pageStack = new();



    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        Debug.Log("UIManager Start");

        UIWindow[] allWindows = FindObjectsOfType<UIWindow>(true);

        foreach (UIWindow window in allWindows)
        {
            Debug.Log($"É¨Ãèµ½£º{window.name}");
            windows.Add(window.PanelType, window);
            window.CloseImmediate();
        }

        Open(UIPanelType.MainMenu);
    }

    public void Open(UIPanelType panel)
    {
        Debug.Log(panel);
        Debug.Log(windows.ContainsKey(panel));

        if (!windows.TryGetValue(panel, out UIWindow window))
        {
            Debug.LogError($"{panel} ²»´æÔÚ");
            return;
        }

        switch (window.WindowType)
        {
            case UIWindowType.Page:
                OpenPage(panel);
                break;

            case UIWindowType.Popup:
                window.Open();
                break;

            case UIWindowType.Overlay:
                window.Open();
                break;
        }
    }

    private void OpenPage(UIPanelType panel)
    {
        if (pageStack.Count > 0)
        {
            UIPanelType current = pageStack.Peek();

            if (current == panel)
                return;

            windows[current].Close();
        }

        pageStack.Push(panel);

        windows[panel].Open();
    }

    public void Back()
    {
        if (pageStack.Count <= 1)
            return;

        UIPanelType current = pageStack.Pop();

        windows[current].Close();

        UIPanelType previous = pageStack.Peek();

        windows[previous].Open();
    }
}