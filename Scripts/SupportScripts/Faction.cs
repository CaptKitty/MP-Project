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
    public List<string> Flaglist = new List<string>();
    public FactionTheme factionTheme;
    [Tooltip("Optional faction override for the governing assembly name.")]
    public string assemblyName;

    [Header("Nation identity contribution")]
    public NationContentLayer content = new NationContentLayer();
    [Tooltip("A null replacement removes the inherited unit.")]
    public List<FactionUnitReplacement> unitReplacements = new List<FactionUnitReplacement>();
    [Tooltip("A null replacement removes the inherited building.")]
    public List<FactionBuildingReplacement> buildingReplacements = new List<FactionBuildingReplacement>();

    public int Income = 500;

    public Faction Init()
    {
        var a = Instantiate(this);
        a.MercenaryUnits.Clear();
        a.name = name;
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
        foreach (var item in Flaglist)
        {
            a.Flaglist.Add(item);
        }
        a.factionTheme = factionTheme;
        return a;
    }
    public void UpgradeBarracks()
    {
        // var potato = BarracksUnits[BarracksLevel];
        // potato.GetComponent<TestCritter>().Mercenary = false;
        // UnitList.Add(potato);
        if (BarracksLevel >= BarracksDataList.Count) return;
        UnitSaveData unlocked = BarracksDataList[BarracksLevel];
        if (unlocked != null && !UnitDataList.Contains(unlocked)) UnitDataList.Add(unlocked);
        BarracksLevel++;
    }

    public int GetBarracksTier(UnitSaveData unit)
    {
        if (unit == null || BarracksDataList == null) return 0;
        for (int i = 0; i < BarracksDataList.Count; i++)
        {
            UnitSaveData candidate = BarracksDataList[i];
            if (candidate == unit || candidate != null &&
                (candidate.name == unit.name || candidate.unitname == unit.unitname))
                return i + 1;
        }
        return 0;
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
    public bool HasFlag(string flag)
    {
        foreach (var item in Flaglist)
        {
            if (item == flag)
            {
                return true;
            }
        }
        return false;
    }
    public int GrabIncome()
    {
        return Income = FarmLevel * 100; //500 +
    }
    public void Set()
    {
        //Debug.LogError(name);
        UnitList.Clear();
        UnitDataList.Clear();
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
