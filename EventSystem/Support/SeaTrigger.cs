using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Trigger/Sea")]
public class SeaTrigger : BaseTrigger
{
    public bool IsThisOnTheSea = false;
    public override bool CanTrigger()
    {
        if (FieldArmyHolder.PlayerFieldArmy.GrabFieldArmyProvince() == null)
        {
            return IsThisOnTheSea;
        }
        return !IsThisOnTheSea;
    }

    public override bool CanTrigger(EventContext context)
    {
        FieldArmyHolder target = context != null ? context.ResolveArmy() : null;
        if (target == null) return false;
        return (target.GrabFieldArmyProvince() == null) == IsThisOnTheSea;
    }
}
