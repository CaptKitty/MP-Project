using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "BaseActions/WeighBuildingSlot")]
public class WeighBuildingSlot : GAction {
    
    public Buildings building;

    public override float GrabCost()
    {
        var b = new List<Province>();
        foreach (var item in Owners.Instance.provincelist)
        {
            if(item.nation.name == Owners.Instance.ActiveBrain.nation)
            {
                b.Add(item);
            }
        }
        Province c = new Province();
        int peasantcount = 0;
        foreach (var item in b)
        {
            int a = 0;
            foreach (var items in item.cultures)
            {
                if(items.name.Contains("Peasant"))
                {
                    a++;
                }
            }
            if(a > peasantcount)
            {
                c = item;
            }
        }
        return (float)(-peasantcount*50);
    }
    public override bool Execute(NationalBrain brainy)
    {
        var b = new List<Province>();
        foreach (var item in Owners.Instance.provincelist)
        {
            if(item.nation.name == brainy.nation)
            {
                b.Add(item);
            }
        }
        Province c = new Province();
        int peasantcount = 0;
        foreach (var item in b)
        {
            int a = 0;
            foreach (var items in item.cultures)
            {
                if(items.name.Contains("Peasant"))
                {
                    a++;
                }
            }
            if(a > peasantcount)
            {
                c = item;
            }
        }
        brainy.province = c.name;
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
