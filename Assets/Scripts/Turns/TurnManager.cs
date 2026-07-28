using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vampire.Core;
using Vampire.Units;

namespace Vampire.Turns
{
// 回合调度：按单位速度（先攻）降序排出行动顺序，单位逐个行动。
// 玩家单位回合：左键移动，空格结束回合。
// 敌方单位回合：自动 AI —— 靠近最近的敌方单位，然后自动结束回合。
public class TurnManager : MonoBehaviour
{
    [Tooltip("玩家操控的阵营；另一阵营由 AI 自动行动。将来可让玩家在开局选择")]
    [SerializeField] private Faction playerFaction = Faction.VampireHunter;
    [Tooltip("留空则运行时自动收集场景中的所有 Unit")]
    [SerializeField] private List<Unit> units = new List<Unit>();
    [Tooltip("AI 每步行动之间的停顿（秒），便于观察")]
    [SerializeField] private float aiActionDelay = 0.6f;

    private int currentIndex = -1;
    private Camera cam;
    private SelectionHighlight highlight;
    private bool aiActing;   // 敌方 AI 正在行动，期间屏蔽玩家输入
    private bool switching;   // 正在等待当前动作完成 / 切换回合，期间屏蔽输入

    public Unit ActiveUnit =>
        currentIndex >= 0 && currentIndex < units.Count ? units[currentIndex] : null;

    // 该单位是否由玩家操控（属于玩家阵营）；否则由 AI 行动
    private bool IsPlayerControlled(Unit u) => u != null && u.Faction == playerFaction;

    void Start()
    {
        cam = Camera.main;
        highlight = SelectionHighlight.Create();

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
        if (active == null || !active.IsAlive) return;

        // AI 行动期间、回合切换等待期间、或当前单位不由玩家操控时，不接受玩家输入
        if (aiActing || switching || !IsPlayerControlled(active)) return;

        // 左键：移动当前行动单位
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Unit clickedUnit = hit.collider.GetComponentInParent<Unit>();
                if (clickedUnit != null)
                {
                    if (!active.IsHostileTo(clickedUnit)) return;

                    // 射程内攻击；射程外先靠近，玩家可在移动完成后再次点击攻击。
                    if (active.TryAttack(clickedUnit))
                        NextTurn();
                    else
                        active.MoveToward(clickedUnit, active.Type.attackRange);
                    return;
                }

                active.MoveTo(hit.point);
            }
        }

        // 空格：结束当前回合（等当前移动走完再真正切换）
        if (Input.GetKeyDown(KeyCode.Space))
            NextTurn();
    }

    // 结束当前回合：等当前单位的移动完成后再进入下一回合
    public void NextTurn()
    {
        if (switching) return;
        StartCoroutine(EndTurnWhenIdle());
    }

    // 等当前单位停下（抵达目的地）后切换到下一回合
    private IEnumerator EndTurnWhenIdle()
    {
        switching = true;
        var active = ActiveUnit;

        // 先等一帧，让刚下达的 SetDestination 完成异步路径计算
        // （否则同帧内 pathPending/hasPath 都还没置位，会被误判为“未移动”）
        yield return null;

        while (active != null && active.IsMoving)
            yield return null;

        switching = false;
        BeginTurn(currentIndex + 1);
    }

    private void BeginTurn(int index)
    {
        if (units.Count == 0) return;

        // 保留原列表顺序并跳过已经阵亡/销毁的单位，避免删除元素后打乱回合索引。
        for (int offset = 0; offset < units.Count; offset++)
        {
            int candidateIndex = (index + offset) % units.Count;
            Unit candidate = units[candidateIndex];
            if (candidate == null || !candidate.IsAlive) continue;

            currentIndex = candidateIndex;
            break;
        }

        var active = ActiveUnit;
        if (active == null || !active.IsAlive)
        {
            currentIndex = -1;
            highlight?.SetTarget(null);
            return;
        }

        active?.OnTurnStart();
        highlight?.SetTarget(active != null ? active.transform : null);

        // 非玩家阵营：自动执行 AI
        if (active != null && !IsPlayerControlled(active))
            StartCoroutine(RunEnemyTurn(active));
    }

    // 敌方 AI：靠近最近的敌方单位，移动完成后尝试攻击并自动结束回合。
    private IEnumerator RunEnemyTurn(Unit self)
    {
        aiActing = true;
        // 行动前的短暂停顿，便于观察轮到了谁
        yield return new WaitForSeconds(aiActionDelay);

        Unit target = FindNearestHostile(self);
        if (target != null)
        {
            if (!self.IsInAttackRange(target))
            {
                self.MoveToward(target, self.Type.attackRange);

                // 等一帧让 NavMeshAgent 开始计算路径，再等待移动结束。
                yield return null;
                while (self != null && self.IsAlive && self.IsMoving)
                    yield return null;
            }

            if (self != null && self.IsAlive && target != null && target.IsAlive)
                self.TryAttack(target);
        }

        aiActing = false;
        NextTurn();
    }

    // 找到距 self 最近的敌对单位
    private Unit FindNearestHostile(Unit self)
    {
        Unit best = null;
        float bestSqr = float.MaxValue;
        foreach (var u in units)
        {
            if (u == null || !u.IsAlive || !self.IsHostileTo(u)) continue;
            float d = (u.transform.position - self.transform.position).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = u; }
        }
        return best;
    }

    void OnGUI()
    {
        var active = ActiveUnit;
        if (active == null) return;

        bool player = IsPlayerControlled(active);
        string faction = FactionNames.Display(active.Faction);
        string ctrl = player ? "（你）" : "（AI）";
        GUI.Label(new Rect(10, 10, 700, 22),
            $"当前回合: [{faction}] {active.Type.displayName}{ctrl}    " +
            $"生命: {active.CurrentHealth}/{active.Type.maxHealth}    " +
            $"耐力: {active.CurrentStamina}/{active.Type.maxStamina}");

        if (player)
            GUI.Label(new Rect(10, 32, 700, 22), "左键地面=移动    左键敌人=靠近/攻击    空格=结束回合");
        else
            GUI.Label(new Rect(10, 32, 700, 22), "AI 单位自动行动中…");
    }
}
}
