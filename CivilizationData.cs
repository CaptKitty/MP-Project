using System;
using UnityEngine;

[Serializable]
public sealed class CivilizationClassBaseline
{
    [Min(0)] public float citizen = 15f;
    [Min(0)] public float tribesman = 15f;
    [Min(0)] public float freemen = 40f;
    [Min(0)] public float elite = 15f;
    [Min(0)] public float enslaved = 15f;

    public float Weight(SocioEconomicClass socialClass)
    {
        switch (SocioEconomicClassRules.Normalize(socialClass))
        {
            case SocioEconomicClass.Citizen: return citizen;
            case SocioEconomicClass.Tribesman: return tribesman;
            case SocioEconomicClass.Elite: return elite;
            case SocioEconomicClass.Enslaved: return enslaved;
            default: return freemen;
        }
    }
}

[CreateAssetMenu(menuName = "Nation Identity/Civilization")]
public class CivilizationData : ScriptableObject
{
    [Tooltip("Default name for this civilization's governing assembly.")]
    public string assemblyName;
    [Tooltip("Desired holding-class weights. They are normalized, so they do not have to total exactly 100.")]
    public CivilizationClassBaseline classBaseline = new CivilizationClassBaseline();
    public NationContentLayer content = new NationContentLayer();
}
