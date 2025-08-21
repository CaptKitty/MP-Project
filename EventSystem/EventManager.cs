using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EventManager : MonoBehaviour
{
    public static EventManager eventManager;
    public BaseEvents thisevent;
    public void Awake()
    {
        if (EventManager.eventManager == null)
        {
            eventManager = this;
        }
    }
    public void TriggerEvent(string name, string nation = null, bool Fixed = false)
    {       
        BaseEvents potato = Instantiate(Resources.Load<BaseEvents>("EventGroup/" + name));

        foreach (var item in potato.OptionList)
        {
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    item.EffectList[i] = Instantiate(item.EffectList[i]);
                }
            }
            catch{}
            foreach (var items in item.EffectList)
            {
                items.GrabRandomTarget();
            }
        }

        GameObject potatoes = Instantiate(Resources.Load<GameObject>("EventSupports/EventWindow"));
        potatoes.GetComponent<EventHolder>().thisevent = potato;
        potatoes.GetComponent<EventHolder>().LoadEvent(nation:nation);
        potatoes.transform.SetParent(this.transform.GetChild(2).transform);
        potatoes.transform.position = new Vector3(0,-50,0);
        potatoes.transform.localScale = new Vector2(1,1);
        
        return;
    }
}
