using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Vampire.Turns;
using Vampire.Units;

namespace Vampire.UI.UI2
{
    /// <summary>
    /// UI2 主控制器 —— 挂在 UI2 GameObject 上。
    /// 职责：
    ///   1. 中间角色选择框：自动列队内可用角色，选中联动左侧信息
    ///   2. 受伤自动置顶 + 高亮，死亡变暗
    ///   3. hover 高亮 + 点击音效
    ///   4. 左侧信息面板：实时显示选中角色的属性（伤害/速度/HP/体力）
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("TurnManager（留空自动查找）")]
        [SerializeField] private TurnManager turnManager;

        [Tooltip("点击音效（留空则不播放）")]
        [SerializeField] private AudioSource clickAudioSource;

        [Tooltip("hover 高亮颜色")]
        [SerializeField] private Color hoverHighlightColor = new Color(1f, 0.85f, 0.3f, 1f);

        [Tooltip("受伤高亮颜色")]
        [SerializeField] private Color injuredHighlightColor = new Color(1f, 0.4f, 0.3f, 1f);

        [Tooltip("死亡暗淡颜色")]
        [SerializeField] private Color deadDimColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

        [Tooltip("选中边框颜色")]
        [SerializeField] private Color selectedBorderColor = new Color(0.2f, 0.9f, 0.5f, 1f);

        // ---- 左侧信息面板元素（按名称查找） ----
        private Text damageText;            // "伤害：" 标签文本（UGUI Text，非 TMP）
        private Text speedText;             // "速度："
        private Text hpText;                // "xue量：" 标签
        private Text staminaText;           // "ti力：" 标签

        private Image hpBar;               // HP 进度条 Image（Filled 类型）
        private Image staminaBar;          // 体力进度条 Image（Filled 类型）

        // ---- 道具槽 ----
        private ItemSlotUI[] itemSlots = new ItemSlotUI[3];

        // ---- 中间角色选择区 ----
        private RectTransform characterListContent;  // mask > Content
        private GameObject characterCardTemplate;    // 角色卡片模板（第一个子物体）

        // ---- 状态 ----
        private Unit selectedUnit;
        private List<CharacterCard> characterCards = new List<CharacterCard>();
        private float nextSortTime;

        // -----------------------------------------------------------------
        // 生命周期
        // -----------------------------------------------------------------

        void Awake()
        {
            ResolveReferences();
        }

        void Start()
        {
            if (turnManager == null)
                turnManager = FindObjectOfType<TurnManager>();

            BuildCharacterList();
            SelectUnit(turnManager?.CurrentUnit);
        }

        void Update()
        {
            // 每隔一段时间刷新一次列表排序（受伤置顶）
            if (Time.time >= nextSortTime)
            {
                SortCharacterCards();
                nextSortTime = Time.time + 0.5f;
            }

            // 实时刷新左侧信息面板（跟随当前选中）
            if (selectedUnit != null)
                RefreshInfoPanel(selectedUnit);
        }

        // -----------------------------------------------------------------
        // 引用解析（按名称查找，不依赖序列化拖拽）
        // -----------------------------------------------------------------

        void ResolveReferences()
        {
            // UI2 结构: Image(主面板) > Image(2)[右侧+信息] | Image(1)[中间选角]
            var rootPanel = transform.GetChild(0); // Image (主面板)

            // --- 左侧信息面板 = rootPanel 的第1个子 (Image fid=1078827093) ---
            Transform leftPanel = null;
            Transform middlePanel = null;
            Transform rightPanel = null;

            for (int i = 0; i < rootPanel.childCount; i++)
            {
                var child = rootPanel.GetChild(i);
                if (i == 0) leftPanel = child;
                else if (i == 1) middlePanel = child;
                else if (i == 2) rightPanel = child;
            }

            // 右侧面板 (Image 2) 包含属性标签和条 + 道具槽
            if (rightPanel != null)
            {
                // 查找 UGUI Text 组件（标签："伤害：" / "速度：" / "xue量：" / "ti力："）
                // 注意：这些是普通 UnityEngine.UI.Text，不是 TextMeshPro
                var labels = rightPanel.GetComponentsInChildren<Text>(true);
                foreach (var t in labels)
                {
                    if (t.text.Contains("伤害")) damageText = t;
                    else if (t.text.Contains("速度")) speedText = t;
                    else if (t.text.Contains("xue") || t.text.Contains("血")) hpText = t;
                    else if (t.text.Contains("ti") || t.text.Contains("体")) staminaText = t;
                }

                // 属性条 Images：只有 HP 和体力用 Filled 进度条
                // 伤害和速度只用文字显示，不绑进度条
                var images = rightPanel.GetComponentsInChildren<Image>(true);
                List<Image> barImages = new List<Image>();
                foreach (var img in images)
                {
                    if (img.transform == rightPanel) continue;
                    if (img.type == Image.Type.Filled)
                        barImages.Add(img);
                }
                // Filled 类型的前2个作为 HP 条和体力条
                if (barImages.Count >= 2)
                {
                    hpBar = barImages[0];
                    staminaBar = barImages[1];
                }

                // 道具槽：找 mask 下的 Content 以外的底部 Image 区域
                // 或直接按名称/位置找最后几个 Image
                var mask = rightPanel.Find("mask");
                if (mask != null)
                {
                    // mask 之外的底部区域就是道具槽
                    int slotIndex = 0;
                    for (int i = 0; i < rightPanel.childCount && slotIndex < 3; i++)
                    {
                        var c = rightPanel.GetChild(i);
                        if (c == mask) continue;
                        var slot = c.GetComponent<ItemSlotUI>();
                        if (slot == null) slot = c.gameObject.AddComponent<ItemSlotUI>();
                        itemSlots[slotIndex++] = slot;
                    }
                }
            }

            // 中间角色选择区 (Image 1) → ScrollRect → viewport → mask → Content
            if (middlePanel != null)
            {
                var scrollRect = middlePanel.GetComponentInChildren<ScrollRect>(true);
                if (scrollRect != null)
                    characterListContent = scrollRect.content;
                else
                {
                    // fallback: 直接找 mask > Content
                    var m = middlePanel.Find("mask");
                    if (m != null) characterListContent = m.Find("Content") as RectTransform;
                }
            }
        }

        // -----------------------------------------------------------------
        // 角色列表构建
        // -----------------------------------------------------------------

        void BuildCharacterList()
        {
            if (characterListContent == null)
            {
                Debug.LogWarning("[CharacterSelectUI] 找不到角色列表 Content！");
                return;
            }

            // 清空旧卡片
            foreach (var card in characterCards)
            {
                if (card != null) Destroy(card.gameObject);
            }
            characterCards.Clear();

            // 从 TurnManager 获取玩家阵营单位
            if (turnManager == null) return;

            var playerUnits = turnManager.Units
                .Where(u => u != null && u.Faction == turnManager.PlayerFaction)
                .ToList();

            // 用第一个子物体作为模板（如果有），否则动态创建
            if (characterListContent.childCount > 0)
                characterCardTemplate = characterListContent.GetChild(0).gameObject;

            foreach (var unit in playerUnits)
            {
                CreateCharacterCard(unit);
            }

            SortCharacterCards();
        }

        void CreateCharacterCard(Unit unit)
        {
            GameObject cardObj;

            if (characterCardTemplate != null && characterListContent.childCount > 0)
            {
                // 复用已有模板
                cardObj = Instantiate(characterCardTemplate, characterListContent);
            }
            else
            {
                // 动态创建简单卡片
                cardObj = new GameObject($"Card_{unit.name}");
                cardObj.transform.SetParent(characterListContent, false);
                // 基础布局
                var rt = cardObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(150, 70);
                var img = cardObj.AddComponent<Image>();
                img.color = new Color(0.9f, 0.9f, 0.9f, 0.8f);

                // 头像
                var portraitGo = new GameObject("Portrait");
                portraitGo.transform.SetParent(cardObj.transform, false);
                var prt = portraitGo.AddComponent<RectTransform>();
                prt.anchorMin = prt.anchorMax = Vector2.zero;
                prt.sizeDelta = new Vector2(40, 40);
                prt.anchoredPosition = new Vector2(5, -15);
                var portraitImg = portraitGo.AddComponent<Image>();
                portraitImg.color = new Color(0.7f, 0.7f, 0.85f, 1f);

                // HP Bar
                var hpBarGo = new GameObject("HPBar");
                hpBarGo.transform.SetParent(cardObj.transform, false);
                var hprt = hpBarGo.AddComponent<RectTransform>();
                hprt.anchorMin = new Vector2(0.5f, 0.7f);
                hprt.anchorMax = new Vector2(0.95f, 0.85f);
                hprt.offsetMin = hprt.offsetMax = Vector2.zero;
                var hpBarImg = hpBarGo.AddComponent<Image>();
                hpBarImg.color = new Color(0.9f, 0.35f, 0.35f, 1f);

                // AP Bar
                var apBarGo = new GameObject("APBar");
                apBarGo.transform.SetParent(cardObj.transform, false);
                var aprt = apBarGo.AddComponent<RectTransform>();
                aprt.anchorMin = new Vector2(0.5f, 0.5f);
                aprt.anchorMax = new Vector2(0.95f, 0.65f);
                aprt.offsetMin = aprt.offsetMax = Vector2.zero;
                var apBarImg = apBarGo.AddComponent<Image>();
                apBarImg.color = new Color(0.95f, 0.7f, 0.2f, 1f);
            }

            // 添加卡片行为脚本
            var card = cardObj.GetComponent<CharacterCard>();
            if (card == null) card = cardObj.AddComponent<CharacterCard>();
            card.Initialize(unit, this);
            characterCards.Add(card);
        }

        // -----------------------------------------------------------------
        // 排序：受伤置顶
        // -----------------------------------------------------------------

        void SortCharacterCards()
        {
            if (characterCards.Count == 0) return;

            // 排序规则：死亡 > 受伤(HP%低在前) > 健康，同级别保持稳定排序
            var sorted = characterCards
                .Where(c => c != null && c.Unit != null)
                .OrderByDescending(c => !c.Unit.IsAlive)     // 死亡放最后？不，需求说"死亡变暗"不是移到底部
                .ThenBy(c =>
                {
                    if (!c.Unit.IsAlive) return 2;          // 死亡放后面
                    if (c.Unit.Type != null && c.Unit.CurrentHealth < c.Unit.Type.maxHealth)
                        return 0;                            // 受伤放前面
                    return 1;                                 // 健康中间
                })
                .ThenBy(c => c.Unit.Type != null ? c.Unit.CurrentHealth : 0) // 受伤越重越靠前
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].transform.SetSiblingIndex(i);
                sorted[i].UpdateVisualState(); // 刷新高亮/暗淡状态
            }
        }

        // -----------------------------------------------------------------
        // 选中联动
        // -----------------------------------------------------------------

        public void SelectUnit(Unit unit)
        {
            if (unit == null || !unit.IsAlive) return;

            selectedUnit = unit;
            RefreshInfoPanel(unit);

            // 更新卡片选中状态
            foreach (var card in characterCards)
            {
                if (card != null)
                    card.SetSelected(card.Unit == unit);
            }

            // 更新道具槽
            RefreshItemSlots(unit);

            // 播放点击音效
            PlayClickSound();
        }

        // -----------------------------------------------------------------
        // 左侧信息面板刷新
        // -----------------------------------------------------------------

        void RefreshInfoPanel(Unit unit)
        {
            if (unit == null || unit.Type == null) return;

            float hpRatio = Mathf.Clamp01((float)unit.CurrentHealth / unit.Type.maxHealth);
            float staminaRatio = Mathf.Clamp01((float)unit.CurrentStamina / unit.Type.maxStamina);

            // 只有血量和体力用进度条
            UpdateBar(hpBar, hpRatio);
            UpdateBar(staminaBar, staminaRatio);

            // 伤害和速度只用文字显示
            if (damageText != null) damageText.text = $"伤害：{unit.Type.attack}";
            if (speedText != null) speedText.text = $"速度：{unit.Type.speed:F1}";
            // 血量和体力的标签也更新为带数值的文字
            if (hpText != null) hpText.text = $"xue量：{unit.CurrentHealth}/{unit.Type.maxHealth}";
            if (staminaText != null) staminaText.text = $"ti力：{unit.CurrentStamina}/{unit.Type.maxStamina}";
        }

        static void UpdateBar(Image bar, float fillAmount)
        {
            if (bar == null) return;
            bar.fillAmount = fillAmount;
        }

        // -----------------------------------------------------------------
        // 道具槽刷新
        // -----------------------------------------------------------------

        void RefreshItemSlots(Unit unit)
        {
            if (unit == null) return;

            var inv = unit.Inventory;
            if (inv == null)
            {
                // 无背包：清空所有槽位
                for (int i = 0; i < itemSlots.Length; i++)
                    if (itemSlots[i] != null) itemSlots[i].Clear();
                return;
            }

            // 用背包的非空槽位填充 UI 槽位
            var nonEmptySlots = inv.Slots;
            for (int i = 0; i < itemSlots.Length; i++)
            {
                if (itemSlots[i] == null) continue;

                if (i < nonEmptySlots.Count)
                {
                    var slot = nonEmptySlots[i];
                    itemSlots[i].SetItem(
                        slot.ItemId,
                        slot.Count,
                        slot.Icon,
                        (usedId) => inv.UseItem(usedId, out _)
                    );
                }
                else
                {
                    itemSlots[i].Clear();
                }
            }
        }

        // -----------------------------------------------------------------
        // 音效
        // -----------------------------------------------------------------

        public void PlayClickSound()
        {
            if (clickAudioSource != null)
                clickAudioSource.Play();
        }

        // -----------------------------------------------------------------
        // 公开接口供 CharacterCard 回调
        // -----------------------------------------------------------------

        public Unit SelectedUnit => selectedUnit;
        public Color HoverColor => hoverHighlightColor;
        public Color InjuredColor => injuredHighlightColor;
        public Color DeadColor => deadDimColor;
        public Color SelectedColor => selectedBorderColor;
    }
}
