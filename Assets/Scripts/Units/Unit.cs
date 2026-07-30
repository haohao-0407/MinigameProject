using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Vampire.Combat;
using Vampire.Core;
using Vampire.Items;

namespace Vampire.Units
{
    // 挂在每个单位 GameObject 上。持有单位种类、阵营和当前状态，负责移动与基础战斗。
    [RequireComponent(typeof(NavMeshAgent))]
    public class Unit : MonoBehaviour
    {
        [SerializeField] private UnitType type;
        [SerializeField] private Faction faction = Faction.VampireHunter;
        [SerializeField] private Inventory inventory;  // 背包（可选组件）

        [Tooltip("角色动画控制器；留空则运行时从自身或子物体上查找")]
        [SerializeField] private Animator animator;

        // 动画 Bool 参数名：移动时置 run，攻击时置 attack。
        private const string RunParam = "run";
        private const string AttackParam = "attack";
        private static readonly int RunHash = Animator.StringToHash(RunParam);
        private static readonly int AttackHash = Animator.StringToHash(AttackParam);

        public UnitType Type => type;
        public Faction Faction => faction;
        public Inventory Inventory => inventory;  // 可能为 null（无背包的单位如敌人）
        public int CurrentHealth { get; private set; }
        public int CurrentStamina { get; private set; }
        public bool IsAlive => CurrentHealth > 0;
        public bool HasStamina => CurrentStamina > 0;

        // 攻击动画正在播放（伤害尚未结算）。回合调度需等其结束再切换回合。
        public bool IsAttacking { get; private set; }

        // 单位实际受到伤害时触发。参数依次为伤害来源和实际扣除的生命值。
        public event Action<Unit, int> Damaged;

        // 单位实际回复生命时触发。参数为实际恢复的生命值。
        public event Action<int> Healed;

        // 本单位击杀了某个单位时触发。参数为被击杀者。供技能被动（如击杀充能）监听。
        public event Action<Unit> DealtKill;

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
        private NavMeshPath movementPreviewPath;
        private bool hasRunParam;
        private bool hasAttackParam;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            movementPreviewPath = new NavMeshPath();

            // 动画控制器可能挂在自身或子物体（如 2D 精灵）上。
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            CacheAnimatorParams();

            if (type == null) return;

            CurrentHealth = Mathf.Max(1, type.maxHealth);
            CurrentStamina = type.maxStamina;
        }

        // 记录控制器是否声明了 run/attack 参数，避免向缺少参数的控制器写入报错。
        private void CacheAnimatorParams()
        {
            hasRunParam = false;
            hasAttackParam = false;
            if (animator == null || animator.runtimeAnimatorController == null) return;

            foreach (var p in animator.parameters)
            {
                if (p.type != AnimatorControllerParameterType.Bool) continue;
                if (p.nameHash == RunHash) hasRunParam = true;
                else if (p.nameHash == AttackHash) hasAttackParam = true;
            }
        }

        void Update()
        {
            // 每帧同步移动动画：只要单位还在朝目的地移动就播放 run。
            if (hasRunParam)
                animator.SetBool(RunHash, IsAlive && IsMoving);
        }

        // 回合开始时恢复行动点
        public void OnTurnStart()
        {
            CurrentStamina = IsAlive && type != null ? type.maxStamina : 0;
        }

