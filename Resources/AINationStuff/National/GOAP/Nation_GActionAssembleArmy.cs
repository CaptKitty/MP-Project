using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "NationalAI/base_order_assemble")]
public class Nation_GActionAssembleArmy : Nation_GAction
{
    public int armycost = 50;
    public override bool IsAchievable() 
    {
        if(nationalbrainy.GrabNation().Manpower > armycost)
        {
            if(nationalbrainy.ArmySpawnCooldown < Owners.Instance.timer)
            {
                return true;
            }   
        }
        return false;
    }
    public override float GrabCost()
    {
        return 0f;
    }
    public override bool Execute()
    {
        Debug.LogError(nationalbrainy.nation + " Orders an army to assemble");
        
        nationalbrainy.GrabNation().SpawnArmy();
        nationalbrainy.GrabNation().Manpower -= armycost;

        nationalbrainy.ArmySpawnCooldown = (int)Owners.Instance.timer + nationalbrainy.SetArmySpawnCooldown;
        if(nationalbrainy.GrabNation().faction.HasFlag("Decentralized"))
        {
            nationalbrainy.ArmySpawnCooldown += nationalbrainy.SetArmySpawnCooldown;
        }
        
        return true;
    }
}
