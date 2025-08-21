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
}
