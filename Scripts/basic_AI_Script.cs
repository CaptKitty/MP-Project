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
                //Vector3 vectory = new Vector3(1, 0.5f, 0);
                var heading  = TargetEnemy.transform.position - critter.gameObject.transform.position;
                var distance = heading.magnitude;
                var direction = heading / distance;
                critter.gameObject.transform.position += direction * Time.deltaTime * speed;
                Attack(distance, critter);
            }
        }
    }
    void Attack(float distance, CritterHolder critter)
    {
        if(distance < combatdistance)
        {
            if(NextAvailableAttack < Time.time)
            {
                NextAvailableAttack = Time.time + attacktime;
                TargetEnemy.GetComponent<CritterHolder>().ReducePopulation(attack);
            }
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
