using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[System.Serializable]
[CreateAssetMenu(menuName = "BaseActions/IsAtWar")]
public class IsAtWar : GAction {
    public override float GrabCost()
    {
        return cost;
    }
    public override bool IsAchievable() {
        foreach (var item in Owners.Instance.nationlist)
        {
            if(item.GrabDiplomaticStatus(Owners.Instance.ActiveBrain.nation) == "war")
            {
                return true;
            }
        }
        return false;
    }
    public override bool Execute(NationalBrain brainy)
    {
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
