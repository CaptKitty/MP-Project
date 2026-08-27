using UnityEngine;

[CreateAssetMenu(menuName = "Nation Identity/Culture")]
public class NationCultureData : ScriptableObject
{
    [Header("Culture Identity")]
    [Tooltip("Name used by holdings, maps and culture UI. Leave empty to use the asset name.")]
    public string cultureName;
    [Tooltip("Authoritative color used by the culture map and culture composition charts.")]
    public Color32 color = new Color32(128, 128, 128, 255);

    public NationContentLayer content = new NationContentLayer();

    public string DisplayName => !string.IsNullOrWhiteSpace(cultureName) ? cultureName.Trim() : name;
    public bool Matches(string value) => !string.IsNullOrWhiteSpace(value) &&
        (string.Equals(DisplayName, value, System.StringComparison.OrdinalIgnoreCase) ||
         string.Equals(name, value, System.StringComparison.OrdinalIgnoreCase));
}
