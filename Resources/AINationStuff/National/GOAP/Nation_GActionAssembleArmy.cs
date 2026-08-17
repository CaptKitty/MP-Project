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
        Nation nation = nationalbrainy.GrabNation();
        nation.armies.RemoveAll(item => item == null);
        int ownedProvinces = Owners.Instance.provincelist.FindAll(province => province.nation == nation).Count;
        int desiredArmyLimit = Mathf.Clamp(1 + ownedProvinces / 4, 1, 5);
        // SpawnArmy uses the same cap. If the cap is already reached this action must be
        // unavailable, otherwise the highest-priority recruitment goal starves conquest.
        if (nation.armies.Count >= desiredArmyLimit)
        {
            return false;
        }
        if (nation.armies.Count > 0 && nation.AverageArmyStrength() < 0.65f)
        {
            return false;
        }
        if(nation.Manpower >= armycost)
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
        //Debug.LogError(nationalbrainy.nation + " Orders an army to assemble");
        
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
