using System.Collections;
using System.Collections.Generic;
using UnityEngine; 
using System.Text.RegularExpressions;

[System.Serializable]
[CreateAssetMenu(fileName = "Effects/RemoveFlag")]
public class RemoveArmyFlag : BaseEffect
{
    public string flagToAdd;
    public override void Execute()
    {
        try
        {
            FieldArmyHolder.PlayerFieldArmy.flaglist.Remove(flagToAdd);
        }
        catch
        {
            Debug.LogError("We tried to remove " + flagToAdd + " but it wasn't present.");
        }
        
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
