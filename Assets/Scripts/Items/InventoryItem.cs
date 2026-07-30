using System.Collections.Generic;
using UnityEngine;

namespace Vampire.Items
{
    /// <summary>
    /// 道具槽位数据（可序列化，用于 Inspector 或运行时赋值）。
    /// 挂在 Unit 上或由 CharacterSelectUI 管理的背包数据。
    /// </summary>
    [System.Serializable]
    public class InventoryItem
    {
        public string itemId;           // 道具唯一标识
        public string displayName;      // 显示名称
        public Sprite icon;             // 图标
        public int maxStack = 99;       // 最大堆叠数

        [Tooltip("使用效果类型")]
        public ItemType itemType;

        [Tooltip("效果数值（回复HP量/伤害等）")]
        public int effectValue;

        [Tooltip("是否消耗品")]
        public bool consumable = true;

        public enum ItemType { Heal, Damage, Buff, Misc }

        /// <summary>道具使用结果</summary>
        public struct UsedResult
        {
            public string itemId;
            public int remaining;
            public int slotIndex;
        }
    }

    /// <summary>
    /// 简单背包管理器。挂在 Unit GameObject 上或作为独立组件。
    /// 提供：添加/移除/查询道具、按槽位遍历。
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

        // 道具变化事件
        public event System.Action OnInventoryChanged;

        /// <summary>获取所有非空槽位（只读）</summary>
        public List<InventorySlot> Slots => slots.FindAll(s => s != null && s.Count > 0);

        /// <summary>总槽数</summary>
        public int SlotCount => slots?.Count ?? 0;

        // -----------------------------------------------------------------
        // 槽位操作
        // -----------------------------------------------------------------

        void Awake()
        {
            // 初始化空槽位（如果未配置）
            if (slots == null || slots.Count == 0)
            {
                slots = new List<InventorySlot>();
                for (int i = 0; i < 3; i++) // 默认3个槽位
                    slots.Add(new InventorySlot());
            }
        }

        /// <summary>添加道具到背包（自动堆叠或找空槽）</summary>
        public bool AddItem(InventoryItem itemData, int quantity = 1)
        {
            if (itemData == null || quantity <= 0) return false;

            // 先尝试堆叠到已有同ID槽位
            foreach (var slot in slots)
            {
                if (slot != null && slot.ItemId == itemData.itemId)
                {
                    int canAdd = Mathf.Min(quantity, itemData.maxStack - slot.Count);
                    if (canAdd > 0)
                    {
                        slot.Count += canAdd;
                        quantity -= canAdd;
                        if (quantity <= 0) { NotifyChange(); return true; }
                    }
                }
            }

            // 找空槽放入剩余
            while (quantity > 0)
            {
                var emptySlot = slots.Find(s => s != null && string.IsNullOrEmpty(s.ItemId));
                if (emptySlot == null) return false; // 背包满了

                int put = Mathf.Min(quantity, itemData.maxStack);
                emptySlot.Set(itemData.itemId, put, itemData.icon, itemData.displayName);
                quantity -= put;
            }

            NotifyChange();
            return true;
        }

        /// <summary>从指定槽位移除/使用道具</summary>
        public bool UseItemAt(int slotIndex, out InventoryItem.UsedResult result)
        {
            result = default;
            if (slotIndex < 0 || slotIndex >= slots.Count) return false;

            var slot = slots[slotIndex];
            if (slot == null || slot.Count <= 0) return false;

            slot.Count--;

            result = new InventoryItem.UsedResult
            {
                itemId = slot.ItemId,
                remaining = slot.Count,
                slotIndex = slotIndex
            };

            if (slot.Count <= 0)
                slot.Clear();

            NotifyChange();
            return true;
        }

        /// <summary>按 ID 使用一个道具（任意槽位）</summary>
        public bool UseItem(string itemId, out InventoryItem.UsedResult result)
        {
            result = default;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i]?.ItemId == itemId && slots[i].Count > 0)
                {
                    return UseItemAt(i, out result);
                }
            }
            return false;
        }

        void NotifyChange() => OnInventoryChanged?.Invoke();
    }

    // -----------------------------------------------------------------
    // 辅助数据结构
    // -----------------------------------------------------------------

    [System.Serializable]
    public class InventorySlot
    {
        public string ItemId = "";
        public int Count = 0;
        public Sprite Icon;
        public string DisplayName = "";

        public void Set(string id, int count, Sprite icon = null, string name = "")
        {
            ItemId = id;
            Count = count;
            Icon = icon;
            DisplayName = name;
        }

        public void Clear()
        {
            ItemId = "";
            Count = 0;
            Icon = null;
            DisplayName = "";
        }
    }
}
