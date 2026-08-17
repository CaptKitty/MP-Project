using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GeneralBrain : GAgent {

    public string nation;
    public FieldArmyHolder army;

    public List<General_GAction> actionss = new List<General_GAction>();

    public List<Priority> priorityList = new List<Priority>();

    public int DiplomacyCooldown = 0;

    public void Startie()
    {
        // SubGoal s1 = new SubGoal("ConquerProvince", 0, false);
        // goals.Add(s1, 5);

        // SubGoal s2 = new SubGoal("RecruitTroops", 0, false);
        // goals.Add(s2, 5);

        // SubGoal s3 = new SubGoal("PrepWar", 0, false);
        // goals.Add(s3, 0);

        // SubGoal s4 = new SubGoal("Sleep", 3, false);
        // goals.Add(s4, 3);

        var acts = Resources.LoadAll<General_GAction>("AINationStuff/General/Actions/");
        ////Debug.Log(acts.Length);
        foreach (General_GAction a in acts)
        {
            // a.Awake();
            var b = Instantiate(a);
            b.generalBrainy = this;
            actionss.Add(b);
        }
    }
    public void NewGoal(string NewGoal)
    {
        goals.Clear();
        // A new national order supersedes every part of the previous plan. Leaving these
        // references alive can make the second order resume the first order's action queue.
        if (currentAction != null) currentAction.running = false;
        currentAction = null;
        currentGoal = null;
        actionQueue = null;
        planner = null;
        SubGoal s1 = new SubGoal(NewGoal, 0, true);
        goals.Add(s1, 5);
        //Debug.LogError(nation + " Ordered " + army.gameObject.name + " to " + NewGoal);
    }
    public Nation GrabNation()
    {
        return Owners.Instance.nationdict[nation];
    }
    public void Think()
    {
        if (goals.Count == 0)
        {
            return;
        }
        LaterUpdate();
    }
    public void LaterUpdate() {

        try{
        //Debug.LogError(actionQueue.Count + "_" + goals.Count);
        }
        catch{}
        if (currentAction != null && currentAction.running)
        {
            if(currentAction.Execute())
            {
                currentAction = null;
                //Debug.LogError(currentAction);
                

                if (actionQueue != null && actionQueue.Count == 0) {

                    // Check if currentGoal is removable
                    if (currentGoal.remove) {

                        // Remove it
                        //Debug.LogError("Removed Goal");
                        goals.Remove(currentGoal);
                        //Debug.LogError(currentAction);
                    }
                    // Set planner = null so it will trigger a new one
                    planner = null;
                }
                actionQueue = null;
                //Debug.LogError("_vs_" + goals.Count);
            }
            return;
        }
        if(goals.Count == 0)
        {
            //Debug.LogError("Why the fuck would this trigger?");
            return;
        }
        //Debug.LogError(currentAction);

        // Check we have a planner and an actionQueue
        if (planner == null || actionQueue == null) {

            // If planner is null then create a new one
            planner = new GPlanner();

            // Sort the goals in descending order and store them in sortedGoals
            var sortedGoals = from entry in goals orderby entry.Value descending select entry;

            //Debug.LogError(goals.Count);
            foreach (var res in goals)
            {
                foreach (var item in res.Key.sGoals)
                {
                    ////Debug.Log(item.Key + " " + res.Value);
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
                ////Debug.Log(varry);
            }

            //look through each goal to find one that has an achievable plan
            foreach (KeyValuePair<SubGoal, int> sg in sortedGoals) {

                var d = new List<GAction>();
                foreach (var item in actionss)
                {
                    d.Add((General_GAction)item);
                }
                actionQueue = planner.plan(d, sg.Key.sGoals, beliefs);
                // If actionQueue is not = null then we must have a plan
                if (actionQueue != null) {

                    // Set the current goal
                    currentGoal = sg.Key;
                    break;
                }
            }
        }

        // Have we an actionQueue
        if (actionQueue != null && actionQueue.Count == 0) {

            // Check if currentGoal is removable
            if (currentGoal.remove) {

                // Remove it
                //Debug.LogError("Removed Goal");
                goals.Remove(currentGoal);
                //Debug.LogError(currentAction);
            }
            // Set planner = null so it will trigger a new one
            planner = null;
        }

        // Do we still have actions
        if (actionQueue != null && actionQueue.Count > 0) {

            //Debug.Log(actionQueue.Count);
            foreach(var item in actionQueue)
            {
                //Debug.LogError(item.actionName);
            }
            
            // Remove the top action of the queue and put it in currentAction
            currentAction = actionQueue.Dequeue();
            //Debug.LogError(currentAction);

            if (currentAction.PrePerform()) {

                // Get our current object
                if (currentAction.target == null && currentAction.targetTag != "") {

                    currentAction.target = GameObject.FindWithTag(currentAction.targetTag);
                }

                currentAction.Execute();

                if (currentAction.target != null) {

                    // Activate the current action
                    currentAction.running = true;
                    // Pass Unities AI the destination for the agent
                    //currentAction.agent.SetDestination(currentAction.target.transform.position);
                }
            } else {

                // Force a new plan
                actionQueue = null;
            }
        }
    }
}
public class Priority
{
    public Province province;
    public int value;
    public Priority(Province a, int b)
    {
        province = a;
        value = b;
    }
}
