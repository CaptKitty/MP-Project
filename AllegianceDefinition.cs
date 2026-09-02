using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Politics/Allegiance Definition")]
public sealed class AllegianceDefinition : ScriptableObject
{
    public string id;
    public string displayName;
    public AllegianceType type;
    [Tooltip("Representative icon shown for this Family or Tribe in political interfaces.")]
    public Sprite icon;
    public PoliticalTrait primaryIdentity;
    public PoliticalTrait startingDynamicIdentity;
    public List<string> startingCurrentInterestRegionIds = new List<string>();
    public List<string> startingFutureInterestRegionIds = new List<string>();

    public string StableId => AllegianceSystem.StableId(!string.IsNullOrWhiteSpace(id) ? id : name);
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : name;
}
