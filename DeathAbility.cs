using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Ability/DeathAbility")]
public class DeathAbility : Ability
{
    public string newspawneename = "grass";

    public override Ability Init()
    {
        var potato = new DeathAbility();
        potato.Description = Description;
        potato.newspawneename = newspawneename;
        return potato;
    }
    public override void OnDeath(CritterHolder critter)
    {
        GeneralManager.Instance.Spawn(critter.spot, name:newspawneename);
    }
}
