using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Politics/Allegiance Definition")]
public sealed class AllegianceDefinition : ScriptableObject
{
    public string id;
    public string displayName;
    public AllegianceType type;
    public PoliticalTrait primaryIdentity;
    public PoliticalTrait startingDynamicIdentity;
    public List<string> startingCurrentInterestRegionIds = new List<string>();
    public List<string> startingFutureInterestRegionIds = new List<string>();

    public string StableId => AllegianceSystem.StableId(!string.IsNullOrWhiteSpace(id) ? id : name);
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : name;
}
