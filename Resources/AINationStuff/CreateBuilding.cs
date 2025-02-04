using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "BaseActions/CreateBuilding")]
public class CreateBuilding : GAction {
    
    public Buildings building;

    public override float GrabCost()
    {
        return building.Cost[0].amount;

        return 0;
    }
    public override bool Execute(NationalBrain brainy)
    {
        if(Owners.Instance.nationlist.Find(x => x.name == brainy.nation).nationalTreasury.Find(x => x.resource.name == "Coin").amount > GrabCost())
        {
            brainy.GrabSelectedProvince().BuildingList.Add(building);
            Owners.Instance.nationlist.Find(x => x.name == brainy.nation).nationalTreasury.Find(x => x.resource.name == "Coin").amount -= building.Cost[0].amount;
            return true;
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
