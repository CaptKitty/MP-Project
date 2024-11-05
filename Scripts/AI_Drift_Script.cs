using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "AIScript/Drift")]
public class AI_Drift_Script : base_AI_Script
{
    public float speed = 0.25f;
    private float Timer = 0;
    private Vector3 direction;
    public GameObject Sleepy;
    public GameObject SleepyActive;
    public override base_AI_Script Init()
    {
        var potato = new AI_Drift_Script();
        potato.Sleepy = Sleepy;
        potato.speed = speed;
        return potato;
    }
    public override void Execute(CritterHolder critter)
    {
        critter.gameObject.transform.position += direction * Time.deltaTime * speed;
        if(Timer == 0)
        {
            Timer = Time.time + 1;
            direction = new Vector3(Random.Range(-1,1),Random.Range(-1,1),0);
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
