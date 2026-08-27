using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Nation Identity/Holding Definition")]
public sealed class HoldingDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable save identifier. Do not change after using this holding in a campaign.")]
    public string id;
    public string displayName;
    [TextArea(2, 6)] public string description;
    public Sprite icon;
    public HoldingCategory category;
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
    public List<HoldingTransformationOption> transformations = new List<HoldingTransformationOption>();
    [Tooltip("Food consumed by each holding instance per tick. This remains active while its levy is mobilized.")]
    [Min(0)] public int foodConsumption = 1;

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
    [Min(0)] public int levyRecoveryTicks = 20;
    [Min(0)] public int levyDemobilizationTicks;

    public string StableId => !string.IsNullOrWhiteSpace(id) ? id.Trim() : name;
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName : name;
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
        HoldingDefinition found = Array.Find(Resources.LoadAll<HoldingDefinition>(string.Empty), candidate => candidate != null &&
            string.Equals(candidate.StableId, stableId, StringComparison.OrdinalIgnoreCase));
        if (found != null) { HoldingArchetypeCatalog.ApplyMetadata(found); return found; }
        found = HoldingArchetypeCatalog.Find(stableId);
        if (found != null) return found;
        if (string.Equals(stableId, "CitizenFarm", StringComparison.OrdinalIgnoreCase)) return DefaultCitizenFarm();
        if (stableId.StartsWith("CitizenFarm:", StringComparison.OrdinalIgnoreCase))
        {
            string unitName = stableId.Substring("CitizenFarm:".Length);
            UnitSaveData unit = Array.Find(Resources.LoadAll<UnitSaveData>("Prefabs/Units"), candidate =>
                candidate != null && candidate.name == unitName);
            return DefaultCitizenFarm(unit);
        }
        return null;
    }

    private static HoldingDefinition defaultCitizenFarm;
    private static readonly Dictionary<string, HoldingDefinition> defaultCitizenFarms = new Dictionary<string, HoldingDefinition>();

    public static HoldingDefinition DefaultCitizenFarm()
    {
        if (defaultCitizenFarm != null) return defaultCitizenFarm;
        defaultCitizenFarm = CreateInstance<HoldingDefinition>();
        defaultCitizenFarm.name = "CitizenFarm"; defaultCitizenFarm.id = "CitizenFarm";
        defaultCitizenFarm.displayName = "Citizen Farm"; defaultCitizenFarm.maximumLevel = 1;
        defaultCitizenFarm.defaultClass = SocioEconomicClass.Citizen;
        defaultCitizenFarm.outputs.Add(new HoldingOutputDefinition { type = HoldingOutputType.Income, baseValue = 2,
            suitability = UrbanizationSuitability.Neutral, disabledWhileMobilized = true });
        defaultCitizenFarm.outputs.Add(new HoldingOutputDefinition { type = HoldingOutputType.Food, baseValue = 2,
            suitability = UrbanizationSuitability.Neutral, disabledWhileMobilized = true });
        HoldingArchetypeCatalog.ApplyMetadata(defaultCitizenFarm);
        return defaultCitizenFarm;
    }

    public static HoldingDefinition DefaultCitizenFarm(UnitSaveData levyUnit)
    {
        if (levyUnit == null) return DefaultCitizenFarm();
        if (defaultCitizenFarms.TryGetValue(levyUnit.name, out HoldingDefinition existing)) return existing;
        HoldingDefinition definition = CreateInstance<HoldingDefinition>();
        definition.name = "CitizenFarm:" + levyUnit.name; definition.id = definition.name;
        definition.displayName = "Citizen Farm"; definition.maximumLevel = 1;
        definition.defaultClass = SocioEconomicClass.Citizen; definition.canRaiseLevies = true;
        definition.levyUnit = levyUnit; definition.levyContributionPermillePerLevel = 1000;
        definition.outputs.Add(new HoldingOutputDefinition { type = HoldingOutputType.Income, baseValue = 2,
            suitability = UrbanizationSuitability.Neutral, disabledWhileMobilized = true });
        definition.outputs.Add(new HoldingOutputDefinition { type = HoldingOutputType.Food, baseValue = 2,
            suitability = UrbanizationSuitability.Neutral, disabledWhileMobilized = true });
        HoldingArchetypeCatalog.ApplyMetadata(definition);
        defaultCitizenFarms.Add(levyUnit.name, definition);
        return definition;
    }
}
