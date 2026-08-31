using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class OptionHolder : MonoBehaviour
{
    public Option thisoption = new Option();
    public string eventName;
    public int optionIndex;
    public EventContext context;
    
    public void OnClickThis()
    {
        try
        {
            ToolTipManager._instance.HideToolTip();
        }
        catch{}

        if(CampaignNetworkPlayer.Local != null && CampaignNetworkPlayer.Local.IsSpawned && !string.IsNullOrEmpty(eventName))
        {
            CampaignNetworkPlayer.Local.RequestEventOption(eventName, optionIndex,
                context != null ? context.nationName : string.Empty);
        }
        else if(thisoption != null && thisoption.EffectList != null)
        {
            foreach (var item in thisoption.EffectList)
            {
                //Debug.Log(thisoption);
                if(item != null)
                {
                    item.Execute(context);
                }
            }
        }

        
        
        Destroy(transform.parent.gameObject);
    }
}
