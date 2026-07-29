using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vampire.Core;
using Vampire.Skills;
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
    [Tooltip("触发结束回合后，等待单位移动完成的最长时间（秒）")]
    [SerializeField, Min(0f)] private float turnEndMoveTimeout = 3f;

    private int currentIndex = -1;
    private Camera cam;
    private SelectionHighlight highlight;
    private readonly List<SelectionHighlight> factionHighlights = new List<SelectionHighlight>();
    private GameObject movementPreviewObject;
    private LineRenderer movementPreviewLine;
    private GameObject movementPreviewEndpoint;
    private Material movementPreviewMaterial;
    private readonly List<Vector3> movementPreviewPoints = new List<Vector3>();
    private bool aiActing;   // 敌方 AI 正在行动，期间屏蔽玩家输入
    private bool switching;   // 正在等待当前动作完成 / 切换回合，期间屏蔽输入
    private bool skillTargeting; // 处于技能目标选择模式：左键点选友军施放，屏蔽移动/攻击

    // 当前行动单位的技能控制器（英雄单位才有）。
    private SkillController ActiveSkills =>
        ActiveUnit != null ? ActiveUnit.GetComponent<SkillController>() : null;

    public Unit ActiveUnit =>
        currentIndex >= 0 && currentIndex < units.Count ? units[currentIndex] : null;

    // 该单位是否由玩家操控（属于玩家阵营）；否则由 AI 行动
    private bool IsPlayerControlled(Unit u) => u != null && u.Faction == playerFaction;

    void Start()
    {
        cam = Camera.main;
        highlight = SelectionHighlight.Create();
        CreateMovementPreview();

        if (units == null || units.Count == 0)
            units = FindObjectsOfType<Unit>().ToList();

        // 过滤无效单位，按 speed 降序决定行动顺序
        units = units.Where(u => u != null && u.Type != null)
                     .OrderByDescending(u => u.Type.speed)
                     .ToList();

        CreateFactionHighlights();
        BeginTurn(0);
    }

    void Update()
    {
        var active = ActiveUnit;
        if (active == null || !active.IsAlive)
        {
            ClearMovementPreview();
            return;
        }

        // AI 行动期间、回合切换等待期间、或当前单位不由玩家操控时，不接受玩家输入
        if (aiActing || switching || !IsPlayerControlled(active))
        {
            ClearMovementPreview();
            return;
        }

        // H：在有可发动主动技能时，切换技能目标选择模式
        var skills = ActiveSkills;
        if (Input.GetKeyDown(KeyCode.H) && skills != null && skills.HasActivatableSkill())
            skillTargeting = !skillTargeting;

        // 技能选择模式：屏蔽移动/攻击，左键点选友军施放，右键取消。
        if (skillTargeting)
        {
            ClearMovementPreview();

            if (Input.GetMouseButtonDown(1))
            {
                skillTargeting = false;
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Ray skillRay = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(skillRay, out RaycastHit skillHit))
                {
                    Unit clicked = skillHit.collider.GetComponentInParent<Unit>();
                    // 施放成功后退出选择模式（不结束回合）；失败则保持模式便于重选。
                    if (clicked != null && skills != null && skills.TryUse(0, clicked))
                        skillTargeting = false;
                }
            }
            return;
        }

        UpdateMovementPreview(active);

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

    // 等当前单位停下后切换回合；超时则强制停止移动，避免回合卡死。
    private IEnumerator EndTurnWhenIdle()
    {
        switching = true;
        var active = ActiveUnit;

        // 先等一帧，让刚下达的 SetDestination 完成异步路径计算
        // （否则同帧内 pathPending/hasPath 都还没置位，会被误判为“未移动”）
        yield return null;

        float deadline = Time.realtimeSinceStartup + turnEndMoveTimeout;
        while (active != null && active.IsMoving && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (active != null && active.IsMoving)
        {
            active.StopMovement();
            Debug.LogWarning($"{active.name} 结束回合等待移动超时，已强制停止移动。");
        }

        switching = false;
        BeginTurn(currentIndex + 1);
    }

    private void BeginTurn(int index)
    {
        skillTargeting = false; // 切换回合时退出技能选择模式
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

    private void CreateMovementPreview()
    {
        movementPreviewObject = new GameObject("MovementPreview");
        movementPreviewLine = movementPreviewObject.AddComponent<LineRenderer>();
        movementPreviewLine.useWorldSpace = true;
        movementPreviewLine.widthMultiplier = 0.08f;
        movementPreviewLine.numCapVertices = 4;
        movementPreviewLine.numCornerVertices = 2;
        movementPreviewLine.positionCount = 0;

        Shader previewShader = Shader.Find("Sprites/Default") ??
                               Shader.Find("Universal Render Pipeline/Unlit") ??
                               Shader.Find("Unlit/Color");
        movementPreviewMaterial = new Material(previewShader)
        {
            color = new Color(0.2f, 1f, 0.35f, 0.85f)
        };
        movementPreviewLine.sharedMaterial = movementPreviewMaterial;

        movementPreviewEndpoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        movementPreviewEndpoint.name = "MovementPreviewEndpoint";
        movementPreviewEndpoint.transform.localScale = Vector3.one * 0.25f;
        movementPreviewEndpoint.layer = LayerMask.NameToLayer("Ignore Raycast");
        var endpointCollider = movementPreviewEndpoint.GetComponent<Collider>();
        if (endpointCollider != null) Destroy(endpointCollider);

        var endpointRenderer = movementPreviewEndpoint.GetComponent<Renderer>();
        endpointRenderer.sharedMaterial = movementPreviewMaterial;
        movementPreviewEndpoint.SetActive(false);
    }

    private void CreateFactionHighlights()
    {
        var enemyColor = new Color(1f, 0.12f, 0.08f, 1f);
        var friendlyColor = new Color(0.15f, 1f, 0.25f, 1f);
        foreach (Unit unit in units)
        {
            if (unit == null) continue;

            bool friendly = IsPlayerControlled(unit);
            Color ringColor = friendly ? friendlyColor : enemyColor;
            string prefix = friendly ? "FriendlyHighlight" : "EnemyHighlight";

            // 使用比行动光环稍大的阵营外圈，行动时两种提示仍能同时看清。
            SelectionHighlight factionHighlight = SelectionHighlight.Create(
                ringColor,
                $"{prefix}_{unit.name}",
                0.82f,
                0.98f,
                0.04f);
            factionHighlight.SetTarget(unit.transform);
            factionHighlights.Add(factionHighlight);
        }
    }

    private void UpdateMovementPreview(Unit active)
    {
        if (cam == null || !TryGetPreviewWorldPoint(active, out Vector3 worldPoint) ||
            !active.TryGetMovementPreview(worldPoint, movementPreviewPoints))
        {
            ClearMovementPreview();
            return;
        }

        movementPreviewLine.positionCount = movementPreviewPoints.Count;
        for (int i = 0; i < movementPreviewPoints.Count; i++)
        {
            Vector3 point = movementPreviewPoints[i];
            point.y += 0.08f;
            movementPreviewLine.SetPosition(i, point);
        }

        Vector3 endpoint = movementPreviewPoints[movementPreviewPoints.Count - 1];
        endpoint.y += 0.12f;
        movementPreviewEndpoint.transform.position = endpoint;
        movementPreviewEndpoint.SetActive(true);
    }

    // 预览点与左键实际行为保持一致：地面为点击点，射程外敌人为靠近后的停留点。
    private bool TryGetPreviewWorldPoint(Unit active, out Vector3 worldPoint)
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            worldPoint = default;
            return false;
        }

        Unit hoveredUnit = hit.collider.GetComponentInParent<Unit>();
        if (hoveredUnit == null)
        {
            worldPoint = hit.point;
            return true;
        }

        if (!active.IsHostileTo(hoveredUnit) || active.IsInAttackRange(hoveredUnit))
        {
            worldPoint = default;
            return false;
        }

        Vector3 toSelf = active.transform.position - hoveredUnit.transform.position;
        Vector3 direction = toSelf.sqrMagnitude > 0.0001f ? toSelf.normalized : active.transform.forward;
        worldPoint = hoveredUnit.transform.position + direction * active.Type.attackRange;
        return true;
    }

    private void ClearMovementPreview()
    {
        if (movementPreviewLine != null)
            movementPreviewLine.positionCount = 0;
        if (movementPreviewEndpoint != null)
            movementPreviewEndpoint.SetActive(false);
    }

    private void OnDestroy()
    {
        if (movementPreviewObject != null)
            Destroy(movementPreviewObject);
        if (movementPreviewEndpoint != null)
            Destroy(movementPreviewEndpoint);
        if (movementPreviewMaterial != null)
            Destroy(movementPreviewMaterial);
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
        {
            GUI.Label(new Rect(10, 32, 700, 22), "左键地面=移动    左键敌人=靠近/攻击    空格=结束回合");

            // 英雄单位：显示主动技能与充能层数、操作提示。
            var skills = ActiveSkills;
            var skill = skills != null ? skills.GetActiveSkill(0) : null;
            if (skill != null)
            {
                string charges = skill.UsesCharges ? $"（充能 {skills.GetCharges(0)}/{skill.maxCharges}）" : "";
                string state = skillTargeting ? "  [选择目标中：左键点友军，右键取消]" : "";
                GUI.Label(new Rect(10, 54, 700, 22),
                    $"H={skill.displayName}{charges}{state}");
            }
        }
        else
        {
            GUI.Label(new Rect(10, 32, 700, 22), "AI 单位自动行动中…");
        }
    }
}
}
