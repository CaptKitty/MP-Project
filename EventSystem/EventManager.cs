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
        TriggerEvent(name, EventContext.ForNation(nation));
    }

    public void TriggerEvent(string name, EventContext context)
    {
        BaseEvents potato = Instantiate(Resources.Load<BaseEvents>("EventGroup/" + name));
        if (potato == null) return;
        if (context == null) context = new EventContext();
        if (!potato.Trigger(context)) return;

        foreach (var item in potato.OptionList)
        {
            for (int i = 0; i < item.EffectList.Count; i++)
            {
                if (item.EffectList[i] == null) continue;
                item.EffectList[i] = Instantiate(item.EffectList[i]);
                item.EffectList[i].GrabRandomTarget(context);
            }
        }

        GameObject potatoes = Instantiate(Resources.Load<GameObject>("EventSupports/EventWindow"));
        potatoes.GetComponent<EventHolder>().thisevent = potato;
        potatoes.GetComponent<EventHolder>().context = context;
        potatoes.GetComponent<EventHolder>().LoadEvent();
        potatoes.transform.SetParent(this.transform); //this.transform.GetChild(2).transform);
        potatoes.transform.localPosition = new Vector3(1000,500,0);
        potatoes.transform.localScale = new Vector2(1,1);
        
        return;
    }

    public bool TriggerRandomNationalEvent(Nation nation)
    {
        if (nation == null) return false;
        EventContext context = EventContext.ForNation(nation.name);
        BaseEvents[] all = Resources.LoadAll<BaseEvents>("EventGroup/");
        List<BaseEvents> viable = new List<BaseEvents>();
        foreach (BaseEvents candidate in all)
            if (candidate != null && candidate.scope == BaseEvents.EventScope.Nation && candidate.Trigger(context))
                viable.Add(candidate);
        if (viable.Count == 0) return false;
        TriggerEvent(viable[Random.Range(0, viable.Count)].name, context);
        return true;
    }
}
