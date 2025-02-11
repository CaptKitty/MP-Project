using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[System.Serializable]
[CreateAssetMenu(menuName = "BaseActions/SendTroops")]
public class SendTroops : GAction {
    public override float GrabCost()
    {
        return cost;
    }
    public override bool Execute(NationalBrain brainy)
    {
        var YourProvince = new List<Province>();
        var SetTargetProvinces = new List<Province>();
        foreach (var item in Owners.Instance.provincelist)
        {
            if(item.nation.name == brainy.nation && item.troops > 1)
            {
                //Debug.Log(item.nation.name + " " + brainy.nation + " " + item.troops);
                YourProvince.Add(item);
            }
        }
        if(YourProvince.Count > 0)
        {
            var OriginProvince = YourProvince[Random.Range(0,YourProvince.Count)]; //YourProvince[0];
            foreach (var item in Owners.Instance.nationlist)
            {
                if(item.GrabDiplomaticStatus(brainy.nation) == "war")
                {
                    foreach (var itemsss in Owners.Instance.statelist)
                    {
                        if(itemsss.nation.name == item.name)
                        {
                            foreach (var items in itemsss.provincelist)
                            {
                                if(items.nation.name == item.name)
                                {
                                    foreach (var itemss in items.GrabAdjacentProvinces())
                                    {
                                        if(itemss.nation.name == brainy.nation)
                                        {
                                            SetTargetProvinces.Add(items);
                                        }
                                    }
                                }
                            }
                        }
                        
                        
                        // if(items.nation.name == item.name)
                        // {
                        //     SetTargetProvinces.Add(items);
                        // }
                    }
                }
            }
            //Debug.Log(SetTargetProvinces.Count);
            if(SetTargetProvinces.Count > 0)
            {
                //Debug.LogError(OriginProvince.name);
                var TargetProvince = SetTargetProvinces[0];//SetTargetProvinces[Random.Range(0,SetTargetProvinces.Count)];
                var currentdistance = (OriginProvince.position - TargetProvince.position).magnitude;
                foreach (var item in SetTargetProvinces)
                {
                    var distance = (item.position - OriginProvince.position).magnitude;
                    if(distance < currentdistance)
                    {
                        TargetProvince = item;
                        currentdistance = distance;
                        //Debug.LogError(distance + " " + OriginProvince.name);
                    }
                }
                //var TargetProvince = SetTargetProvinces[Random.Range(0,SetTargetProvinces.Count)];

                //Debug.Log(TargetProvince);
                foreach (var RPC in TestRelay.Instance.PlayerObjects)
                {
                    if(RPC.GetComponent<NetworkObject>().IsLocalPlayer)
                    {
                        RPC.GetComponent<RpcTest>().SendTroops(OriginProvince.name, TargetProvince.name, OriginProvince.nation.name, troopcount:(int)(OriginProvince.troops/2));
                        OriginProvince.AddTroops((int)-(OriginProvince.troops/2));
                    }
                }
                return true;
            }
            
        }
        return false;
    }
    public override bool SetTarget(Nation nation = null)
    {
        return false;
    }

    public override bool PrePerform() {

        // // Get a free cubicle
        // target = inventory.FindItemWithTag("Cubicle");
        // // Check that we did indeed get a cubicle
        // if (target == null)
        //     // No cubicle so return false
        //     return false;
        // // All good
        return true;
    }

    public override bool PostPerform() {

        // // Add a new state "TreatingPatient"
        // GWorld.Instance.GetWorld().ModifyState("TreatingPatient", 1);
        // // Give back the cubicle
        // GWorld.Instance.AddCubicle(target);
        // // Remove the cubicle from the list
        // inventory.RemoveItem(target);
        // // Give the cubicle back to the world
        // GWorld.Instance.GetWorld().ModifyState("FreeCubicle", 1);
        
        return true;
    }
}
