using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Event")]
public class BaseEvents : ScriptableObject
{
    public string Title;
    [TextArea(15, 20)]
    public string Message = "";
    public List<BaseTrigger> triggers = new List<BaseTrigger>();
    public bool HasTriggered = false;
    public Option initialOption;
    public List<Option> OptionList = new List<Option>();
    public bool Trigger()
    {
        foreach (var item in triggers)
        {
            if (!item.CanTrigger())
            {
                return false;
            }
        }
        return true;
    }
}
[System.Serializable]   
public class Option 
{
    public string Message;
    public string Tooltip;
    public BaseTrigger trigger;
    public List<BaseEffect> EffectList = new List<BaseEffect>();
}
