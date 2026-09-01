using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum AllegianceTypeAvailability : byte { Family = 1, Tribe = 2, Both = Family | Tribe }
[Flags]
public enum PoliticalIdentityRole : byte { Primary = 1, Dynamic = 2, Both = Primary | Dynamic }

[Serializable]
public sealed class PoliticalPreferenceWeights
{
    [Header("Holdings and society")]
    public int citizenHoldings;
    public int eliteHoldings;
    public int freemenHoldings;
    public int preferredCulture;
    public int commerce;
    [Header("State priorities")]
    public int militaryStrength;
    public int territorialExpansion;
    public int regionalInterests;
    public int foreignInfluence;
    public int defense;
    public int statusQuo;
    [Header("Tribal federation")]
    public int federationBenefit;
    public int tribalAutonomy;
    public int ownPoliticalPower;
}

[CreateAssetMenu(menuName = "Politics/Political Trait")]
public sealed class PoliticalTrait : ScriptableObject
{
    private static Dictionary<string, PoliticalTrait> cache;
    public string id;
    public string displayName;
    [TextArea(2, 6)] public string description;
    public AllegianceTypeAvailability availability = AllegianceTypeAvailability.Both;
    public PoliticalIdentityRole suitableRoles = PoliticalIdentityRole.Both;
    [Tooltip("Evaluation weights. Positive values favor an outcome; negative consequences automatically reverse them.")]
    public PoliticalPreferenceWeights preferences = new PoliticalPreferenceWeights();
    public string StableId => !string.IsNullOrWhiteSpace(id) ? id.Trim() : name;
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : name;
    public bool Allows(AllegianceType type) => (availability & (type == AllegianceType.Tribe
        ? AllegianceTypeAvailability.Tribe : AllegianceTypeAvailability.Family)) != 0;
    public bool Suits(PoliticalIdentityRole role) => (suitableRoles & role) != 0;

    public static PoliticalTrait Find(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId)) return null;
        if (cache == null)
        {
            cache = new Dictionary<string, PoliticalTrait>(StringComparer.OrdinalIgnoreCase);
            foreach (PoliticalTrait trait in Resources.LoadAll<PoliticalTrait>("Prefabs/NationData/PoliticalTraits"))
                if (trait != null) cache[trait.StableId] = trait;
        }
        string key = traitId.Trim();
        if (string.Equals(key, "aristocratic_house", StringComparison.OrdinalIgnoreCase)) key = "elite_patron";
        else if (string.Equals(key, "merchant_dynasty", StringComparison.OrdinalIgnoreCase)) key = "commercialist";
        else if (string.Equals(key, "latin_supremacist", StringComparison.OrdinalIgnoreCase)) key = "cultural_supremacist";
        cache.TryGetValue(key, out PoliticalTrait result);
        return result;
    }
}
