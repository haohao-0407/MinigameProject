using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vampire.Items
{
    public class Inventory : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public ItemData item;
            public int quantity;
        }

        [SerializeField] private List<Entry> items = new List<Entry>();

        public IReadOnlyList<Entry> Items => items;

        public event Action<ItemData, int, int> OnItemChanged;

        public int GetQuantity(ItemData item)
        {
            var entry = items.FirstOrDefault(e => e.item == item);
            return entry != null ? entry.quantity : 0;
        }

        public void AddItem(ItemData item, int amount = 1)
        {
            if (item == null || amount <= 0) return;
            var entry = items.FirstOrDefault(e => e.item == item);
            int old = entry != null ? entry.quantity : 0;
            if (entry != null)
                entry.quantity += amount;
            else
                items.Add(new Entry { item = item, quantity = amount });
            OnItemChanged?.Invoke(item, old, GetQuantity(item));
        }

        public int UseItem(ItemData item)
        {
            var entry = items.FirstOrDefault(e => e.item == item);
            if (entry == null || entry.quantity <= 0) return -1;
            int old = entry.quantity;
            entry.quantity--;
            if (entry.quantity <= 0)
                items.Remove(entry);
            OnItemChanged?.Invoke(item, old, GetQuantity(item));
            return GetQuantity(item);
        }

        public bool HasItem(ItemData item) => GetQuantity(item) > 0;
    }
}
