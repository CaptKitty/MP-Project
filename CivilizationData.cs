using UnityEngine;

[CreateAssetMenu(menuName = "Nation Identity/Civilization")]
public class CivilizationData : ScriptableObject
{
    [Tooltip("Default name for this civilization's governing assembly.")]
    public string assemblyName;
    public NationContentLayer content = new NationContentLayer();
}
