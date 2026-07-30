using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vampire.Units;

// 队伍槽位：显示单位名称/血量，根据状态（正常/受伤/死亡）改变外观，
// 悬停高亮，点击选中并播放音效。
public class TeamSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Image background;

    [Header("状态颜色")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color injuredColor = new Color(0.9f, 0.3f, 0.1f, 0.4f);
    [SerializeField] private Color injuredGlowColor = new Color(1f, 0.5f, 0.2f, 0.7f);
    [SerializeField] private Color deadColor = new Color(0.1f, 0.1f, 0.1f, 0.4f);
    [SerializeField] private Color hoverColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    public Unit Unit { get; private set; }
    public event Action<Unit> OnClicked;

    private AudioSource audioSource;
    private Color currentBaseColor;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void SetUnit(Unit unit)
    {
        Unit = unit;
        if (unit == null)
        {
            nameText.text = "";
            hpText.text = "";
            background.color = Color.clear;
            return;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (Unit == null)
        {
            nameText.text = "";
            hpText.text = "";
            background.color = Color.clear;
            return;
        }

        nameText.text = Unit.Type.displayName;
        hpText.text = $"HP: {Unit.CurrentHealth}/{Unit.Type.maxHealth}";

        if (!Unit.IsAlive)
        {
            background.color = deadColor;
            currentBaseColor = deadColor;
        }
        else if (Unit.CurrentHealth < Unit.Type.maxHealth)
        {
            // 受伤：发光
            background.color = injuredGlowColor;
            currentBaseColor = injuredGlowColor;
        }
        else
        {
            background.color = normalColor;
            currentBaseColor = normalColor;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Unit != null)
            background.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (Unit != null)
            background.color = currentBaseColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Unit == null || !Unit.IsAlive) return;

        if (audioSource != null && audioSource.clip != null)
            audioSource.Play();

        OnClicked?.Invoke(Unit);
    }
}
