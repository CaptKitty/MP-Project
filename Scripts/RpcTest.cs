using Unity.Netcode;
using UnityEngine;
using Unity.Services.Authentication;

public class RpcTest : NetworkBehaviour
{
    public static RpcTest Serverchecker;
    public override void OnNetworkSpawn()
    {
        if (!IsServer && IsOwner) //Only send an RPC to the server from the client that owns the NetworkObject of this NetworkBehaviour instance
        {
            ServerOnlyRpc(0, NetworkObjectId);
        }
        this.gameObject.name = NetworkObjectId.ToString();
        try
        {
            //this.transform.SetParent(TestRelay.Instance.gameObject.transform);
            DontDestroyOnLoad(this.gameObject);
        }
        catch{}
        
        if(NetworkObjectId == 1)
        {
            Serverchecker = this;
        }
        TestRelay.Instance.PlayerObjects.Add(this.gameObject);
        // for (int i = 0; i < Manager2.Instance.critterslist.Count; i++)
        // {         
        //     if (IsServer) 
        //     {
        //     }
        //     else
        //     {
        //         UpdateCritters();
        //     }
        // }
    }
    public bool ServerCheck()
    {
        if(IsServer)
        {
            return true;
        }
        return false;
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ClientAndHostRpc(int value, ulong sourceNetworkObjectId)
    {
        Debug.Log($"Client Received the RPC #{value} on NetworkObject #{sourceNetworkObjectId}");
        
        // SendYourCritterRpc(Manager2.Instance.critterslist[0].NutritionAmount, Manager2.Instance.critterslist[0].Actions);
        // SendYourCritterRpc(Manager2.Instance.critterslist[1].NutritionAmount, Manager2.Instance.critterslist[1].Actions);

        if (IsOwner) //Only send an RPC to the owner of the NetworkObject
        {
            ServerOnlyRpc(value + 1, sourceNetworkObjectId);
        }
    }

    public void ExecuteReset()
    {   if(IsServer){SendYourResetServerRpc();}
        else{SendYourResetClientRpc();}}
    [Rpc(SendTo.ClientsAndHost)]
    void SendYourResetServerRpc()
    {BattleManager1.Instance.ResetBattleField();}
    [Rpc(SendTo.Server)]
    void SendYourResetClientRpc()
    {BattleManager1.Instance.ResetBattleField();}

    public void ExecuteAnimation(CritterHolder critter, string WhatToExecute)
    {   if(IsServer){SendYourCommandServerRpc(critter.name, WhatToExecute);}
        else{SendYourCommandClientRpc(critter.name, WhatToExecute);}}
    [Rpc(SendTo.ClientsAndHost)]
    void SendYourCommandServerRpc(string name, string command)
    {   var a = BattleManager1.Instance.enemylist.Find(x => x.name == name);
        a.GetComponent<CritterHolder>().Invoke(command,0f);}
    [Rpc(SendTo.Server)]
    void SendYourCommandClientRpc(string name, string command)
    {   var a = BattleManager1.Instance.enemylist.Find(x => x.name == name);
        a.GetComponent<CritterHolder>().Invoke(command,0f);}

    public void UpdateCritters()
    {
        for (int i = 0; i < BattleManager1.Instance.enemylist.Count; i++)
        {   
            CritterHolder unit = BattleManager1.Instance.enemylist[i].GetComponent<CritterHolder>();
            Vector2 location = BattleManager1.Instance.enemylist[i].transform.position;
            string crittername = unit.name;
            if (IsServer) 
            {
                SendYourCritterServerRpc(i, crittername, (double)location.x, (double)location.y, unit.population);
            }
            else
            {
                SendYourCritterClientRpc(i, crittername, (double)location.x, (double)location.y, unit.population);
            }
        }
    }
    public void Spawn(Vector3Int target, GameObject spawnee = null, string name = "null")
    {
        //Debug.LogError(name);
        if(name == "null")
        {
            name = spawnee.name;
        }

        string futurename = RpcTest.Serverchecker.ServerCheck().ToString() + name + Random.Range(0,1000);

        // //Debug.LogError(name);
        SendYourSpawnServerRpc(target.x, target.y, name, Faction:BattleManager1.Instance.Faction, AIorNot:ServerCheck(), futurename:futurename);
        SendYourSpawnClientRpc(target.x, target.y, name, Faction:BattleManager1.Instance.Faction, AIorNot:ServerCheck(), futurename:futurename);

        // if (IsServer) 
        // {
        //     SendYourSpawnServerRpc(target.x, target.y, name, Faction:BattleManager1.Instance.Faction, AIorNot:ServerCheck(), futurename:futurename);
        //     //SendYourSpawnServerRpc(target.x, target.y, name, Faction:BattleManager1.Instance.Faction);
        // }
        // else
        // {
        //     SendYourSpawnClientRpc(target.x, target.y, name, Faction:BattleManager1.Instance.Faction, AIorNot:ServerCheck(), futurename:futurename);
        //    // SendYourSpawnClientRpc(target.x, target.y, name, Faction:BattleManager1.Instance.Faction);
        // }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SendYourSpawnServerRpc(float x, float y, string name, string Faction, bool AIorNot, string futurename)
    {
        BattleManager1.Instance.Spawn(new Vector3Int((int)x,(int)y,0), name:name, Faction:Faction, AIorNot:AIorNot, futurename:futurename);
        //SessionManager.Instance.SpawnToHost(new Vector3Int((int)x,(int)y,0), name:name, Faction:Faction, AIorNot:AIorNot, futurename:futurename);
    }
    [Rpc(SendTo.Server)]
    void SendYourSpawnClientRpc(float x, float y, string name, string Faction, bool AIorNot, string futurename)
    {
        BattleManager1.Instance.Spawn(new Vector3Int((int)x,(int)y,0), name:name, Faction:Faction, AIorNot:AIorNot, futurename:futurename);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SendYourCritterServerRpc(int i, string name, double value, double value2, double value3)
    {
        var a = BattleManager1.Instance.enemylist.Find(x => x.name == name);
        //var a = BattleManager1.Instance.enemylist[i];
        if(a != null)
        {
            a.transform.position = new Vector2((float)value, (float)value2);
            if(a.GetComponent<CritterHolder>().population != (int)value3)
            {
                //a.GetComponent<CritterHolder>().ReducePopulation(0);
                a.GetComponent<CritterHolder>().population = (int)value3;
                a.GetComponent<CritterHolder>().ReducePopulation(0);
            }
            if(a.GetComponent<CritterHolder>().population < 0)
            {
                a.SetActive(false);
            }
        }
    }

    [Rpc(SendTo.Server)]
    void SendYourCritterClientRpc(int i, string name, double value, double value2, double value3)
    {
        var a = BattleManager1.Instance.enemylist.Find(x => x.name == name);
        //var a = BattleManager1.Instance.enemylist[i];
        if(a != null)
        {
            a.transform.position = new Vector2((float)value, (float)value2);
            if(a.GetComponent<CritterHolder>().population != (int)value3)
            {
                
                a.GetComponent<CritterHolder>().population = (int)value3;
                a.GetComponent<CritterHolder>().ReducePopulation(0);
            }
            if(a.GetComponent<CritterHolder>().population < 0)
            {
                a.SetActive(false);
            }
            //a.GetComponent<CritterHolder>().population = (int)value3;
        }
    }

    [Rpc(SendTo.Server)]
    void ServerOnlyRpc(int value, ulong sourceNetworkObjectId)
    {
        Debug.Log($"Server Received the RPC #{value} on NetworkObject #{sourceNetworkObjectId}");

        ClientAndHostRpc(value, sourceNetworkObjectId);
    }
}


