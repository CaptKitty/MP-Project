using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Trigger/TriggerNo")]
public class TriggerNo : BaseTrigger
{
    public override bool CanTrigger()
    {
        return false;
    }
}
