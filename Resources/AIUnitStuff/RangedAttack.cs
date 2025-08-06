using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BaseUnitActions/Ranged")]
public class RangedAttack : Unit_GAction
{

    public override bool IsAchievable()
    {
        if (critter.unittype == UnitTypes.Ranged || critter.unittype == UnitTypes.LightCavalry)
        {
            return true;
        }
        return false;
    }
    public override bool PrePerform()
    {
        var heading = unitBrainy.TargetEnemy.transform.position - critter.gameObject.transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;
        if (distance < critter.GrabCombatDistance())
        {
            return true;
        }
        return false;
    }
    public override bool Execute()
    {
        var heading = unitBrainy.TargetEnemy.transform.position - critter.gameObject.transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;

        if (critter.NextAvailableAttack < Time.time)
        {
            critter.NextAvailableAttack = Time.time + critter.GrabAttackTime();
            unitBrainy.TargetEnemy.GetComponent<CritterHolder>().ReducePopulation(critter.GrabAttack());
            RpcTest.Serverchecker.ExecuteAnimation(critter, "Attack");
            RpcTest.Serverchecker.ExecuteAnimation(critter, "Throw");
        }
        return false;
    }
}