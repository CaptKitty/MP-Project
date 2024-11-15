using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Faction")]
public class Faction: ScriptableObject
{
    public Color color, color2, color3;

    public List<GameObject> UnitList = new List<GameObject>();
    public int BarracksLevel = 2;
    public int MercLevel = 0;
    public int FarmLevel = 0;
    public List<GameObject> BarracksUnits = new List<GameObject>();
    public List<GameObject> MercenaryUnits = new List<GameObject>();
    public int Income = 500;

    public Faction Init()
    {
        var a = Instantiate(this);
        a.MercenaryUnits.Clear();
        foreach (var item in MercenaryUnits)
        {
            a.MercenaryUnits.Add(item);
        }
        return a;
    }
    public void UpgradeBarracks()
    {
        UnitList.Add(BarracksUnits[BarracksLevel]);
    }
    public void UpgradeMercenaries()
    {
        var potato = MercenaryUnits[Random.Range(0, MercenaryUnits.Count)];
        potato.GetComponent<TestCritter>().Mercenary = true;
        UnitList.Add(potato);
        MercenaryUnits.Remove(potato);
    }
    public void UpgradeIncome()
    {
        Income = 500 + FarmLevel * 100;
    }
    public void Set()
    {
        UnitList.Clear();
        for (int i = 0; i < BarracksLevel; i++)
        {
            UnitList.Add(BarracksUnits[i]);
        }
        for (int i = 0; i < MercLevel; i++)
        {
            var potato = MercenaryUnits[Random.Range(0, MercenaryUnits.Count)];
            potato.GetComponent<TestCritter>().Mercenary = true;
            UnitList.Add(potato);
            MercenaryUnits.Remove(potato);
        }
        Income = 500 + FarmLevel * 100;
    }
}
