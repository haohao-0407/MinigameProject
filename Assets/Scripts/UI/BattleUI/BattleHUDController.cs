using UnityEngine;
using Vampire.Units;
using Vampire.Turns;

public class BattleHUDController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleHUD battleHUD;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private UnitHoverDetector hoverDetector;

    [Header("Input")]
    [Tooltip("鼠标右键解除信息锁定。")]
    [SerializeField] private bool rightClickUnlock = true;

    // 用户点击后锁定显示的单位。
    private Unit lockedUnit;

    // 上一帧的回合行动单位。
    private Unit lastTurnUnit;

    // 当前已经交给 BattleHUD 显示的单位。
    private Unit displayedUnit;

    private void Awake()
    {
        if (battleHUD == null)
            battleHUD = GetComponent<BattleHUD>();

        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();

        if (hoverDetector == null)
            hoverDetector = FindObjectOfType<UnitHoverDetector>();
    }

    private void Start()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        lastTurnUnit = turnManager.CurrentUnit;
        ShowUnit(lastTurnUnit);
    }

    private void Update()
    {
        if (turnManager == null || battleHUD == null)
            return;

        UpdateTurnText();
        HandleTurnChanged();
        HandleMouseInput();
        UpdateDisplayedUnit();
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        if (battleHUD == null)
        {
            Debug.LogError(
                "[BattleHUDController] 没有连接 BattleHUD。",
                this
            );

            valid = false;
        }

        if (turnManager == null)
        {
            Debug.LogError(
                "[BattleHUDController] 没有连接 TurnManager。",
                this
            );

            valid = false;
        }

        if (hoverDetector == null)
        {
            Debug.LogError(
                "[BattleHUDController] 没有连接 UnitHoverDetector。",
                this
            );

            valid = false;
        }

        return valid;
    }

    private void UpdateTurnText()
    {
        /*
         * 保留你现有的 currentIndex 用法。
         *
         * 如果 currentIndex 从0开始，而你希望UI从 Turn 1 开始显示，
         * 可以改为：
         *
         * battleHUD.SetTurn(turnManager.currentIndex + 1);
         */
        battleHUD.SetTurn(turnManager.currentIndex);
    }

    private void HandleTurnChanged()
    {
        Unit currentTurnUnit = turnManager.CurrentUnit;

        if (currentTurnUnit == lastTurnUnit)
            return;

        lastTurnUnit = currentTurnUnit;

        /*
         * 进入下一个单位的回合时：
         * 1. 自动解除旧锁定
         * 2. 默认显示新回合主角
         */
        lockedUnit = null;

        ShowUnit(currentTurnUnit);
    }

    private void HandleMouseInput()
    {
        Unit hoverUnit = hoverDetector.HoverUnit;

        // 左键点击单位：锁定该单位的信息。
        if (Input.GetMouseButtonDown(0) && hoverUnit != null)
        {
            lockedUnit = hoverUnit;
            ShowUnit(lockedUnit);
        }

        // 右键：解除锁定。
        if (rightClickUnlock && Input.GetMouseButtonDown(1))
        {
            lockedUnit = null;
        }
    }

    private void UpdateDisplayedUnit()
    {
        Unit targetUnit;

        if (lockedUnit != null)
        {
            // 已经点击锁定时，始终显示锁定对象。
            targetUnit = lockedUnit;
        }
        else if (hoverDetector.HoverUnit != null)
        {
            // 没锁定时，鼠标碰到谁就临时显示谁。
            targetUnit = hoverDetector.HoverUnit;
        }
        else
        {
            // 鼠标移开后恢复当回合主角。
            targetUnit = turnManager.CurrentUnit;
        }

        if (targetUnit != displayedUnit)
        {
            ShowUnit(targetUnit);
        }
        else
        {
            // 显示对象未改变，但HP、AP等数值可能改变。
            battleHUD.Refresh();
        }
    }

    private void ShowUnit(Unit unit)
    {
        displayedUnit = unit;
        battleHUD.SetUnit(unit);
    }
}