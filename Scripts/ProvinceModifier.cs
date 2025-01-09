using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "ProvinceModifier")]
public class ProvinceModifier : ScriptableObject
{
    public int BaseTroops = 0; //GrowthCaph
    public float BaseTroopsModifier = 1; //GrowthCaphModifier
    public int DefensiveDice = 0; //D6+x on defence
    public int OffensiveDice = 0; //D6+x on offence
    public int BonusSpawns = 0; // 1+x unit spawns per 5 ticks (1 tick/second)
}
