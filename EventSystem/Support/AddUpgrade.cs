using System.Collections;
using System.Collections.Generic;
using UnityEngine; 
using System.Text.RegularExpressions;

[System.Serializable]
[CreateAssetMenu(fileName = "Effects/AddUpgrade")]
public class AddUpgrade : BaseEffect
{
    public string unitname;
    public int amount;
    public Faction faction;
    public override void Execute()
    {
        if (faction == null)
        {
            faction = SessionManager.Instance.HostFaction;
        }
        else
        {
            faction = Owners.Instance.nationlist.Find(x => x.faction.name == faction.name).faction;
        }
        var unitSaveData = new UnitSaveData();
        if (unitname == "")
        {
            unitSaveData = faction.UnitDataList[Random.Range(0, faction.UnitDataList.Count)];
        }
        else
        {
            unitSaveData = faction.UnitDataList.Find(x => x.name == unitname);
        }

        var mod = unitSaveData.GrabModule();
        if (mod.modifier != null)
        {
            unitSaveData.modifierlist.Add(mod.modifier);
        }
        if (mod.NewRangedWeapon != null)
        {
            unitSaveData.RangedWeapon = mod.NewRangedWeapon;
        }
        if (mod.NewMeleeWeapon != null)
        {
            unitSaveData.MeleeWeapon = mod.NewMeleeWeapon;
        }
        if (mod.NewArmors != null)
        {
            unitSaveData.Armor = mod.NewArmors;
        }
        if (mod.NewShields != null)
        {
            unitSaveData.Shield = mod.NewShields;
        }
        unitSaveData.upgradeModules.Remove(mod);
    }
    public override void Execute(EventContext context)
    {
        Nation target = context != null ? context.ResolveNation() : null;
        if (target == null || target.faction == null) { Execute(); return; }
        Faction previous = faction;
        faction = target.faction;
        Execute();
        faction = previous;
    }
    public override void GrabRandomNation(string ownernation = "")
    {
        if(ownernation == "")
        {
            nation = Owners.Instance.nationlist[Random.Range(0,Owners.Instance.nationlist.Count)].name;
        }
    }
    public override void GrabRandomTarget(){}
    public override string GrabTooltip()
    {
        string newstring = tooltip;
        newstring = Regex.Replace(newstring, "<province>", province);
        newstring = Regex.Replace(newstring, "<nation>", nation);

        return newstring;
    }
}
