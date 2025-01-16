using System.Collections;
using System.Collections.Generic;
using UnityEngine; 
using System.Text.RegularExpressions;
using Unity.Netcode;

[System.Serializable]
[CreateAssetMenu(menuName = "Effects/SpawnArmyModifier")]
public class SpawnArmyEffect  : BaseEffect
{
    public string HomeCountry;
    public int troopcount;
    public bool PlayerNation = true;

    public override void Execute()
    {
        if(province != "" && nation != "")
        {
            foreach (var RPC in TestRelay.Instance.PlayerObjects)
            {
                if(RPC.GetComponent<NetworkObject>().IsLocalPlayer)
                {
                    RPC.GetComponent<RpcTest>().SendTroops(province, province, HomeCountry, troopcount, SpawnNewArmy: true);
                }
            }
        }
    }
    public override void GrabRandomTarget()
    {
        if(province == "")
        {
            // if(PlayerNation)
            // {
            //     var a = new List<Province>();
            //     foreach (var item in Owners.Instance.provincelist)
            //     {
            //         if(item.nation == Owners.Instance.CallPlayer())
            //         {
            //             a.Add(item);
            //         }
            //     }
            //     province = a[Random.Range(0,a.Count)].name;
            //     return;
            // }
            var b = new List<Province>();
            foreach (var item in Owners.Instance.provincelist)
            {
                if(item.nation.name == nation)
                {
                    b.Add(item);
                }
            }
            if(b.Count > 0)
            {
                province = b[Random.Range(0,b.Count)].name;
                return;
            }
            
            //province = Owners.Instance.provincelist[Random.Range(0,Owners.Instance.provincelist.Count)].name;
        }
        province = Owners.Instance.provincelist[Random.Range(0,Owners.Instance.provincelist.Count)].name;
    }
    public override string GrabTooltip()
    {
        string newstring = tooltip;
        newstring = Regex.Replace(newstring, "<province>", province);
        newstring = Regex.Replace(newstring, "<nation>", nation);
        newstring = Regex.Replace(newstring, "<troopcount>", troopcount.ToString());
        newstring = Regex.Replace(newstring, "<homecountry>", nation);

        return newstring;
    }
}
