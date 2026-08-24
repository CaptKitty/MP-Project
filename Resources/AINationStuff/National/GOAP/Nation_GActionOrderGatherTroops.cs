using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "NationalAI/base_order_gather")]
public class Nation_GActionOrderGatherTroops : Nation_GAction
{
    public FieldArmyHolder army;
    public override bool IsAchievable() 
    {
        // Action ScriptableObjects are reused between plans. Never allow a previously chosen
        // army to make this higher-priority recruitment goal appear achievable forever.
        army = null;
        if(nationalbrainy.GrabNation().armies.Count == 0)
        {
            return false;
        }
        if(nationalbrainy.GrabNation().Manpower < 5)
        {
            return false;
        }
        float weakestStrength = 2f;
        foreach(var a in nationalbrainy.GrabNation().armies)
        {
            if(a.IsPlayer)
            {
                continue;
            }
            //if(a.CanArmyAct() == true)
            if(nationalbrainy.GrabNation().IsArmyCombatReady(a, true))
            {
                continue;
            }
            if(a.IsArmyAvailable() == true)
            {
                float strength = a.fieldArmy.MaxArmySize <= 0 ? 1f :
                    (float)a.fieldArmy.GrabArmySize() / a.fieldArmy.MaxArmySize;
                if (strength < weakestStrength)
                {
                    weakestStrength = strength;
                    army = a;
                }
            }
        }
        return army != null;
    }
    public float AggroNumber(Province province)
    {
        if(province.nation == nationalbrainy.GrabNation())
        {
            return 30f;
        }
        return 0f;
    }
    public override float GrabCost()
    {
        if(nationalbrainy.name.Contains("Rome"))
        {
            //Debug.LogError("Gathering Costs " + (10-army.fieldArmy.GrabArmySize()));
        }
        return army.fieldArmy.GrabArmySize();
    }
    public override bool Execute()
    {
        //Debug.LogError(nationalbrainy.nation + " Orders the " + army.gameObject.name + " to recruit");
        army.generalbrain.NewGoal("RecruitTroops");
        army.generalbrain.Think();
        return true;
        // if(running)
        // {
        //     if(Time.time > Timer)
        //     {
        //         running = false;
        //     }
        //     return true;
        // }
        // if(army == null)
        // {
        //     return false;
        // }
        // Priority b = nationalbrainy.priorityList[0];
        // float distance = army.GrabDistanceToProvince(b.province);
        // foreach(Priority prio in nationalbrainy.priorityList)
        // {
        //     if(army.TargetProvince == prio.province)
        //     {
        //         continue;
        //     }
        //     if((prio.value - (army.GrabDistanceToProvince(prio.province)/5) + AggroNumber(prio.province)) > (b.value + Random.Range(-5,6) - (distance/5) + AggroNumber(b.province)))
        //     {
        //         b = prio;
        //         distance = army.GrabDistanceToProvince(b.province);
        //         // if(nationalbrainy.name.Contains("Rome"))
        //         // {
        //         //     Debug.LogError("Rome Wakes and sees: " + distance);
        //         // }
        //     }
        // }
        
        // army.SetTarget(b.province);
        // army.TargetProvince = b.province;
        // Timer = Time.time + 0.1f;
        // running = true;

        // if(nationalbrainy.name.Contains("Rome"))
        // {
        //     //Debug.LogError("Rome Chills");
        // }

        // return true;
    }
}
