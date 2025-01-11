using System.Collections;
using System.Collections.Generic;
using UnityEngine; 
using System.Text.RegularExpressions;

[System.Serializable]
[CreateAssetMenu(fileName = "Effects/Base")]
public class BaseEffect : ScriptableObject
{
    public string tooltip;
    public string province = ""; 
    public string nation = ""; 
    public virtual void Execute(){}
    public virtual void GrabRandomProvince(string ownernation = "")
    {
        if(ownernation == "")
        {
            province = Owners.Instance.provincelist[Random.Range(0,Owners.Instance.provincelist.Count)].name;
        }
    }
    public virtual void GrabRandomNation(){}
    public virtual string GrabTooltip()
    {
        string newstring = tooltip;
        newstring = Regex.Replace(newstring, "<province>", province);
        newstring = Regex.Replace(newstring, "<nation>", nation); 
        return newstring;
    }
}
