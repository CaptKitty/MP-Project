using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Brain : GAgent {

    public string nation;
    public string province;
    public int WarThirst;

    //character
    public int Initiative;
    public int resources = 500;
    public int i = 0;
    public int j = 0;
    public int k = 0;

    public Vector3Int PrimaryLineSpot = new Vector3Int(3, -8, 0);
    public Vector3Int SecondaryLineSpot = new Vector3Int(6, -11, 0);
    public Vector3Int TertiaryLineSpotA = new Vector3Int(2, -13, 0);
    public Vector3Int TertiaryLineSpotB = new Vector3Int(13, -2, 0);

    public int DiplomacyCooldown = 0;

    public void Startie()//Nation nations) 
    {
        

        // Call base Start method
        //base.Start();


        //nation = nations.name;

        Initiative = Random.Range(1, 10);

        // SubGoal s1 = new SubGoal("WinWar", 0, false);
        // goals.Add(s1, 1);

        // SubGoal s3 = new SubGoal("PrepWar", 0, false); 
        // goals.Add(s3, 0);

        // SubGoal s2 = new SubGoal("GrowEconomy", 1, false);
        // goals.Add(s2, 1);

        // SubGoal s4 = new SubGoal("Sleep", 3, false);
        // goals.Add(s4, 3);

        SubGoal s1 = new SubGoal("DeploySoldiers", 0, false);
        goals.Add(s1, 2);

        SubGoal s2 = new SubGoal("RemoveGoals", 0, false);
        goals.Add(s2, 1);

        var acts = Resources.LoadAll<GAction>("AINationStuff/Actions/");
        //debug.log(acts.Length);
        foreach (GAction a in acts) 
        {
            // a.Awake();
            var b = Instantiate(a);
            b.brainy = this;
            actions.Add(b);
        }
    }
        //WarThirst = Random.Range(-1000,1000);

    //     // Set goal so that it can't be removed so the nurse can repeat this action
    //     SubGoal s1 = new SubGoal("treatPatient", 1, false);
    //     goals.Add(s1, 3);

    //     // Resting goal
    //     SubGoal s2 = new SubGoal("rested", 1, false);
    //     goals.Add(s2, 1);

    //     // Call the GetTired() method for the first time
    //     Invoke("GetTired", Random.Range(10.0f, 20.0f));
    // }

    // void GetTired() {

    //     beliefs.ModifyState("exhausted", 0);
    //     //call the get tired method over and over at random times to make the nurse
    //     //get tired again
    //     Invoke("GetTired", Random.Range(0.0f, 20.0f));
    //}
    public void Think()
    {
        if (goals.Count == 0)
        {
            return;
        }
        //CheckWarThirst();
            LaterUpdate();
    }
    // void CheckWarThirst()
    // {
    //     DiplomacyCooldown--;
    //     foreach (var item in Owners.Instance.nationlist)
    //     {
    //         if(item.GrabDiplomaticStatus(Owners.Instance.ActiveBrain.nation) == "war")
    //         {
    //             WarThirst--;
    //             return;
    //         }
    //     }
    //     WarThirst += 3;
    //     return;
    // }
    public void LaterUpdate() {

        //if there's a current action and it is still running
        // if (currentAction != null && currentAction.running) {

        //     // Find the distance to the target
        //     float distanceToTarget = Vector3.Distance(currentAction.target.transform.position, this.transform.position);
        //     // Check the agent has a goal and has reached that goal
        //     if (currentAction.agent.hasPath && distanceToTarget < 2.0f) { // currentAction.agent.remainingDistance < 1.0f) 

        //         if (!invoked) {

        //             //if the action movement is complete wait
        //             //a certain duration for it to be completed
        //             Invoke("CompleteAction", currentAction.duration);
        //             invoked = true;
        //         }
        //     }
        //     return;
        // }

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
            foreach (var res in actions)
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

                actionQueue = planner.plan(actions, sg.Key.sGoals, beliefs);
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