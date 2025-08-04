using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BaseUnitActions/Idle")]
public class IdleAction : Unit_GAction
{
    public override bool IsAchievable()
    {
        return true;
    }
    public override bool Execute()
    {
        return true;
    }
    
}