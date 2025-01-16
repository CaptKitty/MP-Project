using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "ProvinceModifier")]
public class ProvinceModifier : ScriptableObject
{
    public string description = "";
    public int Enddate = 0;

    public int BaseTroops = 0; //GrowthCaph
    public float BaseTroopsModifier = 1; //GrowthCaphModifier

    //Combat
    public int DefensiveDice = 0; //D6+x on defence
    public int OffensiveDice = 0; //D6+x on offence

    public int OrderDice = 0; //D6+x on high troop count
    public int ChaosDice = 0; //D6+x on low troop count

    public int BonusSpawns = 0; // 1+x unit spawns per 5 ticks (1 tick/second)

    public int BonusCombatWidth = 0;
    public float SpeedModifier = 1;
    
    public ProvinceModifier Init()
    {
        return Instantiate(this);
    }
}
