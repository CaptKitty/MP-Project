using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "BaseActions/Unit/Base")]
public class Unit_GAction : GAction {
    public UnitBrain unitBrainy;
    public CritterHolder critter;

    // // // Constructor
    // // public GAction() {

    // //     // Set up the preconditions and effects
    // //     preconditions = new Dictionary<string, int>();
    // //     effects = new Dictionary<string, int>();
    // // }

    // public void Awake()
    // {

    //     // Get hold of the agents NavMeshAgent
    //     //agent = this.gameObject.GetComponent<NavMeshAgent>();

    //     // Check if there are any preConditions in the Inspector
    //     // and add to the dictionary
    //     if (preConditions != null)
    //     {

    //         foreach (WorldState w in preConditions)
    //         {

    //             // Add each item to our Dictionary
    //             preconditions.Add(w.key, w.value);
    //         }
    //     }

    //     // Check if there are any afterEffects in the Inspector
    //     // and add to the dictionary
    //     if (afterEffects != null)
    //     {

    //         foreach (WorldState w in afterEffects)
    //         {

    //             // Add each item to our Dictionary
    //             effects.Add(w.key, w.value);
    //         }
    //     }
    //     // // Populate our inventory
    //     // inventory = this.GetComponent<GAgent>().inventory;
    //     // // Get our agents beliefs
    //     // beliefs = this.GetComponent<GAgent>().beliefs;
    // }

    // public virtual float GrabCost(){return 0f;}
    // public virtual bool Execute(){return true;}
    // //public virtual bool SetTarget(Nation nation = null){return false;}

    // public virtual bool IsAchievable() {

    //     return true;
    // }

    // //check if the action is achievable given the condition of the
    // //world and trying to match with the actions preconditions
    // public bool IsAhievableGiven(Dictionary<string, int> conditions) {

    //     foreach (KeyValuePair<string, int> p in preconditions) {

    //         if (!conditions.ContainsKey(p.Key)) {

    //             return false;
    //         }
    //     }
    //     return true;
    // }

    // public virtual bool PrePerform(){return true;}
    // public virtual bool PostPerform(){return true;}
}
