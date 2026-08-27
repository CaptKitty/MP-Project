using System;
using System.Collections.Generic;
using UnityEngine;

public enum SocioEconomicClass : byte
{
    Subsistence,
    Laborers,
    Freemen,
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
public enum HoldingCategory : byte
{
    FreeFarmers, TribalSubsistence, EliteAgriculture, CommercialAgriculture, ServileAgriculture,
    Artisans, Commerce, Pastoralists, Hunters, Mining
}

[Serializable]
public sealed class HoldingTransformationOption
{
    public string targetHoldingId;
    [Range(0, 100)] public int minimumUrbanization;
    [Range(0, 100)] public int maximumUrbanization = 100;
    public bool IsAvailable(Province province) => province != null &&
        province.urbanization >= minimumUrbanization && province.urbanization <= maximumUrbanization;
}

public static class UrbanizationOutputScaling
{
    // Holding economies were producing too much relative to the rest of the campaign.
    // Keep authored values readable and apply the shared balance rate after local modifiers.
    public const float HoldingResourceOutputRate = 2f / 3f;

    public static int Apply(int baseValue, int response, int urbanization)
    {
        response = Mathf.Clamp(response, -100, 100);
        if (response == 0 || baseValue == 0) return baseValue;
        float modifierPercent = Mathf.Lerp(-response, response, Mathf.Clamp01(urbanization / 100f));
        return Mathf.RoundToInt(baseValue * (1f + modifierPercent / 100f));
    }

    public static int ApplyHoldingResourceRate(int value)
    {
        return Mathf.RoundToInt(value * HoldingResourceOutputRate);
    }
}

[Serializable]
public sealed class HoldingOutputDefinition
{
    public HoldingOutputType type;
    public int baseValue;
    public bool scalesWithUrbanization;
    public UrbanizationSuitability suitability = UrbanizationSuitability.Neutral;
    [Tooltip("Negative favors low urbanization; positive favors high urbanization; zero ignores urbanization.")]
    [Range(-100, 100)] public int urbanizationResponse;
    public bool disabledWhileMobilized;

    public int EffectiveUrbanizationResponse => urbanizationResponse != 0 ? urbanizationResponse :
        scalesWithUrbanization ? suitability == UrbanizationSuitability.Urban ? 50 :
            suitability == UrbanizationSuitability.Rural ? -50 : 0 : 0;

    public int EffectiveValue(int urbanization, bool mobilized)
    {
        if (mobilized && disabledWhileMobilized) return 0;
        int urbanizationAdjusted = UrbanizationOutputScaling.Apply(baseValue, EffectiveUrbanizationResponse, urbanization);
        return UrbanizationOutputScaling.ApplyHoldingResourceRate(urbanizationAdjusted);
    }
}

[Serializable]
public sealed class HoldingLevelDefinition
{
    [Min(1)] public int level = 1;
    [Min(0)] public int goldCost;
    [Min(0)] public int constructionTicks;
    public int goldIncome;
    [Range(-100, 100)] public int urbanizationResponse;
    [TextArea(1, 4)] public string displayedEffect;
    public ProvinceLocalModifiers localModifiers = new ProvinceLocalModifiers();
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
    public SocioEconomicClass socioEconomicClass = SocioEconomicClass.Freemen;
    [Tooltip("Political actor, movement, or cause to which this holding belongs or gives its allegiance. Empty means Unaligned.")]
    public string allegiance;
    public bool levyEnabled = true;
    [Header("Natural adaptation")]
    public string adaptationTargetId;
    [Min(0)] public int adaptationPressure;
    [Min(0)] public int adaptationCooldownTicks;

    public string HoldingId => definition != null ? definition.StableId : id;
    public string DisplayName => definition != null ? definition.DisplayName : id;
    public int MaximumLevel => definition != null ? Mathf.Max(1, definition.maximumLevel) : 5;
    public bool CanRaiseLevies => levyEnabled && definition != null && definition.canRaiseLevies &&
        (definition.levyArchetype != LevyArchetype.None || definition.levyUnit != null);
    public int LevyContributionPermille => CanRaiseLevies
        ? Mathf.Max(0, definition.levyContributionPermillePerLevel) * Mathf.Max(1, level) : 0;
    public float EffectiveLevyContribution(Nation nation) => LevyContributionPermille *
        (nation != null ? Mathf.Max(0, nation.LevyLawPermille) : 0) / 1000000f;
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
    public int GoldIncomeAt(int urbanization)
    {
        if (definition == null || definition.levels == null) return 0;
        int total = 0;
        foreach (HoldingLevelDefinition entry in definition.levels)
            if (entry != null && entry.level <= level)
                total += UrbanizationOutputScaling.ApplyHoldingResourceRate(
                    UrbanizationOutputScaling.Apply(entry.goldIncome, entry.urbanizationResponse, urbanization));
        return total;
    }
    public int GetOutput(HoldingOutputType type, int urbanization, bool mobilized)
    {
        // Political allegiance is an identity relationship, not a produced holding resource.
        // Keep the legacy enum member for serialized-data compatibility, but holdings never output it.
        if (type == HoldingOutputType.PoliticalInfluence) return 0;
        int total = 0;
        if (definition != null && definition.outputs != null)
            foreach (HoldingOutputDefinition output in definition.outputs)
                if (output != null && output.type == type) total += output.EffectiveValue(urbanization, mobilized);
        if (type == HoldingOutputType.Food)
            total = Mathf.Max(0, total) - (definition != null ? Mathf.Max(0, definition.foodConsumption) : 1);
        if (type == HoldingOutputType.Income && total == 0) total = GoldIncomeAt(urbanization);
        return total;
    }

    public int FoodConsumption => definition != null ? Mathf.Max(0, definition.foodConsumption) : 1;
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
