using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

using Unity.Netcode;
using UnityEngine;
using Unity.Services.Authentication;

public class BattleManager1 : BattleManager
{
    public static BattleManager1 Instance;
    private bool FightIsOn = false;
    public int FriendlyCounter = 0;
    public string Faction = "Royal";
    public bool AIPlayer = false;

    public override void Awake()
    {
        Instance = this;
        Reserves = 1000;
        texty.text = Reserves.ToString();
        foreach (Vector3Int position in ownermap.cellBounds.allPositionsWithin)
        {
            dicty.Add(position, null);
        }
    }
    void Update()
    {
        if(Input.GetKeyDown("escape"))
        {
            SceneManager.LoadScene("FightScene 1");
        }
        try
        {
            if(RpcTest.Serverchecker.ServerCheck())
            {
                RpcTest.Serverchecker.UpdateCritters();
                
                if(Input.GetKeyDown("space"))
                {
                    MousePet.SetActive(false);
                    if(!FightIsOn)
                    {
                        foreach (var item in enemylist)
                        {
                            if(item == null)
                            {
                                continue;
                            }
                            if(item.GetComponent<CritterMovement>())
                            {
                                item.GetComponent<CritterMovement>().online = true;
                            }
                            if(item.GetComponent<CritterHolder>())
                            {
                                item.GetComponent<CritterHolder>().online = true;
                            }
                        }
                        FightIsOn = true;
                        //ChangeReserves(0);
                    }
                }
            }
        }
        catch{}
        
        // if(Input.GetKeyDown("1"))
        // {
        //     Faction = "Royal";
        // }
        // if(Input.GetKeyDown("2"))
        // {
        //     Faction = "Northern";
        // }
        // if(Input.GetKeyDown("3"))
        // {
        //     Faction = "Western";
        // }
        
        for (int i = 0; i < 2; i++)
        {
            GameObject a = null;
            foreach (var item in enemylist)
            {
                if(item != null)
                {
                    if(item.GetComponent<CritterHolder>().population < 1)
                    {
                        a = item;
                    }
                }
            }
            if(a != null)
            {
                a.SetActive(false);
                enemylist.Remove(a);
            }
        }

        Vector3Int target = ownermap.WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));
        target.z = 0;
        if (!ownermap.HasTile(target)) {
            return;
        }
        MousePet.transform.position = new Vector3(ownermap.CellToWorld(target).x + 0.0f, ownermap.CellToWorld(target).y +0.25f, 0);;
        // if (!highlightmap.HasTile(target)) {
        //     return;
        // }
        // foreach (var RPC in TestRelay.Instance.PlayerObjects)
        // {
        //     RPC.GetComponent<RpcTest>().UpdateCritters();
        // }
        
        if(!FightIsOn)
        {
            if (Input.GetMouseButtonDown(1))
            {
                if(dicty[target] == null)
                {
                    if(SelectedCritter.GetComponent<CritterHolder>().cost.amount <= Reserves)
                    {
                        foreach (var RPC in TestRelay.Instance.PlayerObjects)
                        {
                            RPC.GetComponent<RpcTest>().Spawn(target, SelectedCritter);
                        }
                        Reserves -= SelectedCritter.GetComponent<CritterHolder>().cost.amount;
                        texty.text = Reserves.ToString();
                    }
                }
                //Spawn(target, SelectedCritter);
                
            }
        }
        // foreach (Vector3Int position in highlightmap.cellBounds.allPositionsWithin)
        // {
        //     highlightmap.SetTile(position, GeneralManager.Instance.tilea);
        // }
        // foreach(Ability item in SelectedCritter.GetComponent<CritterHolder>().AbilityList)
        // {
        //     if(item.GetType() == typeof(ForageAbility) || item.GetType() == typeof(HarvesterAbility) || item.GetType() == typeof(CutterAbility))
        //     {
        //         //ForageAbility items = (ForageAbility)item;
        //         Vector2Int vector = item.arrayBool.GridSize;
        //         for (int x = -(vector.x-1)/2; x <= (vector.y)/2; x++)
        //         {
        //             for (int y = -(vector.y-1)/2; y <= (vector.y)/2; y++)
        //             {
        //                 if(item.arrayBool.GetCell((x+(vector.x)/2),(y+(vector.y)/2)))
        //                 {
        //                     Vector3Int potatoes = new Vector3Int(target.x + x, (target.y + y), 0);
        //                     if (!highlightmap.HasTile(potatoes))
        //                     {
        //                         continue;
        //                     }
        //                     highlightmap.SetTile(potatoes, GeneralManager.Instance.tileb);
        //                 }
        //             }
        //         }
        //     }
        // }
        // foreach(Ability item in SelectedCritter.GetComponent<CritterHolder>().AbilityList)
        // {
        //     if(item.GetType() == typeof(ForageAbility) || item.GetType() == typeof(HarvesterAbility) || item.GetType() == typeof(CutterAbility))
        //     {
        //         //ForageAbility items = (ForageAbility)item;
        //         Vector2Int vector = item.arrayBool.GridSize;
        //         for (int x = -(vector.x-1)/2; x <= (vector.y)/2; x++)
        //         {
        //             for (int y = -(vector.y-1)/2; y <= (vector.y)/2; y++)
        //             {
        //                 if(item.arrayBool.GetCell((x+(vector.x)/2),(y+(vector.y)/2)))
        //                 {
        //                     Vector3Int potatoes = new Vector3Int(target.x + x, (target.y + y), 0);
        //                     if (!highlightmap.HasTile(potatoes))
        //                     {
        //                         continue;
        //                     }
        //                     if(dicty[potatoes] != null)
        //                     {
        //                         if(dicty[potatoes].GetComponent<CritterHolder>().IsThisViable(item.food))
        //                         {
        //                             highlightmap.SetTile(potatoes, GeneralManager.Instance.tilec);
        //                         }
        //                     }
        //                     // if(GeneralManager.Instance.tiledict[potatoes] != null)
        //                     // {
        //                     //     if(GeneralManager.Instance.tiledict[potatoes].name == item.food)
        //                     //     {
        //                     //         highlightmap.SetTile(potatoes, GeneralManager.Instance.tilec);
        //                     //     }
        //                     // }
        //                 }
        //             }
        //         }
        //     }
        //     highlightmap.SetTile(target, GeneralManager.Instance.tiled);
        // }

    }
    public override void ChangeReserves(int testy)
    {
        //Reserves = CityManager.Instance.ResourceList.Find(x => x.name == "Piety").amount;
        //CityManager.Instance.ResourceList.Find(x => x.name == "Manpower").amount -= testy;
        //Rtexty.text = (CityManager.Instance.ResourceList.Find(x => x.name == "Manpower").amount/100) + " Friendly Lances Remaining.";
        //CityManager.Instance.ResourceList.Find(x => x.name == "Manpower").amount -= testy;
        //Rtexty.text = CityManager.Instance.ResourceList.Find(x => x.name == "Manpower") + " Friendly Manpower Remaining.";
        
        //if(Score > 0 && Reserves <= 0 && FightIsOn == true)
        if(Score > 0 && FriendlyCounter <= 0 && FightIsOn == true)
        {
            Banner.SetActive(true);
            Banner.transform.GetChild(0).GetComponent<Text>().text = "WE ARE OUTMANOUVERED, FALL BACK!";
            //IEnumerator coroutine = Restart();
            //StartCoroutine(coroutine);
        }
    }
    public override void ChangeScore(int testy)
    {
        Score -= testy;
        texty.text = Score + " Enemy Morale Remaining.";
        if(Score <= 0 && FriendlyCounter > 0 && FightIsOn == true)
        {
            Banner.SetActive(true);
            Banner.transform.GetChild(0).GetComponent<Text>().text = "THE COWARDS HAVE BROKEN AND ABANDON THEIR LINES";
            //IEnumerator coroutine = ReturnToCity();
            //StartCoroutine(coroutine);
        }
    }
    // IEnumerator ReturnToCity()
    // {
    //     yield return new WaitForSeconds(3);
    //     GeneralManager.Instance.cam1.gameObject.SetActive(true);
    //     GeneralManager.Instance.cam2.gameObject.SetActive(false);
    //     GeneralManager.Instance.cam3.gameObject.SetActive(true);
        
        

    //     List<GameObject> enemylists = new List<GameObject>();
    //     foreach (var item in enemylist)
    //     {
    //         if(item == null)
    //         {
    //             continue;
    //         }
    //         if(item.GetComponent<CritterHolder>().IsthisAI == false)
    //         {
    //             CityManager.Instance.ResourceList.Find(x => x.name == "Manpower").amount += item.GetComponent<CritterHolder>().cost.amount;
    //         }
    //     }


    //     SceneManager.UnloadScene("FightScene 1");
    // }
    // IEnumerator Restart()
    // {
    //     yield return new WaitForSeconds(3);
    //     SceneManager.LoadScene("SampleScene");
    // }
    public void ChangeFaction(string newfaction)
    {
        Faction = newfaction;
    }
    public void Spawn(Vector3Int target, GameObject spawnee = null, string Faction = "Royal", string name = "null", bool AIorNot = false, string futurename = "null")
    {
        //Debug.LogError("Spawning a twat");
        if(dicty[target] == null)
        {
            
            GameObject spawner = null;
            if(name != "null")
            {
                spawner = Resources.Load<GameObject>("Prefabs/Units/"+ Faction + "/" + name);
            }
            else
            {
                spawner = spawnee;
            }
            GameObject trader = Instantiate(spawner, ownermap.transform);

            // Reserves -= trader.GetComponent<CritterHolder>().cost.amount;

            //ChangeReserves(trader.GetComponent<CritterHolder>().cost.amount);

            if(trader.name != "AIDude")
            {
                if(1==1)//CityManager.Instance.ResourceList.Find(x => x.name == "Manpower").amount >= trader.GetComponent<CritterHolder>().cost.amount)
                {
                    //ChangeReserves(trader.GetComponent<CritterHolder>().cost.amount);
                    FriendlyCounter++;
                }
                else
                {
                    Destroy(trader);
                    return;
                }
            }

            
            trader.transform.position = new Vector3(ownermap.CellToWorld(target).x + 0.0f, ownermap.CellToWorld(target).y +0.25f, 0);
            trader.GetComponent<CritterHolder>().spot = target;

            trader.GetComponent<CritterHolder>().IsthisAI = AIorNot;

            trader.GetComponent<CritterHolder>().name = futurename;
            trader.name = futurename;
            
            Destroy(dicty[target]);
            dicty[target] = trader; 
        }
    }
}
