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
    public void Awake()
    {
        if(AbilityList.Count == 0)
        {
            Wakey();
        }
        if(AIScript != null)
        {
            scriptlist.Add(AIScript.Init());
            GrabNewScript();         
        }
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
        if(online)
        {
            AIScript.Execute(this);
        }
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
    public void ReducePopulation(int a)
    {
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
            //Destroy(this.gameObject);

        }

    }
}
