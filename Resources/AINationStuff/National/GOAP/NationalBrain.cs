using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NationalBrain : GAgent {

    public string nation;
    public string province;
    public int WarThirst;

    public List<Nation_GAction> actionss = new List<Nation_GAction>();

    public List<Priority> priorityList = new List<Priority>();

    public int DiplomacyCooldown = 0;
    public int ArmySpawnCooldown = 0;
    public int SetArmySpawnCooldown = 100;

    public void Startie()
    {
        SubGoal s0 = new SubGoal("RecruitArmies", 0, false);
        goals.Add(s0, 6);

        SubGoal s1 = new SubGoal("RecruitTroops", 0, false);
        goals.Add(s1, 5);

        SubGoal s2 = new SubGoal("ConquerProvince", 0, false);
        goals.Add(s2, 4);

        SubGoal s3 = new SubGoal("PrepWar", 0, false);
        goals.Add(s3, 0); 

        SubGoal s4 = new SubGoal("Sleep", 3, false);
        goals.Add(s4, 3);

        var acts = Resources.LoadAll<Nation_GAction>("AINationStuff/National/Actions/");
        //debug.log(acts.Length);
        foreach (Nation_GAction a in acts) 
        {
            // a.Awake();
            var b = Instantiate(a);
            b.nationalbrainy = this;
            actionss.Add(b);
        }
        SetPriorities();
    }
    public Nation GrabNation()
    {
        return Owners.Instance.nationdict[nation];
    }
    public void SetPriorities()
    {
        foreach(Province prov in Owners.Instance.provincelist)
        {
            var a = new Priority(prov,10);
            if(prov.nation.name == nation)
            {
                a.value += 5;
            }
            if(prov.OriginalNation.name == nation)
            {
                a.value -= 10;
            }
            if(nation.Contains("Rome"))
            {
                if(prov.OriginalNation.name.Contains("Carthage"))
                {
                    a.value -= 20;
                }
            }
            priorityList.Add(a);
        }
    }
    public void ReSetPriorities()
    {
        priorityList.Clear();
        foreach(Province prov in Owners.Instance.provincelist)
        {
            var a = new Priority(prov,10);
            if(prov.nation.name == nation)
            {
                a.value += 5;
                if(prov.OriginalNation.name == nation)
                {
                    a.value -= 10;
                }
            }
            else
            {
                if(prov.OriginalNation.name == nation)
                {
                    a.value += 50;
                }
            }
            
            if(nation.Contains("Rome"))
            {
                if(prov.OriginalNation.name.Contains("Carthage"))
                {
                    a.value -= 20;
                }
            }
            priorityList.Add(a);
        }
    }

    public void Think()
    {
        if(GrabNation().faction.HasFlag("Braindead"))
        {
            return;
        }
        if (goals.Count == 0)
        {
            return;
        }
        LaterUpdate();
    }
    public void LaterUpdate() {

        if (currentAction != null && currentAction.running)
        {

            currentAction.Execute();
            return;
        }

        // Check we have a planner and an actionQueue
        if (planner == null || actionQueue == null) {

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
            foreach (KeyValuePair<SubGoal, int> sg in sortedGoals) {

                var d = new List<GAction>();
                foreach (var item in actionss)
                {
                    d.Add((Nation_GAction)item);
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
                goals.Remove(currentGoal);
            }
            // Set planner = null so it will trigger a new one
            planner = null;
        }

        // Do we still have actions
        if (actionQueue != null && actionQueue.Count > 0) {

            // Remove the top action of the queue and put it in currentAction
            currentAction = actionQueue.Dequeue();

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