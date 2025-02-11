using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Trait/Base")]
public class Trait : ScriptableObject
{
    public string name;
    public virtual Trait GrabTrait()
    {
        Trait traits = new Trait();
        traits.name = name;
        return traits;
    }
    public virtual List<EcoData> GrabOutput(Jobs jobs)
    {
        return new List<EcoData>();
    }
}