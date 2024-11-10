using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "AIScript/RangedAmmo")]
public class basic_Ranged_AI_script_ammo : base_AI_Script
{
    GameObject TargetEnemy;
    public float combatdistance = 1f;
    public float speed = 1f;
    public int attack = 1;
    public double attacktime = 1;
    public double NextAvailableAttack = 0;
    public GameObject Throwable;
    public int ammo;
    public override base_AI_Script Init()
    {
        var potato = new basic_Ranged_AI_script_ammo();
        potato.TargetEnemy = TargetEnemy;
        potato.combatdistance = combatdistance;
        potato.speed = speed;
        potato.attack = attack;
        potato.attacktime = attacktime;
        potato.NextAvailableAttack = NextAvailableAttack;
        potato.Throwable = Throwable;
        potato.ammo = ammo;
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

                if(distance < combatdistance)
                {
                    Attack(distance, critter);
                }
                else
                {
                    critter.gameObject.transform.position += direction * Time.deltaTime * speed;
                }
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
                
                
                if(ammo < 1)
                {
                    critter.gameObject.GetComponent<TestCritter>().DoesThisHaveSword = true;
                    critter.gameObject.GetComponent<TestCritter>().DoesThisHaveJavelin = false;
                    critter.GrabNewScript();
                    return;
                }
                ammo -= 1;
                if(ammo > 0)
                {
                    TargetEnemy.GetComponent<CritterHolder>().ReducePopulation(attack);
                }
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
