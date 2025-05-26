using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BaseActions/RemoveGoals")]
public class RemoveGoals : GAction
{
    public override bool IsAchievable()
    {
        return true;
    }
    public override bool Execute()
    {
        // brainy.goals.Remove("DeploySoldiers");
        // brainy.goals.Remove("RemoveGoals");
        //Debug.LogError("potato");
        brainy.goals.Clear();
        return true;
    }
    
}