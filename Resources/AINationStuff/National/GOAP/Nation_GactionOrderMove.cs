using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "NationalAI/base_order_move")]
public class Nation_GactionOrderMove : Nation_GAction
{
    public FieldArmyHolder army;
    public override bool IsAchievable() 
    {
        if(nationalbrainy.GrabNation().armies.Count == 0)
        {
            return false;
        }
        foreach(var a in nationalbrainy.GrabNation().armies)
        {
            if(a.IsPlayer)
            {
                continue;
            }
            if(a.IsTargetNull() == true)
            {
                army = a;
                return true;
            }
        }
        return false;
    }
    public float AggroNumber(Province province)
    {
        if(province.nation == nationalbrainy.GrabNation())
        {
            return 0f;
        }
        return 30f;
    }
    public override float GrabCost()
    {
        if(nationalbrainy.name.Contains("Rome"))
        {
            //Debug.LogError("Attack Costs " + army.fieldArmy.GrabArmySize());
        }
        return army.fieldArmy.GrabArmySize();
    }
    public override bool Execute()
    {
        if(running)
        {
            if(Time.time > Timer)
            {
                running = false;
            }
            return true;
        }
        if(army == null)
        {
            return false;
        }
        Priority b = nationalbrainy.priorityList[0];
        float distance = army.GrabDistanceToProvince(b.province);
        foreach(Priority prio in nationalbrainy.priorityList)
        {
            if(army.TargetProvince == prio.province)
            {
                continue;
            }
            if((prio.value - (army.GrabDistanceToProvince(prio.province)/5) + AggroNumber(prio.province)) > (b.value + Random.Range(-5,6) - (distance/5) + AggroNumber(b.province)))
            {
                if(nationalbrainy.name.Contains("Rome"))
                {
                    // Debug.LogError(prio.value - (army.GrabDistanceToProvince(prio.province)/10) + AggroNumber(prio.province));
                    // Debug.LogError(b.value + Random.Range(-5,6) - (distance/10) + AggroNumber(b.province));
                }
                b = prio;
                distance = army.GrabDistanceToProvince(b.province);
                // if(nationalbrainy.name.Contains("Rome"))
                // {
                //     Debug.LogError("Rome Wakes and sees: " + distance);
                // }
            }
        }
        
        army.SetTarget(b.province);
        army.TargetProvince = b.province;
        Timer = Time.time + 0.1f;
        running = true;
        
        if(nationalbrainy.name.Contains("Rome"))
        {
            //Debug.LogError("Rome Wakes");
        }
        
        return true;
    }
}
