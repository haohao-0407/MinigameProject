namespace Vampire.Core
{
    // 阵营：吸血鬼 vs 吸血鬼猎人团。仅表示队伍归属，与“由玩家还是 AI 操控”无关
    // （后者由 TurnManager 的 playerFaction 决定，将来玩家可选择游玩任一阵营）。
    public enum Faction { Vampire, VampireHunter }

    // 阵营的中文显示名
    public static class FactionNames
    {
        public static string Display(Faction f) =>
            f == Faction.Vampire ? "吸血鬼" : "吸血鬼猎人团";
    }
}
