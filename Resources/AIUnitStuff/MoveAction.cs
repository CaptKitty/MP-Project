using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BaseUnitActions/Move")]
public class MoveAction : Unit_GAction
{
    public Vector3Int TargetPosition;

    public override bool IsAchievable()
    {
        return true;
    }
    // public override bool PrePerform()
    // {
    //     Debug.LogError("PrePerform");
    //     return true;
    // }
    public Vector3Int GrabNewspot()
    {
        return new Vector3Int((int)unitBrainy.TargetEnemy.transform.position.x, (int)unitBrainy.TargetEnemy.transform.position.y, 0);

        //return Vector3Int((int)critter.gameObject.transform.position.x - 1, (int)critter.gameObject.transform.position.y, 0);
    }
    public override bool Execute()
    {
        //TargetPosition = new Vector3Int((int)critter.gameObject.transform.position.x - 1, (int)critter.gameObject.transform.position.y, 0);
        TargetPosition = GrabNewspot();
        //Debug.LogError("executed");
        var heading = TargetPosition - critter.gameObject.transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;

        if (critter.formation != null) critter.formation.SetFacing(direction);
        critter.gameObject.transform.position += direction * Time.deltaTime * (float)critter.GrabSpeed();

        bool reachedEngagement = critter.formation != null && unitBrainy.TargetEnemy != null
            ? critter.formation.CanEngageTarget(unitBrainy.TargetEnemy.GetComponent<CritterHolder>(),
                critter.RangedWeapon != null && critter.RangedWeapon.Throwable != null)
            : distance < critter.GrabCombatDistance();
        if (reachedEngagement)
        {
            running = false;
            return true;
        }

        running = true;

        return true;
    }
}
