using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "FieldArmy/Basic")]
public class FieldArmy : ScriptableObject
{
    public Faction faction;
    // public Nation nation;
    public List<ArmyReserves> USDReserves = new List<ArmyReserves>();
    public int ArmySupply;
    public int MaxArmySize = 20;
    public void AddTroop(UnitSaveData UnitToAdd, int amount = 1, bool ForceRecruit = false)
    {
        if (amount > 0 && GrabArmySize() > MaxArmySize && ForceRecruit == false)
        {
            return;
        }
        foreach (ArmyReserves item in USDReserves)
        {
            if (item.USD.name == UnitToAdd.name)
            {
                item.amount += amount;
                UpdateUI();
                return;
            }
        }
        ArmyReserves UR = new ArmyReserves();
        UR.name = UnitToAdd.name;
        UR.USD = UnitToAdd;
        UR.amount = amount;
        USDReserves.Add(UR);
        UpdateUI();
    }
    public void UpdateUI()
    {
        string newtext = "";
        foreach (ArmyReserves item in USDReserves)
        {
            newtext += item.amount + "X : " + item.USD.name + "\n";
        }
        UIElement.ArmyHost.UpdateTitle("Army", ArmySupply.ToString());
        UIElement.ArmyHost.UpdateSecond("Army", ArmySupply.ToString());
        UIElement.ArmyHost.UpdateThree(newtext);
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
    public void AddSupply(int suppliesToAdd)
    {
        ArmySupply += suppliesToAdd;
        if (ArmySupply < 0)
        {
            ArmySupply = 0;
        }
        if (ArmySupply > faction.FarmLevel * 100)
        {
            ArmySupply = faction.FarmLevel * 100;
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