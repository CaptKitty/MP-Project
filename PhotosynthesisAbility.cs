using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Ability/PhotosynthesisAbility")]
public class PhotosynthesisAbility : Ability
{
    public int amount = 1;

    public override Ability Init()
    {
        var potato = new PhotosynthesisAbility();
        
        base.Init();

        potato.name = name;
        potato.arrayBool = arrayBool;
        potato.amount = amount;
        return potato;
    }

    public override void Turn(CritterHolder critter)
    {
        critter.ReducePopulation(-amount);
    }
}
