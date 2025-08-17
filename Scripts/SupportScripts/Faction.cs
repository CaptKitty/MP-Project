using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;

[System.Serializable]
[CreateAssetMenu(menuName = "Faction")]
public class Faction : ScriptableObject
{
    public Color color, color2, color3;

    public List<GameObject> UnitList = new List<GameObject>();
    public List<UnitSaveData> UnitDataList = new List<UnitSaveData>();
    public int BarracksLevel = 1;
    public int MercLevel = 0;
    public int FarmLevel = 0;
    public List<GameObject> BarracksUnits = new List<GameObject>();
    public List<UnitSaveData> BarracksDataList = new List<UnitSaveData>();
    public List<GameObject> MercenaryUnits = new List<GameObject>();
    public List<UnitSaveData> MercenaryDataList = new List<UnitSaveData>();

    public int Income = 500;

    public Faction Init()
    {
        var a = Instantiate(this);
        a.MercenaryUnits.Clear();
        foreach (var item in MercenaryUnits)
        {
            a.MercenaryUnits.Add(item);
        }
        for (int i = 0; i < BarracksDataList.Count; i++)
        {
            a.BarracksDataList[i] = Instantiate(BarracksDataList[i]);
            a.BarracksDataList[i].name = BarracksDataList[i].name;
            try
            {
                a.BarracksDataList[i].MeleeWeapon = a.BarracksDataList[i].MeleeWeapon.GrabCopy();
                a.BarracksDataList[i].RangedWeapon = a.BarracksDataList[i].RangedWeapon.GrabCopy();
            }
            catch { }
        }
        for (int i = 0; i < MercenaryDataList.Count; i++)
        {
            a.MercenaryDataList[i] = Instantiate(MercenaryDataList[i]);
            a.MercenaryDataList[i].name = MercenaryDataList[i].name;
            try
            {
                a.MercenaryDataList[i].MeleeWeapon = a.MercenaryDataList[i].MeleeWeapon.GrabCopy();
                a.MercenaryDataList[i].RangedWeapon = a.MercenaryDataList[i].RangedWeapon.GrabCopy();
            }
            catch { }
            
            
        }
        return a;
    }
    public void UpgradeBarracks()
    {
        // var potato = BarracksUnits[BarracksLevel];
        // potato.GetComponent<TestCritter>().Mercenary = false;
        // UnitList.Add(potato);
        UnitDataList.Add(BarracksDataList[BarracksLevel]);

        BarracksLevel++;
    }
    public void UpgradeMercenaries()
    {
        var MercenaryLevel = UnityEngine.Random.Range(0, MercenaryUnits.Count);
        // // var potato = MercenaryUnits[MercenaryLevel];
        // // potato.GetComponent<TestCritter>().Mercenary = true;
        // // UnitList.Add(potato);
        // MercenaryDataList[MercenaryLevel].Mercenary = true;
        // UnitDataList.Add(MercenaryDataList[MercenaryLevel]);
        var a = MercenaryDataList[MercenaryLevel];
        a.Mercenary = true;
        MercenaryDataList.Remove(a);
        UnitDataList.Add(a);

        //MercenaryUnits.Remove(potato);
    }
    public int GrabIncome()
    {
        return Income = FarmLevel * 100; //500 +
    }
    public void Set()
    {
        UnitList.Clear();
        for (int i = 0; i < BarracksLevel; i++)
        {
            try
            {
                //UnitList.Add(BarracksUnits[i]);
                UnitDataList.Add(BarracksDataList[i]);
            }
            catch { }
        }
        for (int i = 0; i < MercLevel; i++)
        {
            try
            {
                //var potato = MercenaryUnits[UnityEngine.Random.Range(0, MercenaryUnits.Count)];
                //potato.GetComponent<TestCritter>().Mercenary = true;
                //UnitList.Add(potato);
                //MercenaryUnits.Remove(potato);
                var a = MercenaryDataList[i];
                a.Mercenary = true;
                MercenaryDataList.Remove(a);
                UnitDataList.Add(a);
            }
            catch { }
            
        }
        Income = 500 + FarmLevel * 100;
    }
}
