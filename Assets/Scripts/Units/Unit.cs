using UnityEngine;
using UnityEngine.AI;

// 阵营：吸血鬼 vs 吸血鬼猎人团。仅表示队伍归属，与“由玩家还是 AI 操控”无关
// （后者由 TurnManager 的 playerFaction 决定，将来玩家可选择游玩任一阵营）。
public enum Faction { Vampire, VampireHunter }

// 阵营的中文显示名
public static class FactionNames
{
    public static string Display(Faction f) =>
        f == Faction.Vampire ? "吸血鬼" : "吸血鬼猎人团";
}

// 挂在每个单位 GameObject 上。持有单位种类、阵营、当前耐力，负责按耐力预算移动。
// 战斗（攻击/受伤/阵亡）尚未实现，留待后续。
[RequireComponent(typeof(NavMeshAgent))]
public class Unit : MonoBehaviour
{
    [SerializeField] private UnitType type;
    [SerializeField] private Faction faction = Faction.VampireHunter;

    public UnitType Type => type;
    public Faction Faction => faction;
    public int CurrentStamina { get; private set; }
    public bool HasStamina => CurrentStamina > 0;

    // 是否与另一单位敌对
    public bool IsHostileTo(Unit other) => other != null && other.faction != faction;

    // 是否仍在朝目的地移动（路径计算中、或尚未抵达停止距离）
    public bool IsMoving
    {
        get
        {
            if (agent == null || !agent.isOnNavMesh) return false;
            if (agent.pathPending) return true;
            return agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.05f;
        }
    }

    private NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (type != null) CurrentStamina = type.maxStamina;
    }

    // 回合开始时恢复行动点
    public void OnTurnStart()
    {
        CurrentStamina = type != null ? type.maxStamina : 0;
    }

    // 尝试移动到目标点：按当前耐力换算出可走距离，截断路径后移动并扣除耐力
    public void MoveTo(Vector3 worldPoint)
    {
        if (type == null || !HasStamina) return;

        var path = new NavMeshPath();
        if (!agent.CalculatePath(worldPoint, path)) return;
        if (path.status == NavMeshPathStatus.PathInvalid) return;

        float budgetMeters = CurrentStamina / Mathf.Max(0.0001f, type.moveCostPerMeter);
        Vector3 target = TruncateToBudget(path.corners, budgetMeters, out float traveled);

        // 落点吸附回 NavMesh，避免截断点落在网格边缘外
        if (NavMesh.SamplePosition(target, out var navHit, 1f, agent.areaMask))
        {
            agent.SetDestination(navHit.position);
            int cost = Mathf.CeilToInt(traveled * type.moveCostPerMeter);
            CurrentStamina = Mathf.Max(0, CurrentStamina - cost);
        }
    }

    // AI 用：朝目标单位靠近，但停在距其 stopDistance 处（为将来的攻击射程留空间）。
    public void MoveToward(Unit target, float stopDistance)
    {
        if (target == null) return;

        Vector3 toSelf = transform.position - target.transform.position;
        // 目标点取“目标单位朝我方向、退开 stopDistance”的位置
        Vector3 desired = target.transform.position + toSelf.normalized * stopDistance;
        MoveTo(desired);
    }

    // 沿折线累加长度，超预算就在该段中间返回落点；预算够则返回终点。
    // traveled 输出实际走过的距离，用于扣除耐力。
    private Vector3 TruncateToBudget(Vector3[] corners, float budget, out float traveled)
    {
        traveled = 0f;
        if (corners.Length == 0) return transform.position;

        float remaining = budget;
        for (int i = 1; i < corners.Length; i++)
        {
            float seg = Vector3.Distance(corners[i - 1], corners[i]);
            if (seg <= remaining)
            {
                remaining -= seg;
                traveled += seg;
                continue;
            }
            Vector3 dir = (corners[i] - corners[i - 1]).normalized;
            traveled += remaining;
            return corners[i - 1] + dir * remaining;
        }
        return corners[corners.Length - 1];
    }
}
