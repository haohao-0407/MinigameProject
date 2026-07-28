using Vampire.Units;

namespace Vampire.Skills
{
    // 被动效果：无需主动发动，在特定时机自动触发（回合开始、受伤、靠近敌人等）。
    // 具体被动（如再生、见血狂暴）之后各建一个继承此类的 SO 并重写相应钩子。
    public abstract class PassiveEffect : SkillType
    {
        // 被动通常不主动发动 —— Execute 默认空实现，改用下面的时机钩子。
        public override void Execute(Unit caster) { }

        // 回合开始时触发（如每回合回血）。
        public virtual void OnTurnStart(Unit owner) { }

        // 拥有者受到伤害时触发（如反伤、狂暴）。
        public virtual void OnDamaged(Unit owner, Unit source, int amount) { }
    }
}
