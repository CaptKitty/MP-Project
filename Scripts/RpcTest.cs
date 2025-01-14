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
    public void OnDisable()
    {
        TestRelay.Instance.PlayerObjects.Remove(this.gameObject);
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

    public void HandleUpdate()
    {
        
        //Everyone is the Server's bitch.
        if(IsServer)
        {
            Owners.Instance.ServerUpdateHandler();
            //SendUpdateServerRpc();
        }
    }
    [Rpc(SendTo.ClientsAndHost)]
    public void AddProvinceModifierServerRpc(string modifiername, string provincename)
    {
        Owners.Instance.provincelist.Find(x => x.name == provincename).AddLocalModifier(modifiername);
    }
    [Rpc(SendTo.ClientsAndHost)]
    public void AddNationModifierServerRpc(string modifiername, string nationname)
    {
        Owners.Instance.nationlist.Find(x => x.name == nationname).AddNationalModifier(modifiername);
    }
    [Rpc(SendTo.ClientsAndHost)]
    public void RemoveNationModifierServerRpc(string modifiername, string nationname)
    {
        Owners.Instance.nationlist.Find(x => x.name == nationname).RemoveNationalModifier(modifiername);
    }
    [Rpc(SendTo.ClientsAndHost)]
    public void SendCityUpdateServerRpc(string provincename, string nationname, int troops)
    {
        //Owners.Instance.provincedict[provincename].nation = Owners.Instance.nationdict[nationname];
        //Owners.Instance.provincedict[provincename].troops = troops;

        Mapshower.Instance.ChangeProvinceOwner(provincename, nationname);
        Owners.Instance.provincelist.Find(x => x.name == provincename).troops = troops;
        Owners.Instance.provincelist.Find(x => x.name == provincename).SetTroopsMarker();
    }
    [Rpc(SendTo.ClientsAndHost)]
    public void UpdateTroopsMovementServerRpc()
    {
        Owners.Instance.HandleMovement();
    }
    [Rpc(SendTo.ClientsAndHost)]
    public void UpdateTroopsServerRpc(string name)
    {
        Owners.Instance.UpdateCount(name);
    }
    [Rpc(SendTo.ClientsAndHost)]
    public void KillTroopsServerRpc(string name)
    {
        Owners.Instance.Kill(name);
    }
    [Rpc(SendTo.ClientsAndHost)]
    public void SetSecondsPerTurnServerRpc(float time)
    {
        Owners.Instance.TimeScale = time;
    }


    public void SendTroops(string origin, string province, string owner)
    {
        int a = Random.Range(0,1000000);
                
        int b = Owners.Instance.provincelist.Find(x => x.name == origin).troops;
        int count = b/2;
        if(IsServer)
        {
            SendSendTroopsServerRpc(origin, province, owner, a, count);
        }
        else
        {
            SendSendTroopsServerRpc(origin, province, owner, a, count);
            //SendSendTroopsClientRpc(origin, province, owner, a);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SendSendTroopsServerRpc(string origin, string province, string owner, int a, int count)
    {
        Mapshower.Instance.SendTroops(origin, province, owner, a, count);
    }
    [Rpc(SendTo.Server)]
    void SendSendTroopsClientRpc(string origin, string province, string owner, int a, int count)
    {
        Mapshower.Instance.SendTroops(origin, province, owner, a, count);
    }

    public void ChangeProvinceOwner(string province, string owner)
    {   
        SendChangeProvinceOwnerServerRpc(province, owner);
        SendChangeProvinceOwnerClientRpc(province, owner);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SendChangeProvinceOwnerServerRpc(string province, string owner)
    {
        Mapshower.Instance.ChangeProvinceOwner(province, owner);
    }
    [Rpc(SendTo.Server)]
    void SendChangeProvinceOwnerClientRpc(string province, string owner)
    {
        Mapshower.Instance.ChangeProvinceOwner(province, owner);
    }

    //ClientSendsThis
    public void SendFaction()
    {   
        if(IsServer)
        {
            SendYourFactionServerRpc(SessionManager.Instance.HostFaction.name, SessionManager.Instance.ClientFaction.name);
        }
        else
        {
            SendYourFactionClientRpc(SessionManager.Instance.HostFaction.name, SessionManager.Instance.ClientFaction.name);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SendYourFactionServerRpc(string host, string client)
    {
        SessionManager.Instance.ClientChangePlayerFaction(host);
        SessionManager.Instance.ClientChangeEnemyFaction(client);
    }
    [Rpc(SendTo.Server)]
    void SendYourFactionClientRpc(string host, string client)
    {
        SessionManager.Instance.ClientChangePlayerFaction(host);
        SessionManager.Instance.ClientChangeEnemyFaction(client);   
    }

    public void ExecuteReset(bool testy = false)
    {   if(IsServer){SendYourResetServerRpc(testy);}
        else{SendYourResetClientRpc(testy);}}
    [Rpc(SendTo.ClientsAndHost)]
    void SendYourResetServerRpc(bool testy = false)
    {BattleManager1.Instance.ResetBattleField(testy);}
    [Rpc(SendTo.Server)]
    void SendYourResetClientRpc(bool testy = false)
    {BattleManager1.Instance.ResetBattleField(testy);}

    public void StartBattle()
    {   if(IsServer){SendYourStartBattleServerRpc();}
        else{SendYourStartBattleClientRpc();}}
    [Rpc(SendTo.ClientsAndHost)]
    void SendYourStartBattleServerRpc()
    {BattleManager1.Instance.StartFight();}
    [Rpc(SendTo.Server)]
    void SendYourStartBattleClientRpc()
    {BattleManager1.Instance.StartFight();}

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

        string futurename = RpcTest.Serverchecker.ServerCheck().ToString() + name + BattleManager1.Instance.enemylist.Count;//Random.Range(0,1000);

        string Host = "Client";
        if(RpcTest.Serverchecker.ServerCheck())
        {
            Host = "Host";
        }

        // //Debug.LogError(name);
        SendYourSpawnServerRpc(target.x, target.y, name, Faction:BattleManager1.Instance.Faction, AIorNot:ServerCheck(), futurename:futurename, ClientOrHost:Host);
        SendYourSpawnClientRpc(target.x, target.y, name, Faction:BattleManager1.Instance.Faction, AIorNot:ServerCheck(), futurename:futurename, ClientOrHost:Host);

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
    public void Spawn(Vector3Int target, string name = "null", bool AIorNot = false, string futurename = "null", string ClientOrHost = "Host")
    {
        SendYourSpawnServerRpc(target.x, target.y, name, Faction:BattleManager1.Instance.Faction, AIorNot:AIorNot, futurename:futurename, ClientOrHost:ClientOrHost);
        SendYourSpawnClientRpc(target.x, target.y, name, Faction:BattleManager1.Instance.Faction, AIorNot:AIorNot, futurename:futurename, ClientOrHost:ClientOrHost);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SendYourSpawnServerRpc(float x, float y, string name, string Faction, bool AIorNot, string futurename, string ClientOrHost = "Host")
    {
        BattleManager1.Instance.Spawn(new Vector3Int((int)x,(int)y,0), name:name, Faction:Faction, AIorNot:AIorNot, futurename:futurename, ClientOrHost:ClientOrHost);
        SessionManager.Instance.SpawnToSM(new Vector3Int((int)x,(int)y,0), name:name, Faction:Faction, AIorNot:AIorNot, futurename:futurename, ClientOrHost:ClientOrHost);
    }
    [Rpc(SendTo.Server)]
    void SendYourSpawnClientRpc(float x, float y, string name, string Faction, bool AIorNot, string futurename, string ClientOrHost = "Host")
    {
        BattleManager1.Instance.Spawn(new Vector3Int((int)x,(int)y,0), name:name, Faction:Faction, AIorNot:AIorNot, futurename:futurename, ClientOrHost:ClientOrHost);
        SessionManager.Instance.SpawnToSM(new Vector3Int((int)x,(int)y,0), name:name, Faction:Faction, AIorNot:AIorNot, futurename:futurename, ClientOrHost:ClientOrHost);
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


