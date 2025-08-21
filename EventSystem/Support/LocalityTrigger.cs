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
}
