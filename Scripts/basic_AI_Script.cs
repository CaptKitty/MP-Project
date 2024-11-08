using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "AIScript/Base")]
public class basic_AI_Script : base_AI_Script
{
    GameObject TargetEnemy;
    public float combatdistance = 1f;
    public float speed = 1f;
    public int attack = 1;
    public double attacktime = 1;
    public double NextAvailableAttack = 0;
    public override base_AI_Script Init()
    {
        var potato = new basic_AI_Script();
        potato.TargetEnemy = TargetEnemy;
        potato.combatdistance = combatdistance;
        potato.speed = speed;
        potato.attack = attack;
        potato.attacktime = attacktime;
        potato.NextAvailableAttack = NextAvailableAttack;
        return potato;
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
                var heading  = TargetEnemy.transform.position - critter.gameObject.transform.position;
                var distance = heading.magnitude;

                if(distance < combatdistance)
                {
                    Attack(distance, critter);
                }
                else
                {
                    var direction = heading / distance;
                    critter.gameObject.transform.position += direction * Time.deltaTime * speed;
                }
            }
        }
    }
    void Attack(float distance, CritterHolder critter)
    {
        
        if(NextAvailableAttack < Time.time)
        {
            NextAvailableAttack = Time.time + attacktime;
            TargetEnemy.GetComponent<CritterHolder>().ReducePopulation(attack);
            critter.gameObject.GetComponent<Animator>().SetTrigger("Attack");
        }
        
    }
    public void FindTarget(CritterHolder critter)
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
