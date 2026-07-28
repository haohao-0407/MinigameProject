using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 回合调度：按单位速度（先攻）降序排出行动顺序，单位逐个行动。
// 当前行动单位可用左键移动；空格结束该单位回合，轮到下一个并恢复其行动点。
public class TurnManager : MonoBehaviour
{
    [Tooltip("留空则运行时自动收集场景中的所有 Unit")]
    [SerializeField] private List<Unit> units = new List<Unit>();

    private int currentIndex = -1;
    private Camera cam;

    public Unit ActiveUnit =>
        currentIndex >= 0 && currentIndex < units.Count ? units[currentIndex] : null;

    void Start()
    {
        cam = Camera.main;

        if (units == null || units.Count == 0)
            units = FindObjectsOfType<Unit>().ToList();

        // 过滤无效单位，按 speed 降序决定行动顺序
        units = units.Where(u => u != null && u.Type != null)
                     .OrderByDescending(u => u.Type.speed)
                     .ToList();

        BeginTurn(0);
    }

    void Update()
    {
        var active = ActiveUnit;
        if (active == null) return;

        // 左键：移动当前行动单位
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
                active.MoveTo(hit.point);
        }

        // 空格：结束当前回合
        if (Input.GetKeyDown(KeyCode.Space))
            NextTurn();
    }

    public void NextTurn()
    {
        BeginTurn(currentIndex + 1);
    }

    private void BeginTurn(int index)
    {
        if (units.Count == 0) return;
        currentIndex = index % units.Count;
        ActiveUnit?.OnTurnStart();
    }

    void OnGUI()
    {
        var active = ActiveUnit;
        if (active == null) return;

        GUI.Label(new Rect(10, 10, 500, 22),
            $"当前回合: {active.Type.displayName}    耐力: {active.CurrentStamina}/{active.Type.maxStamina}");
        GUI.Label(new Rect(10, 32, 500, 22), "左键=移动当前单位    空格=结束回合");
    }
}
