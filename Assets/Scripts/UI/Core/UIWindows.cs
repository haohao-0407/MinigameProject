using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public abstract class UIWindow : MonoBehaviour
{
    [Header("Window Info")]
    [SerializeField]
    private UIPanelType panelType;
    [SerializeField]
    private UIWindowType windowType = UIWindowType.Page;
    public UIPanelType PanelType => panelType;
    public UIWindowType WindowType => windowType;


    protected CanvasGroup canvasGroup;

    public bool IsOpen { get; private set; }
   
    #region Unity

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    protected virtual void Start()
    {
        Initialize();
        CloseImmediate();
    }

    #endregion
    #region Life Cycle

    protected virtual void Initialize()
    {

    }

    public virtual void Open()
    {
        IsOpen = true;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public virtual void Close()
    {
        IsOpen = false;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 初始化时直接隐藏，不播放动画
    /// </summary>
    public virtual void CloseImmediate() //初始化专用。
    {
        Debug.Log($"CloseImmediate : {name}");

        if (canvasGroup == null)
        {
            Debug.LogError($"{name} 的 CanvasGroup 为 NULL！");
            return;
        }

        IsOpen = false;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    #endregion

}