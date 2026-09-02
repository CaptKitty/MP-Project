using System;
using System.Collections.Generic;
using UnityEngine;

public enum AllegianceType : byte { Family, Tribe }

[Serializable]
public sealed class Allegiance
{
    public string id;
    public string displayName;
    public AllegianceType type;
    public string primaryIdentityId;
    public string dynamicIdentityId;
    public List<string> currentInterestRegionIds = new List<string>();
    public List<string> futureInterestRegionIds = new List<string>();

    public PoliticalTrait PrimaryIdentity => PoliticalTrait.Find(primaryIdentityId);
    public PoliticalTrait DynamicIdentity => PoliticalTrait.Find(dynamicIdentityId);
    public Allegiance Clone() => new Allegiance { id = id, displayName = displayName, type = type,
        primaryIdentityId = primaryIdentityId, dynamicIdentityId = dynamicIdentityId,
        currentInterestRegionIds = new List<string>(currentInterestRegionIds ?? new List<string>()),
        futureInterestRegionIds = new List<string>(futureInterestRegionIds ?? new List<string>()) };
}

public static class AllegianceSystem
{
    public static void EnsureNationAllegiances(Nation nation)
    {
        if (nation == null) return;
        if (nation.allegiances == null) nation.allegiances = new List<Allegiance>();
        nation.allegiances.RemoveAll(item => item == null);
        List<AllegianceDefinition> definitions = NationContentResolver.ResolveAllegiances(nation);
        foreach (AllegianceDefinition definition in definitions)
        {
            if (definition == null) continue;
            string id = definition.StableId;
            Allegiance existing = nation.allegiances.Find(item => Same(item.id, id) || Same(item.displayName, definition.DisplayName));
            if (existing == null) { existing = new Allegiance { id = id }; nation.allegiances.Add(existing); }
            existing.id = id; existing.displayName = definition.DisplayName; existing.type = definition.type;
            existing.primaryIdentityId = definition.primaryIdentity != null ? definition.primaryIdentity.StableId : existing.primaryIdentityId;
            if (string.IsNullOrWhiteSpace(existing.dynamicIdentityId) && definition.startingDynamicIdentity != null)
                existing.dynamicIdentityId = definition.startingDynamicIdentity.StableId;
            if (existing.currentInterestRegionIds == null || existing.currentInterestRegionIds.Count == 0)
                existing.currentInterestRegionIds = new List<string>(definition.startingCurrentInterestRegionIds ?? new List<string>());
            if (existing.futureInterestRegionIds == null || existing.futureInterestRegionIds.Count == 0)
                existing.futureInterestRegionIds = new List<string>(definition.startingFutureInterestRegionIds ?? new List<string>());
        }

        // Backward-compatible migration from the former string/group content.
        AllegianceType fallbackType = LegacyType(NationContentResolver.ResolveAllegianceType(nation));
        if (definitions.Count == 0)
            foreach (string name in NationContentResolver.ResolveAllegianceNames(nation)) AddLegacy(nation, name, fallbackType);
        if (nation.politicalGroups != null)
            foreach (PoliticalGroup group in nation.politicalGroups)
                if (group != null && !group.representsUnalignedHoldings) AddLegacy(nation, group.displayName, fallbackType, group.id);
    }

    private static void AddLegacy(Nation nation, string name, AllegianceType type, string requestedId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        string id = StableId(!string.IsNullOrWhiteSpace(requestedId) ? requestedId : name);
        if (nation.allegiances.Exists(item => Same(item.id, id) || Same(item.displayName, name))) return;
        nation.allegiances.Add(new Allegiance { id = id, displayName = name.Trim(), type = type });
    }

    public static Allegiance Find(Nation nation, string idOrName)
    {
        EnsureNationAllegiances(nation);
        return nation != null && nation.allegiances != null
            ? nation.allegiances.Find(item => item != null && (Same(item.id, idOrName) || Same(item.displayName, idOrName))) : null;
    }

