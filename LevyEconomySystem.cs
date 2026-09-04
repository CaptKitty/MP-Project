using System;
using System.Collections.Generic;
using UnityEngine;

public enum LevyPressureType : byte { LightInfantry, HeavyInfantry, Cavalry }
public enum MobilizationSensitivity : byte { Low, Normal, Severe }

public static class LevyEconomySystem
{
    public const int DefaultRecoveryTicks = 120;
    public const int DefaultDemobilizationTicks = 0;
    private sealed class MobilizationCache { public int frame = -1; public float value; }
    private sealed class EffectCache { public int frame = -1; public readonly List<BuildingEconomicEffect> values = new List<BuildingEconomicEffect>(); }
    private static readonly Dictionary<string, MobilizationCache> MobilizationByRegion = new Dictionary<string, MobilizationCache>();
    private static readonly Dictionary<Province, EffectCache> EffectsByProvince = new Dictionary<Province, EffectCache>();
    public static void InvalidateAll() { MobilizationByRegion.Clear(); EffectsByProvince.Clear(); ValueTradeSystem.InvalidateAll(); }
    public static float SensitivityMultiplier(MobilizationSensitivity value) =>
        value == MobilizationSensitivity.Low ? .5f : value == MobilizationSensitivity.Severe ? 2f : 1f;

    public static MobilizationSensitivity Sensitivity(SocioEconomicClass socialClass)
    {
        switch (SocioEconomicClassRules.Normalize(socialClass))
        {
            case SocioEconomicClass.Citizen: return MobilizationSensitivity.Severe;
            case SocioEconomicClass.Enslaved:
            case SocioEconomicClass.Elite: return MobilizationSensitivity.Low;
            default: return MobilizationSensitivity.Normal;
        }
    }

    public static float HoldingCapacity(Province province, ProvinceHolding holding)
    {
        if (holding == null) return 0f;
        float value;
        switch (SocioEconomicClassRules.Normalize(holding.socioEconomicClass))
        {
            case SocioEconomicClass.Citizen: value = 1f; break;
            case SocioEconomicClass.Tribesman: value = 1.25f; break;
            case SocioEconomicClass.Freemen: value = .75f; break;
            case SocioEconomicClass.Elite: value = .25f; break;
            case SocioEconomicClass.Enslaved: value = .1f; break;
            default: value = 0f; break;
        }
        return value * NationContentResolver.ClassLevyCapacityMultiplier(province != null ? province.nation : null,
            SocioEconomicClassRules.Normalize(holding.socioEconomicClass));
    }

    public static float ProvinceCapacity(Province province)
    {
        if (province == null) return 0f;
        float value = 0f;
        if (province.holdings != null) foreach (ProvinceHolding holding in province.holdings) value += HoldingCapacity(province, holding);
        foreach (BuildingEconomicEffect effect in EffectsAffecting(province))
            if (effect.type == BuildingEconomicEffectType.LocalLevyCapacity) value += effect.amount;
        return Mathf.Max(0f, value);
    }

    public static float CapacityShareForEntitlements(Province province, ProvinceHolding holding)
    {
        float value = HoldingCapacity(province, holding);
        int count = 0; if (province != null && province.holdings != null) foreach (ProvinceHolding item in province.holdings) if (item != null) count++;
        if (count > 0) foreach (BuildingEconomicEffect effect in EffectsAffecting(province))
            if (effect.type == BuildingEconomicEffectType.LocalLevyCapacity) value += effect.amount / count;
        return Mathf.Max(0f, value);
    }

    public static float RegionCapacity(Province province)
    {
        float value = 0f;
        foreach (Province item in RegionProvinces(province)) value += ProvinceCapacity(item);
        return value;
    }

    public static float RaisedCapacity(Province province)
    {
        float result = 0f;
        foreach (Province item in RegionProvinces(province))
            if (item.levyEntitlements != null) foreach (ProvinceLevyEntitlement entitlement in item.levyEntitlements)
                if (entitlement != null && (entitlement.state == LevyEntitlementState.Mobilizing ||
                    entitlement.state == LevyEntitlementState.Raised || entitlement.state == LevyEntitlementState.Recovering)) result += 1f;
        return result;
    }

