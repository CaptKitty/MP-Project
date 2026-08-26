using System;
using System.Collections.Generic;
using UnityEngine;

public enum SocioEconomicClass : byte
{
    Subsistence,
    Laborers,
    Peasants,
    Burghers,
    Clergy,
    Aristocracy,
    Citizen,
    Elite,
    Enslaved
}

public enum HoldingOutputType : byte
{
    Income, Food, PoliticalInfluence, Manpower, CulturalInfluence, ReligiousInfluence
}

public enum UrbanizationSuitability : byte { Rural, Neutral, Urban }

[CreateAssetMenu(menuName = "Nation Identity/Holding Definition")]
public sealed class HoldingDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable save identifier. Do not change after using this holding in a campaign.")]
    public string id;
    public string displayName;
    [TextArea(2, 6)] public string description;
    public Sprite icon;

    [Header("Progression")]
    [Min(1)] public int maximumLevel = 5;
    [Min(1)] public int defaultConstructionTicks = 10;
    public List<HoldingLevelDefinition> levels = new List<HoldingLevelDefinition>();
    public List<HoldingOutputDefinition> outputs = new List<HoldingOutputDefinition>();

    [Header("People")]
    public SocioEconomicClass defaultClass = SocioEconomicClass.Peasants;
    [Tooltip("When enabled, a holding instance may supply levy formations.")]
    public bool canRaiseLevies;
    public UnitSaveData levyUnit;
    [Min(1)] public int levyFormationsPerLevel = 1;
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
    public static HoldingDefinition Find(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId)) return null;
        HoldingDefinition found = Array.Find(Resources.LoadAll<HoldingDefinition>(string.Empty), candidate => candidate != null &&
            string.Equals(candidate.StableId, stableId, StringComparison.OrdinalIgnoreCase));
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
        defaultCitizenFarm.outputs.Add(new HoldingOutputDefinition { type = HoldingOutputType.PoliticalInfluence, baseValue = 1 });
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
        definition.levyUnit = levyUnit; definition.levyFormationsPerLevel = 1;
        definition.outputs.Add(new HoldingOutputDefinition { type = HoldingOutputType.Income, baseValue = 2,
            suitability = UrbanizationSuitability.Neutral, disabledWhileMobilized = true });
        definition.outputs.Add(new HoldingOutputDefinition { type = HoldingOutputType.Food, baseValue = 2,
            suitability = UrbanizationSuitability.Neutral, disabledWhileMobilized = true });
        definition.outputs.Add(new HoldingOutputDefinition { type = HoldingOutputType.PoliticalInfluence, baseValue = 1 });
        defaultCitizenFarms.Add(levyUnit.name, definition);
        return definition;
    }
}

[Serializable]
public sealed class HoldingOutputDefinition
{
    public HoldingOutputType type;
    public int baseValue;
    public bool scalesWithUrbanization;
    public UrbanizationSuitability suitability = UrbanizationSuitability.Neutral;
    public bool disabledWhileMobilized;

    public int EffectiveValue(int urbanization, bool mobilized)
    {
        if (mobilized && disabledWhileMobilized) return 0;
        if (!scalesWithUrbanization || suitability == UrbanizationSuitability.Neutral) return baseValue;
        float urban = Mathf.Clamp01(urbanization / 100f);
        float multiplier = suitability == UrbanizationSuitability.Urban
            ? Mathf.Lerp(.5f, 1.5f, urban) : Mathf.Lerp(1.5f, .5f, urban);
        return Mathf.RoundToInt(baseValue * multiplier);
    }
}

[Serializable]
public sealed class HoldingLevelDefinition
{
    [Min(1)] public int level = 1;
    [Min(0)] public int goldCost;
    [Min(0)] public int constructionTicks;
    public int goldIncome;
    [TextArea(1, 4)] public string displayedEffect;
}

[Serializable]
public sealed class ProvinceHolding
{
    public string instanceId;
    public HoldingDefinition definition;
    public string id;
    public int level = 1;
    public int slotIndex = -1;
    public string cultureName;
    public SocioEconomicClass socioEconomicClass = SocioEconomicClass.Peasants;
    public bool levyEnabled = true;

    public string HoldingId => definition != null ? definition.StableId : id;
    public string DisplayName => definition != null ? definition.DisplayName : id;
    public int MaximumLevel => definition != null ? Mathf.Max(1, definition.maximumLevel) : 5;
    public bool CanRaiseLevies => levyEnabled && definition != null && definition.canRaiseLevies && definition.levyUnit != null;
    public int LevyFormationCount => CanRaiseLevies ? Mathf.Max(1, definition.levyFormationsPerLevel) * Mathf.Max(1, level) : 0;
    public int GoldIncome
    {
        get
        {
            if (definition == null || definition.levels == null) return 0;
            int total = 0;
            foreach (HoldingLevelDefinition entry in definition.levels)
                if (entry != null && entry.level <= level) total += entry.goldIncome;
            return total;
        }
    }
    public int GetOutput(HoldingOutputType type, int urbanization, bool mobilized)
    {
        int total = 0;
        if (definition != null && definition.outputs != null)
            foreach (HoldingOutputDefinition output in definition.outputs)
                if (output != null && output.type == type) total += output.EffectiveValue(urbanization, mobilized);
        if (type == HoldingOutputType.Income && total == 0) total = GoldIncome;
        return total;
    }
}

[Serializable]
public sealed class HoldingConstructionOrder
{
    public string holdingInstanceId;
    public int slotIndex;
    public string holdingId;
    public int targetLevel = 1;
    public int remainingTicks;
}