    public static List<ProvinceHolding> Holdings(Nation nation, Allegiance allegiance)
    {
        List<ProvinceHolding> result = new List<ProvinceHolding>();
        if (nation == null || allegiance == null || Owners.Instance == null) return result;
        foreach (Province province in Owners.Instance.provincelist)
            if (province != null && province.nation == nation && province.holdings != null)
                foreach (ProvinceHolding holding in province.holdings)
                    if (holding != null && (Same(holding.allegiance, allegiance.id) || Same(holding.allegiance, allegiance.displayName)))
                        result.Add(holding);
        return result;
    }

    public static Sprite Icon(Nation nation, Allegiance allegiance)
    {
        if (nation == null || allegiance == null) return null;
        List<AllegianceDefinition> definitions = NationContentResolver.ResolveAllegiances(nation);
        AllegianceDefinition definition = definitions.Find(candidate => candidate != null &&
            (Same(candidate.StableId, allegiance.id) || Same(candidate.DisplayName, allegiance.displayName)));
        return definition != null ? definition.icon : null;
    }

    public static List<CampaignRegion> ResolveRegions(IEnumerable<string> ids)
    {
        List<CampaignRegion> result = new List<CampaignRegion>();
        if (ids == null || Owners.Instance == null) return result;
        foreach (string id in ids)
        {
            CampaignRegion region = Owners.Instance.CallRegionByString(id);
            if (region != null && !result.Contains(region)) result.Add(region);
        }
        return result;
    }

    public static void EnsureStartingRegionalFocuses(Nation nation)
    {
        if (nation == null || Owners.Instance == null) return;
        EnsureNationAllegiances(nation);
        if (nation.allegiances == null || nation.allegiances.Count == 0) return;
        List<string> ownedRegions = new List<string>();
        foreach (Province province in Owners.Instance.provincelist)
            if (province != null && province.nation == nation && !string.IsNullOrWhiteSpace(province.region) &&
                !ownedRegions.Exists(region => Same(region, province.region))) ownedRegions.Add(province.region);
        ownedRegions.Sort(StringComparer.Ordinal);
        if (ownedRegions.Count == 0) return;

        foreach (Allegiance allegiance in nation.allegiances)
        {
            if (allegiance == null) continue;
            if (allegiance.currentInterestRegionIds == null)
                allegiance.currentInterestRegionIds = new List<string>();
            if (allegiance.currentInterestRegionIds.Count > 0) continue;
            Dictionary<string, int> alignedByRegion = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Province province in Owners.Instance.provincelist)
            {
                if (province == null || province.nation != nation || province.holdings == null ||
                    string.IsNullOrWhiteSpace(province.region)) continue;
                foreach (ProvinceHolding holding in province.holdings)
                    if (holding != null && (Same(holding.allegiance, allegiance.id) ||
                        Same(holding.allegiance, allegiance.displayName)))
                        alignedByRegion[province.region] = alignedByRegion.TryGetValue(province.region, out int count)
                            ? count + 1 : 1;
            }
            string focus = null;
            int strongest = -1;
            foreach (string region in ownedRegions)
            {
                int count = alignedByRegion.TryGetValue(region, out int value) ? value : 0;
                if (count > strongest) { strongest = count; focus = region; }
            }
            if (strongest <= 0)
                focus = ownedRegions[StableHash(nation.name + "|" + allegiance.id) % ownedRegions.Count];
            if (!string.IsNullOrWhiteSpace(focus)) allegiance.currentInterestRegionIds.Add(focus);
        }
    }

    public static string StableId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "allegiance";
        System.Text.StringBuilder result = new System.Text.StringBuilder();
        foreach (char character in value.Trim().ToLowerInvariant())
            if (char.IsLetterOrDigit(character)) result.Append(character);
            else if (result.Length > 0 && result[result.Length - 1] != '_') result.Append('_');
        return result.ToString().Trim('_');
    }

    public static AllegianceType LegacyType(string value) => value != null &&
        value.IndexOf("trib", StringComparison.OrdinalIgnoreCase) >= 0 ? AllegianceType.Tribe : AllegianceType.Family;
    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            if (value != null) foreach (char character in value) hash = hash * 31 + character;
            return hash & int.MaxValue;
        }
    }
    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
