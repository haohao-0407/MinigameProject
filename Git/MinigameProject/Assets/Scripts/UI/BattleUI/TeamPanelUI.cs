using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vampire.Core;
using Vampire.Units;
using Vampire.Turns;

// 队伍面板：自动捕获玩家阵营所有单位，按状态排序（受伤→健康→死亡），
// 管理 TeamSlotUI 列表。
public class TeamPanelUI : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotContainer;

    public event Action<Unit> OnUnitClicked;

    private readonly List<TeamSlotUI> slots = new List<TeamSlotUI>();
    private List<Unit> cachedUnits = new List<Unit>();

    void Update()
    {
        if (turnManager == null) return;

        // 获取玩家阵营的所有单位
        var playerUnits = FindObjectsOfType<Unit>()
            .Where(u => u != null && u.Faction == turnManager.GetPlayerFaction())
            .OrderBy(u => !u.IsAlive ? 2 : (u.CurrentHealth < u.Type.maxHealth ? 0 : 1))
            .ToList();

        // 仅在列表变化时重建
        if (!UnitsChanged(playerUnits)) return;
        cachedUnits = playerUnits;

        RebuildSlots(playerUnits);
    }

    private bool UnitsChanged(List<Unit> current)
    {
        if (current.Count != cachedUnits.Count) return true;
        for (int i = 0; i < current.Count; i++)
        {
            if (current[i] != cachedUnits[i]) return true;
            // 同一单位但血量变了也需要刷新
            if (current[i].CurrentHealth != cachedUnits[i].CurrentHealth ||
                current[i].IsAlive != cachedUnits[i].IsAlive)
                return true;
        }
        return false;
    }

    private void RebuildSlots(List<Unit> units)
    {
        // 清理旧槽位
        foreach (var slot in slots)
        {
            slot.OnClicked -= HandleSlotClicked;
            Destroy(slot.gameObject);
        }
        slots.Clear();

        // 创建新槽位
        foreach (var unit in units)
        {
            var go = Instantiate(slotPrefab, slotContainer);
            var slot = go.GetComponent<TeamSlotUI>();
            slot.SetUnit(unit);
            slot.OnClicked += HandleSlotClicked;
            slots.Add(slot);
        }
    }

    private void HandleSlotClicked(Unit unit)
    {
        OnUnitClicked?.Invoke(unit);
    }

    // 刷新所有槽位（不重建，用于实时更新血量/状态）
    public void RefreshAll()
    {
        foreach (var slot in slots)
            slot.Refresh();
    }
}
