using System.Collections;
using System.Collections.Generic;
using UnityEngine; 
using System.Text.RegularExpressions;

[System.Serializable]
[CreateAssetMenu(menuName = "Effects/SpawnProvinceModifier")]
public class SpawnProvinceModifierEffect : BaseEffect
{
    public ProvinceModifier Modifier;
    public bool PlayerNation = true;

    public override void Execute()
    {
        if(province != "")
        {
            Owners.Instance.provincelist.Find(x => x.name == province).AddModifier(Modifier);
        }
    }
    public override void GrabRandomTarget()
    {
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
        newstring = Regex.Replace(newstring, "<modifier>", "'" + Modifier.name + "'");
        newstring = Regex.Replace(newstring, "<duration> ", (Modifier.Enddate/50).ToString());

        
        
        newstring += "\n>" + Modifier.description;

        return newstring;
    }
}
