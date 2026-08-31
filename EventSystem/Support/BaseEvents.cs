using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Event")]
public class BaseEvents : ScriptableObject
{
    public enum EventScope { LegacyArmy, Nation }
    [Tooltip("National events are eligible for the faction-level event scheduler; legacy events remain army incidents.")]
    public EventScope scope = EventScope.LegacyArmy;
    public string Title;
    [TextArea(15, 20)]
    public string Message = "";
    public List<BaseTrigger> triggers = new List<BaseTrigger>();
    public bool HasTriggered = false;
    public Option initialOption;
    public List<Option> OptionList = new List<Option>();
    public bool Trigger()
    {
        return Trigger(null);
    }
    public bool Trigger(EventContext context)
    {
        foreach (var item in triggers)
        {
            if (item != null && !item.CanTrigger(context))
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
