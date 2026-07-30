using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vampire.Units;

public class BattleHUD : MonoBehaviour
{
    [Header("Avatar")]
    [SerializeField] private Image avatarImage;

    [Header("Selected Unit")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private TMP_Text factionText;
    [SerializeField] private TMP_Text skillText;

    [Header("Bars")]
    [SerializeField] private Image hpBarFill;
    [SerializeField] private TMP_Text hpLabelText;
    [SerializeField] private Image staminaBarFill;
    [SerializeField] private TMP_Text staminaLabelText;

    [Header("Turn")]
    [SerializeField] private TMP_Text turnText;

    private Unit currentUnit;

    public Unit CurrentDisplayedUnit => currentUnit;

    public void SetTurn(int turn)
    {
        if (turnText != null)
            turnText.text = $"Round {turn}";
    }

    public void SetUnit(Unit unit)
    {
        currentUnit = unit;

        if (currentUnit == null)
        {
            ClearUnitInformation();
            return;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (currentUnit == null)
        {
            ClearUnitInformation();
            return;
        }

        // avatar
        if (avatarImage != null && currentUnit.Type != null)
        {
            Sprite portrait = currentUnit.Type.portrait;
            if (portrait != null)
            {
                avatarImage.sprite = portrait;
                avatarImage.enabled = true;
            }
            else
            {
                avatarImage.enabled = false;
            }
        }

        if (nameText != null)
        {
            if (currentUnit.Type != null)
                nameText.text = currentUnit.Type.displayName;
            else
                nameText.text = currentUnit.name;
        }

        // HP bar
        if (hpBarFill != null && currentUnit.Type != null)
        {
            float ratio = Mathf.Clamp01((float)currentUnit.CurrentHealth / currentUnit.Type.maxHealth);
            hpBarFill.fillAmount = ratio;
        }

        if (hpLabelText != null)
        {
            hpLabelText.text = currentUnit.Type != null
                ? $"{currentUnit.CurrentHealth}/{currentUnit.Type.maxHealth}"
                : $"{currentUnit.CurrentHealth}";
        }

        // Stamina bar
        if (staminaBarFill != null && currentUnit.Type != null)
        {
            float ratio = Mathf.Clamp01((float)currentUnit.CurrentStamina / currentUnit.Type.maxStamina);
            staminaBarFill.fillAmount = ratio;
        }

        if (staminaLabelText != null)
        {
            staminaLabelText.text = currentUnit.Type != null
                ? $"{currentUnit.CurrentStamina}/{currentUnit.Type.maxStamina}"
                : $"{currentUnit.CurrentStamina}";
        }

        // legacy text
        if (hpText != null)
            hpText.text = $"HP: {currentUnit.CurrentHealth}";

        if (staminaText != null)
            staminaText.text = $"AP: {currentUnit.CurrentStamina}";

        if (factionText != null)
        {
            factionText.text =
                $"Faction: {currentUnit.Faction}";
        }

        if (skillText != null)
        {
            skillText.text = GetSkillInfo();
        }
    }

    public void Clear()
    {
        currentUnit = null;
        ClearUnitInformation();
    }

    private void ClearUnitInformation()
    {
        if (avatarImage != null)
            avatarImage.enabled = false;

        if (nameText != null)
            nameText.text = string.Empty;

        if (hpText != null)
            hpText.text = string.Empty;

        if (staminaText != null)
            staminaText.text = string.Empty;

        if (hpBarFill != null)
            hpBarFill.fillAmount = 0f;

        if (hpLabelText != null)
            hpLabelText.text = string.Empty;

        if (staminaBarFill != null)
            staminaBarFill.fillAmount = 0f;

        if (staminaLabelText != null)
            staminaLabelText.text = string.Empty;

        if (factionText != null)
            factionText.text = string.Empty;

        if (skillText != null)
            skillText.text = string.Empty;

        // turnText retained -- turn number stays visible even when no unit is selected.
    }

    private string GetSkillInfo()
    {
        // Skill system not yet wired to Unit -- placeholder.
        return "Skill Ready";
    }
}
