using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class OptionHolder : MonoBehaviour
{
    public Option thisoption;
    
    public void OnClickThis()
    {
        try
        {
            ToolTipManager._instance.HideToolTip();
        }
        catch{}

        foreach (var item in thisoption.EffectList)
        {
            item.Execute();
        }
        
        Destroy(transform.parent.gameObject);
    }
}
