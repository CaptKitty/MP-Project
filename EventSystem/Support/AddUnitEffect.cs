using System.Collections;
using System.Collections.Generic;
using UnityEngine; 
using System.Text.RegularExpressions;

[System.Serializable]
[CreateAssetMenu(fileName = "Effects/AddUnit")]
public class AddUnitEffect : BaseEffect
{
    public string unitname = "";
    public int amount = 1;
    public override void Execute()
    {
        FieldArmyHolder.PlayerFieldArmy.AddTroop(name:unitname, amount:amount);
    }
    public override void GrabRandomNation(string ownernation = "")
    {
        if(ownernation == "")
        {
            nation = Owners.Instance.nationlist[Random.Range(0,Owners.Instance.nationlist.Count)].name;
        }
    }
    public override void GrabRandomTarget(){}
    public override string GrabTooltip()
    {
        string newstring = tooltip;
        newstring = Regex.Replace(newstring, "<province>", province);
        newstring = Regex.Replace(newstring, "<nation>", nation);

        return newstring;
    }
}
