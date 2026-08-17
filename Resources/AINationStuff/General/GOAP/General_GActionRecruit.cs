using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "GeneralAI/Recruit")]
public class General_GActionRecruit : General_GAction
{
    public int countsmade = 0;
    public int countsneeded = 10;

    public override bool IsAchievable() 
    {
        if(generalBrainy.GrabNation().Manpower < 5)
        {
            return false;
        }
        countsmade = 0;
        running = true;
        return true;
    }
    public override float GrabCost()
    {
        return 0f;
    }
    public override bool Execute()
    {
        //Debug.Log(countsmade);
        if(running)
        {
            if(countsmade >= countsneeded)
            {
                running = false;
                generalBrainy.GrabNation().ReinforceArmy(generalBrainy.army);
                //Debug.LogError("Hired a Dude");
                return true;
            }
        }
        countsmade++;
        return false;
    }
}
