using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Trigger/Supply")]
public class SupplyTrigger : BaseTrigger
{
    public int supply;
    public bool more;
    public override bool CanTrigger()
    {
        if (more && FieldArmyHolder.PlayerFieldArmy.fieldArmy.ArmySupply >= supply)
        {
            return true;
        }
        if (!more && !(FieldArmyHolder.PlayerFieldArmy.fieldArmy.ArmySupply >= supply))
        {
            return true;
        }
        return false;
    }

    public override bool CanTrigger(EventContext context)
    {
        FieldArmyHolder target = context != null ? context.ResolveArmy() : null;
        if (target == null || target.fieldArmy == null) return false;
        return more ? target.fieldArmy.ArmySupply >= supply : target.fieldArmy.ArmySupply < supply;
    }
}
