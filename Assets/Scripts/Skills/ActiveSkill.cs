using UnityEngine;
using Vampire.Units;

namespace Vampire.Skills
{
    // 主动技能：需玩家/AI 在回合内主动发动，通常消耗耐力/冷却。
    // 具体技能（如吸血、冲锋）之后各建一个继承此类的 SO 并实现 Execute。
    public abstract class ActiveSkill : SkillType
    {
        [Header("消耗 / 冷却")]
        public int staminaCost = 0;   // 发动消耗的耐力（行动点）
        public int cooldownTurns = 0; // 冷却回合数

        [Header("充能（可选）")]
        [Tooltip("充能上限。为 0 表示该技能不使用充能机制")]
        public int maxCharges = 0;
        [Tooltip("开局自带的充能层数")]
        public int startingCharges = 0;
        [Tooltip("拥有者击杀敌方单位时获得 1 层充能")]
        public bool gainChargeOnKill = false;

        // 该技能是否使用充能机制。运行时充能层数由单位上的 SkillController 持有。
        public bool UsesCharges => maxCharges > 0;

        // 该技能当前是否可对给定施法者发动（耐力、冷却等）。充能校验由 SkillController 负责。
        public virtual bool CanExecute(Unit caster) =>
            caster != null && caster.CurrentStamina >= staminaCost;

        // 需要指定目标单位的技能是否可对该目标发动（射程、阵营、目标状态等）。
        // 默认无目标约束；需要目标的技能（如治疗）重写此方法。
        public virtual bool CanTarget(Unit caster, Unit target) => true;

        // 对指定目标发动。默认转调无目标的 Execute，便于不需要目标的技能沿用。
        public virtual void Execute(Unit caster, Unit target) => Execute(caster);
    }
}
