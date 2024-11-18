using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Modifier")]
public class Modifier : ScriptableObject
{
    public double base_combatdistance = 0;
    public double base_speed = 0;
    public int base_attack = 0;
    public double base_attacktime = 0;
    public double combatdistance_modifier = 1;
    public double speed_modifier = 1;
    public double attack_modifier = 1;
    public double attacktime_modifier = 1;
    public double duration;
    public double EndDuration;
    public GameObject Aura;
    public bool StunEffect = false;
    public GameObject potato;
    private GameObject thisObject;

    public void SetTimer()
    {
        EndDuration = Time.time + duration;
    }

    public void LoadAura()
    {
        if(Aura == null)
        {
            return;
        }
        thisObject = Instantiate(Aura, potato.transform);
    }
    public void DestroyAura()
    {
        try
        {
            for (int i = 0; i < 10; i++)
            {
                if(potato.transform.GetChild(i).GetComponent<SpriteRenderer>().sprite == Aura.GetComponent<SpriteRenderer>().sprite)
                {
                    Destroy(potato.transform.GetChild(i).gameObject);
                    return;
                }
            }
            Destroy(thisObject);
        }
        catch{}
    }
    public void DestroyThis()
    {
        DestroyAura();
        Destroy(this);
    }
}
