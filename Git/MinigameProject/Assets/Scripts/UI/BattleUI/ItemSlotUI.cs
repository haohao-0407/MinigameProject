using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vampire.Items;

// 道具槽位：悬停高亮、点击消耗并播放音效、数量归零清空
[RequireComponent(typeof(Image))]
public class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.6f);

    private Image background;
    private ItemData item;
    private Inventory inventory;
    private AudioSource audioSource;

    public event Action<ItemData> OnItemUsed;

    void Awake()
    {
        background = GetComponent<Image>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void SetItem(ItemData itemData, Inventory inv)
    {
        item = itemData;
        inventory = inv;
        Refresh();
    }

    public void Refresh()
    {
        if (item == null || inventory == null)
        {
            nameText.text = "";
            quantityText.text = "";
            background.color = Color.clear;
            return;
        }

        int qty = inventory.GetQuantity(item);
        if (qty <= 0)
        {
            item = null;
            nameText.text = "";
            quantityText.text = "";
            background.color = Color.clear;
            return;
        }

        nameText.text = item.displayName;
        quantityText.text = $"x{qty}";
        background.color = normalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item != null)
            background.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (item != null)
            background.color = normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (item == null || inventory == null) return;

        // 应用道具效果
        ApplyItemEffect();

        // 减少数量
        inventory.UseItem(item);

        // 播放音效
        if (audioSource != null && audioSource.clip != null)
            audioSource.Play();

        OnItemUsed?.Invoke(item);
        Refresh();
    }

    private void ApplyItemEffect()
    {
        var unit = inventory.GetComponent<Vampire.Units.Unit>();
        if (unit == null) return;

        if (item.healAmount > 0)
            unit.Heal(item.healAmount);
        if (item.staminaRecovery > 0)
            unit.SpendStamina(-item.staminaRecovery); // negative = restore
        if (item.chargeRestore > 0)
        {
            var sc = unit.GetComponent<Vampire.Skills.SkillController>();
            if (sc != null)
                sc.AddCharge(0, item.chargeRestore);
        }
    }
}
