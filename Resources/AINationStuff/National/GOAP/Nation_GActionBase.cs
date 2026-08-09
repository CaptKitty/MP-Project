using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "NationalAI/base_sleep")]
public class Nation_GActionBase : Nation_GAction
{
    public override bool Execute()
    {
        //Debug.LogError("Sleepy");
        return true;
    }
}
