using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vampire.Units;

namespace Vampire.UI.UI2
{
    /// <summary>
    /// 挂在每个角色卡片上的行为脚本。
    /// 负责：hover 高亮、点击选中、受伤/死亡视觉状态。
    /// 由 CharacterSelectUI 在构建列表时初始化。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class CharacterCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [System.NonSerialized] public Unit Unit;

        private CharacterSelectUI controller;
        private Image cardImage;
        private Image portraitImage;
        private Image hpBarFill;
        private Image apBarFill;
        private bool isSelected;
        private bool isHovering;

        // 原始颜色（用于 hover 恢复）
        private Color normalColor = new Color(0.93f, 0.93f, 0.93f, 0.85f);
        private Color selectedGlowColor = new Color(0.3f, 0.95f, 0.55f, 1f);

        // -----------------------------------------------------------------
        // 初始化（由 CharacterSelectUI 调用）
        // -----------------------------------------------------------------

        public void Initialize(Unit unit, CharacterSelectUI ctrl)
        {
            Unit = unit;
            controller = ctrl;

            cardImage = GetComponent<Image>();
            if (cardImage != null)
                normalColor = cardImage.color;

            // 查找子元素
            var portrait = transform.Find("Portrait");
            if (portrait != null) portraitImage = portrait.GetComponent<Image>();

            var hpBar = transform.Find("HPBar");
            if (hpBar != null) hpBarFill = hpBar.GetComponent<Image>();

            var apBar = transform.Find("APBar");
            if (apBar != null) apBarFill = apBar.GetComponent<Image>();

            // 添加点击事件
            var btn = GetComponent<Button>();
            if (btn == null) btn = gameObject.AddComponent<Button>();
            btn.onClick.AddListener(OnClick);

            // 添加 hover 检测事件触发器
            var evt = GetComponent<EventTrigger>() ?? gameObject.AddComponent<EventTrigger>();

            UpdateVisualState();
        }

        // -----------------------------------------------------------------
        // 交互
        // -----------------------------------------------------------------

        void OnClick()
        {
            if (Unit == null || !Unit.IsAlive) return;
            controller?.SelectUnit(Unit);
        }

        // -----------------------------------------------------------------
        // IPointerHandler 接口实现（hover 高亮）
        // -----------------------------------------------------------------

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
            UpdateVisualState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            UpdateVisualState();
        }

        // -----------------------------------------------------------------
        // 视觉状态更新
        // -----------------------------------------------------------------

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateVisualState();
        }

        /// <summary>
        /// 刷新卡片的完整视觉状态：死亡/受伤/选中/hover
        /// </summary>
        public void UpdateVisualState()
        {
            if (Unit == null) return;

            // ---- 死亡：整体变暗 ----
            if (!Unit.IsAlive)
            {
                SetCardAlpha(controller?.DeadColor ?? new Color(0.4f, 0.4f, 0.4f, 0.5f));
                if (cardImage != null) cardImage.color = controller?.DeadColor ?? deadDimColor;
                return;
            }

            // ---- 受伤高亮 ----
            bool isInjured = Unit.Type != null && Unit.CurrentHealth < Unit.Type.maxHealth;

            if (cardImage != null)
            {
                if (isSelected)
                {
                    cardImage.color = controller?.SelectedColor ?? selectedGlowColor;
                }
                else if (isHovering)
                {
                    cardImage.color = controller?.HoverColor ?? new Color(1f, 0.85f, 0.3f, 1f);
                }
                else if (isInjured)
                {
                    // 受伤时发亮（偏红/橙色）
                    cardImage.color = controller?.InjuredColor ?? new Color(1f, 0.4f, 0.3f, 1f);
                }
                else
                {
                    cardImage.color = normalColor;
                }
            }

            // ---- HP/AP 条更新 ----
            if (hpBarFill != null && Unit.Type != null)
            {
                float hpRatio = Mathf.Clamp01((float)Unit.CurrentHealth / Unit.Type.maxHealth);
                hpBarFill.fillAmount = hpRatio;
                // 低血量变红
                hpBarFill.color = hpRatio < 0.3f ? new Color(1f, 0.2f, 0.2f, 1f) : new Color(0.9f, 0.35f, 0.35f, 1f);
            }

            if (apBarFill != null && Unit.Type != null)
            {
                float apRatio = Mathf.Clamp01((float)Unit.CurrentStamina / Unit.Type.maxStamina);
                apBarFill.fillAmount = apRatio;
            }
        }

        void SetCardAlpha(Color c)
        {
            if (cardImage != null) cardImage.color = c;
            if (portraitImage != null)
            {
                var pc = portraitImage.color;
                pc.a = c.a;
                portraitImage.color = pc;
            }
            if (hpBarFill != null)
            {
                var hc = hpBarFill.color;
                hc.a = c.a * 0.7f;
                hpBarFill.color = hc;
            }
            if (apBarFill != null)
            {
                var ac = apBarFill.color;
                ac.a = c.a * 0.7f;
                apBarFill.color = ac;
            }
        }

        // 备用死色
        private static readonly Color deadDimColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
    }
}
