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

    public GameObject Aura;

    public void LoadAura(GameObject potato)
    {
        if(Aura == null)
        {
            return;
        }
        Instantiate(Aura, potato.transform);
    }
}
