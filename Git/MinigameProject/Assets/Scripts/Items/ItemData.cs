using UnityEngine;

namespace Vampire.Items
{
    // 道具定义 ScriptableObject。通过 Create > Game > Item Data 生成具体道具。
    [CreateAssetMenu(fileName = "ItemData", menuName = "Game/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("标识")]
        public string displayName = "Item";
        [TextArea] public string description;

        [Header("效果")]
        public int healAmount;
        public int staminaRecovery;
        public int chargeRestore;
    }
}
