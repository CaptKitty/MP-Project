using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "TerrainFlagModifier/Basic")]
public class TerrainFlagModifier : ScriptableObject
{
    public string flag;
    [TextArea(10,20)]
    public string tooltip;
    public Modifier modifier;
    public Sprite icon;
}