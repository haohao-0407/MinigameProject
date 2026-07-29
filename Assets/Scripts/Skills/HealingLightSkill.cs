using UnityEngine;
using Vampire.Units;

namespace Vampire.Skills
{
    // 治愈圣光：吸血鬼猎人方英雄的专属技能。
    // 主动：消耗 1 层充能，治疗治疗半径内的一个友方单位。
    // 被动：击杀敌方单位获得 1 层充能（开局自带 1 层，有上限）—— 充能机制由基类 ActiveSkill 提供。
    [CreateAssetMenu(fileName = "HealingLight", menuName = "Game/Skills/Healing Light")]
    public class HealingLightSkill : ActiveSkill
    {
        [Header("治疗")]
        public int healAmount = 8;      // 每次治疗恢复的生命值
        public float healRadius = 5f;   // 可治疗的最大距离（施法者到目标中心）

        // 目标须为存活的友方、未满血、且在治疗半径内。
        public override bool CanTarget(Unit caster, Unit target)
        {
            if (caster == null || target == null || !target.IsAlive) return false;
            if (caster.IsHostileTo(target)) return false;                 // 仅友方（含自身）
            if (target.Type == null || target.CurrentHealth >= target.Type.maxHealth) return false;
            return Vector3.Distance(caster.transform.position, target.transform.position) <= healRadius;
        }

        // 治愈圣光必须指定目标；无目标的调用无意义，交由带目标的重载处理。
        public override void Execute(Unit caster)
        {
            Debug.LogWarning($"{caster?.name} 的治愈圣光需要指定目标，已忽略无目标调用。");
        }

        public override void Execute(Unit caster, Unit target)
        {
            if (target == null) return;
            int restored = target.Heal(healAmount);
            Debug.Log($"{caster?.name} 对 {target.name} 施放治愈圣光，恢复 {restored} 点生命。");
        }
    }
}
