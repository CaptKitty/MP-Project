using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Trigger/NotFlag")]
public class NotFlagTrigger : BaseTrigger
{
    public string flagToTrigger;

    public override bool CanTrigger()
    {
        if (FieldArmyHolder.PlayerFieldArmy.HasFlag(flagToTrigger))
        {
            return false;
        }
        return true;
    }

    public override bool CanTrigger(EventContext context)
    {
        FieldArmyHolder target = context != null ? context.ResolveArmy() : null;
        return target != null ? !target.HasFlag(flagToTrigger) : CanTrigger();
    }
}
