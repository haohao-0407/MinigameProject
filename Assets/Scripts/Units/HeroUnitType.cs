using UnityEngine;
using Vampire.Skills;

namespace Vampire.Units
{
    // 英雄单位类型：在普通单位属性之上，拥有专属技能与更强的成长。
    // 通过 Create > Game > Hero Unit Type 生成。技能列表引用 Skills 系统的 SO。
    [CreateAssetMenu(fileName = "HeroUnitType", menuName = "Game/Hero Unit Type")]
    public class HeroUnitType : UnitType
    {
        [Header("英雄专属")]
        public string title;                 // 称号，如“血族领主”
        public SkillType[] skills;           // 专属技能 / 被动（数据驱动，可空）
    }
}
