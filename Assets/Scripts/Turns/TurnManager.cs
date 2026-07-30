using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vampire.Core;
using Vampire.Skills;
using Vampire.Units;

namespace Vampire.Turns
{
    // 战斗阶段
    public enum BattlePhase { Player, Enemy }

    // 回合调度（阵营回合制）：
    // 玩家阶段：左键友方=切换操控角色 / 左键地面=移动 / 左键敌方=攻击或靠近，空格结束整体回合。
    // 敌方阶段：敌方按速度降序依次由 AI 自动行动，全部行动完毕后进入下一轮。
    public class TurnManager : MonoBehaviour
    {
        [Tooltip("玩家操控的阵营；另一阵营由 AI 自动行动。")]
        [SerializeField] private Faction playerFaction = Faction.VampireHunter;
        [Tooltip("留空则运行时自动收集场景中的所有 Unit")]
        [SerializeField] private List<Unit> units = new List<Unit>();
        [Tooltip("AI 每步行动之间的停顿（秒），便于观察")]
        [SerializeField] private float aiActionDelay = 0.6f;

        // 供 BattleHUDController 等外部读取当前回合数（从 1 开始）。
        [HideInInspector] public int currentIndex = 0;

        private Camera cam;
        private SelectionHighlight highlight;
        private readonly List<SelectionHighlight> factionHighlights = new List<SelectionHighlight>();
        private GameObject movementPreviewObject;
        private LineRenderer movementPreviewLine;
        private GameObject movementPreviewEndpoint;
        private Material movementPreviewMaterial;
        private readonly List<Vector3> movementPreviewPoints = new List<Vector3>();

        private BattlePhase currentPhase = BattlePhase.Player;
        private int selectedIndex = -1;      // 玩家阶段当前选中的友方单位在 units 中的下标
        private bool skillTargeting;         // 技能目标选择模式

        // 供外部（BattleHUDController 等）使用的接口。
        public BattlePhase CurrentPhase => currentPhase;
        public Unit CurrentUnit { get; private set; }

        // 向后兼容旧代码中的 ActiveUnit 引用（与 CurrentUnit 同）。
        public Unit ActiveUnit => CurrentUnit;

        // 供 UI2 / CharacterSelectUI 读取的队伍列表和阵营。
        public List<Unit> Units => units;
        public Faction PlayerFaction => playerFaction;

        // 当前选中/行动单位的技能控制器
        private SkillController ActiveSkills =>
            CurrentUnit != null ? CurrentUnit.GetComponent<SkillController>() : null;

        private bool IsPlayerControlled(Unit u) => u != null && u.Faction == playerFaction;

        // -----------------------------------------------------------------
        // 生命周期
        // -----------------------------------------------------------------

        void Start()
        {
            cam = Camera.main;
            highlight = SelectionHighlight.Create();
            CreateMovementPreview();

            if (units == null || units.Count == 0)
                units = FindObjectsOfType<Unit>().ToList();

            units = units.Where(u => u != null && u.Type != null).ToList();

            CreateFactionHighlights();
            BeginPlayerPhase();
        }

