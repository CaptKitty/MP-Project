using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class General_Manager : MonoBehaviour
{
    public static General_Manager Instance;

    public void Awake()
    {
        Instance = this;
    }
    public void Update()
    {
        if(Input.GetKeyDown("5"))
        {
            TriggerEvent("PotatoTime");
        }
    }

    public void TriggerEvent(string name)
    {
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
        potatoes.GetComponent<EventHolder>().LoadEvent();
        potatoes.transform.SetParent(this.transform.GetChild(2).transform);
        potatoes.transform.position = new Vector3(0,0,0);
        potatoes.transform.localScale = new Vector2(1,1);
        
        return;
    }
}
