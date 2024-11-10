using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Campaign")]
public class Campaign : ScriptableObject
{
    public List<SpawnBait> Layout1, Layout2, Layout3, Layout4, Layout5;
}
