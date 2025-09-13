using UnityEngine.AI;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
// [CreateAssetMenu(menuName = "NationalAI/base")]
public class Nation_GAction : GAction
{
    public NationalBrain nationalbrainy;
    public float Timer;
}
