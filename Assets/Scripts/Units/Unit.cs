using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Vampire.Combat;
using Vampire.Core;

namespace Vampire.Units
{
    // 挂在每个单位 GameObject 上。持有单位种类、阵营和当前状态，负责移动与基础战斗。
    [RequireComponent(typeof(NavMeshAgent))]
    public class Unit : MonoBehaviour
    {
        [SerializeField] private UnitType type;
        [SerializeField] private Faction faction = Faction.VampireHunter;

        public UnitType Type => type;
        public Faction Faction => faction;
        public int CurrentHealth { get; private set; }
        public int CurrentStamina { get; private set; }
        public bool IsAlive => CurrentHealth > 0;
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
        private NavMeshPath movementPreviewPath;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            movementPreviewPath = new NavMeshPath();
            if (type == null) return;

            CurrentHealth = Mathf.Max(1, type.maxHealth);
            CurrentStamina = type.maxStamina;
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

        // 尝试进行一次普通攻击。成功命中返回 true；非法目标或超出射程返回 false。
        public bool TryAttack(Unit target)
        {
            if (!IsHostileTo(target) || !IsInAttackRange(target)) return false;

            // 攻击是本回合的最终动作，停止尚未完成的移动。
            if (agent != null && agent.isOnNavMesh && agent.hasPath)
                agent.ResetPath();

            int amount = Damage.Compute(type.attack, target.type.defense);
            target.TakeDamage(this, amount);
            Debug.Log($"{name} 攻击 {target.name}，造成 {amount} 点伤害。");
            return true;
        }

        // 承受已经计算完成的伤害，返回实际扣除的生命值。
        public int TakeDamage(Unit source, int amount)
        {
            if (!IsAlive || amount <= 0) return 0;

            int previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            int applied = previousHealth - CurrentHealth;

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
