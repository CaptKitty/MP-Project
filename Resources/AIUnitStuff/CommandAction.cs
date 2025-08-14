using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BaseUnitActions/Command")]
public class CommandAction : Unit_GAction
{
    public Vector3Int TargetPosition;

    public Modifier modifier;
    public int CommandDistance;
    public string CommandType = "Commander";


    public List<GameObject> subjects = new List<GameObject>();
    public List<Vector3> subjectrelation = new List<Vector3>();

    public bool HasExecuted = false;

    public override bool IsAchievable()
    {
        if(critter.flaglist.Count >= 1 && critter.flaglist[0] == CommandType)
        {
            if (HasExecuted == false)
            {
                return true;
            }
        }
        return false;
    }

    public Vector3Int GrabNewspot()
    {
        return new Vector3Int((int)unitBrainy.TargetEnemy.transform.position.x, (int)unitBrainy.TargetEnemy.transform.position.y, 0);

        //return Vector3Int((int)critter.gameObject.transform.position.x - 1, (int)critter.gameObject.transform.position.y, 0);
    }
    public void GrabAllSubjects()
    {
        Debug.LogError("GrabSubjects");
        HasExecuted = true;
        List<GameObject> frenlists = new List<GameObject>();
        foreach (var item in BattleManager1.Instance.enemylist)
        {
            if(item == null)
            {
                continue;
            }
            if(item.name == critter.gameObject.name)
            {
                item.GetComponent<CritterHolder>().modifierlist.Add(modifier);
                foreach (var items in item.GetComponent<CritterHolder>().modifierlist)
                {
                    items.potato = item;
                    items.LoadAura();
                }
                continue;
            }
            if(item.GetComponent<CritterHolder>().IsthisAI == critter.IsthisAI)
            {
                frenlists.Add(item);
            }
        }
        if(frenlists.Count > 0)
        {
            foreach (var item in frenlists)
            {
                var heading  = item.transform.position - critter.gameObject.transform.position;
                var distance = heading.magnitude;
                
                if(Vector3.Distance(item.transform.position, critter.gameObject.transform.position) < CommandDistance)
                {
                    subjects.Add(item);
                    //item.GetComponent<CritterHolder>().AIScript = subjectscript;
                    var moddy = Instantiate(modifier);
                    item.GetComponent<CritterHolder>().modifierlist.Add(moddy);
                    
                    moddy.potato = item;
                    moddy.LoadAura();

                    critter.onDeath += item.GetComponent<CritterHolder>().GrabNewScript;
                    critter.onDeath += moddy.DestroyAura;
                    critter.onDeath += moddy.DestroyThis;

                    subjectrelation.Add(heading);
                }
            }
        }
    }
    public override bool Execute()
    {
        GrabAllSubjects();
        return true;
    }
}