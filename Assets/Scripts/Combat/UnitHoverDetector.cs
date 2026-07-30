using UnityEngine;
using UnityEngine.EventSystems;
using Vampire.Units;

public class UnitHoverDetector : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera worldCamera;

    [Tooltip("能够被鼠标检测的物理层。默认 Everything。")]
    [SerializeField] private LayerMask detectionMask = ~0;

    [SerializeField] private float maxDistance = 1000f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog;

    /// <summary>
    /// 当前鼠标指向的单位。
    /// 没有指向单位时为 null。
    /// </summary>
    public Unit HoverUnit { get; private set; }

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void Update()
    {
        DetectUnitUnderMouse();
    }

    private void DetectUnitUnderMouse()
    {
        // 鼠标处于UI上时，不检测场景中的单位。
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            SetHoverUnit(null);
            return;
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;

            if (worldCamera == null)
            {
                SetHoverUnit(null);
                return;
            }
        }

        Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);

        /*
         * 使用 RaycastAll，而不是普通 Raycast。
         *
         * 原因：
         * 鼠标射线可能同时穿过角色碰撞体和地面碰撞体。
         * 我们遍历全部命中结果，主动寻找带 Unit 的物体。
         */
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            maxDistance,
            detectionMask,
            QueryTriggerInteraction.Collide
        );

        Unit detectedUnit = null;
        float nearestDistance = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            Unit unit = hit.collider.GetComponentInParent<Unit>();

            if (unit == null)
                continue;

            if (hit.distance >= nearestDistance)
                continue;

            detectedUnit = unit;
            nearestDistance = hit.distance;
        }

        SetHoverUnit(detectedUnit);
    }

    private void SetHoverUnit(Unit newUnit)
    {
        // 单位没有改变时，不重复处理。
        if (HoverUnit == newUnit)
            return;

        HoverUnit = newUnit;

        if (!showDebugLog)
            return;

        string unitName = HoverUnit != null
            ? HoverUnit.name
            : "None";

        Debug.Log($"[UnitHoverDetector] Hover changed: {unitName}");
    }
}