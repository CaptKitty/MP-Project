using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[System.Serializable]
[CreateAssetMenu(menuName = "BaseActions/MakeWar")]
public class MakeWar : GAction {
    public override float GrabCost()
    {
        return (Owners.Instance.ActiveBrain.WarThirst - 25);
    }
    public override bool Execute(NationalBrain brainy)
    {
        Nation a = Owners.Instance.CallNation(brainy.nation);//nationlist.Find(x => x.name == Owners.Instance.ActiveBrain.nation);
        var nationlisty = new List<Nation>();
        foreach (var item in Owners.Instance.nationlist)
        {
            if(item == a)
            {
                continue;
            }
            if(item.IsAlive)
            {
                nationlisty.Add(item);
            }
        }

        var OriginProvince = new Province();
        foreach (var item in Owners.Instance.statelist)
        {
            if(item.nation.name == brainy.nation)
            {
                OriginProvince = item.Capitol;
            }
        }

        //Nation b = nationlisty[Random.Range(0,nationlisty.Count)];
        var SetTargetProvinces = new List<Province>();
        foreach (var itemsss in Owners.Instance.statelist)
        {
            foreach (var items in itemsss.provincelist)
            {
                foreach (var itemss in items.GrabAdjacentProvinces())
                {
                    if(itemss.nation.name == brainy.nation)
                    {
                        SetTargetProvinces.Add(items);
                    }
                }
            }
        }
        if(SetTargetProvinces.Count == 0)
        {
            return false;
        }

        //Debug.LogError(OriginProvince.name);
        var TargetProvince = SetTargetProvinces[0];//SetTargetProvinces[Random.Range(0,SetTargetProvinces.Count)];
        var currentdistance = (OriginProvince.position - TargetProvince.position).magnitude;
        foreach (var item in Owners.Instance.provincelist)
        {
            if(item.nation.name != brainy.nation)
            {
                var distance = (item.position - OriginProvince.position).magnitude;
                if(distance < currentdistance)
                {
                    TargetProvince = item;
                    currentdistance = distance;
                    //Debug.LogError(distance + " " + OriginProvince.name);
                }
            }
        }
        

        Nation b = TargetProvince.nation;

        if(a == b)
        {
            return false;
        }

        //Debug.LogError(a.name + " " + b.name);
        a.SetDiplomaticStatus(b.name, "war");
        
        SpawnDiplomaticEffect c = new SpawnDiplomaticEffect();
        c.nation = b.name;
        c.othercountry = a.name;
        c.newstatus = "war";
        c.Execute();

        //Debug.LogError(a.name + " Declares war on " + b.name);

        brainy.DiplomacyCooldown = 25;
        return true;
    }
    public override bool IsAchievable() {

        foreach (var item in Owners.Instance.nationlist)
        {
            if(item.GrabDiplomaticStatus(Owners.Instance.ActiveBrain.nation) == "war")
            {
                return false;
            }
        }
        if(Owners.Instance.ActiveBrain.DiplomacyCooldown > 0)
        {
            return false;
        }
        return true;
    }
    public override bool SetTarget(Nation nation = null)
    {
        return false;
    }

    public override bool PrePerform() {

        // // Get a free cubicle
        // target = inventory.FindItemWithTag("Cubicle");
        // // Check that we did indeed get a cubicle
        // if (target == null)
        //     // No cubicle so return false
        //     return false;
        // // All good
        return true;
    }

    public override bool PostPerform() {

        // // Add a new state "TreatingPatient"
        // GWorld.Instance.GetWorld().ModifyState("TreatingPatient", 1);
        // // Give back the cubicle
        // GWorld.Instance.AddCubicle(target);
        // // Remove the cubicle from the list
        // inventory.RemoveItem(target);
        // // Give the cubicle back to the world
        // GWorld.Instance.GetWorld().ModifyState("FreeCubicle", 1);
        
        return true;
    }
}