        // 尝试移动到目标点：按当前耐力换算出可走距离，截断路径后移动并扣除耐力
        public void MoveTo(Vector3 worldPoint)
        {
            if (!IsAlive || type == null || !HasStamina) return;

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

        // 立即取消当前移动。用于结束回合超时等需要强制停止的情况。
        public void StopMovement()
        {
            if (agent == null || !agent.isOnNavMesh) return;
            agent.ResetPath();
        }

        // 计算一次移动预览。points 返回实际移动会经过的 NavMesh 折线和预计终点。
        // 该方法只计算路径，不会移动单位或消耗耐力。
        public bool TryGetMovementPreview(Vector3 worldPoint, List<Vector3> points)
        {
            if (points == null) return false;
            points.Clear();
            if (!IsAlive || type == null || !HasStamina) return false;

            if (movementPreviewPath == null)
                movementPreviewPath = new NavMeshPath();

            if (!agent.CalculatePath(worldPoint, movementPreviewPath) ||
                movementPreviewPath.status == NavMeshPathStatus.PathInvalid)
                return false;

            float budgetMeters = CurrentStamina / Mathf.Max(0.0001f, type.moveCostPerMeter);
            Vector3 endpoint = BuildTruncatedPath(movementPreviewPath.corners, budgetMeters, points, out _);

            // 与 MoveTo 保持一致：最终落点吸附回 NavMesh。
            if (!NavMesh.SamplePosition(endpoint, out var navHit, 1f, agent.areaMask))
            {
                points.Clear();
                return false;
            }

            if (points.Count == 0)
                points.Add(transform.position);

            points[points.Count - 1] = navHit.position;
            return points.Count > 1 && Vector3.Distance(points[0], points[points.Count - 1]) > 0.01f;
        }

        // 朝目标单位靠近，但停在距其 stopDistance 处。
        public void MoveToward(Unit target, float stopDistance)
        {
            if (target == null || !target.IsAlive) return;

            Vector3 toSelf = transform.position - target.transform.position;
            // 目标点取“目标单位朝我方向、退开 stopDistance”的位置
            Vector3 direction = toSelf.sqrMagnitude > 0.0001f ? toSelf.normalized : transform.forward;
            Vector3 desired = target.transform.position + direction * Mathf.Max(0f, stopDistance);
            MoveTo(desired);
        }

        // 目标是否处于本单位的普通攻击射程内（按单位中心距离计算）。
        public bool IsInAttackRange(Unit target)
        {
            if (!IsAlive || type == null || target == null || !target.IsAlive) return false;
            return Vector3.Distance(transform.position, target.transform.position) <= type.attackRange;
        }

        // 尝试进行一次普通攻击：合法目标则播放攻击动画，动画播放完毕后再结算伤害。
        // 返回 true 表示攻击已发起（伤害可能稍后才结算）；非法目标或超出射程返回 false。
        // 调用方可用 IsAttacking 等待动画与伤害结算完成。
        public bool TryAttack(Unit target)
        {
            if (!IsHostileTo(target) || !IsInAttackRange(target)) return false;
            if (IsAttacking) return false; // 上一次攻击尚未结算，忽略重复触发

            // 攻击是本回合的最终动作，停止尚未完成的移动。
            if (agent != null && agent.isOnNavMesh && agent.hasPath)
                agent.ResetPath();

            IsAttacking = true;
            StartCoroutine(AttackRoutine(target));
            return true;
        }

        // 攻击流程：置 attack=true 播放攻击动画 → 等动画播完 → 结算伤害 → 复位 attack。
        private IEnumerator AttackRoutine(Unit target)
        {
            if (hasAttackParam)
            {
                animator.SetBool(AttackHash, true);
                // 等一帧让状态机从 idle 切入 attack 状态，再读取该状态的时长。
                yield return null;
                yield return new WaitForSeconds(GetCurrentStateRemaining());
                animator.SetBool(AttackHash, false);
            }

            // 动画播放完毕后再判定伤害；期间目标可能已阵亡或销毁。
            if (IsAlive && target != null && target.IsAlive && type != null && target.type != null)
            {
                int amount = Damage.Compute(type.attack, target.type.defense);
                target.TakeDamage(this, amount);
                Debug.Log($"{name} 攻击 {target.name}，造成 {amount} 点伤害。");
            }

            IsAttacking = false;
        }

        // 当前动画状态的剩余播放时间（秒）。无动画或时长无效时回退到一个短固定值。
        private float GetCurrentStateRemaining()
        {
            if (animator == null) return 0.35f;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            float length = state.length;
            if (length <= 0f || float.IsInfinity(length)) return 0.35f;
            // normalizedTime 的小数部分表示当前循环已播放的比例。
            float played = Mathf.Repeat(state.normalizedTime, 1f);
            return Mathf.Max(0f, length * (1f - played));
        }

        // 回复生命值，上限为最大生命。返回实际恢复的量；实际回血 >0 时触发 Healed。
        public int Heal(int amount)
        {
            if (!IsAlive || amount <= 0 || type == null) return 0;

            int previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Min(type.maxHealth, CurrentHealth + amount);
            int restored = CurrentHealth - previousHealth;

            if (restored > 0)
                Healed?.Invoke(restored);

            return restored;
        }

        // 消耗耐力（行动点）。供技能等外部系统扣除，钳制到 0。
        public void SpendStamina(int amount)
        {
            if (amount <= 0) return;
            CurrentStamina = Mathf.Max(0, CurrentStamina - amount);
        }

        // 承受已经计算完成的伤害，返回实际扣除的生命值。
        public int TakeDamage(Unit source, int amount)
        {
            if (!IsAlive || amount <= 0) return 0;

            int previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            int applied = previousHealth - CurrentHealth;

            if (applied > 0)
                Damaged?.Invoke(source, applied);

            if (CurrentHealth == 0)
                Die(source);

            return applied;
        }

        private void Die(Unit killer)
        {
            CurrentStamina = 0;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            string killerName = killer != null ? killer.name : "未知来源";
            Debug.Log($"{name} 被 {killerName} 击败。");

            // 通知击杀者（用于击杀充能等被动）。在 Destroy 前触发。
            killer?.DealtKill?.Invoke(this);

            Destroy(gameObject);
        }

        // 沿折线累加长度，超预算就在该段中间返回落点；预算够则返回终点。
        // traveled 输出实际走过的距离，用于扣除耐力。
        private Vector3 TruncateToBudget(Vector3[] corners, float budget, out float traveled)
        {
            return BuildTruncatedPath(corners, budget, null, out traveled);
        }

        // 沿折线累加长度，必要时在当前段中截断；points 不为空时同时写入预览折线。
        private Vector3 BuildTruncatedPath(Vector3[] corners, float budget, List<Vector3> points,
            out float traveled)
        {
            traveled = 0f;
            if (points != null) points.Clear();
            if (corners == null || corners.Length == 0)
            {
                if (points != null) points.Add(transform.position);
                return transform.position;
            }

            if (points != null) points.Add(corners[0]);

            float remaining = Mathf.Max(0f, budget);
            for (int i = 1; i < corners.Length; i++)
            {
                float seg = Vector3.Distance(corners[i - 1], corners[i]);
                if (seg <= remaining)
                {
                    remaining -= seg;
                    traveled += seg;
                    if (points != null) points.Add(corners[i]);
                    continue;
                }

                Vector3 dir = seg > 0.0001f ? (corners[i] - corners[i - 1]).normalized : Vector3.zero;
                traveled += remaining;
                Vector3 endpoint = corners[i - 1] + dir * remaining;
                if (points != null) points.Add(endpoint);
                return endpoint;
            }

            return corners[corners.Length - 1];
        }
    }
}
