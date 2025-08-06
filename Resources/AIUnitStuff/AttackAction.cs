using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BaseUnitActions/Attack")]
public class AttackAction : Unit_GAction
{

    public override bool IsAchievable()
    {
        return true;
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
            if(critter.EquippedWeapon.modifier != null)
            {
                var moddy = Instantiate(critter.EquippedWeapon.modifier);
                moddy.SetTimer();
                unitBrainy.TargetEnemy.GetComponent<CritterHolder>().modifierlist.Add(moddy);
                foreach (var items in unitBrainy.TargetEnemy.GetComponent<CritterHolder>().modifierlist)
                {
                    items.potato = unitBrainy.TargetEnemy;
                    items.DestroyAura();
                    items.LoadAura();
                    critter.onDeath += items.DestroyAura;
                }
            }
            critter.NextAvailableAttack = Time.time + critter.GrabAttackTime();
            unitBrainy.TargetEnemy.GetComponent<CritterHolder>().ReducePopulation(critter.GrabAttack());
            RpcTest.Serverchecker.ExecuteAnimation(critter, "Attack");
        }
        return false;
    }
}