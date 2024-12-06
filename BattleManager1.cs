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
    public bool Starter = true;
    public GameObject a,b;

    public Faction Playerfaction;

    public delegate void DoSomething();
    public static event DoSomething OnVictory;


    public override void Awake()
    {
        Instance = this;
        Reserves = SessionManager.Instance.HostFaction.GrabIncome();
        texty.text = Reserves.ToString();
        foreach (Vector3Int position in ownermap.cellBounds.allPositionsWithin)
        {
            dicty.Add(position, null);
        }
    }
    public void Start()
    {
        SessionManager.Instance.SpawnArmy();
        
        TerrainTime();
    }
    public void TerrainTime()
    {
        transform.GetChild(0).GetComponent<TilemapEditor>().GenerateTerrain();
        foreach (Vector3Int position in ownermap.transform.GetChild(0).GetComponent<Tilemap>().cellBounds.allPositionsWithin)
        {
            if(ownermap.transform.GetChild(0).GetComponent<Tilemap>().GetTile(position) == null)
            {
                continue;
            }
            if(ownermap.transform.GetChild(0).GetComponent<Tilemap>().GetTile(position).name == "_Forest")
            {
                foreach (var RPC in TestRelay.Instance.PlayerObjects)
                {
                    RPC.GetComponent<RpcTest>().Spawn(position, "Foliage_Tree");
                }
                // int a = Random.Range(0,2);
                // if(a == 0)
                // {
                    
                // }
                // if(a == 1)
                // {
                    
                // }
            }
            if(ownermap.transform.GetChild(0).GetComponent<Tilemap>().GetTile(position).name == "_Hills")
            {
                foreach (var RPC in TestRelay.Instance.PlayerObjects)
                {
                    RPC.GetComponent<RpcTest>().Spawn(position, "Foliage_Brush");
                }
            }
            if(ownermap.transform.GetChild(0).GetComponent<Tilemap>().GetTile(position).name == "_Mountain")
            {
                foreach (var RPC in TestRelay.Instance.PlayerObjects)
                {
                    RPC.GetComponent<RpcTest>().Spawn(position, "Foliage_Cliff");
                }
            }
        }
    }
    public void ResetBattleField(bool ResetSession = false)
    {
        SessionManager.Instance.Escalation += 1;
        if(ResetSession == true)
        {
            SessionManager.Instance.HostArmy.Clear();
            SessionManager.Instance.ClientArmy.Clear();
            SessionManager.Instance.CampaignLevel = 1;
            SessionManager.Instance.Escalation = 1;
        }
        if(TestRelay.Instance.PlayerObjects.Count == 1)
        {
            SessionManager.Instance.LoadCampaign();
        }
        SceneManager.LoadScene("FightScene 1");
        
    }
    public void StartFight()
    {
        a.SetActive(false);
        b.SetActive(false);
    }
    void Update()
    {
        if(Input.GetKeyDown("5"))
        {
            OnVictory?.Invoke();
        }
        if(Starter)
        {   
            try
            {
                if(TestRelay.Instance.PlayerObjects.Count == 2)
                {
                    if(RpcTest.Serverchecker.ServerCheck())
                    {
                        a.SetActive(false);
                    }
                    else
                    {
                        b.SetActive(false);
                    }
                    
                    foreach (var RPC in TestRelay.Instance.PlayerObjects)
                    {
                        RPC.GetComponent<RpcTest>().SendFaction();
                    }
                    Playerfaction = SessionManager.Instance.HostFaction;
                    Reserves = SessionManager.Instance.HostFaction.GrabIncome();
                    // if(RpcTest.Serverchecker.ServerCheck())
                    // {
                    //     Playerfaction = SessionManager.Instance.HostFaction;
                    // }
                    // else
                    // {
                    //     Playerfaction = SessionManager.Instance.ClientFaction_client;
                    // }
                    MenuArmyLoader.Instance.LoadFiles();
                    Starter = false;
                }
                if(TestRelay.Instance.PlayerObjects.Count == 1 && SessionManager.Instance.Campaign == true)
                {
                    a.SetActive(false);
                    RpcTest.Serverchecker.ServerCheck();
                    // foreach (var RPC in TestRelay.Instance.PlayerObjects)
                    // {
                    //     RPC.GetComponent<RpcTest>().SendFaction();
                    // }
                    Playerfaction = SessionManager.Instance.HostFaction;
                    Reserves = SessionManager.Instance.HostFaction.GrabIncome();
                    // if(RpcTest.Serverchecker.ServerCheck())
                    // {
                    //     Playerfaction = SessionManager.Instance.HostFaction;
                    // }
                    // else
                    // {
                    //     Playerfaction = SessionManager.Instance.ClientFaction_client;
                    // }
                    MenuArmyLoader.Instance.LoadFiles();
                    Starter = false;
                }
            }
            catch{}
        }
        try
        {
            if(RpcTest.Serverchecker.ServerCheck())
            {
                if(Input.GetKeyDown("escape") && SessionManager.Instance.Campaign == false)
                {
                    if(Input.GetKey("space"))
                    {
                        RpcTest.Serverchecker.ExecuteReset(true);
                        return;
                    }
                    RpcTest.Serverchecker.ExecuteReset(false);
                }
                RpcTest.Serverchecker.UpdateCritters();
                
                if(Input.GetKeyDown("space"))
                {
                    foreach (var item in enemylist)
                    {
                        item.GetComponent<CritterHolder>().AIScript.FindTarget(item.GetComponent<CritterHolder>());
                    }
                    
                    RpcTest.Serverchecker.StartBattle();
                    
                    //MousePet.SetActive(false);
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
        //MousePet.transform.position = new Vector3(ownermap.CellToWorld(target).x + 0.0f, ownermap.CellToWorld(target).y +0.0f, 0);//ownermap.CellToWorld(target).y +0.25f, 0);;
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
                if(SelectedCritter != null)
                {
                    if(dicty[target] == null)
                    {
                        if(RpcTest.Serverchecker.ServerCheck() && ownermap.GetTile(target).name == "Grassland 4")
                        {
                            if(SelectedCritter.GetComponent<CritterHolder>().cost.amount <= Reserves)
                            {
                                foreach (var RPC in TestRelay.Instance.PlayerObjects)
                                {
                                    //RPC.GetComponent<RpcTest>().SendFaction();
                                    RPC.GetComponent<RpcTest>().Spawn(target, SelectedCritter);
                                }
                                Reserves -= SelectedCritter.GetComponent<CritterHolder>().cost.amount;
                                texty.text = Reserves.ToString();
                            }
                        }
                        if(!RpcTest.Serverchecker.ServerCheck() && ownermap.GetTile(target).name == "Grassland 5")
                        {
                            if(SelectedCritter.GetComponent<CritterHolder>().cost.amount <= Reserves)
                            {
                                foreach (var RPC in TestRelay.Instance.PlayerObjects)
                                {
                                    //RPC.GetComponent<RpcTest>().SendFaction();
                                    RPC.GetComponent<RpcTest>().Spawn(target, SelectedCritter);
                                }
                                Reserves -= SelectedCritter.GetComponent<CritterHolder>().cost.amount;
                                texty.text = Reserves.ToString();
                            }
                        }
                        
                    }
                }

                
                //Spawn(target, SelectedCritter);
                
            }
        }
        else
        {
            int friendlyalive = 0;
            int enemyalive = 0;
            foreach (var item in enemylist)
            {
                if(item.GetComponent<CritterHolder>().IsthisAI && item.active)
                {
                    friendlyalive++;
                }
                if(!item.GetComponent<CritterHolder>().IsthisAI && item.active)
                {
                    enemyalive++;
                }
            }
            if(friendlyalive < 1 || enemyalive < 1)
            {
                if(friendlyalive < 1)
                {
                    //SessionManager.Instance.BattleStatus = "Defeat";
                    
                }
                if(enemyalive < 1)
                {
                    //SessionManager.Instance.BattleStatus = "Victorious";
                    SessionManager.Instance.savedProvince.nation = Owners.Instance.CallPlayer();
                    SessionManager.Instance.CampaignLevel++;
                    SessionManager.Instance.HostFaction.FarmLevel++;
                    FactionUpgrade.Instance.gameObject.SetActive(true);
                }
                SessionManager.Instance.HostArmy.Clear();
                SessionManager.Instance.ClientArmy.Clear();

                Mapshower.Instance.gameObject.SetActive(true);
                SceneManager.UnloadScene("FightScene 1");
                return;
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
    public void Spawn(Vector3Int target, GameObject spawnee = null, string Faction = "Royal", string name = "null", bool AIorNot = false, string futurename = "null", string ClientOrHost = "Host")
    {
        // try
        // {
            if(dicty[target] == null)
            {
                GameObject spawner = Resources.Load<GameObject>("Prefabs/Units/Normies/" + name);
                GameObject trader = Instantiate(spawner, ownermap.transform);

                
                trader.transform.position = new Vector3(ownermap.CellToWorld(target).x + 0.5f, ownermap.CellToWorld(target).y +0.5f , 0); //+0.25f
                if(trader.GetComponent<CritterHolder>() != null)
                {
                    trader.GetComponent<CritterHolder>().spot = target;

                    trader.GetComponent<CritterHolder>().IsthisAI = AIorNot;

                    trader.GetComponent<CritterHolder>().name = futurename;

                    if(ClientOrHost == "Host")
                    {
                        if(RpcTest.Serverchecker.ServerCheck())
                        {
                            trader.GetComponent<TestCritter>().faction = SessionManager.Instance.HostFaction;
                        }
                        else
                        {
                            trader.GetComponent<TestCritter>().faction = SessionManager.Instance.HostFaction_client;
                        }
                        
                    }
                    else
                    {
                        if(RpcTest.Serverchecker.ServerCheck())
                        {
                            trader.GetComponent<TestCritter>().faction = SessionManager.Instance.HostFaction_client;
                        }
                        else
                        {
                            trader.GetComponent<TestCritter>().faction = SessionManager.Instance.HostFaction;
                        }
                        //trader.GetComponent<TestCritter>().faction = Playerfaction;
                    }

                    trader.name = futurename;
                    
                    Destroy(dicty[target]);
                    dicty[target] = trader; 
                }
            }
        // }
        // catch
        // {
        //     Debug.LogError(name);
        // }
    }
}
