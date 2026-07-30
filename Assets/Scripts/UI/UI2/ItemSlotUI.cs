using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Vampire.UI.UI2
{
    /// <summary>
    /// 挂在每个道具槽（左侧面板底部的 3 个格子）上的脚本。
    /// 功能：
    ///   - 显示选中角色的道具图标和数量
    ///   - 左键点击减少数量
    ///   - 数量为0时清空显示
    /// </summary>
    public class ItemSlotUI : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("道具图标 Image")]
        [SerializeField] private Image iconImage;

        [Tooltip("数量文本")]
        [SerializeField] private TMP_Text countText;

        [Tooltip("空槽时显示的默认颜色")]
        [SerializeField] private Color emptyColor = new Color(0.85f, 0.85f, 0.85f, 0.3f);

        // 当前槽位数据
        private string itemId;
        private int count;
        private Sprite itemSprite;
        private System.Action<string> onItemUsed; // 点击回调

        // -----------------------------------------------------------------
        // 公开接口
        // -----------------------------------------------------------------

        /// <summary>设置槽位显示的道具</summary>
        public void SetItem(string id, int quantity, Sprite sprite = null, System.Action<string> onUsed = null)
        {
            itemId = id;
            count = quantity;
            itemSprite = sprite;
            onItemUsed = onUsed;
            RefreshDisplay();
        }

        /// <summary>清空槽位</summary>
        public void Clear()
        {
            itemId = null;
            count = 0;
            itemSprite = null;
            onItemUsed = null;
            RefreshDisplay();
        }

        /// <summary>获取当前道具ID</summary>
        public string ItemId => itemId;

        /// <summary>获取当前数量</summary>
        public int Count => count;

        // -----------------------------------------------------------------
        // Unity 事件
        // -----------------------------------------------------------------

        void Awake()
        {
            // 自动查找子元素（如果未手动绑定）
            if (iconImage == null) iconImage = GetComponent<Image>();
            if (countText == null) countText = GetComponentInChildren<TMP_Text>(true);
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            // 只响应左键
            if (eventData.button != PointerEventData.InputButton.Left) return;

            if (string.IsNullOrEmpty(itemId) || count <= 0) return;

            // 减少数量
            count--;

            // 触发回调（通知 CharacterSelectUI / Unit 消耗道具）
            onItemUsed?.Invoke(itemId);

            if (count <= 0)
            {
                Clear();
            }
            else
            {
                RefreshDisplay();
            }
        }

        // -----------------------------------------------------------------
        // 内部
        // -----------------------------------------------------------------

        void RefreshDisplay()
        {
            bool hasItem = !string.IsNullOrEmpty(itemId) && count > 0;

            if (iconImage != null)
            {
                iconImage.sprite = hasItem ? itemSprite : null;
                iconImage.color = hasItem ? Color.white : emptyColor;
            }

            if (countText != null)
            {
                countText.text = hasItem ? count.ToString() : "";
                countText.enabled = hasItem;
            }
        }
    }
}
