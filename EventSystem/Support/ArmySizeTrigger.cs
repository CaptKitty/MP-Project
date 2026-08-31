using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Trigger/ArmySize")]
public class ArmySizeTrigger : BaseTrigger
{
    public int armysize;
    public bool more;
    public override bool CanTrigger()
    {
        if (more && FieldArmyHolder.PlayerFieldArmy.fieldArmy.GrabArmySize() >= armysize)
        {
            return true;
        }
        if (!more && !(FieldArmyHolder.PlayerFieldArmy.fieldArmy.GrabArmySize() >= armysize))
        {
            return true;
        }
        return false;
    }

    public override bool CanTrigger(EventContext context)
    {
        FieldArmyHolder target = context != null ? context.ResolveArmy() : null;
        if (target == null || target.fieldArmy == null) return false;
        int size = target.fieldArmy.GrabArmySize();
        return more ? size >= armysize : size < armysize;
    }
}
