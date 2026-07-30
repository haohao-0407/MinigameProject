using UnityEngine;
using UnityEngine.EventSystems;
using Vampire.Units;

public class UnitHoverDetector : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera worldCamera;

    [Tooltip("�ܹ�������������㡣Ĭ�� Everything��")]
    [SerializeField] private LayerMask detectionMask = ~0;

    [SerializeField] private float maxDistance = 1000f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog;

    /// <summary>
    /// ��ǰ���ָ��ĵ�λ��
    /// û��ָ��λʱΪ null��
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
        // ��괦��UI��ʱ������ⳡ���еĵ�λ��
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
         * ʹ�� RaycastAll����������ͨ Raycast��
         *
         * ԭ��
         * ������߿���ͬʱ������ɫ��ײ��͵�����ײ�塣
         * ���Ǳ���ȫ�����н��������Ѱ�Ҵ� Unit �����塣
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
        // ��λû�иı�ʱ�����ظ������
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