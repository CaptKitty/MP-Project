using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class EventHolder : MonoBehaviour
{
    public BaseEvents thisevent;
    
    public void Awake()
    {
        // LoadEvent();
    }
    public void LoadEvent()
    {
        transform.GetChild(0).GetComponent<Text>().text = thisevent.Title;
        transform.GetChild(1).GetComponent<Text>().text = thisevent.Message;

        float i = 0;
        foreach (var Option in thisevent.OptionList)
        {
            GameObject NewButton = Instantiate(Resources.Load<GameObject>("Prefabs/Event/EventWindowButton"));
            NewButton.transform.SetParent(this.transform);
            NewButton.GetComponent<OptionHolder>().thisoption = Option;
            NewButton.transform.position = new Vector2(0, -165 + 50 * i);//new Vector2(200 * i, -300);
            NewButton.transform.GetChild(0).GetComponent<Text>().text = Option.Message;
            NewButton.GetComponent<Tooltip>().message = Option.Tooltip;
                     
            foreach (var item in Option.EffectList)
            {
                NewButton.GetComponent<Tooltip>().message += item.GrabTooltip() + "\n";
            }

            // if(Option.trigger != null)
            // {
            //     if(!Option.trigger.CanTrigger())
            //     {
            //         NewButton.GetComponent<Button>().enabled = !enabled;
                    
            //     }
            //     NewButton.GetComponent<Tooltip>().message = "This option is available if:\n" + Option.trigger.triggerdescription + "\n" + NewButton.GetComponent<Tooltip>().message;
            // }


            i++;
        }
        if(thisevent != null && thisevent.initialOption != null && thisevent.initialOption.EffectList != null && thisevent.initialOption.EffectList.Count != 0)
        {
            GameObject NewButton = Instantiate(Resources.Load<GameObject>("Prefabs/Event/EventWindowButton"));
            NewButton.transform.SetParent(this.transform);
            NewButton.GetComponent<OptionHolder>().thisoption = thisevent.initialOption;
            NewButton.GetComponent<OptionHolder>().OnClickThis();
        }
    }
}