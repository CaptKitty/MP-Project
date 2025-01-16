using System.Collections;
using System.Collections.Generic;
using UnityEngine; 
using System.Text.RegularExpressions;
using Unity.Netcode;

[System.Serializable]
[CreateAssetMenu(menuName = "Effects/SetDiplomaticEffect")]
public class SpawnDiplomaticEffect  : BaseEffect
{
    public string othercountry;
    public string newstatus;
    public bool PlayerNation = true;

    public override void Execute()
    {
        if(nation != "")
        {
            Owners.Instance.nationlist.Find(x => x.name == nation).SetDiplomaticStatus(othercountry, newstatus);
        }
    }
    public override string GrabTooltip()
    {
        string newstring = tooltip;
        newstring = Regex.Replace(newstring, "<province>", province);
        newstring = Regex.Replace(newstring, "<nation>", nation);
        newstring = Regex.Replace(newstring, "<othercountry>", othercountry);
        newstring = Regex.Replace(newstring, "<newstatus>", newstatus);

        return newstring;
    }
}
