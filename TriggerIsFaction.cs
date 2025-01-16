using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Trigger/IsFaction")]
public class TriggerIsFaction : BaseTrigger
{
    public string IsFaction = "";
    public override bool CanTrigger()
    {
        if(Owners.Instance.CallPlayer().name == IsFaction)
        {
            return true;
        }
        return false;
    }
}
