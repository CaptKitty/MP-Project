using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "FactionTheme/Basic")]
public class FactionTheme : ScriptableObject
{
    public Sprite TooltipBird;
    public List<Sprite> Beardlist = new List<Sprite>();
    public Sprite GrabViableBeard()
    {
        return Beardlist[Random.Range(0,Beardlist.Count)];
    }
}