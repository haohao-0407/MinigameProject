using UnityEngine;
using Vampire.Units;

namespace Vampire.Skills
{
    // 挂在英雄单位上，持有该单位各主动技能的运行时充能状态（SkillType 是共享 SO，不能存每单位状态）。
    // 负责：开局按技能配置初始化充能；监听击杀事件为“击杀充能”技能加充能；对外提供施放与查询接口。
    // 技能列表来源于 HeroUnitType.skills，索引与之一一对应。
    [RequireComponent(typeof(Unit))]
    public class SkillController : MonoBehaviour
    {
        private Unit unit;
        private SkillType[] skills;   // 来自 HeroUnitType.skills，可能为空
        private int[] charges;        // 与 skills 平行；非充能技能恒为 0

        private void Awake()
        {
            unit = GetComponent<Unit>();
            skills = (unit.Type as HeroUnitType)?.skills ?? System.Array.Empty<SkillType>();
            charges = new int[skills.Length];

            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] is ActiveSkill active && active.UsesCharges)
                    charges[i] = Mathf.Clamp(active.startingCharges, 0, active.maxCharges);
            }
        }

        private void OnEnable()
        {
            if (unit != null) unit.DealtKill += OnDealtKill;
        }

        private void OnDisable()
        {
            if (unit != null) unit.DealtKill -= OnDealtKill;
        }

        // 击杀敌方单位：为所有“击杀充能”技能各加 1 层（不超上限）。
        private void OnDealtKill(Unit victim)
        {
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] is ActiveSkill active && active.UsesCharges && active.gainChargeOnKill)
                    charges[i] = Mathf.Min(active.maxCharges, charges[i] + 1);
            }
        }

        public int SkillCount => skills.Length;

        public ActiveSkill GetActiveSkill(int index) =>
            index >= 0 && index < skills.Length ? skills[index] as ActiveSkill : null;

        public int GetCharges(int index) =>
            index >= 0 && index < charges.Length ? charges[index] : 0;

        // 该索引的技能当前是否可发动（存在、是主动、耐力足够、若用充能则有充能）。不含目标校验。
        public bool CanActivate(int index)
        {
            var active = GetActiveSkill(index);
            if (active == null || !active.CanExecute(unit)) return false;
            return !active.UsesCharges || charges[index] > 0;
        }

        // 是否至少有一个可发动的主动技能（供 TurnManager 判断是否允许进入技能选择模式）。
        public bool HasActivatableSkill()
        {
            for (int i = 0; i < skills.Length; i++)
                if (CanActivate(i)) return true;
            return false;
        }

        // 尝试对目标发动指定技能。成功返回 true 并扣除充能与耐力。
        public bool TryUse(int index, Unit target)
        {
            var active = GetActiveSkill(index);
            if (active == null || !CanActivate(index) || !active.CanTarget(unit, target))
                return false;

            active.Execute(unit, target);
            if (active.UsesCharges) charges[index]--;
            unit.SpendStamina(active.staminaCost);
            return true;
        }
    }
}
