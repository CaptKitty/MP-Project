using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CritterHolder : MonoBehaviour
{
    public Vector3Int spot;
    public string name;
    public List<string> nametype;
    public Resource cost;
    public bool DoesThisgoOnTheCity = false;
    public List<string> ViablePlacingSpots = new List<string>();
    public List<Vector3Int> targetlists = new List<Vector3Int>();
    public int population = 1;
    public List<Ability> AbilityList = new List<Ability>();
    [SerializeField]
    private List<Ability> PrivateAbilityList = new List<Ability>();
    public List<Sprite> SpriteList = new List<Sprite>();
    public bool CanBeOverwritten = false;
    public bool IsthisAI = false;
    public bool IsThisAlive = true;
    public bool online = false;
    public base_AI_Script AIScript;
    public List<base_AI_Script> scriptlist = new List<base_AI_Script>();

    public double combatdistance = 1f;
    public double speed = 1f;
    public int attack = 1;
    public double attacktime = 1;
    public double NextAvailableAttack = 0;
    public List<Modifier> modifierlist = new List<Modifier>();

    public double GrabCombatDistance()
    {
        double newvariable = combatdistance;
        foreach (var item in modifierlist)
        {
            if(item.base_combatdistance != 0)
            {
                newvariable = item.base_combatdistance;
            }
        }
        foreach (var item in modifierlist)
        {
            if(item.combatdistance_modifier != 1)
            {
                newvariable *= item.combatdistance_modifier;
            }
        }
        return newvariable;
    }
    public int GrabAttack()
    {
        double newvariable = (double)attack;
        foreach (var item in modifierlist)
        {
            if(item.base_attack != 0)
            {
                newvariable = item.base_attack;
            }
        }
        foreach (var item in modifierlist)
        {
            if(item.attack_modifier != 1)
            {
                newvariable *= item.attack_modifier;
            }
        }
        return (int)newvariable;
    }
    public double GrabSpeed()
    {
        double newvariable = speed;
        foreach (var item in modifierlist)
        {
            if(item.base_speed != 0)
            {
                newvariable = item.base_speed;
            }
        }
        foreach (var item in modifierlist)
        {
            if(item.speed_modifier != 1)
            {
                newvariable *= item.speed_modifier;
            }
        }
        return newvariable;
    }
    public double GrabAttackTime()
    {
        double newvariable = attacktime;
        foreach (var item in modifierlist)
        {
            if(item.base_attacktime != 0)
            {
                newvariable = item.base_attacktime;
            }
        }
        foreach (var item in modifierlist)
        {
            if(item.attacktime_modifier != 1)
            {
                newvariable *= item.attacktime_modifier;
            }
        }
        return newvariable;
    }

    public void Awake()
    {
        if(AbilityList.Count == 0)
        {
            Wakey();
        }
        if(scriptlist.Count == 0)
        {
            scriptlist.Add(AIScript.Init());
            GrabNewScript();         
        }
        AIScript = AIScript.Init();
    }
    public void GrabNewScript()
    {
        AIScript = scriptlist[Random.Range(0,scriptlist.Count)].Init();
    }
    public void Wakey()
    {
        AbilityList.Clear();
        foreach (var item in PrivateAbilityList)
        {
            Debug.Log(item.name);
            AbilityList.Add(item.Init());
        }
    }
    public void FixedUpdate()
    {
        if(!RpcTest.Serverchecker.ServerCheck())
        {
            AIScript.Direction(this);
        }
        if(online)
        {
            AIScript.Execute(this);
        }
    }
    public void Update()
    {
        // if(Input.GetKeyDown("1"))
        // {
        //     Debug.LogError(name);
        // }
    }
    public bool IsThisViable(string potato)
    {
        foreach (var item in nametype)
        {
            if(potato == item)
            {
                return true;
            }
        }
        if(potato == name)
        {
            return true;
        }
        if(potato == "any")
        {
            return true;
        }
        return false;
    }
    public void Start()
    {
        if(SpriteList.Count > 0)
        {
            var a = SpriteList[Random.Range(0,SpriteList.Count)];
            transform.GetComponent<SpriteRenderer>().sprite = a;
        }
        if(BattleManager1.Instance)
        {
            BattleManager1.Instance.enemylist.Add(this.gameObject);
        }

        //Turn();
        if(GeneralManager.Instance)
        {
            GeneralManager.Instance.highlight = false;
        }
        
        gameObject.name = name;
    }
    public void Turn()
    {
        foreach(var Abilitie in AbilityList)
        {
            Abilitie.Execute(this);
        }
    }
    // public void EndOfTurn()
    // {
    //     if(population < 1)
    //     {
    //         foreach(var Abilitie in AbilityList)
    //         {
    //             Abilitie.OnDeath(this);
    //         }
    //         Destroy(this.gameObject);
    //     }
    // }
    public void Throw()
    {
        if(AIScript.GetType() == typeof(basic_Ranged_AI_script))
        {
            var a = (basic_Ranged_AI_script)AIScript;
            a.Throw(this);
        }
        if(AIScript.GetType() == typeof(basic_Ranged_AI_script_ammo))
        {
            var a = (basic_Ranged_AI_script_ammo)AIScript;
            a.Throw(this);
        }
    }
    public void Attack()
    {
        GetComponent<Animator>().SetTrigger("Attack");
    }
    public void ReducePopulation(int a)
    {
        GetComponent<Animator>().SetTrigger("Hurt");
        population -= a;
        if(population < 1)
        {
            // foreach(var Abilitie in AbilityList)
            // {
            //     Abilitie.OnDeath(this);
            // }
            // if(BattleManager1.Instance && IsThisAlive && IsthisAI)
            // {
            //     BattleManager1.Instance.ChangeScore(1);
            // }
            // if(BattleManager1.Instance && IsThisAlive && !IsthisAI)
            // {
            //     BattleManager1.Instance.FriendlyCounter -= 1;
            //     BattleManager1.Instance.ChangeReserves(0);
            // }
            IsThisAlive = false;
            IsthisAI = false;
            this.gameObject.SetActive(false);
            
            if(AIScript.GetType() == typeof(basic_AI_Command_Script))
            {
                var b = (basic_AI_Command_Script)AIScript;
                foreach (var item in b.subjects)
                {
                    b.modifier.DestroyAura(item);
                    item.GetComponent<CritterHolder>().GrabNewScript();
                    item.GetComponent<CritterHolder>().modifierlist.Remove(b.modifier);
                    
                    
                }
            }

            //Destroy(this.gameObject);

        }

    }
}
