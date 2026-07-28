using UnityEngine;
using Vampire.Units;

namespace Vampire.Skills
{
    // 技能/被动/机制的数据驱动基类。每个具体技能是一个 ScriptableObject 资产，
    // 单位类型（UnitType）引用一组 SkillType 来获得能力。
    //
    // 尚未接入战斗系统 —— 这里只定义接口形状。具体主动技能继承 ActiveSkill，
    // 持续性被动继承 PassiveEffect。
    public abstract class SkillType : ScriptableObject
    {
        [Header("标识")]
        public string displayName = "Skill";
        [TextArea] public string description;

        // 发动/应用该技能。caster 为技能拥有者。
        // 具体参数（目标、位置等）待战斗系统落地后细化。
        public abstract void Execute(Unit caster);
    }
}
