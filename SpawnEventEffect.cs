using System.Collections;
using System.Collections.Generic;
using UnityEngine; 
using System.Text.RegularExpressions;
using Unity.Netcode;

[System.Serializable]
[CreateAssetMenu(menuName = "Effects/SpawnEventEffect")]
public class SpawnEventEffect  : BaseEffect
{
    public string HomeCountry;
    public int troopcount;
    public bool PlayerNation = true;
    public BaseEvents EventToTrigger;

    public override void Execute()
    {
        General_Manager.Instance.TriggerEvent(EventToTrigger.name, nation: nation);
    }
    public override void GrabRandomTarget()
    {
        if(HomeCountry == "")
        {
            HomeCountry = Owners.Instance.nationlist[Random.Range(0,Owners.Instance.nationlist.Count)].name;
        }
        if(province == "")
        {
            if(PlayerNation)
            {
                var a = new List<Province>();
                foreach (var item in Owners.Instance.provincelist)
                {
                    if(item.nation == Owners.Instance.CallPlayer())
                    {
                        a.Add(item);
                    }
                }
                province = a[Random.Range(0,a.Count)].name;
                return;
            }
            province = Owners.Instance.provincelist[Random.Range(0,Owners.Instance.provincelist.Count)].name;
        }
    }
    public override string GrabTooltip()
    {
        string newstring = tooltip;
        newstring = Regex.Replace(newstring, "<province>", province);
        newstring = Regex.Replace(newstring, "<nation>", nation);
        newstring = Regex.Replace(newstring, "<troopcount>", troopcount.ToString());
        newstring = Regex.Replace(newstring, "<homecountry>", HomeCountry);

        return newstring;
    }
}
