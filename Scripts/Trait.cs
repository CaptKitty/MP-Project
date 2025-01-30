using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Trait/Base")]
public class Trait : ScriptableObject
{
    public string name;
    public Trait GrabTrait()
    {
        Trait traits = new Trait();
        traits.name = name;
        return traits;
    }
}