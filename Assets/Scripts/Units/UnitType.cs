using UnityEngine;

// 单位种类模板：定义某一类单位的基础属性。
// 在 Project 里通过 Create > Game > Unit Type 生成不同种类（如战士、弓手）。
[CreateAssetMenu(fileName = "UnitType", menuName = "Game/Unit Type")]
public class UnitType : ScriptableObject
{
    [Header("标识")]
    public string displayName = "Unit";

    [Header("战斗属性（战斗系统未实现，先预留）")]
    public int attack = 5;            // 攻击力
    public int defense = 2;           // 防御力
    public float attackRange = 1.5f;  // 射程

    [Header("回合 / 行动")]
    public int maxStamina = 10;        // 耐力：每回合可用的行动点
    public float speed = 5f;           // 先攻值：决定行动顺序，越大越先行动
    public float moveCostPerMeter = 1f; // 每移动 1 米消耗的耐力
}