    public static float MobilizationFraction(Province province)
    {
        string key = (province != null ? province.region : string.Empty) + "|" +
            (province != null && province.nation != null ? province.nation.name : string.Empty);
        if (!MobilizationByRegion.TryGetValue(key, out MobilizationCache cache))
        { cache = new MobilizationCache(); MobilizationByRegion.Add(key, cache); }
        if (cache.frame == Time.frameCount) return cache.value;
        float capacity = RegionCapacity(province);
        cache.frame = Time.frameCount;
        cache.value = capacity > .0001f ? Mathf.Clamp01(RaisedCapacity(province) / capacity) : 0f;
        return cache.value;
    }

    public static float OutputMultiplier(Province province, ProvinceHolding holding)
    {
        float sensitivity = SensitivityMultiplier(Sensitivity(holding != null ? holding.socioEconomicClass : SocioEconomicClass.Freemen));
        return Mathf.Max(0f, 1f - MobilizationFraction(province) * sensitivity);
    }

    public static Dictionary<LevyPressureType, float> Pressure(Province province)
    {
        Dictionary<LevyPressureType, float> result = new Dictionary<LevyPressureType, float>
        { { LevyPressureType.LightInfantry, 0f }, { LevyPressureType.HeavyInfantry, 0f }, { LevyPressureType.Cavalry, 0f } };
        float romanCitizenLight = 0f;
        foreach (Province source in RegionProvinces(province))
        {
            if (source.holdings != null) foreach (ProvinceHolding holding in source.holdings)
            {
                if (holding == null) continue;
                SocioEconomicClass socialClass = SocioEconomicClassRules.Normalize(holding.socioEconomicClass);
                float light = socialClass == SocioEconomicClass.Citizen ? 10f : socialClass == SocioEconomicClass.Tribesman ? 8f :
                    socialClass == SocioEconomicClass.Freemen ? 5f : 0f;
                bool replaceLight = false;
                foreach (NationClassModifier modifier in NationContentResolver.ResolveClassModifiers(source.nation))
                    if (modifier != null && SocioEconomicClassRules.Normalize(modifier.socialClass) == socialClass)
                    { replaceLight |= modifier.replaceLightLevyWithHeavy; result[LevyPressureType.HeavyInfantry] += modifier.heavyInfantryPressure; }
                if (replaceLight) romanCitizenLight += light; else result[LevyPressureType.LightInfantry] += light;
                HoldingEconomicType type = holding.definition != null ? holding.definition.EffectiveEconomicType : HoldingEconomicType.Unspecified;
                if (type == HoldingEconomicType.Pasture) result[LevyPressureType.Cavalry] += 5f;
                if (type == HoldingEconomicType.Workshop) result[LevyPressureType.HeavyInfantry] += 5f;
                if (socialClass == SocioEconomicClass.Elite) result[LevyPressureType.HeavyInfantry] += 2f;
            }
        }
        foreach (BuildingEconomicEffect effect in EffectsAffecting(province))
            if (effect.type == BuildingEconomicEffectType.LevyTypePressure) result[effect.levyType] += effect.amount;
        foreach (LevyPressureType type in new[] { LevyPressureType.LightInfantry, LevyPressureType.HeavyInfantry, LevyPressureType.Cavalry })
            result[type] += ValueTradeSystem.LevyPressureOutput(province, type);
        // Central conversion rule: Roman Citizen light pressure is replaced, never duplicated.
        result[LevyPressureType.HeavyInfantry] += romanCitizenLight;
        return result;
    }

    public static Dictionary<LevyPressureType, float> Composition(Province province)
    {
        Dictionary<LevyPressureType, float> result = Pressure(province);
        float total = 0f; foreach (float value in result.Values) total += Mathf.Max(0f, value);
        if (total <= 0f) { result[LevyPressureType.LightInfantry] = 1f; return result; }
        List<LevyPressureType> keys = new List<LevyPressureType>(result.Keys);
        foreach (LevyPressureType key in keys) result[key] = Mathf.Max(0f, result[key]) / total;
        return result;
    }

