using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "NationalAI/base_order_attack")]
public class Nation_GActionOrderAttack : Nation_GAction
{
    public FieldArmyHolder army;
    public override bool IsAchievable() 
    {
        army = null;
        Nation nation = nationalbrainy.GrabNation();
        if(nation.armies.Count == 0 || !nationalbrainy.priorityList.Exists(item => item.province != null && item.province.nation != nation))
        {
            return false;
        }
        foreach(var a in nation.armies)
        {
            if(a == null || a.IsPlayer || a.flaglist.Contains("Battle") || a.fieldArmy == null ||
                !nation.IsArmyCombatReady(a) || a.fieldArmy.GrabQueuedArmySize() > 0)
            {
                continue;
            }
            if(a.IsArmyAvailable() == true)
            {
                army = a;
                return true;
            }
        }
        //Debug.Log("Not able to Attack");
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
            ////Debug.LogError("Attack Costs " + army.fieldArmy.GrabArmySize());
        }
        return 10f;//army.fieldArmy.GrabArmySize();
    }
    public override bool Execute()
    {
        //Debug.LogError(nationalbrainy.nation + " Orders " + army.gameObject.name + " to Conquer");
        // if(running)
        // {
        //     if(Time.time > Timer)
        //     {
        //         running = false;
        //     }
        //     return true;
        // }
        if(army == null)
        {
            return false;
        }
        Nation nation = nationalbrainy.GrabNation();
        List<Priority> hostile = nationalbrainy.priorityList.FindAll(item =>
            item != null && item.province != null && item.province.nation != nation);
        if (hostile.Count == 0) { running = false; return false; }

        // Prefer a connected frontier conquest. This prevents armies crossing the entire map
        // while also ensuring a friendly/current province can never become an attack target.
        List<Priority> frontier = hostile.FindAll(item =>
        {
            List<Province> adjacent = item.province.GrabAdjacents();
            return adjacent != null && adjacent.Exists(province => province != null && province.nation == nation);
        });
        List<Priority> candidates = frontier.Count > 0 ? frontier : hostile;
        Priority b = null;
        float bestScore = float.NegativeInfinity;
        foreach(Priority prio in candidates)
        {
            if(army.TargetProvince == prio.province)
            {
                continue;
            }
            float score = prio.value - army.GrabDistanceToProvince(prio.province) / 5f + AggroNumber(prio.province);
            int enemyStrength = nation.GetHostileFieldArmyStrengthNear(prio.province);
            score -= Mathf.Max(0, enemyStrength - army.fieldArmy.GrabArmySize()) * 10f;
            if (b == null || score > bestScore || Mathf.Approximately(score, bestScore) &&
                string.CompareOrdinal(prio.province.name, b.province.name) < 0)
            {
                b = prio;
                bestScore = score;
            }
        }
        if (b == null) { running = false; return false; }
        army.SetTarget(b.province);
        army.TargetProvince = b.province;
        army.generalbrain.NewGoal("MoveArmy");

        Timer = Time.time + 0.1f;
        running = false;
        
        return true;
    }
}
