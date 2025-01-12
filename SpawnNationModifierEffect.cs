using System.Collections;
using System.Collections.Generic;
using UnityEngine; 
using System.Text.RegularExpressions;

[System.Serializable]
[CreateAssetMenu(menuName = "Effects/SpawnNationalModifier")]
public class SpawnNationModifierEffect : BaseEffect
{
    public ProvinceModifier Modifier;
    public bool PlayerNation = false;

    public override void Execute()
    {
        if(nation != "")
        {

            Owners.Instance.nationlist.Find(x => x.name == nation).AddModifier(Modifier);
        }
    }
    public override void GrabRandomTarget()
    {
        if(nation == "")
        {
            if(PlayerNation)
            {
                var a = new List<Nation>();
                foreach (var item in Owners.Instance.nationlist)
                {
                    if(item != Owners.Instance.CallPlayer())
                    {
                        a.Add(item);
                    }
                }
                nation = a[Random.Range(0,a.Count)].name;
                return;
            }
            nation = Owners.Instance.nationlist[Random.Range(0,Owners.Instance.nationlist.Count)].name;
        }
    }
    public override string GrabTooltip()
    {
        string newstring = tooltip;
        newstring = Regex.Replace(newstring, "<province>", province);
        newstring = Regex.Replace(newstring, "<nation>", nation);
        newstring = Regex.Replace(newstring, "<modifier>", "'" + Modifier.name + "'");
        newstring = Regex.Replace(newstring, "<duration>", (Modifier.Enddate/50).ToString());
        
        newstring += "\n>" + Modifier.description;

        return newstring;
    }
}
