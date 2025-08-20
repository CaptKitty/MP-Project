using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class OptionHolder : MonoBehaviour
{
    public Option thisoption = new Option();
    
    public void OnClickThis()
    {
        try
        {
            ToolTipManager._instance.HideToolTip();
        }
        catch{}

        if(thisoption != null && thisoption.EffectList != null)
        {
            foreach (var item in thisoption.EffectList)
            {
                Debug.Log(thisoption);
                if(item != null)
                {
                    item.Execute();
                }
            }
        }

        
        
        Destroy(transform.parent.gameObject);
    }
}
