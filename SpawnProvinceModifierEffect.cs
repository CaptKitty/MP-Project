using System.Collections;
using System.Collections.Generic;
using UnityEngine; 
using System.Text.RegularExpressions;

[System.Serializable]
[CreateAssetMenu(fileName = "Effects/SpawnModifier")]
public class SpawnProvinceModifierEffect : BaseEffect
{
    public ProvinceModifier Modifier;
    
    public override void Execute()
    {
        if(province != "")
        {
            Owners.Instance.provincelist.Find(x => x.name == province).AddModifier(Modifier);
        }
    }
    public override string GrabTooltip()
    {
        string newstring = tooltip;
        newstring = Regex.Replace(newstring, "<province>", province);
        newstring = Regex.Replace(newstring, "<nation>", nation);
        newstring = Regex.Replace(newstring, "<modifier>", Modifier.name);
        

        newstring += "\n>" + Modifier.description;

        return newstring;
    }
}
