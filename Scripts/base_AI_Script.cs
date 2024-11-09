using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
//[CreateAssetMenu(menuName = "AIScript/Base")]
public class base_AI_Script : ScriptableObject
{
    public virtual base_AI_Script Init()
    {
        var potato = new base_AI_Script();
        return potato;
    }
    public virtual void Execute(CritterHolder critter)
    {

    }
    public virtual void Direction(CritterHolder critter)
    {

    }
    public virtual void FindTarget(CritterHolder critter)
    {

    }
}
