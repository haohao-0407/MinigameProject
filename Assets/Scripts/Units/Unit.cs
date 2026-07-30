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

        [Header("音效")]
        [Tooltip("脚步声音频源；留空则运行时从自身或子物体上查找")]
        [SerializeField] private AudioSource footstepSource;

        [Tooltip("脚步声音频（可选）；留空则沿用 AudioSource 上已设置的 clip")]
        [SerializeField] private AudioClip footstepClip;

        [Tooltip("攻击音效：本单位攻击命中目标时播放")]
        [SerializeField] private AudioClip attackClip;

        [Tooltip("死亡音效：本单位被击败时播放")]
        [SerializeField] private AudioClip deathClip;

        [Tooltip("受击音效表：按攻击者的单位种类(Unit Type)播放不同受击音效")]
        [SerializeField] private HitSoundEntry[] hitSounds;

        [Tooltip("受击音效兜底：攻击者种类未匹配表中任一项、或来源未知时播放")]
        [SerializeField] private AudioClip defaultHitClip;

        [Tooltip("单发音效(攻击/受击)的播放源；留空则复用脚步声音频源")]
        [SerializeField] private AudioSource sfxSource;

        // 受击音效表条目：某种攻击者(Unit Type)击中本单位时，播放对应 clip。
        [System.Serializable]
        private struct HitSoundEntry
        {
            public UnitType attackerType;  // 攻击者的单位种类
            public AudioClip clip;         // 被这种单位击中时播放的受击音效
        }

        [Tooltip("脚下光环的高度微调（米）：正值抬高、负值压低，供每个角色单独对齐脚底")]
        [SerializeField] private float highlightHeightOffset = 0f;

        // 脚下光环相对默认脚底位置的高度偏移，由 SelectionHighlight 读取。
        public float HighlightHeightOffset => highlightHeightOffset;

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

            // 脚步声音频源同样可能挂在自身或子物体上。
            if (footstepSource == null)
                footstepSource = GetComponentInChildren<AudioSource>();
            if (footstepSource != null)
            {
                // 脚步声随移动开始/停止循环播放，由 Update 控制开关。
                footstepSource.loop = true;
                footstepSource.playOnAwake = false;
                if (footstepClip != null)
                    footstepSource.clip = footstepClip;
            }

            // 单发音效需独立音频源：脚步声源在停下时会被 Pause，
            // 而对 Pause 状态的音频源调用 PlayOneShot 不会出声。
            // 未指定或误填成脚步声源时，运行时自建一个专用源。
            if (sfxSource == null || sfxSource == footstepSource)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
                // 复用脚步声源的空间/混音设置，保持定位一致。
                if (footstepSource != null)
                {
                    sfxSource.spatialBlend = footstepSource.spatialBlend;
                    sfxSource.rolloffMode = footstepSource.rolloffMode;
                    sfxSource.minDistance = footstepSource.minDistance;
                    sfxSource.maxDistance = footstepSource.maxDistance;
                    sfxSource.outputAudioMixerGroup = footstepSource.outputAudioMixerGroup;
                }
            }

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
            bool moving = IsAlive && IsMoving;

            // 每帧同步移动动画：只要单位还在朝目的地移动就播放 run。
            if (hasRunParam)
                animator.SetBool(RunHash, moving);

            // 脚步声与移动状态保持一致：开始移动时播放、停止时暂停。
            UpdateFootstep(moving);
        }

        // 根据移动状态开关脚步声：移动中循环播放，停下时暂停。
        private void UpdateFootstep(bool moving)
        {
            if (footstepSource == null || footstepSource.clip == null) return;

            if (moving)
            {
                if (!footstepSource.isPlaying)
                    footstepSource.Play();
            }
            else if (footstepSource.isPlaying)
            {
                footstepSource.Pause();
            }
        }

        // 播放一次性音效(攻击/受击)，使用独立的 sfxSource。
        // PlayOneShot 可与循环的脚步声叠加，互不打断。
        private void PlaySfx(AudioClip clip)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip);
        }

        // 在本单位自己的 sfxSource 上（延迟）播放一段 clip 作为主 clip。
        // 死亡音效走这里：物体会存活到它播完再销毁，故直接用自身音源即可。
        private void PlayOnSelf(AudioClip clip, float delay = 0f)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.clip = clip;
            if (delay > 0f)
                sfxSource.PlayDelayed(delay);
            else
                sfxSource.Play();
        }

        // 按攻击者种类查受击音效；未匹配或来源未知时用兜底 clip。
        private AudioClip ResolveHitClip(Unit source)
        {
            UnitType attackerType = source != null ? source.type : null;
            if (attackerType != null && hitSounds != null)
            {
                foreach (var entry in hitSounds)
                {
                    if (entry.attackerType == attackerType && entry.clip != null)
                        return entry.clip;
                }
            }
            return defaultHitClip;
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

        // attack 动画状态的名字（与控制器中的状态名一致）。
        private const string AttackStateName = "attack";
        // 攻击动画等待的兜底超时（秒），防止状态名不匹配或无动画时卡死。
        private const float AttackTimeout = 5f;

        // 攻击流程：置 attack=true 播放攻击动画 → 等动画真正播完 → 结算伤害 → 复位 attack。
        private IEnumerator AttackRoutine(Unit target)
        {
            if (hasAttackParam)
            {
                animator.SetBool(AttackHash, true);

                // 阶段一：等状态机真正切入 attack 状态（切换需要一到多帧，
                // 过早读取时长会读到 idle/过渡态而等得太短）。
                float deadline = Time.realtimeSinceStartup + AttackTimeout;
                while (!IsInAttackState() && Time.realtimeSinceStartup < deadline)
                    yield return null;

                // 阶段二：等 attack 状态播放到结尾（normalizedTime 到 1）。
                while (IsInAttackState() &&
                       animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f &&
                       Time.realtimeSinceStartup < deadline)
                    yield return null;

                // 复位，让状态机从 attack 退回 idle。
                animator.SetBool(AttackHash, false);
            }

            // 动画播放完毕后再判定伤害；期间目标可能已阵亡或销毁。
            if (IsAlive && target != null && target.IsAlive && type != null && target.type != null)
            {
                int amount = Damage.Compute(type.attack, target.type.defense);
                PlaySfx(attackClip);          // 攻击命中，播放本单位攻击音效
                target.TakeDamage(this, amount);
                Debug.Log($"{name} 攻击 {target.name}，造成 {amount} 点伤害。");
            }

            IsAttacking = false;
        }

        // 动画状态机（第 0 层）当前是否处于 attack 状态。
        private bool IsInAttackState()
        {
            if (animator == null) return false;
            return animator.GetCurrentAnimatorStateInfo(0).IsName(AttackStateName);
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

            // 致命打时，让死亡音效等受击音效播完再响；非致命打则为 0。
            float deathSoundDelay = 0f;

            if (applied > 0)
            {
                Damaged?.Invoke(source, applied);

                // 按攻击者种类播放受击音效。
                AudioClip hitClip = ResolveHitClip(source);
                if (hitClip != null)
                {
                    // 受击音效在自身音源上叠加播放。物体会存活到死亡音效播完再销毁，
                    // 故致命打时也能安全地用自身音源，无需脱离物体。
                    PlaySfx(hitClip);
                    if (CurrentHealth == 0)
                        deathSoundDelay = hitClip.length;  // 死亡音效顺延到受击音效之后
                }
            }

            if (CurrentHealth == 0)
                Die(source, deathSoundDelay);

            return applied;
        }

        // deathSoundDelay：死亡音效的延迟播放秒数，用于等致命打的受击音效先播完。
        private void Die(Unit killer, float deathSoundDelay = 0f)
        {
            CurrentStamina = 0;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            // 尸体在等待死亡音效播完期间不应再拦截点击/阻挡：立即关掉碰撞体。
            // 逻辑上已死亡（IsAlive=false），TurnManager 会跳过它，故保留物体不影响回合。
            foreach (var col in GetComponentsInChildren<Collider>())
                col.enabled = false;

            // 死亡音效：在自身音源上延迟 deathSoundDelay 秒播放（排在受击音效之后）。
            // 物体延后到音效播完再销毁，无需脱离本物体。
            float destroyDelay = 0f;
            if (deathClip != null && sfxSource != null)
            {
                PlayOnSelf(deathClip, deathSoundDelay);
                // 用真实时间兜底销毁：延迟 + 时长 + 余量。音频按真实时间播放，
                // 而 Destroy(delay) 用缩放时间，此处按真实秒数近似，正常 timeScale 下一致。
                destroyDelay = deathSoundDelay + deathClip.length + 0.1f;
            }
            else
            {
                // 没有死亡音效时，仍等致命打受击音效播完（deathSoundDelay）再销毁。
                destroyDelay = deathSoundDelay;
            }

            string killerName = killer != null ? killer.name : "未知来源";
            Debug.Log($"{name} 被 {killerName} 击败。");

            // 通知击杀者（用于击杀充能等被动）。在 Destroy 前触发。
            killer?.DealtKill?.Invoke(this);

            Destroy(gameObject, destroyDelay);
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
