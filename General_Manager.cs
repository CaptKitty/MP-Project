using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class General_Manager : MonoBehaviour
{
    public static General_Manager Instance;
    public List<BaseEvents> PossibleEvents = new List<BaseEvents>();
    public List<BaseEffect> DownLoadedEventData = new List<BaseEffect>();

    public void Awake()
    {
        Instance = this;
        var a = Resources.LoadAll<BaseEvents>("Events/");
        foreach (var item in a)
        {
            var b = (BaseEvents)item;
            PossibleEvents.Add(b);
        }
    }
    public void Update()
    {
        if(Input.GetKeyDown("5"))
        {
            //TriggerEvent("PotatoTime");
            var RealPossibleEvents = new List<BaseEvents>();
            foreach (var item in PossibleEvents)
            {
                if(item.trigger == null)
                {
                    //Debug.Log(item.name);
                    RealPossibleEvents.Add(item);
                    continue;
                }
                if(item.trigger.CanTrigger())
                {
                    //Debug.Log(item.name);
                    RealPossibleEvents.Add(item);
                }
            }
            TriggerEvent(RealPossibleEvents[Random.Range(0,RealPossibleEvents.Count)].name);
        }
    }

    public BaseEffect TranslateDataIntoEffects(string Effecttype = null, string targetnation = null, string originnation = null, string bonusdata = null, string bonusdata2 = null, string bonusdata3 = null)
    {
        var a = new BaseEffect();
        if(Effecttype == "SpawnDiplomaticEffect")
        {
            var b = new SpawnDiplomaticEffect();
            b.nation = targetnation;
            b.othercountry = originnation;
            b.newstatus = bonusdata;
            return b;
        }
        return a;
    }
    public void ExecuteEventData(string Title = null, string Description = null, string Option = null, string targetnation = null, string originnation = null, string bonusdata = null, string bonusdata2 = null, string bonusdata3 = null)
    {
        var a = new BaseEvents();
        a.Title = Title;
        a.Message = Description;
        var b = new Option();
        b.Message = Option;
        foreach (var item in DownLoadedEventData)
        {
            item.tooltip = bonusdata;
            b.EffectList.Add(Instantiate(item));
        }
        
        a.OptionList = new List<Option>();
        a.OptionList.Add(b);
        
        TriggerEvent(a, targetnation);
        DownLoadedEventData.Clear();
    }

    public void TriggerEvent(BaseEvents potato, string nation = null)
    {
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

        GameObject potatoes = Instantiate(Resources.Load<GameObject>("Prefabs/Event/EventWindow"));
        potatoes.GetComponent<EventHolder>().thisevent = potato;
        potatoes.GetComponent<EventHolder>().LoadEvent(nation:nation);
        potatoes.transform.SetParent(this.transform.GetChild(2).transform);
        potatoes.transform.position = new Vector3(0,0,0);
        potatoes.transform.localScale = new Vector2(1,1);
        return;
    }
    
    public void TriggerEvent(string name, string nation = null, bool Fixed = false)
    {
        if(Fixed == false)
        {
            foreach (var RPC in TestRelay.Instance.PlayerObjects)
            {
                Debug.Log(RPC.GetComponent<RpcTest>().PlayerNation + " + " + nation);
                if(RPC.GetComponent<RpcTest>().PlayerNation == nation)
                {
                    
                    RPC.GetComponent<RpcTest>().SetEventRpc(name, nation);
                    return;
                }
            }
        }
        


        BaseEvents potato = Instantiate(Resources.Load<BaseEvents>("Events/" + name));

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

        GameObject potatoes = Instantiate(Resources.Load<GameObject>("Prefabs/Event/EventWindow"));
        potatoes.GetComponent<EventHolder>().thisevent = potato;
        potatoes.GetComponent<EventHolder>().LoadEvent(nation:nation);
        potatoes.transform.SetParent(this.transform.GetChild(2).transform);
        potatoes.transform.position = new Vector3(0,0,0);
        potatoes.transform.localScale = new Vector2(1,1);
        
        return;
    }
}
