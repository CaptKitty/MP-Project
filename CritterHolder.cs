using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class CritterHolder : MonoBehaviour
{
    public Vector3Int spot;
    public string name;
    public string typename;
    public UnitTypes unittype;
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

    public UnitBrain unitbrain;
    public List<Weapon> weaponlist;
    public List<string> flaglist;
    public Weapon RangedWeapon;
    public Weapon MeleeWeapon;
    public Weapon Armor;
    public Weapon Shield;

    public base_AI_Script AIScript;
    public List<base_AI_Script> scriptlist = new List<base_AI_Script>();

    public double combatdistance = 1f;
    public double speed = 1f;
    public int attack = 1;
    public double attacktime = 1;
    public double NextAvailableAttack = 0;
    public List<Modifier> modifierlist = new List<Modifier>();


    public delegate void OnDeath();
    public OnDeath onDeath;

    public int GrabHealth()
    {
        List<Modifier> _modifierlist = new List<Modifier>();
        foreach (var item in modifierlist)
        {
            if (item == null)
            {
                continue;
            }
            if (_modifierlist.Find(x => x.name == item.name))
            {
                continue;
            }
            _modifierlist.Add(item);
        }
        int newvariable = population;
        foreach (var item in _modifierlist)
        {
            if (item.base_health != 0)
            {
                newvariable = item.base_health;
            }
        }
        foreach (var item in _modifierlist)
        {
            if (item.bonus_health != 0)
            {
                newvariable += item.bonus_health;
            }
        }
        foreach (var item in _modifierlist)
        {
            if (item.combatdistance_modifier != 1)
            {
                newvariable = (int)(newvariable * item.health_modifier);
            }
        }
        return newvariable;
    }
    public double GrabCombatDistance()
    {
        List<Modifier> _modifierlist = new List<Modifier>();
        foreach (var item in modifierlist)
        {
            if (item == null)
            {
                continue;
            }
            if (_modifierlist.Find(x => x.name == item.name))
            {
                continue;
            }
            _modifierlist.Add(item);
        }
        double newvariable = RangedWeapon.combatdistance;
        foreach (var item in _modifierlist)
        {
            if (item.base_combatdistance != 0)
            {
                newvariable = item.base_combatdistance;
            }
        }
        foreach (var item in _modifierlist)
        {
            if (item.bonus_combatdistance != 0)
            {
                newvariable += item.bonus_combatdistance;
            }
        }
        foreach (var item in _modifierlist)
        {
            if (item.combatdistance_modifier != 1)
            {
                newvariable *= item.combatdistance_modifier;
            }
        }
        return newvariable;
    }
    public int GrabAttack()
    {
        List<Modifier> _modifierlist = new List<Modifier>();
        foreach (var item in modifierlist)
        {
            if (item == null)
            {
                continue;
            }
            if (_modifierlist.Find(x => x.name == item.name))
            {
                continue;
            }
            _modifierlist.Add(item);
        }
        double newvariable = (double)RangedWeapon.attack;
        foreach (var item in _modifierlist)
        {
            if (item.base_attack != 0)
            {
                newvariable = item.base_attack;
            }
        }
        foreach (var item in _modifierlist)
        {
            if (item.bonus_attack != 0)
            {
                newvariable += item.bonus_attack;
            }
        }
        foreach (var item in _modifierlist)
        {
            if (item.attack_modifier != 1)
            {
                newvariable *= item.attack_modifier;
            }
        }
        return (int)newvariable;
    }
    public double GrabSpeed()
    {
        List<Modifier> _modifierlist = new List<Modifier>();
        foreach (var item in modifierlist)
        {
            if (item == null)
            {
                continue;
            }
            if (_modifierlist.Find(x => x.name == item.name))
            {
                continue;
            }
            _modifierlist.Add(item);
        }
        double newvariable = speed;
        foreach (var item in _modifierlist)
        {
            if (item.base_speed != 0)
            {
                newvariable = item.base_speed;
            }
        }
        foreach (var item in _modifierlist)
        {
            if (item.speed_modifier != 1)
            {
                newvariable *= item.speed_modifier;
            }
        }
        return newvariable;
    }
    public double GrabAttackTime()
    {
        List<Modifier> _modifierlist = new List<Modifier>();
        foreach (var item in modifierlist)
        {
            if (item == null)
            {
                continue;
            }
            if (_modifierlist.Find(x => x.name == item.name))
            {
                continue;
            }
            _modifierlist.Add(item);
        }
        double newvariable = RangedWeapon.attacktime;
        foreach (var item in _modifierlist)
        {
            if (item.base_attacktime != 0)
            {
                newvariable = item.base_attacktime;
            }
        }
        foreach (var item in _modifierlist)
        {
            if (item.attacktime_modifier != 1)
            {
                newvariable *= item.attacktime_modifier;
            }
        }
        return newvariable;
    }
    public void GrabAIScripts()
    {
        return;
        foreach (var item in modifierlist)
        {
            if (item == null)
            {

            }
            if (item.aiscripts != null)
            {
                var a = item.aiscripts.Init();
                AIScript = a;
            }
        }
    }

    public void FixWeapons()
    {
        
        if (RangedWeapon != null)
        {
            RangedWeapon = RangedWeapon.GrabCopy();
            EquipWeapon(RangedWeapon);
        }
        if (MeleeWeapon != null)
        {
            MeleeWeapon = MeleeWeapon.GrabCopy();
        }
        if (RangedWeapon == null && MeleeWeapon != null)
        {
            MeleeWeapon = MeleeWeapon.GrabCopy();
            EquipWeapon(MeleeWeapon);
        }
        try
        {
            transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = RangedWeapon.sprite;
        }
        catch
        {
            transform.GetChild(2).GetComponent<SpriteRenderer>().sprite = MeleeWeapon.sprite;
        }
        if (Armor != null)
        {
            if (Armor.sprite != null)
            {
                gameObject.GetComponent<TestCritter>().listy[0].GetComponent<SpriteRenderer>().sprite = Armor.sprite;
            }
        }
        if (Shield != null)
        {
            if (Shield.sprite != null)
            {
                gameObject.GetComponent<TestCritter>().listy[1].GetComponent<SpriteRenderer>().sprite = Shield.sprite;
            }
        }
        gameObject.GetComponent<TestCritter>().SetWeapon(RangedWeapon.animationtype);
        
    }

    public void Awake()
    {

        modifierlist = new List<Modifier>();
        List<Modifier> deletelist = new List<Modifier>();
        foreach (var item in modifierlist)
        {
            if (item == null)
            {
                deletelist.Add(item);
            }
        }
        foreach (var item in deletelist)
        {
            modifierlist.Remove(item);
        }

        if (AbilityList.Count == 0)
        {
            Wakey();
        }

        unitbrain = new UnitBrain();
        unitbrain.critter = this;
        unitbrain.Startie();

        BattleManager1.OnVictory += Cheer;

        Cheer();
    }
    public void EquipWeapon(Weapon newRangedWeapon)
    {
        
        combatdistance = newRangedWeapon.combatdistance;
        attack = newRangedWeapon.attack;
        attacktime = newRangedWeapon.attacktime;
        RangedWeapon = newRangedWeapon;
        gameObject.GetComponent<TestCritter>().listy[2].GetComponent<SpriteRenderer>().sprite = newRangedWeapon.sprite;
        if (MeleeWeapon == newRangedWeapon)
        {
            MeleeWeapon = null;
        }
        
    }

    public void GrabNewScript()
    {
        AIScript = scriptlist[Random.Range(0, scriptlist.Count)].Init();
    }
    public void Wakey()
    {
        AbilityList.Clear();
        foreach (var item in PrivateAbilityList)
        {
            AbilityList.Add(item.Init());
        }
        FixWeapons();
        
    }
    public void FixedUpdate()
    {
        if (gameObject.name == "Stabby")
        {
            return;
        }
        if (!RpcTest.Serverchecker.ServerCheck())
        {
            AIScript.Direction(this);
        }
        if (online)
        {
            
            if (CanIAct())
            {
                unitbrain.Think();
                //AIScript.Execute(this);
            }
            HandleModifiers();
        }
    }
    public bool IsThisViable(string potato)
    {
        foreach (var item in nametype)
        {
            if (potato == item)
            {
                return true;
            }
        }
        if (potato == name)
        {
            return true;
        }
        if (potato == "any")
        {
            return true;
        }
        return false;
    }
    public void Start()
    {

        FixWeapons();
        population = GrabHealth();
        // if (SpriteList.Count > 0)
        // {
        //     var a = SpriteList[Random.Range(0, SpriteList.Count)];
        //     transform.GetComponent<SpriteRenderer>().sprite = a;
        // }
        if (BattleManager1.Instance)
        {
            BattleManager1.Instance.enemylist.Add(this.gameObject);
        }

        //Turn();
        if (GeneralManager.Instance)
        {
            GeneralManager.Instance.highlight = false;
        }
        gameObject.name = name;
        FixWeapons();
    }
    public void Turn()
    {
        foreach (var Abilitie in AbilityList)
        {
            if (CanIAct())
            {
                unitbrain.Think();
                //Abilitie.Execute(this);
            }

        }
        //HandleModifiers();
    }
    public bool CanIAct()
    {
        foreach (var item in modifierlist)
        {
            if (item.StunEffect == true)
            {
                return false;
            }
        }
        return true;
    }
    public void HandleModifiers()
    {
        List<Modifier> listy = new List<Modifier>();
        foreach (var item in modifierlist)
        {
            if (item.duration != 0)
            {
                if (item.EndDuration < Time.time)
                {
                    item.DestroyAura();
                    item.DestroyThis();
                    listy.Add(item);
                }
                if (item == null)
                {
                    listy.Add(item);
                }
            }
        }
        foreach (var item in listy)
        {
            modifierlist.Remove(item);
        }
    }
    // public void Throw()
    // {
    //     if(AIScript.GetType() == typeof(basic_Ranged_AI_script))
    //     {
    //         var a = (basic_Ranged_AI_script)AIScript;
    //         a.Throw(this);
    //     }
    //     if(AIScript.GetType() == typeof(basic_Ranged_AI_script_ammo))
    //     {
    //         var a = (basic_Ranged_AI_script_ammo)AIScript;
    //         a.Throw(this);
    //     }
    //     if(AIScript.GetType() == typeof(basic_Skirmish_Ranged_AI_script_ammo))
    //     {
    //         var a = (basic_Skirmish_Ranged_AI_script_ammo)AIScript;
    //         a.Throw(this);
    //     }
    // }
    public void Throw()
    {
        if (BattleManager1.Instance == null)
        {
            return;
        }
        if (RangedWeapon.Throwable == null)
        {
            return;
        }
        var potato = Instantiate(RangedWeapon.Throwable, BattleManager1.Instance.transform);
        RangedWeapon.ammo -= 1;
        potato.transform.position = this.gameObject.transform.GetChild(2).position;
        potato.transform.LookAt(new Vector3(unitbrain.TargetEnemy.gameObject.transform.position.x, unitbrain.TargetEnemy.gameObject.transform.position.y, -90), Vector3.forward);
        potato.GetComponent<Projectile>().TargetEnemy = unitbrain.TargetEnemy;

        if (RangedWeapon != null && RangedWeapon.ammo < 1)
        {
            EquipWeapon(MeleeWeapon);
        }
    }
    public void Attack()
    {
        GetComponent<Animator>().SetTrigger("Attack");
    }
    public void Cheer()
    {
        GetComponent<Animator>().SetTrigger("Cheer");
    }
    public double GrabArmor(string attacktype = "attack")
    {
        int a = 0;//zero armor;
        if (attacktype == "attack")
        {
            a += Shield.armor.armor;
            a += Armor.armor.armor;
        }
        if (attacktype == "ranged")
        {
            a += Shield.armor.rangedarmor;
            a += Armor.armor.rangedarmor;
        }
        if (attacktype == "pierce")
        {
        }
        if (a > 80)
        {
            a = 80;
        }
        //Debug.LogError(attacktype + " " + a);
        return a;
    }
    public void LoseHealth(int incoming, string attacktype = "attack")
    {
        double armorreduction = 1 - (GrabArmor(attacktype)/100);
        int damage = (int)(armorreduction * incoming);

        GetComponent<Animator>().SetTrigger("Hurt");
        population -= damage;
        if (population < 1)
        {
            IsThisAlive = false;
            IsthisAI = false;

            onDeath?.Invoke();


            this.gameObject.SetActive(false);

            // if(AIScript.GetType() == typeof(basic_AI_Command_Script))
            // {
            //     var b = (basic_AI_Command_Script)AIScript;
            //     foreach (var item in b.subjects)
            //     {
            //         b.modifier.DestroyAura(item);
            //         item.GetComponent<CritterHolder>().GrabNewScript();
            //         item.GetComponent<CritterHolder>().modifierlist.Remove(b.modifier);
            //     }
            // }


        }

    }
}
public enum UnitTypes
{
    None,
    LightInfantry,
    HeavyInfantry,
    Ranged,
    LightCavalry,
    HeavyCavalry,
}