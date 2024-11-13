using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "AIScript/Command")]
public class basic_AI_Command_Script : base_AI_Script
{
    GameObject TargetEnemy;
    public bool HasCommanded = false;
    public List<GameObject> subjects = new List<GameObject>();
    public List<Vector3> subjectrelation = new List<Vector3>();
    public base_AI_Script subjectscript;
    public double CommandDistance = 1;
    public Modifier modifier;
    public override base_AI_Script Init()
    {
        var potato = new basic_AI_Command_Script();
        potato.TargetEnemy = TargetEnemy;
        potato.HasCommanded = false;
        potato.subjects = new List<GameObject>();
        potato.subjectrelation = new List<Vector3>();
        potato.subjectscript = subjectscript.Init();
        potato.CommandDistance = CommandDistance;
        potato.modifier = modifier;
        return potato;
    }
    public override void Direction(CritterHolder critter)
    {
        if(TargetEnemy == null || TargetEnemy.active == false)
        {
            FindTarget(critter);
        }
        else
        {
            //Vector3 vectory = new Vector3(1, 0.5f, 0);
            var heading  = TargetEnemy.transform.position - critter.gameObject.transform.position;
            var distance = heading.magnitude;
            var direction = heading / distance;

            if(direction.x > 0)
            {
                critter.gameObject.transform.LookAt( new Vector3(critter.gameObject.transform.position.x+1,critter.gameObject.transform.position.y,360), new Vector3(0,0,0));
            }
            else
            {
                critter.gameObject.transform.LookAt( new Vector3(critter.gameObject.transform.position.x-1,critter.gameObject.transform.position.y,-360), new Vector3(0,0,0));
            }
        }
    }
    public void GrabAllSubjects(CritterHolder critter)
    {
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
                    items.LoadAura(item);
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
                    item.GetComponent<CritterHolder>().AIScript = subjectscript;
                    item.GetComponent<CritterHolder>().modifierlist.Add(modifier);
                    subjectrelation.Add(heading);
                    
                }
            }
            foreach (var item in frenlists)
            {
                foreach (var items in item.GetComponent<CritterHolder>().modifierlist)
                {
                    items.LoadAura(item);
                }
            }
        }
    }
    public override void Execute(CritterHolder critter)
    {
        if(critter.online == true)
        {
            if(HasCommanded == false)
            {
                GrabAllSubjects(critter);
                HasCommanded = true;
            }
            if(TargetEnemy == null || TargetEnemy.active == false)
            {
                FindTarget(critter);
            }
            else
            {
                var heading  = TargetEnemy.transform.position - critter.gameObject.transform.position;
                var distance = heading.magnitude;
                var direction = heading / distance;

                if(direction.x > 0)
                {
                    critter.gameObject.transform.LookAt( new Vector3(critter.gameObject.transform.position.x+1,critter.gameObject.transform.position.y,360));//, new Vector3(0,0,0));
                }
                else
                {
                    critter.gameObject.transform.LookAt( new Vector3(critter.gameObject.transform.position.x-1,critter.gameObject.transform.position.y,-360));//, new Vector3(0,0,0));
                }

                if(distance < critter.GrabCombatDistance())
                {
                    Attack(distance, critter);
                }
                else
                {
                    critter.gameObject.transform.position += direction * Time.deltaTime * (float)critter.GrabSpeed();
                }
                for (int i = 0; i < subjects.Count; i++)
                {
                    var subject = subjects[i];
                    var location = critter.gameObject.transform.position + subjectrelation[i];
                    if(subject == null)
                    {
                        continue;
                    }
                    if(subject.GetComponent<CritterHolder>().AIScript.GetType() == typeof(basic_AI_Follower_Script))
                    {
                        if(location != null)
                        {
                            var a = (basic_AI_Follower_Script)subject.GetComponent<CritterHolder>().AIScript;
                            a.ExecuteOrder(subject.GetComponent<CritterHolder>(), location);
                        }
                    }
                }
            }
        }
    }
    void Attack(float distance, CritterHolder critter)
    {   
        if(critter.NextAvailableAttack < Time.time)
        {
            critter.NextAvailableAttack = Time.time + critter.GrabAttackTime();
            TargetEnemy.GetComponent<CritterHolder>().ReducePopulation(critter.GrabAttack());
            RpcTest.Serverchecker.ExecuteAnimation(critter, "Attack");
        }
        
    }
    public override void FindTarget(CritterHolder critter)
    {
        List<GameObject> enemylists = new List<GameObject>();
        foreach (var item in BattleManager1.Instance.enemylist)
        {
            if(item == null)
            {
                continue;
            }
            if(item.GetComponent<CritterHolder>().IsthisAI != critter.IsthisAI)
            {
                enemylists.Add(item);
            }
        }
        if(enemylists.Count > 0)
        {
            TargetEnemy = enemylists[0];
            foreach (var item in enemylists)
            {
                var heading  = item.transform.position - critter.gameObject.transform.position;
                var distance = heading.magnitude;

                var heading2  = TargetEnemy.transform.position - critter.gameObject.transform.position;
                var distance2 = heading2.magnitude;
                
                if(distance < distance2)
                {
                    TargetEnemy = item;
                }
            }
        }
    }
}
