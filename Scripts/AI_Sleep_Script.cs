using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "AIScript/Sleepy")]
public class AI_Sleep_Script : base_AI_Script
{
    public GameObject Sleepy;
    public GameObject SleepyActive;
    private float Timer = 0;
    public override base_AI_Script Init()
    {
        var potato = new AI_Sleep_Script();
        potato.Sleepy = Sleepy;
        return potato;
    }
    public override void Execute(CritterHolder critter)
    {
        if(Timer == 0)
        {
            Timer = Time.time + 2;
        }
        if(!SleepyActive)
        {
            GameObject potato = Instantiate(Sleepy,critter.gameObject.transform);
            SleepyActive = potato;
        }
        if(Time.time > Timer)
        {
            Destroy(SleepyActive);
            critter.GrabNewScript();
        }
    }
}
