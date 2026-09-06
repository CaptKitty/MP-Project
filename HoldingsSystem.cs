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

public static class HoldingCategoryRules
{
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

// Still used by building-level effects. It is no longer part of holding output calculation.
public static class UrbanizationOutputScaling
{
    public static int Apply(int baseValue, int response, int urbanization) =>
        Mathf.RoundToInt(ApplyUnrounded(baseValue, response, urbanization));

    public static float ApplyUnrounded(float baseValue, int response, float urbanization)
    {
        response = Mathf.Clamp(response, -100, 100);
        if (response == 0 || Mathf.Approximately(baseValue, 0f)) return baseValue;
        return baseValue * (1f + response * Mathf.Clamp(urbanization, -100f, 100f) / 10000f);
    }
}

[Serializable]
public sealed class ProvinceHolding
{
    public string instanceId;
    public HoldingDefinition definition;
    public string id;
    public int slotIndex = -1;
    public string cultureName;
    public SocioEconomicClass socioEconomicClass = SocioEconomicClass.Freemen;
    [Tooltip("Political actor, movement, or cause to which this holding belongs or gives its allegiance. Empty means Unaligned.")]
    public string allegiance;
    [Header("Natural adaptation")]
    [Min(0)] public int adaptationCooldownTicks;

    public string HoldingId => definition != null ? definition.StableId : id;
    public string DisplayName => definition != null ? definition.DisplayName : id;
    public int FoodConsumption => definition != null
        ? Mathf.Max(0, definition.foodConsumption) + Mathf.Max(0, definition.foodUpkeep)
        : 1;
    public int FoodUpkeep => definition != null ? Mathf.Max(0, definition.foodUpkeep) : 0;
}

