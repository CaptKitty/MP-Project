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
        if (critter.RangedWeapon != null && critter.RangedWeapon.Throwable != null)
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
        if (critter.formation != null
            ? critter.formation.CanEngageTarget(unitBrainy.TargetEnemy.GetComponent<CritterHolder>(), true)
            : distance < critter.GrabCombatDistance())
        {
            return true;
        }
        return false;
    }
    public override bool Execute()
    {
        CritterHolder target = unitBrainy.TargetEnemy != null ? unitBrainy.TargetEnemy.GetComponent<CritterHolder>() : null;
        if (target == null || !target.IsThisAlive)
        {
            unitBrainy.ResetPlan();
            return true;
        }
        if (critter.formation != null && !critter.formation.CanEngageTarget(target, true))
        {
            unitBrainy.ResetPlan();
            return true;
        }
        var heading = unitBrainy.TargetEnemy.transform.position - critter.gameObject.transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;

        if (critter.NextAvailableAttack < Time.time)
        {
            if (running && critter.formation == null)
            {
                running = false;
                return true;
            }
            running = true;

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
            int participants = critter.formation != null ? critter.formation.CountEligibleAttackers(target, true) : 1;
            if (participants > 0)
            {
                if (critter.formation != null)
                {
                    critter.formation.ResolveMemberAttacks(target, critter.GrabAttack(), critter.RangedWeapon.attacktype, true);
                    critter.formation.SpawnMemberProjectiles(target);
                    critter.formation.PlayMemberAnimation("Attack", target, true);
                }
                else target.LoseHealthFrom(critter.GrabAttack(), critter.RangedWeapon.attacktype, critter.transform.position);
            }
            RpcTest.Serverchecker.ExecuteAnimation(critter, "Attack");
            if (critter.formation == null) RpcTest.Serverchecker.ExecuteAnimation(critter, "Throw");

        }
        return false;
    }
}
