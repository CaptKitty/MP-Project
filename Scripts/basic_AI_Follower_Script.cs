using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "AIScript/Follower")]
public class basic_AI_Follower_Script : base_AI_Script
{
    GameObject TargetEnemy;
    public override base_AI_Script Init()
    {
        var potato = new basic_AI_Follower_Script();
        potato.TargetEnemy = TargetEnemy;
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
        FindTarget(critter);
        if(TargetEnemy != null)
        {
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

            var disty = Vector3.Distance(TargetEnemy.transform.position, critter.gameObject.transform.position);
            if(disty < critter.GrabCombatDistance())
            {
                Attack(disty, critter);
            }
        }
    }
    public void ExecuteOrder(CritterHolder critter, Vector3 position)
    {
        if(critter.online == true)
        {
            // if(TargetEnemy == null || TargetEnemy.active == false)
            // {
                FindTarget(critter);
            // }
            // else
            // {
                var disty = Vector3.Distance(TargetEnemy.transform.position, critter.gameObject.transform.position);
                var heading  = position - critter.gameObject.transform.position; //TargetEnemy.transform.position
                var distance = heading.magnitude;
                var direction = heading / distance;

                // if(direction.x > 0)
                // {
                //     critter.gameObject.transform.LookAt( new Vector3(critter.gameObject.transform.position.x+1,critter.gameObject.transform.position.y,360));//, new Vector3(0,0,0));
                // }
                // else
                // {
                //     critter.gameObject.transform.LookAt( new Vector3(critter.gameObject.transform.position.x-1,critter.gameObject.transform.position.y,-360));//, new Vector3(0,0,0));
                // }

                if(disty < critter.GrabCombatDistance())
                {
                    //Attack(disty, critter);
                }
                else
                {
                    critter.gameObject.transform.position += direction * Time.deltaTime * (float)critter.GrabSpeed();
                }
            //}
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
