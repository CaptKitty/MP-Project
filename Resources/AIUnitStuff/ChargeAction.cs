using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BaseUnitActions/Charge")]
public class ChargeAction : Unit_GAction
{
    public override bool IsAchievable()
    {
        if (critter.flaglist.Count >= 1 && critter.flaglist.Find(x => x == "Charger") != null)
        {
            if (critter.modifierlist.Find(x => x.name == "Charged") == null)
            {
                return true;
            }
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
            if (critter.RangedWeapon != null && critter.RangedWeapon.modifier != null)
            {
                var moddy = Instantiate(critter.RangedWeapon.modifier);
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
            unitBrainy.TargetEnemy.GetComponent<CritterHolder>().LoseHealth(critter.GrabAttack() * 2, critter.RangedWeapon.attacktype);

            var modifier = new Modifier();
            modifier.name = "Charged";
            modifier.duration = 20;
            modifier.SetTimer();
            critter.modifierlist.Add(modifier);
            
            RpcTest.Serverchecker.ExecuteAnimation(critter, "Attack");
        }
        return false;
    }
}