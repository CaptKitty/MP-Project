using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Event/Trigger/Faction")]
public class FactionTrigger : BaseTrigger
{
    public Faction faction;

    public override bool CanTrigger()
    {
        if (FieldArmyHolder.PlayerFieldArmy.fieldArmy.nation.faction.name == faction.name)
        {
            return true;
        }
        return false;
    }
}
