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
    [Tooltip("Ctrl+左键点击单位锁定显示，点击空地解除锁定。")]
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
        // Ctrl+左键点击单位：锁定该单位的信息；Ctrl+左键点击空地：解除锁定。
        if (Input.GetMouseButtonDown(0) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            Unit hoverUnit = hoverDetector.HoverUnit;
            lockedUnit = hoverUnit;
            if (lockedUnit != null)
                ShowUnit(lockedUnit);
        }
    }

    private void UpdateDisplayedUnit()
    {
        Unit targetUnit;

        if (hoverDetector.HoverUnit != null)
        {
            // 鼠标碰到任何单位时，始终优先显示该单位
            targetUnit = hoverDetector.HoverUnit;
        }
        else if (lockedUnit != null)
        {
            // 鼠标移开后，恢复到上一次点击锁定的单位
            targetUnit = lockedUnit;
        }
        else
        {
            // 没有锁定对象时，显示当前回合单位
            targetUnit = turnManager.CurrentUnit;
        }

        if (targetUnit != displayedUnit)
        {
            ShowUnit(targetUnit);
        }
        else
        {
            battleHUD.Refresh();
        }
    }

    private void ShowUnit(Unit unit)
    {
        displayedUnit = unit;
        battleHUD.SetUnit(unit);
    }
}