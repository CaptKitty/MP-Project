using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class UnitBrain : GAgent
{
    public CritterHolder critter;
    public GameObject TargetEnemy;
    public List<Unit_GAction> actionss = new List<Unit_GAction>();
    public Queue<GAction> actionQueues;
    public void Startie()
    {
        SubGoal s1 = new SubGoal("DealDamage", 0, false);
        goals.Add(s1, 2);

        SubGoal s2 = new SubGoal("MoveToEnemy", 0, false);
        goals.Add(s2, 1);

        var acts = Resources.LoadAll<Unit_GAction>("AIUnitStuff/Actions/");
        foreach (Unit_GAction a in acts)
        {
            Unit_GAction b = (Unit_GAction)Instantiate(a);
            b.unitBrainy = this;
            b.critter = critter;
            actionss.Add(b);
        }
    }
    public void Think()
    {
        if (goals.Count == 0)
        {
            //Debug.LogError("No Goals");
            return;
        }
        LaterUpdate();
    }
    public void GrabTarget()
    {
        List<GameObject> enemylists = new List<GameObject>();
        foreach (var item in BattleManager1.Instance.enemylist)
        {
            if(item == null)
            {
                continue;
            }
            if(item.GetComponent<CritterHolder>().IsthisAI != critter.IsthisAI)
            {
                enemylists.Add(item);
            }
        }
        if(enemylists.Count > 0)
        {
            TargetEnemy = enemylists[0];
            foreach (var item in enemylists)
            {
                var heading  = item.transform.position - critter.gameObject.transform.position;
                var distance = heading.magnitude;

                var heading2  = TargetEnemy.transform.position - critter.gameObject.transform.position;
                var distance2 = heading2.magnitude;
                
                if(distance < distance2)
                {
                    TargetEnemy = item;
                }
            }
        }
    }
    public void LaterUpdate()
    {
        if (TargetEnemy == null || !TargetEnemy.activeInHierarchy ||
            TargetEnemy.GetComponent<CritterHolder>() == null || !TargetEnemy.GetComponent<CritterHolder>().IsThisAlive)
        {
            ResetPlan();
            GrabTarget();
        }
        if (currentAction != null && currentAction.running)
        {

            currentAction.Execute();
            return;
        }
        // Check we have a planner and an actionQueues
        if (planner == null || actionQueues == null)
        {

            // If planner is null then create a new one
            planner = new GPlanner();

            // Sort the goals in descending order and store them in sortedGoals
            var sortedGoals = from entry in goals orderby entry.Value descending select entry;

            foreach (var res in goals)
            {
                foreach (var item in res.Key.sGoals)
                {
                    //debug.log(item.Key + " " + res.Value);
                }
            }
            foreach (var res in actionss)
            {
                string varry = res.actionName + " " + res.preconditions.Count + res.preConditions.Length + " + " + res.effects.Count + res.afterEffects.Length;
                foreach (var item in res.preconditions)
                {
                    varry += item.Key;
                }
                foreach (var item in res.effects)
                {
                    varry += item.Key;
                }
                //debug.log(varry);
            }

            //look through each goal to find one that has an achievable plan
            foreach (KeyValuePair<SubGoal, int> sg in sortedGoals)
            {
                var d = new List<GAction>();
                foreach (var item in actionss)
                {
                    d.Add((Unit_GAction)item);
                }
                actionQueues = planner.plan(d, sg.Key.sGoals, beliefs);
                // If actionQueues is not = null then we must have a plan
                if (actionQueues != null)
                {

                    // Set the current goal
                    currentGoal = sg.Key;
                    break;
                }
            }
            GrabTarget();
        }
        // Have we an actionQueues
        if (actionQueues != null && actionQueues.Count == 0)
        {

            // Check if currentGoal is removable
            if (currentGoal.remove)
            {

                // Remove it
                goals.Remove(currentGoal);
            }
            // Set planner = null so it will trigger a new one
            planner = null;
        }
        // Do we still have actions
        if (actionQueues != null && actionQueues.Count > 0)
        {
            // Remove the top action of the queue and put it in currentAction
            currentAction = actionQueues.Dequeue();

            if (currentAction.PrePerform())
            {

                // Get our current object
                if (currentAction.target == null && currentAction.targetTag != "")
                {

                    currentAction.target = GameObject.FindWithTag(currentAction.targetTag);
                }
                currentAction.Execute();

                if (currentAction.target != null)
                {

                    // Activate the current action
                    currentAction.running = true;
                    // Pass Unities AI the destination for the agent
                    //currentAction.agent.SetDestination(currentAction.target.transform.position);
                }
            }
            else
            {

                // Force a new plan
                actionQueues = null;
            }
        }
    }
    public void ResetPlan()
    {
        if (currentAction != null) currentAction.running = false;
        currentAction = null;
        actionQueues = null;
        planner = null;
    }
}
