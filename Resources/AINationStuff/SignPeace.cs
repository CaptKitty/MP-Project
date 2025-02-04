using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[System.Serializable]
[CreateAssetMenu(menuName = "BaseActions/SignPeace")]
public class SignPeace : GAction {
    public override float GrabCost()
    {
        return (Owners.Instance.ActiveBrain.WarThirst + 25);
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
        Nation b = nationlisty[Random.Range(0,nationlisty.Count)];
        a.SetDiplomaticStatus(b.name, "peace");
        
        SpawnDiplomaticEffect c = new SpawnDiplomaticEffect();
        c.nation = b.name;
        c.othercountry = a.name;
        c.newstatus = "peace";
        c.Execute();


        Debug.LogError(a.name + " Declares peace on " + b.name);
        Debug.LogError(a.GrabDiplomaticStatus(b.name));
        Debug.LogError(b.GrabDiplomaticStatus(a.name));
        brainy.DiplomacyCooldown = 25;
        return true;
    }
    public override bool IsAchievable() {
        foreach (var item in Owners.Instance.nationlist)
        {
            if(item.GrabDiplomaticStatus(Owners.Instance.ActiveBrain.nation) == "war")
            {
                return true;
            }
        }
        if(Owners.Instance.ActiveBrain.DiplomacyCooldown > 0)
        {
            return false;
        }
        return false;
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
