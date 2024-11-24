using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance;
    public List<SpawnBait> HostArmy = new List<SpawnBait>();
    public Faction HostFaction;
    public Faction HostFaction_client;
    public List<SpawnBait> ClientArmy = new List<SpawnBait>();
    public Faction ClientFaction;
    public Faction ClientFaction_client;
    public Province savedProvince;
    public string BattleStatus = "None";
    public bool Campaign = false;
    public int CampaignLevel = 1;
    public int Escalation = 1;
    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
        HostFaction = HostFaction.Init();
        HostFaction_client = HostFaction_client.Init();
        HostFaction.Set();
        HostFaction_client.Set();
        foreach (var item in HostFaction.UnitList)
        {
            item.GetComponent<CritterHolder>().modifierlist.Clear();
        }
    }
    public void ChangePlayerFaction(string newfaction)
    {
        HostFaction = Resources.Load<Faction>("Prefabs/Factions/" + newfaction);
        HostFaction = HostFaction.Init();
        HostFaction.Set();
        foreach (var item in HostFaction.UnitList)
        {
            item.GetComponent<CritterHolder>().modifierlist.Clear();
        }
        foreach (var item in HostFaction.BarracksUnits)
        {
            item.GetComponent<CritterHolder>().modifierlist.Clear();
        }
        foreach (var item in HostFaction.MercenaryUnits)
        {
            item.GetComponent<CritterHolder>().modifierlist.Clear();
        }
    }
    public void ChangeEnemyFaction(string newEnemy)
    {
        ClientFaction =  Resources.Load<Faction>("Prefabs/Factions/" + newEnemy);
        ClientFaction = ClientFaction.Init();
        ClientFaction.Set();
        //Debug.LogError("Client is " + ClientFaction.name);
    }
    public void ClientChangePlayerFaction(string newfaction)
    {
        HostFaction_client = Resources.Load<Faction>("Prefabs/Factions/" + newfaction);
        HostFaction_client = HostFaction_client.Init();
        HostFaction_client.Set();
        //Debug.LogError("Host is " + HostFaction_client.name);
    }
    public void ClientChangeEnemyFaction(string newEnemy)
    {
        ClientFaction_client =  Resources.Load<Faction>("Prefabs/Factions/" + newEnemy);
        ClientFaction_client = ClientFaction_client.Init();
        ClientFaction_client.Set();
        //Debug.LogError("Client is " + ClientFaction_client.name);
    }

    public void SpawnToSM(Vector3Int target, GameObject spawnee = null, string Faction = "Royal", string name = "null", bool AIorNot = false, string futurename = "null", string ClientOrHost = "Host")
    {
        if(HostArmy.Find(x => x.futurename == futurename) != null)
        {
            return;
        }
        if(ClientArmy.Find(x => x.futurename == futurename) != null)
        {
            return;
        }

        SpawnBait spawnbait = new SpawnBait();
        spawnbait.target = target;
        spawnbait.Faction = Faction;
        spawnbait.name = name;
        spawnbait.AIorNot = AIorNot;
        spawnbait.futurename = futurename;
        spawnbait.ClientOrHost = ClientOrHost;
        if(ClientOrHost == "Host")
        {
            HostArmy.Add(spawnbait);
        }
        else
        {
            ClientArmy.Add(spawnbait);
        }
    }
    public void SpawnArmy()
    {
        foreach(var unit in HostArmy)
        {
            foreach (var RPC in TestRelay.Instance.PlayerObjects)
            {
                RPC.GetComponent<RpcTest>().Spawn(unit.target, unit.name, unit.AIorNot, unit.futurename, unit.ClientOrHost);
            }
        }
        List<SpawnBait> _ClientArmy = new List<SpawnBait>();
        foreach(var unit in ClientArmy)
        {
            _ClientArmy.Add(unit);
        }
        for (int i = 0; i < _ClientArmy.Count; i++)
        {
            var unit = _ClientArmy[i];
            foreach (var RPC in TestRelay.Instance.PlayerObjects)
            {
                RPC.GetComponent<RpcTest>().Spawn(unit.target, unit.name, unit.AIorNot, unit.name+""+i.ToString(), unit.ClientOrHost);
            }
        }
    }
    public void LoadCampaign(string enemy = "Carthage")
    {
        string Campaignstring = "CarthageCampaign";
        if(enemy.Contains("Rome"))
        {
            Campaignstring = "RomeCampaign";
        }
        if(enemy.Contains("Spain"))
        {
            Campaignstring = "SpainCampaign";
        }
        if(enemy.Contains("Gaul"))
        {
            Campaignstring = "GaulCampaign";
        }
        
        Campaign = true;
        int phase = CampaignLevel;
        var a = Instantiate(Resources.Load<Campaign>(Campaignstring));
        if(phase == 1)
        {
            ClientArmy = a.Layout1;
        }
        if(phase == 2)
        {
            ClientArmy = a.Layout2;
        }
        if(phase == 3)
        {
            ClientArmy = a.Layout3;
        }
        if(phase == 4)
        {
            ClientArmy = a.Layout4;
        }
        if(phase >= 5)
        {
            ClientArmy = a.Layout5;
        }
        //CampaignLevel++;
    }
}

