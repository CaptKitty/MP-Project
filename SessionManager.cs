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
    public bool Campaign = false;
    public int CampaignLevel = 1;
    public int Escalation = 1;
    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
        //DontDestroyOnLoad(gameObject);
    }
    public void ChangePlayerFaction(string newfaction)
    {
        HostFaction = Resources.Load<Faction>("Prefabs/Factions/" + newfaction);
        //Debug.LogError("Host is " + HostFaction.name);
    }
    public void ChangeEnemyFaction(string newEnemy)
    {
        ClientFaction =  Resources.Load<Faction>("Prefabs/Factions/" + newEnemy);
        //Debug.LogError("Client is " + ClientFaction.name);
    }
    public void ClientChangePlayerFaction(string newfaction)
    {
        HostFaction_client = Resources.Load<Faction>("Prefabs/Factions/" + newfaction);
        //Debug.LogError("Host is " + HostFaction_client.name);
    }
    public void ClientChangeEnemyFaction(string newEnemy)
    {
        ClientFaction_client =  Resources.Load<Faction>("Prefabs/Factions/" + newEnemy);
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
        foreach(var unit in ClientArmy)
        {
            foreach (var RPC in TestRelay.Instance.PlayerObjects)
            {
                RPC.GetComponent<RpcTest>().Spawn(unit.target, unit.name, unit.AIorNot, unit.futurename, unit.ClientOrHost);
            }
        }
    }
    public void LoadCampaign()
    {
        Campaign = true;
        int phase = CampaignLevel;
        if(phase == 1)
        {
            ClientArmy = Resources.Load<Campaign>("Campaign").Layout1;
        }
        if(phase == 2)
        {
            ClientArmy = Resources.Load<Campaign>("Campaign").Layout2;
        }
        if(phase == 3)
        {
            ClientArmy = Resources.Load<Campaign>("Campaign").Layout3;
        }
        if(phase == 4)
        {
            ClientArmy = Resources.Load<Campaign>("Campaign").Layout4;
        }
        if(phase >= 5)
        {
            ClientArmy = Resources.Load<Campaign>("Campaign").Layout5;
        }
        CampaignLevel++;
    }
}