    public static LevyPressureType RoleFor(Province province, ProvinceHolding holding)
    {
        Dictionary<LevyPressureType, float> shares = Composition(province);
        int hash = StableHash((holding != null ? holding.instanceId : string.Empty) + "|levy") % 10000;
        float cursor = hash / 10000f;
        if (cursor < shares[LevyPressureType.LightInfantry]) return LevyPressureType.LightInfantry;
        if (cursor < shares[LevyPressureType.LightInfantry] + shares[LevyPressureType.HeavyInfantry]) return LevyPressureType.HeavyInfantry;
        return LevyPressureType.Cavalry;
    }

    public static float EconomicValueModifierPercent(Province province, HoldingOutputType type)
    {
        float result = 0f;
        foreach (BuildingEconomicEffect effect in EffectsAffecting(province))
            if (effect.type == BuildingEconomicEffectType.EconomicValuePercent &&
                (effect.outputType == HoldingOutputType.Income || effect.outputType == type)) result += effect.amount;
        return result;
    }

    public static float FoodOutputModifierPercent(Province province)
    {
        float result = 0f;
        foreach (BuildingEconomicEffect effect in EffectsAffecting(province))
            if (effect.type == BuildingEconomicEffectType.FoodOutputPercent) result += effect.amount;
        return result;
    }

    public static List<BuildingEconomicEffect> EffectsAffecting(Province target)
    {
        if (target == null) return new List<BuildingEconomicEffect>();
        if (!EffectsByProvince.TryGetValue(target, out EffectCache cache))
        { cache = new EffectCache(); EffectsByProvince.Add(target, cache); }
        if (cache.frame == Time.frameCount) return cache.values;
        cache.frame = Time.frameCount; cache.values.Clear();
        foreach (Province source in RegionProvinces(target))
        {
            if (source.buildings == null) continue;
            foreach (ProvinceBuilding building in source.buildings)
            {
                if (building == null || building.definition == null || building.definition.economicEffects == null) continue;
                foreach (BuildingEconomicEffect effect in building.definition.economicEffects)
                    if (effect != null && building.level >= Mathf.Max(1, effect.minimumLevel) &&
                        (source == target || effect.scope == BuildingEffectScope.Region)) cache.values.Add(effect);
            }
        }
        return cache.values;
    }

    public static List<Province> RegionProvinces(Province province)
    {
        if (province == null) return new List<Province>();
        List<Province> result = province.GetOccupiedRegionProvinces(province.nation);
        if (result.Count == 0) result.Add(province);
        result.Sort((a, b) => string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));
        return result;
    }

    private static int StableHash(string value) { unchecked { int hash = 17; if (value != null) foreach (char c in value) hash = hash * 31 + c; return hash & int.MaxValue; } }
}

public static class BuildingPlacementSystem
{
    public static bool CanPlace(Province province, BuildingDefinition definition, int slotIndex, out string reason)
    {
        reason = string.Empty;
        if (province == null || definition == null) { reason = "Missing province or building definition."; return false; }
        if (definition.locationRequirement == BuildingLocationRequirement.Coastal && province.terrainProfile != CampaignTerrainProfile.Coastal)
        { reason = "Requires a coastal province."; return false; }
        BuildingPlacementLimit limit = definition.EffectivePlacementLimit;
        if (limit == BuildingPlacementLimit.Unlimited) return true;
        List<Province> scope = new List<Province>();
        if (limit == BuildingPlacementLimit.ProvinceUnique) scope.Add(province);
        else
        {
            CampaignRegion region = Owners.Instance != null ? Owners.Instance.CallRegionByString(province.region) : null;
            if (region != null && region.provincelist != null) scope.AddRange(region.provincelist); else scope.Add(province);
        }
        foreach (Province item in scope)
        {
            if (item == null) continue;
            if (item.buildings != null && item.buildings.Exists(value => value != null &&
                !(item == province && value.slotIndex == slotIndex) && string.Equals(value.BuildingId, definition.StableId, StringComparison.OrdinalIgnoreCase)))
            { reason = limit == BuildingPlacementLimit.RegionUnique ? "Only one may exist in this region." : "Only one may exist in this province."; return false; }
            if (item.constructionOrders != null && item.constructionOrders.Exists(value => value != null &&
                !(item == province && value.slotIndex == slotIndex) && string.Equals(value.buildingId, definition.StableId, StringComparison.OrdinalIgnoreCase)))
            { reason = limit == BuildingPlacementLimit.RegionUnique ? "Another is under construction in this region." : "Another is under construction in this province."; return false; }
        }
        return true;
    }
}
