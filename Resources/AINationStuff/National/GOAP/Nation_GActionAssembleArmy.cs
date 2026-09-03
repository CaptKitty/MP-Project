using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "NationalAI/base_order_assemble")]
public class Nation_GActionAssembleArmy : Nation_GAction
{
    public override bool IsAchievable() 
    {
        Nation nation = nationalbrainy.GrabNation();
        nation.armies.RemoveAll(item => item == null);
        int desiredArmyLimit = nation.DesiredAIArmyLimit();
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
        if (nation.Gold < CampaignEconomy.ArmyCreationCost) return false;
        // Army containers are manpower-free; manpower is spent when units are recruited.
        if(nationalbrainy.ArmySpawnCooldown <= Owners.Instance.turncounter)
        {
            return true;
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
        
        Nation nation = nationalbrainy.GrabNation();
        if (!nation.SpawnArmy()) return false;

        nationalbrainy.ArmySpawnCooldown = Owners.Instance.turncounter + nationalbrainy.SetArmySpawnCooldown;
        if(nationalbrainy.GrabNation().faction.HasFlag("Decentralized"))
        {
            nationalbrainy.ArmySpawnCooldown += nationalbrainy.SetArmySpawnCooldown;
        }
        
        return true;
    }
}
