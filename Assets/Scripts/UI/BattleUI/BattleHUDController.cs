using UnityEngine;
using Vampire.Units;
using Vampire.Turns;
using TMPro;

public class BattleHUDController : MonoBehaviour
{
    [SerializeField]
    private BattleHUD battleHUD;

    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private TMP_Text factionText;

    [SerializeField]
    private TMP_Text skillText;


    private Unit lastUnit;


    private void Update()
    {
        battleHUD.SetTurn(turnManager.currentIndex);

        if (turnManager.CurrentUnit == null)
            return;


        if (lastUnit != turnManager.CurrentUnit)
        {
            lastUnit = turnManager.CurrentUnit;

            battleHUD.SetUnit(lastUnit);
        }
        else
        {
            battleHUD.Refresh();
        }
    }
}