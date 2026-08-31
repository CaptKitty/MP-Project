using System.Collections;
using System.Collections.Generic;
using UnityEngine; 
using System.Text.RegularExpressions;

[System.Serializable]
[CreateAssetMenu(fileName = "Effects/AddFlag")]
public class AddArmyFlag : BaseEffect
{
    public string flagToAdd;
    public override void Execute()
    {
        FieldArmyHolder.PlayerFieldArmy.flaglist.Add(flagToAdd);
    }
    public override void Execute(EventContext context)
    {
        FieldArmyHolder target = context != null ? context.ResolveArmy() : null;
        if (target != null && !target.flaglist.Contains(flagToAdd)) target.flaglist.Add(flagToAdd);
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
