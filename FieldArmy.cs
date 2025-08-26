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
                var a = nation.faction.UnitDataList[Random.Range(0, nation.faction.UnitDataList.Count)];
                AddTroop(a, amount);
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