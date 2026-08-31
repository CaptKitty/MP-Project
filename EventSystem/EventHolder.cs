using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class EventHolder : MonoBehaviour
{
    public BaseEvents thisevent;
    public EventContext context;

    public void Awake()
    {
        Time.timeScale = 0;
        //GetComponent<Canvas>().worldCamera = Camera.main;
    }   
    public void OnDestroy()
    {
        Time.timeScale = 1;
    }
    public void LoadEvent(string nation = null)
    {
        if (context == null) context = EventContext.ForNation(nation);
        Nation targetNation = context.ResolveNation();
        //Debug.Log(nation);
        //Debug.Log(Owners.Instance.CallPlayer().name);
        //AI-Time
        if (targetNation != null && !targetNation.IsPlayer && !CampaignNetworkPlayer.IsNationPlayerControlled(targetNation.name))
        {
            Option aiOption = thisevent.OptionList.Find(option => option != null &&
                (option.trigger == null || option.trigger.CanTrigger(context)));
            if (aiOption == null) { Destroy(gameObject); return; }
            foreach (var item in aiOption.EffectList)
            {
                if (item != null) item.Execute(context);
            }
            Destroy(gameObject);
            return;
        }

        transform.GetChild(0).GetComponent<Text>().text = thisevent.Title;
        transform.GetChild(1).GetComponent<Text>().text = thisevent.Message;

        float i = 0;
        int optionIndex = 0;
        foreach (var Option in thisevent.OptionList)
        {
            GameObject NewButton = Instantiate(Resources.Load<GameObject>("EventSupports/EventWindowButton"));
            NewButton.transform.SetParent(this.transform);
            NewButton.GetComponent<OptionHolder>().thisoption = Option;
            NewButton.GetComponent<OptionHolder>().eventName = thisevent.name.Replace("(Clone)", "");
            NewButton.GetComponent<OptionHolder>().optionIndex = optionIndex;
            NewButton.GetComponent<OptionHolder>().context = context;
            NewButton.transform.localPosition = new Vector2(0, -215 + 50 * i);//new Vector2(200 * i, -300);
            NewButton.transform.GetChild(0).GetComponent<Text>().text = Option.Message;
            NewButton.GetComponent<Tooltip>().message = Option.Tooltip;

            foreach (var item in Option.EffectList)
            {
                NewButton.GetComponent<Tooltip>().message += item.GrabTooltip() + "\n";
            }

            if(Option.trigger != null)
            {
                if(!Option.trigger.CanTrigger(context))
                {
                    NewButton.GetComponent<Button>().enabled = !enabled;

                }
                NewButton.GetComponent<Tooltip>().message = "<color=red>This option is available if:" + Option.trigger.triggerdescription + "</color>\n" + NewButton.GetComponent<Tooltip>().message;
            }


            i++;
            optionIndex++;
        }
        if (thisevent != null && thisevent.initialOption != null && thisevent.initialOption.EffectList != null && thisevent.initialOption.EffectList.Count != 0)
        {
            Instaclick();
            // GameObject NewButton = Instantiate(Resources.Load<GameObject>("Prefabs/Event/EventWindowButton"));
            // NewButton.transform.SetParent(this.transform);
            // NewButton.GetComponent<OptionHolder>().thisoption = thisevent.initialOption;
            // NewButton.GetComponent<OptionHolder>().OnClickThis();
        }
    }
    public void Instaclick()
    {
        GameObject NewButton = Instantiate(Resources.Load<GameObject>("EventSupports/EventWindowButton"));
        if (NewButton == null) return;
        NewButton.transform.SetParent(this.transform);
        NewButton.GetComponent<OptionHolder>().thisoption = thisevent.initialOption;
        NewButton.GetComponent<OptionHolder>().context = context;
        NewButton.GetComponent<OptionHolder>().OnClickThis();
    }
}
