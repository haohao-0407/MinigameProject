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

        // 该技能当前是否可对给定施法者发动（耐力、冷却等）。
        public virtual bool CanExecute(Unit caster) =>
            caster != null && caster.CurrentStamina >= staminaCost;
    }
}
