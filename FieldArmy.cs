using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "FieldArmy/Basic")]
public class FieldArmy : ScriptableObject
{
    public Faction faction;
    public List<ArmyReserves> USDReserves = new List<ArmyReserves>();
    public void AddTroop(UnitSaveData UnitToAdd, int amount = 1)
    {
        foreach (ArmyReserves item in USDReserves)
        {
            if (item.USD.name == UnitToAdd.name)
            {
                item.amount += amount;
                return;
            }
        }
        ArmyReserves UR = new ArmyReserves();
        UR.name = UnitToAdd.name;
        UR.USD = UnitToAdd;
        UR.amount = amount;
        USDReserves.Add(UR);
    }
}
[System.Serializable]
public class ArmyReserves
{
    public string name;
    public UnitSaveData USD;
    public int amount;
}