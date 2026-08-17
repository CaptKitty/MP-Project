using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "FieldArmy/Basic")]
public class FieldArmy : ScriptableObject
{
    public Faction faction;
    public Nation nation;
    public List<ArmyReserves> USDReserves = new List<ArmyReserves>();
    public int ArmySupply;
    public int MaxArmySize = 20;
    public List<ArmyRecruitmentOrder> recruitmentOrders = new List<ArmyRecruitmentOrder>();
    [Header("Deterministic Battle Deployment")]
    public SavedBattleDeployment battleDeployment = new SavedBattleDeployment();
    public void RemoveRandomUnit()
    {
        List<ArmyReserves> templist = new List<ArmyReserves>();
        foreach (ArmyReserves item in USDReserves)
        {
            if (item.amount > 0)
            {
                templist.Add(item);
            }
        }
        if (templist.Count > 0)
        {
            templist[Random.Range(0, templist.Count)].amount -= 1;
        }
    }
    public void AddTroop(UnitSaveData unittoAdd = null, string name = "", int amount = 1)
    {
        //Debug.LogError("Trying to add " + amount + " of " + name + unittoAdd);
        if (name != "")
        {
            try
            {
                var a = USDReserves.Find(x => x.name == name).USD;
                AddTroop(a, amount);
            }
            catch
            {
                try
                {
                    var b = Resources.Load<UnitSaveData>("Prefabs/Units/NormieData/" + name);
                    var c = Instantiate(b);
                    c.name = b.name;
                    AddTroop(c, amount);
                }
                catch
                {
                    Debug.LogError("Could not find " + name + " Unit in database");
                }

            }

        }
        else
        {
            if (unittoAdd == null)
            {
                List<NationUnitEntry> roster = NationContentResolver.ResolveUnits(nation);
                if (roster.Count > 0) AddTroop(roster[Random.Range(0, roster.Count)].unit, amount);
            }
            else
            {

                AddTroop(unittoAdd, amount);
            }
        }
    }
    public void AddTroop(UnitSaveData UnitToAdd, int amount = 1, bool ForceRecruit = false)
    {
        if (amount > 0 && GrabArmySize() > MaxArmySize && ForceRecruit == false)
        {
            return;
        }
        foreach (ArmyReserves item in USDReserves)
        {
            try
            {
                if (item.USD.name == UnitToAdd.name)
                {
                    item.amount += amount;
                    if (item.amount < 0)
                    {
                        item.amount = 0;
                    }
                    //UpdateUI();
                    return;
                }
            }
            catch
            {
                Debug.LogError(UnitToAdd.name);
                Debug.LogError(item.USD.name);
            }

        }
        //Debug.LogError("We don't have " + UnitToAdd.name + " yet.");
        ArmyReserves UR = new ArmyReserves();
        UR.name = UnitToAdd.name;
        UR.USD = UnitToAdd;
        UR.amount = amount;
        USDReserves.Add(UR);
    }
    public void UpdateUI()
    {
        if (UIElement.ArmyHost == null) return;
        // The army panel is selection-based. UpdateUI can also be invoked by
        // hovering an unrelated army, so the panel resolves the selected holder
        // rather than blindly presenting this ScriptableObject.
        UIElement.ArmyHost.RefreshArmyPanel(true);
    }
    public int GrabArmySize()
    {
        int a = 0;
        foreach (ArmyReserves item in USDReserves)
        {
            a += item.amount;
        }
        return a;
    }
    public int GrabQueuedArmySize()
    {
        int amount = 0;
        if (recruitmentOrders == null) recruitmentOrders = new List<ArmyRecruitmentOrder>();
        foreach (ArmyRecruitmentOrder order in recruitmentOrders)
            if (order != null) amount += Mathf.Max(0, order.amount);
        return amount;
    }
    public bool QueueRecruitment(UnitSaveData unit, int amount)
    {
        if (unit == null || amount <= 0 || GrabArmySize() + GrabQueuedArmySize() + amount > MaxArmySize) return false;
        if (recruitmentOrders == null) recruitmentOrders = new List<ArmyRecruitmentOrder>();
        recruitmentOrders.Add(new ArmyRecruitmentOrder
        {
            unit = unit,
            amount = amount,
            remainingTicks = unit.EffectiveRecruitmentTicks
        });
        return true;
    }
    public void ProcessRecruitmentTick()
    {
        if (recruitmentOrders == null || recruitmentOrders.Count == 0) return;

        // Recruitment is a FIFO queue. Only its first valid order consumes a tick,
        // and batched orders produce their units one at a time.
        while (recruitmentOrders.Count > 0)
        {
            ArmyRecruitmentOrder order = recruitmentOrders[0];
            if (order == null || order.unit == null || order.amount <= 0)
            {
                recruitmentOrders.RemoveAt(0);
                continue;
            }

            order.remainingTicks--;
            if (order.remainingTicks > 0)
            {
                RecruitmentMenu.RefreshQueueFor(this);
                return;
            }

            AddTroop(order.unit, 1, true);
            order.amount--;
            if (order.amount <= 0)
                recruitmentOrders.RemoveAt(0);
            else
                order.remainingTicks = order.unit.EffectiveRecruitmentTicks;
            RecruitmentMenu.RefreshQueueFor(this);
            return;
        }

        RecruitmentMenu.RefreshQueueFor(this);
    }
    public void AddSupply(int suppliesToAdd)
    {
        ArmySupply += suppliesToAdd;
        if (ArmySupply < 0)
        {
            ArmySupply = 0;
        }
        if (ArmySupply > nation.faction.FarmLevel * 100)
        {
            ArmySupply = nation.faction.FarmLevel * 100;
        }
        UpdateUI();
    }
}
[System.Serializable]
public class ArmyReserves
{
    public string name;
    public UnitSaveData USD;
    public int amount;
}

[System.Serializable]
public class ArmyRecruitmentOrder
{
    public UnitSaveData unit;
    public int amount = 1;
    public int remainingTicks = 1;
}
