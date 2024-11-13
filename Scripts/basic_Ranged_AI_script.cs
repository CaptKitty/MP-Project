using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "AIScript/Ranged")]
public class basic_Ranged_AI_script : base_AI_Script
{
    GameObject TargetEnemy;
    public GameObject Throwable;
    public override base_AI_Script Init()
    {
        var potato = new basic_Ranged_AI_script();
        potato.TargetEnemy = TargetEnemy;
        potato.Throwable = Throwable;
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
    public override void Execute(CritterHolder critter)
    {
        if(critter.online == true)
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

                if(distance < critter.GrabCombatDistance())
                {
                    Attack(distance, critter);
                }
                else
                {
                    critter.gameObject.transform.position += direction * Time.deltaTime * (float)critter.GrabSpeed();
                }
            }
        }
    }
    void Attack(float distance, CritterHolder critter)
    {
        if(distance < critter.GrabCombatDistance())
        {
            if(critter.NextAvailableAttack < Time.time)
            {
                critter.NextAvailableAttack = Time.time + critter.GrabAttackTime();
                TargetEnemy.GetComponent<CritterHolder>().ReducePopulation(critter.GrabAttack());
                
                //critter.gameObject.GetComponent<Animator>().SetTrigger("Attack");
                RpcTest.Serverchecker.ExecuteAnimation(critter, "Attack");
                RpcTest.Serverchecker.ExecuteAnimation(critter, "Throw"); //Throw(critter);
            }
        }
    }
    public void Throw(CritterHolder critter)
    {
        if(TargetEnemy == null)
        {
            FindTarget(critter);
        }
        var potato = Instantiate(Throwable);
        potato.transform.position = critter.gameObject.transform.GetChild(2).position;
        potato.transform.rotation = critter.gameObject.transform.GetChild(2).rotation;
        potato.GetComponent<Projectile>().TargetEnemy = TargetEnemy;
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
