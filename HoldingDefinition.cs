using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Nation Identity/Holding Definition")]
public sealed class HoldingDefinition : ScriptableObject
{
    private const string ResourcePath = "Prefabs/NationData/HoldingData";
    private static HoldingDefinition[] cachedDefinitions;
    [Header("Identity")]
    [Tooltip("Stable save identifier. Do not change after using this holding in a campaign.")]
    public string id;
    public string displayName;
    [TextArea(2, 6)] public string description;
    public Sprite icon;
    public HoldingCategory category;
    [Tooltip("New economy identity. Unspecified safely derives from the legacy category.")]
    public HoldingEconomicType economicType;
    [Range(1, 3)] public int categoryTier = 1;
    [Tooltip("Overlapping economic identities used by provincial composition and efficiency systems. None derives tags from Category.")]
    public HoldingTag tags;
    [Tooltip("Likely allegiance for generated content. Existing holding allegiance is never overwritten.")]
    public string suggestedAllegiance;

    [Header("Progression")]
    [Min(1)] public int maximumLevel = 5;
    [Min(1)] public int defaultConstructionTicks = 10;
    public List<HoldingLevelDefinition> levels = new List<HoldingLevelDefinition>();
    public List<HoldingOutputDefinition> outputs = new List<HoldingOutputDefinition>();
    [Tooltip("Class-scaled output pipeline. Empty uses the migration profile for Economic Type.")]
    public List<HoldingEconomicOutputDefinition> economicOutputs = new List<HoldingEconomicOutputDefinition>();
    public List<HoldingTransformationOption> transformations = new List<HoldingTransformationOption>();
    [Tooltip("Food consumed by each holding instance per tick. This remains active while its levy is mobilized.")]
    [Min(0)] public int foodConsumption = 1;
    [Tooltip("Additional food upkeep beyond the universal one-food holding consumption.")]
    [Min(0)] public int foodUpkeep;
    [Tooltip("Additional formations supported by this holding in the provincial garrison.")]
    [Min(0)] public int garrisonCapacity;

    [Header("People")]
    public SocioEconomicClass defaultClass = SocioEconomicClass.Freemen;
    [Tooltip("When enabled, a holding instance may supply levy formations.")]
    public bool canRaiseLevies;
    public UnitSaveData levyUnit;
    [Tooltip("Culture-aware role requested by this holding. The fixed unit remains a migration and no-match fallback.")]
    public LevyArchetype levyArchetype;
    [Tooltip("Fixed-point levy capacity contributed per holding level. 1000 equals one full levy-capacity point before national law.")]
    [Min(0)] public int levyContributionPermillePerLevel = 1000;
    [HideInInspector] public int levyFormationsPerLevel = 1;
    [Min(0)] public int levyMobilizationTicks;
    [Min(0)] public int levyRecoveryTicks = 120;
    [Min(0)] public int levyDemobilizationTicks;

    public string StableId => !string.IsNullOrWhiteSpace(id) ? id.Trim() : name;
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName : name;
    public HoldingEconomicType EffectiveEconomicType => HoldingEconomySystem.ResolveType(this);
    public HoldingLevelDefinition GetLevel(int targetLevel) => levels != null
        ? levels.Find(entry => entry != null && entry.level == targetLevel) : null;
    public int ConstructionTicksForLevel(int targetLevel)
    {
        HoldingLevelDefinition configured = GetLevel(targetLevel);
        if (configured != null && configured.constructionTicks > 0) return configured.constructionTicks;
        return Mathf.Clamp(Mathf.Max(defaultConstructionTicks, 10 + (Mathf.Max(1, targetLevel) - 1) * 5), 10, 30);
    }
    public int GoldCostForLevel(int targetLevel)
    {
        HoldingLevelDefinition configured = GetLevel(targetLevel);
        return configured != null ? Mathf.Max(0, configured.goldCost) : 0;
    }
    public bool CanTransformTo(string targetId, Province province) => transformations != null &&
        transformations.Exists(option => option != null && option.IsAvailable(province) &&
            string.Equals(option.targetHoldingId, targetId, StringComparison.OrdinalIgnoreCase));

    public static HoldingDefinition Find(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId)) return null;
        string canonicalId = HoldingArchetypeCatalog.CanonicalizeId(stableId);
        HoldingDefinition found = Array.Find(cachedDefinitions ?? (cachedDefinitions = Resources.LoadAll<HoldingDefinition>(ResourcePath)), candidate => candidate != null &&
            string.Equals(candidate.StableId, canonicalId, StringComparison.OrdinalIgnoreCase));
        if (found != null) { HoldingArchetypeCatalog.ApplyMetadata(found); return found; }
        found = HoldingArchetypeCatalog.Find(canonicalId);
        if (found != null) return found;
        return null;
    }

    private static HoldingDefinition defaultCitizenFarm;
    private static readonly Dictionary<string, HoldingDefinition> defaultCitizenFarms = new Dictionary<string, HoldingDefinition>();

    public static HoldingDefinition DefaultCitizenFarm()
    {
        return defaultCitizenFarm != null ? defaultCitizenFarm : (defaultCitizenFarm = HoldingArchetypeCatalog.Find(HoldingEconomicType.Farm));
    }

    public static HoldingDefinition DefaultCitizenFarm(UnitSaveData levyUnit)
    {
        return DefaultCitizenFarm();
    }
}
