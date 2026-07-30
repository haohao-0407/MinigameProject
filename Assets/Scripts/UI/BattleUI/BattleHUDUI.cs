using TMPro;
using UnityEngine;
using Vampire.Units;

public class BattleHUD : MonoBehaviour
{
    [Header("Selected Unit")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private TMP_Text factionText;
    [SerializeField] private TMP_Text skillText;

    [Header("Turn")]
    [SerializeField] private TMP_Text turnText;

    private Unit currentUnit;

    public Unit CurrentDisplayedUnit => currentUnit;

    public void SetTurn(int turn)
    {
        if (turnText != null)
            turnText.text = $"Turn {turn}";
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

        if (nameText != null)
        {
            if (currentUnit.Type != null)
                nameText.text = currentUnit.Type.displayName;
            else
                nameText.text = currentUnit.name;
        }

        if (hpText != null)
        {
            hpText.text =
                $"HP: {currentUnit.CurrentHealth}";
        }

        if (staminaText != null)
        {
            staminaText.text =
                $"AP: {currentUnit.CurrentStamina}";
        }

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
        if (nameText != null)
            nameText.text = string.Empty;

        if (hpText != null)
            hpText.text = string.Empty;

        if (staminaText != null)
            staminaText.text = string.Empty;

        if (factionText != null)
            factionText.text = string.Empty;

        if (skillText != null)
            skillText.text = string.Empty;

        /*
         * turnText 不清空。
         * 因为没有选中单位时，当前回合数字仍然应该显示。
         */
    }

    private string GetSkillInfo()
    {
        /*
         * Unit 暂时没有公开 SkillController，
         * 所以先保留占位内容。
         */
        return "Skill Ready";
    }
}