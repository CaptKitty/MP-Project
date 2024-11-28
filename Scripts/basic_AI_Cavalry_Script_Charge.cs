using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "AIScript/Cav_Charge")]
public class basic_AI_Cavalry_Script_Charge : base_AI_Script
{
    GameObject TargetEnemy;
    public double FlankTime;
    public double Timer;
    public int ChargeDamage;
    public double ChargeTime;
    public override base_AI_Script Init()
    {
        var potato = new basic_AI_Cavalry_Script_Charge();
        potato.TargetEnemy = TargetEnemy;
        potato.FlankTime = FlankTime;
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
            if(Timer == 0)
            {
                Timer = Time.time + FlankTime;
                FindFlank(critter);
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
                if(Timer > Time.time)
                {
                    direction.y = 0;
                    TargetEnemy = null;
                    FindTarget(critter);
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
        if(critter.NextAvailableAttack < Time.time)
        {
            if(critter.NextAvailableAttack + ChargeTime < Time.time)
            {
                TargetEnemy.GetComponent<CritterHolder>().ReducePopulation(ChargeDamage);
            }

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
    public void FindFlank(CritterHolder critter)
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
                
                if(distance > distance2)
                {
                    TargetEnemy = item;
                }
            }
        }
    }
}
