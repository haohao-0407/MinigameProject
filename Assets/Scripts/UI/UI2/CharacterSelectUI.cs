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

        [Tooltip("顶部 BattleHUD（留空则自动查找；UI2 信息面板会同步显示它当前展示的单位，包括悬停/锁定的敌人）")]
        [SerializeField] private BattleHUD battleHUD;

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
        // 注意：项目中文本组件统一使用 TextMeshPro (TMP_Text)，不是 UGUI Text。
        private TMP_Text damageText;        // "伤害" 标签文本
        private TMP_Text speedText;         // "速度"
        private TMP_Text hpText;            // "血量/xue量" 标签
        private TMP_Text staminaText;       // "体力/ti力" 标签

        private Slider hpBar;              // HP 进度条（Slider，包含 Fill Image）
        private Slider staminaBar;         // 体力进度条（Slider，包含 Fill Image）

        // ---- 道具槽 ----
        private ItemSlotUI[] itemSlots = new ItemSlotUI[3];

        // ---- 中间角色选择区 ----
        private RectTransform characterListContent;  // mask > Content
        private GameObject characterCardTemplate;    // 角色卡片模板（第一个子物体）

        // ---- 状态 ----
        private Unit selectedUnit;
        private Unit displayedUnit;        // 当前信息面板实际显示的单位（可能是悬停敌人）
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

            if (battleHUD == null)
                battleHUD = FindObjectOfType<BattleHUD>();

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

            // 实时刷新信息面板：优先同步 BattleHUD 当前显示的单位
            //（鼠标悬停或 Ctrl+点击锁定的单位，可能是敌人）
            Unit target = GetCurrentDisplayUnit();
            if (target != null)
                RefreshInfoPanel(target);
        }

        /// <summary>
        /// 获取信息面板当前应该显示的单位。
        /// 优先级：BattleHUD 当前显示的单位 > 玩家选中的单位。
        /// 这样 UI2 就能像顶部 UI 一样读取现在选择/悬停的敌人数据。
        /// </summary>
        Unit GetCurrentDisplayUnit()
        {
            if (battleHUD != null && battleHUD.CurrentDisplayedUnit != null)
                return battleHUD.CurrentDisplayedUnit;

            return selectedUnit;
        }

        // -----------------------------------------------------------------
        // 引用解析（按名称查找，不依赖序列化拖拽）
        // -----------------------------------------------------------------

        void ResolveReferences()
        {
            // UI2 实际结构（SampleScene）：
            // UI2 (Canvas)
            //   └ Image (主面板，fileID 1275423618)
            //       ├ Image (信息面板，fileID 1078827093)  ← 伤害/速度/血量/体力条
            //       └ Image (1) (角色选择区，fileID 1105883273) ← 中间角色列表
            var rootPanel = transform.GetChild(0); // Image (主面板)

            Transform infoPanel = null;
            Transform characterSelectPanel = null;

            for (int i = 0; i < rootPanel.childCount; i++)
            {
                var child = rootPanel.GetChild(i);
                if (i == 0) infoPanel = child;
                else if (i == 1) characterSelectPanel = child;
            }

            // 信息面板包含属性标签、进度条和道具槽
            if (infoPanel != null)
            {
                // 查找 TextMeshPro 标签（中/英都支持）
                // 项目中文本组件统一是 TMP_Text，不是 UnityEngine.UI.Text。
                var labels = infoPanel.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in labels)
                {
                    string txt = t.text;
                    if (txt.Contains("伤害") || txt.Contains("Damage")) damageText = t;
                    else if (txt.Contains("速度") || txt.Contains("Speed")) speedText = t;
                    else if (txt.Contains("xue") || txt.Contains("血") || txt.Contains("HP") || txt.Contains("Health")) hpText = t;
                    else if (txt.Contains("ti") || txt.Contains("体") || txt.Contains("AP") || txt.Contains("Stamina")) staminaText = t;
                }

                // 属性条：场景里用的是 Slider（HPslider / SPslider）。
                // 伤害和速度只用文字显示，不绑进度条。
                var sliders = infoPanel.GetComponentsInChildren<Slider>(true);
                List<Slider> barSliders = new List<Slider>();
                foreach (var s in sliders)
                {
                    if (s.transform == infoPanel) continue;
                    barSliders.Add(s);
                }
                // 前2个 Slider 作为 HP 条和体力条
                if (barSliders.Count >= 2)
                {
                    hpBar = barSliders[0];
                    staminaBar = barSliders[1];
                }

                // 道具槽：找 mask 下的 Content 以外的底部 Image 区域
                var mask = infoPanel.Find("mask");
                if (mask != null)
                {
                    int slotIndex = 0;
                    for (int i = 0; i < infoPanel.childCount && slotIndex < 3; i++)
                    {
                        var c = infoPanel.GetChild(i);
                        if (c == mask) continue;
                        var slot = c.GetComponent<ItemSlotUI>();
                        if (slot == null) slot = c.gameObject.AddComponent<ItemSlotUI>();
                        itemSlots[slotIndex++] = slot;
                    }
                }
            }

            // 中间角色选择区 (Image 1) → ScrollRect → viewport → mask → Content
            if (characterSelectPanel != null)
            {
                var scrollRect = characterSelectPanel.GetComponentInChildren<ScrollRect>(true);
                if (scrollRect != null)
                    characterListContent = scrollRect.content;
                else
                {
                    // fallback: 直接找 mask > Content
                    var m = characterSelectPanel.Find("mask");
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

            // 更新卡片选中状态（仅在玩家阵营单位之间切换）
            foreach (var card in characterCards)
            {
                if (card != null)
                    card.SetSelected(card.Unit == unit);
            }

            // 播放点击音效
            PlayClickSound();

            // 信息面板刷新由 Update 统一处理，以便同步 BattleHUD/悬停单位
        }

        // -----------------------------------------------------------------
        // 左侧信息面板刷新
        // -----------------------------------------------------------------

        void RefreshInfoPanel(Unit unit)
        {
            if (unit == null || unit.Type == null) return;

            displayedUnit = unit;

            float hpRatio = Mathf.Clamp01((float)unit.CurrentHealth / unit.Type.maxHealth);
            float staminaRatio = Mathf.Clamp01((float)unit.CurrentStamina / unit.Type.maxStamina);

            // 只有血量和体力用进度条
            UpdateBar(hpBar, hpRatio);
            UpdateBar(staminaBar, staminaRatio);

            // 伤害和速度只用文字显示（使用英文，避免当前字体缺少中文字形导致方块）
            if (damageText != null) damageText.text = $"Damage:{unit.Type.attack}";
            if (speedText != null) speedText.text = $"Speed:{unit.Type.speed:F1}";
            // 血量和体力的标签也更新为带数值的文字
            if (hpText != null) hpText.text = $"Health:{unit.CurrentHealth}/{unit.Type.maxHealth}";
            if (staminaText != null) staminaText.text = $"Stamina:{unit.CurrentStamina}/{unit.Type.maxStamina}";

            // 敌人没有背包，显示敌人时清空道具槽；显示玩家单位时刷新道具槽
            if (unit.Faction != turnManager?.PlayerFaction)
                ClearItemSlots();
            else
                RefreshItemSlots(unit);
        }

        void ClearItemSlots()
        {
            for (int i = 0; i < itemSlots.Length; i++)
                if (itemSlots[i] != null) itemSlots[i].Clear();
        }

        static void UpdateBar(Slider bar, float fillAmount)
        {
            if (bar == null) return;
            bar.value = fillAmount;
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
