using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BaseUnitActions/MoveAway")]
public class MoveAwayAction : Unit_GAction
{
    public Vector3Int TargetPosition;

    public override bool IsAchievable()
    {
        if(critter.unittype == UnitTypes.Ranged || critter.unittype == UnitTypes.LightCavalry)
        {
            return true;
        }
        // var heading = TargetPosition - critter.gameObject.transform.position;
        // var distance = heading.magnitude;
        // var direction = heading / distance;

        // if (distance < (critter.GrabCombatDistance()/2))
        // {
        //     return true;
        // }

        return false;
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
        TargetPosition = GrabNewspot();
        var heading = TargetPosition - critter.gameObject.transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;

        //Direction
        if (direction.x > -0.5)
        {
            critter.gameObject.transform.LookAt(new Vector3(critter.gameObject.transform.position.x + 1, critter.gameObject.transform.position.y, 360));//, new Vector3(0,0,0));
        }
        else
        {
            if (direction.x < 0.5)
            {
                critter.gameObject.transform.LookAt(new Vector3(critter.gameObject.transform.position.x - 1, critter.gameObject.transform.position.y, -360));//, new Vector3(0,0,0));
            }
        }

        //Movement
        if(distance < critter.GrabCombatDistance())
        {
            if(distance < critter.GrabCombatDistance()/2)
            {
                critter.gameObject.transform.position += new Vector3(-direction.x, -direction.y,-direction.z) * Time.deltaTime * (float)critter.GrabSpeed();
            }
        }
        else
        {
            critter.gameObject.transform.position += direction * Time.deltaTime * (float)critter.GrabSpeed();
        }


        //EndCondition
        if (distance < critter.GrabCombatDistance()/2)
        {
            running = true;
            return true;
        }

        running = false;

        return true;
    }
}