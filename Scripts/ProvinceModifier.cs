using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "ProvinceModifier")]
public class ProvinceModifier : ScriptableObject
{
    public int BaseTroops = 0;
    public float BaseTroopsModifier = 1;
    public int DefensiveDice = 0;

    public int BonusSpawns = 0;
}
