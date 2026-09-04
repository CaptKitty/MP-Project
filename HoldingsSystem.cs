using System;
using System.Collections.Generic;
using UnityEngine;

public enum SocioEconomicClass : byte
{
    // Legacy serialized values remain in place so old saves/assets can be migrated safely.
    Subsistence,
    Laborers,
    Freemen,
    Burghers,
    Clergy,
    Aristocracy,
    Citizen,
    Elite,
    Enslaved,
    Tribesman
}

public static class SocioEconomicClassRules
{
    public static SocioEconomicClass Normalize(SocioEconomicClass value)
    {
        switch (value)
        {
            case SocioEconomicClass.Subsistence:
                return SocioEconomicClass.Tribesman;
            case SocioEconomicClass.Laborers:
            case SocioEconomicClass.Burghers:
            case SocioEconomicClass.Clergy:
                return SocioEconomicClass.Freemen;
            case SocioEconomicClass.Aristocracy:
                return SocioEconomicClass.Elite;
            default:
                return value;
        }
    }

    public static string DisplayName(SocioEconomicClass value)
    {
        value = Normalize(value);
        if (value == SocioEconomicClass.Enslaved) return "Slave";
        if (value == SocioEconomicClass.Freemen) return "Freeman";
        return value.ToString();
    }
}

public enum HoldingOutputType : byte
{
    Income, Food, PoliticalInfluence, Manpower, CulturalInfluence, ReligiousInfluence,
    AgriculturalValue, IndustrialValue, CommercialValue
}

public enum HoldingEconomicType : byte { Unspecified, Farm, Pasture, Workshop, Commerce, Mine, Fishery }
public enum HoldingLabourCategory : byte { Automatic, Raw, Skilled, Value }

[Serializable]
public sealed class HoldingEconomicOutputDefinition
{
    public HoldingOutputType type;
    public float baseValue;
    public HoldingLabourCategory labourCategory = HoldingLabourCategory.Automatic;
    public bool disabledWhileMobilized;
}

[Serializable]
public sealed class HoldingTypePressure
{
    public HoldingEconomicType type;
    public float amount;
    public string source;
}

[Serializable]
public sealed class HoldingClassPressure
{
    public SocioEconomicClass socialClass = SocioEconomicClass.Freemen;
    public float amount;
    public string source;
}

public enum UrbanizationSuitability : byte { Rural, Neutral, Urban }
public enum HoldingCategory : byte
{
    FreeFarmers, TribalSubsistence, EliteAgriculture, CommercialAgriculture, ServileAgriculture,
    Artisans, Commerce, Pastoralists, Hunters, Mining
}

public static class HoldingCategoryRules
{
    public static string DisplayName(HoldingCategory category)
    {
        switch (category)
        {
            case HoldingCategory.FreeFarmers: return "Free Farmers";
            case HoldingCategory.TribalSubsistence: return "Tribal Subsistence";
            case HoldingCategory.EliteAgriculture: return "Aristocratic Households";
            case HoldingCategory.CommercialAgriculture: return "Commercial Agriculture";
            case HoldingCategory.ServileAgriculture: return "Servile Agriculture";
            default: return category.ToString();
        }
    }

    public static string GroupName(ProvinceHolding holding) => holding != null && holding.definition != null
        ? holding.definition.EffectiveEconomicType.ToString() : "Unassigned";

    public static Sprite RepresentativeIcon(IList<ProvinceHolding> holdings)
    {
        if (holdings == null) return null;
        Dictionary<HoldingDefinition, int> counts = new Dictionary<HoldingDefinition, int>();
        foreach (ProvinceHolding holding in holdings)
            if (holding != null && holding.definition != null && holding.definition.icon != null)
                counts[holding.definition] = counts.TryGetValue(holding.definition, out int count) ? count + 1 : 1;
        HoldingDefinition best = null; int bestCount = -1;
        foreach (KeyValuePair<HoldingDefinition, int> entry in counts)
            if (entry.Value > bestCount || entry.Value == bestCount && (best == null ||
                string.CompareOrdinal(entry.Key.StableId, best.StableId) < 0))
            { best = entry.Key; bestCount = entry.Value; }
        return best != null ? best.icon : null;
    }
}

[Serializable]
public sealed class HoldingTransformationOption
{
    public string targetHoldingId;
    [Range(-100, 100)] public int minimumUrbanization = -100;
    [Range(-100, 100)] public int maximumUrbanization = 100;
    public bool IsAvailable(Province province) => province != null &&
        province.urbanization >= minimumUrbanization && province.urbanization <= maximumUrbanization;
}

public static class UrbanizationOutputScaling
{
    public static int Apply(int baseValue, int response, int urbanization)
    {
        return Mathf.RoundToInt(ApplyUnrounded(baseValue, response, urbanization));
    }

    public static float ApplyUnrounded(float baseValue, int response, float urbanization)
    {
        response = Mathf.Clamp(response, -100, 100);
        if (response == 0 || Mathf.Approximately(baseValue, 0f)) return baseValue;
        float modifierPercent = response * Mathf.Clamp(urbanization, -100f, 100f) / 100f;
        return baseValue * (1f + modifierPercent / 100f);
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
        return UrbanizationOutputScaling.Apply(baseValue, EffectiveUrbanizationResponse, urbanization);
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
    [Obsolete("Use LevyEconomySystem.CapacityShareForEntitlements with the holding's province.")]
    public bool CanRaiseLevies => levyEnabled;
    [Obsolete("Use LevyEconomySystem.CapacityShareForEntitlements with the holding's province.")]
    public int LevyContributionPermille => 0;
    [Obsolete("Use LevyEconomySystem.CapacityShareForEntitlements with the holding's province.")]
    public float EffectiveLevyContribution(Nation nation) => 0f;
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
                total += UrbanizationOutputScaling.Apply(entry.goldIncome, entry.urbanizationResponse, urbanization);
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
            total = Mathf.Max(0, total) - FoodConsumption;
        if (type == HoldingOutputType.Income && total == 0) total = GoldIncomeAt(urbanization);
        return total;
    }

    public int FoodConsumption => definition != null
        ? Mathf.Max(0, definition.foodConsumption) + Mathf.Max(0, definition.foodUpkeep)
        : 1;
    public int FoodUpkeep => definition != null ? Mathf.Max(0, definition.foodUpkeep) : 0;
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
