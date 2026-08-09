using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "GeneralAI/Move")]
public class General_GActionMove : General_GAction
{
    public override bool IsAchievable() 
    {
        running = true;
        return true;
    }
    public override float GrabCost()
    {
        return 0f;
    }
    public override bool Execute()
    {
        //Debug.Log("Walking");
        if(generalBrainy.army.Move())
        {
            running = false;
            //Debug.LogError("Arrived!");
            return true;
        }
        return false;
    }
}
