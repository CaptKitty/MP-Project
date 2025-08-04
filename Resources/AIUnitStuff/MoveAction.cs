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
    public override bool PrePerform()
    {
        Debug.LogError("PrePerform");
        return true;
    }
    public override bool Execute()
    {
        TargetPosition = new Vector3Int((int)critter.gameObject.transform.position.x - 1, (int)critter.gameObject.transform.position.y, 0);
        Debug.LogError("executed");
        var heading = TargetPosition - critter.gameObject.transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;

        if (direction.x > 0)
        {
            critter.gameObject.transform.LookAt(new Vector3(critter.gameObject.transform.position.x + 1, critter.gameObject.transform.position.y, 360));//, new Vector3(0,0,0));
        }
        else
        {
            critter.gameObject.transform.LookAt(new Vector3(critter.gameObject.transform.position.x - 1, critter.gameObject.transform.position.y, -360));//, new Vector3(0,0,0));
        }
        critter.gameObject.transform.position += direction * Time.deltaTime * (float)critter.GrabSpeed();

        if (distance < 1)
        {
            return true;
        }

        running = true;

        return true;
    }
}