        void Update()
        {
            // 敌方阶段：不接受玩家输入（由协程控制）。
            if (currentPhase == BattlePhase.Enemy)
            {
                ClearMovementPreview();
                return;
            }

            // ---- 玩家阶段 ----
            var selected = CurrentUnit;

            // 当前选中角色死亡 → 自动切换到下一个活着的友方。
            if (selected == null || !selected.IsAlive)
            {
                SelectNextPlayerUnit();
                return;
            }

            // ---- 技能目标选择模式（H 键切换） ----
            var skills = ActiveSkills;
            if (Input.GetKeyDown(KeyCode.H) && skills != null && skills.HasActivatableSkill())
                skillTargeting = !skillTargeting;

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
                        if (clicked != null && skills != null && skills.TryUse(0, clicked))
                            skillTargeting = false;
                    }
                }
                return;
            }

            // ---- 移动预览 ----
            UpdateMovementPreview(selected);

            // ---- 空格：结束玩家整体回合 ----
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ClearMovementPreview();
                skillTargeting = false;
                EndPlayerPhase();
                return;
            }

            // ---- 左键（无 Ctrl）：移动 / 攻击 / 切换角色 ----
            // Ctrl+左键留给 BattleHUDController 锁定 HUD。
            if (Input.GetMouseButtonDown(0) &&
                !Input.GetKey(KeyCode.LeftControl) &&
                !Input.GetKey(KeyCode.RightControl))
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Unit clickedUnit = hit.collider.GetComponentInParent<Unit>();
                    if (clickedUnit != null)
                    {
                        // 点击友方：切换操控角色
                        if (IsPlayerControlled(clickedUnit))
                        {
                            SelectPlayerUnit(clickedUnit);
                            return;
                        }

                        // 点击敌方：攻击（射程内）或靠近
                        if (selected.IsInAttackRange(clickedUnit))
                            selected.TryAttack(clickedUnit);
                        else
                            selected.MoveToward(clickedUnit, selected.Type.attackRange);
                        return;
                    }

                    // 点击地面：移动
                    selected.MoveTo(hit.point);
                }
            }
        }

        // -----------------------------------------------------------------
        // 玩家阶段
        // -----------------------------------------------------------------

        private void BeginPlayerPhase()
        {
            currentPhase = BattlePhase.Player;
            currentIndex++;                         // 回合数 +1
            skillTargeting = false;

            // 所有存活单位恢复行动点（耐力）。
            foreach (var u in units)
                if (u != null && u.IsAlive)
                    u.OnTurnStart();

            // 自动选择第一个存活的友方单位。
            selectedIndex = units.FindIndex(u => u != null && u.IsAlive && IsPlayerControlled(u));
            if (selectedIndex < 0)
            {
                // 我方全灭。
                CurrentUnit = null;
                highlight?.SetTarget(null);
                return;
            }

            CurrentUnit = units[selectedIndex];
            highlight?.SetTarget(CurrentUnit != null ? CurrentUnit.transform : null);

            Debug.Log($"[TurnManager] 第 {currentIndex} 轮 · 玩家阶段开始 · 选中 {CurrentUnit?.name}");
        }

        private void SelectPlayerUnit(Unit unit)
        {
            int idx = units.IndexOf(unit);
            if (idx < 0 || !unit.IsAlive || !IsPlayerControlled(unit)) return;

            selectedIndex = idx;
            CurrentUnit = unit;
            highlight?.SetTarget(unit.transform);
        }

        private void SelectNextPlayerUnit()
        {
            // 从当前位置往后找下一个存活的友方。
            for (int offset = 1; offset <= units.Count; offset++)
            {
                int idx = (selectedIndex + offset) % units.Count;
                Unit candidate = units[idx];
                if (candidate != null && candidate.IsAlive && IsPlayerControlled(candidate))
                {
                    SelectPlayerUnit(candidate);
                    return;
                }
            }

            // 没有存活的友方了。
            CurrentUnit = null;
            highlight?.SetTarget(null);
        }

        private void EndPlayerPhase()
        {
            Debug.Log("[TurnManager] 玩家阶段结束");
            highlight?.SetTarget(null);
            StartCoroutine(RunEnemyPhase());
        }

        // -----------------------------------------------------------------
        // 敌方阶段
        // -----------------------------------------------------------------

        private IEnumerator RunEnemyPhase()
        {
            currentPhase = BattlePhase.Enemy;

            // 按速度降序排列敌方单位。
            var enemies = units
                .Where(u => u != null && u.IsAlive && !IsPlayerControlled(u))
                .OrderByDescending(u => u.Type.speed)
                .ToList();

            Debug.Log($"[TurnManager] 敌方阶段开始 · {enemies.Count} 个单位");

            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;

                CurrentUnit = enemy;
                highlight?.SetTarget(enemy.transform);

                yield return new WaitForSeconds(aiActionDelay);

                Unit target = FindNearestHostile(enemy);
                if (target != null)
                {
                    if (!enemy.IsInAttackRange(target))
                    {
                        enemy.MoveToward(target, enemy.Type.attackRange);

                        yield return null;      // 等一帧让 NavMeshAgent 开始计算路径
                        while (enemy != null && enemy.IsAlive && enemy.IsMoving)
                            yield return null;
                    }

                    if (enemy != null && enemy.IsAlive && target != null && target.IsAlive)
                        enemy.TryAttack(target);
                }
            }

            Debug.Log("[TurnManager] 敌方阶段结束");
            BeginPlayerPhase();
        }

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

        // -----------------------------------------------------------------
        // 移动预览
        // -----------------------------------------------------------------

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
                SelectionHighlight factionHighlight = SelectionHighlight.Create(
                    ringColor,
                    $"{prefix}_{unit.name}",
                    0.82f, 0.98f, 0.04f);
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

            // 友方或已在射程内的敌人 → 不显示预览（点击时会切换角色 / 攻击而不是移动）。
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
    }
}
