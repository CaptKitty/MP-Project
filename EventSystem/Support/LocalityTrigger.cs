using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Trigger/Locality")]
public class LocalityTrigger : BaseTrigger
{
    public Faction localFaction;

    public override bool CanTrigger()
    {
        if (FieldArmyHolder.PlayerFieldArmy.GrabFieldArmyProvince() == null)
        {
            return false;
        }
        if (FieldArmyHolder.PlayerFieldArmy.GrabFieldArmyProvince().nation.faction.name == localFaction.name)
        {
            return true;
        }
        return false;
    }

    public override bool CanTrigger(EventContext context)
    {
        Province target = context != null ? context.province : null;
        if (target == null && context != null && context.ResolveArmy() != null)
            target = context.ResolveArmy().GrabFieldArmyProvince();
        return target != null && target.nation != null && target.nation.faction != null && localFaction != null &&
            target.nation.faction.name == localFaction.name;
    }
}
