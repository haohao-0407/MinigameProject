namespace Vampire.Combat
{
    // 伤害计算。集中放置公式，供普通攻击与将来的技能共用。
    public static class Damage
    {
        // 基础伤害：攻击力 - 防御力，最小为 1。
        public static int Compute(int attack, int defense)
        {
            return System.Math.Max(1, attack - defense);
        }
    }
}
