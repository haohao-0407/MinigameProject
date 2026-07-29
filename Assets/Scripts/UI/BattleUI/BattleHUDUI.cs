using TMPro;
using UnityEngine;
using Vampire.Units;

public class BattleHUD : MonoBehaviour
{
    [Header("Selected Unit")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text factionText;
    [SerializeField] private TMP_Text skillText;


    public void SetTurn(int turn)
    {
        turnText.text = $"Turn {turn}";
    }


    private Unit currentUnit;


    public void SetUnit(Unit unit)
    {
        currentUnit = unit;

        if (unit == null)
        {
            Clear();
            return;
        }

        Refresh();
    }


    public void Refresh()
    {
        if (currentUnit == null)
            return;


        nameText.text =
            currentUnit.Type.displayName;


        hpText.text =
            $"HP: {currentUnit.CurrentHealth}";


        staminaText.text =
            $"AP: {currentUnit.CurrentStamina}";


        factionText.text =
            $"Faction: {currentUnit.Faction}";


        skillText.text =
            GetSkillInfo();
    }


    private void Clear()
    {
        nameText.text = "";
        hpText.text = "";
        staminaText.text = "";
    }

    private string GetSkillInfo()
    {
        return "Skill Ready";

    }
}