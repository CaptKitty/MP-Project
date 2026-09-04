using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Authoritative, deterministic holding economy. Legacy tier and urbanization data remain serialized only.</summary>
public static class HoldingEconomySystem
{
    public struct ClassMultipliers
    {
        public float raw, skilled, value;
        public ClassMultipliers(float raw, float skilled, float value)
        { this.raw = raw; this.skilled = skilled; this.value = value; }
        public float For(HoldingLabourCategory category) => category == HoldingLabourCategory.Raw ? raw :
            category == HoldingLabourCategory.Skilled ? skilled : value;
    }

    private static readonly HoldingEconomicType[] Types = { HoldingEconomicType.Farm, HoldingEconomicType.Pasture,
        HoldingEconomicType.Workshop, HoldingEconomicType.Commerce, HoldingEconomicType.Mine, HoldingEconomicType.Fishery };
    private static readonly SocioEconomicClass[] Classes = { SocioEconomicClass.Freemen, SocioEconomicClass.Citizen,
        SocioEconomicClass.Enslaved, SocioEconomicClass.Tribesman, SocioEconomicClass.Elite };
    private static HoldingDefinition[] definitions;

    public static HoldingEconomicType ResolveType(HoldingDefinition definition)
    {
        if (definition == null) return HoldingEconomicType.Unspecified;
        if (definition.economicType != HoldingEconomicType.Unspecified) return definition.economicType;
        switch (definition.category)
        {
            case HoldingCategory.Pastoralists:
            case HoldingCategory.Hunters: return HoldingEconomicType.Pasture;
            case HoldingCategory.Artisans: return HoldingEconomicType.Workshop;
            case HoldingCategory.Commerce: return HoldingEconomicType.Commerce;
            case HoldingCategory.Mining: return HoldingEconomicType.Mine;
            default: return HoldingEconomicType.Farm;
        }
    }

    public static ClassMultipliers Multipliers(SocioEconomicClass socialClass)
    {
        switch (SocioEconomicClassRules.Normalize(socialClass))
        {
            case SocioEconomicClass.Citizen: return new ClassMultipliers(1f, 2f, 1f);
            case SocioEconomicClass.Enslaved: return new ClassMultipliers(2f, .5f, 1f);
            case SocioEconomicClass.Tribesman: return new ClassMultipliers(1f, .5f, 1f);
            case SocioEconomicClass.Elite: return new ClassMultipliers(.5f, 1f, 2f);
            default: return new ClassMultipliers(1f, 1f, 1f);
        }
    }

    public static float GetOutput(Province province, ProvinceHolding holding, HoldingOutputType requested,
        bool mobilized, float externalEfficiency)
    {
        if (holding == null || holding.definition == null || requested == HoldingOutputType.PoliticalInfluence) return 0f;
        float result = 0f;
        if (requested == HoldingOutputType.Income)
        {
            result += TypedValue(province, holding, HoldingOutputType.AgriculturalValue, mobilized) * .1f *
                (1f + LevyEconomySystem.EconomicValueModifierPercent(province, HoldingOutputType.AgriculturalValue) / 100f) *
                ValueTradeSystem.RemainingFraction(province, HoldingOutputType.AgriculturalValue);
            result += TypedValue(province, holding, HoldingOutputType.IndustrialValue, mobilized) * .1f *
                (1f + LevyEconomySystem.EconomicValueModifierPercent(province, HoldingOutputType.IndustrialValue) / 100f) *
                ValueTradeSystem.RemainingFraction(province, HoldingOutputType.IndustrialValue);
            result += TypedValue(province, holding, HoldingOutputType.CommercialValue, mobilized) * .1f *
                (1f + LevyEconomySystem.EconomicValueModifierPercent(province, HoldingOutputType.CommercialValue) / 100f) *
                ValueTradeSystem.RemainingFraction(province, HoldingOutputType.CommercialValue);
        }
        else
        {
            result = TypedValue(province, holding, requested, mobilized);
            if (requested == HoldingOutputType.AgriculturalValue || requested == HoldingOutputType.IndustrialValue ||
                requested == HoldingOutputType.CommercialValue)
                result *= 1f + LevyEconomySystem.EconomicValueModifierPercent(province, requested) / 100f;
        }
        if (requested == HoldingOutputType.Food)
            result *= 1f + LevyEconomySystem.FoodOutputModifierPercent(province) / 100f;
        if (requested == HoldingOutputType.Income || requested == HoldingOutputType.AgriculturalValue ||
            requested == HoldingOutputType.IndustrialValue || requested == HoldingOutputType.CommercialValue)
            result *= NationContentResolver.ClassValueMultiplier(province != null ? province.nation : null,
                SocioEconomicClassRules.Normalize(holding.socioEconomicClass));
        if (requested == HoldingOutputType.Food || requested == HoldingOutputType.Income ||
            requested == HoldingOutputType.AgriculturalValue || requested == HoldingOutputType.IndustrialValue ||
            requested == HoldingOutputType.CommercialValue)
            result *= LevyEconomySystem.OutputMultiplier(province, holding);
        result *= Mathf.Max(0f, externalEfficiency);
        return requested == HoldingOutputType.Food ? result - holding.FoodConsumption : result;
    }

    private static float TypedValue(Province province, ProvinceHolding holding, HoldingOutputType type, bool mobilized)
    {
        float result = 0f;
        List<HoldingEconomicOutputDefinition> configured = holding.definition.economicOutputs;
        if (configured != null && configured.Count > 0)
        {
            foreach (HoldingEconomicOutputDefinition output in configured)
                if (output != null && output.type == type && !(mobilized && output.disabledWhileMobilized))
                    result += output.baseValue * Multipliers(holding.socioEconomicClass).For(EffectiveCategory(output.type, output.labourCategory));
            return result;
        }
        HoldingEconomicType economicType = ResolveType(holding.definition);
        ClassMultipliers multiplier = Multipliers(holding.socioEconomicClass);
        if (type == HoldingOutputType.Food)
        {
            float food = economicType == HoldingEconomicType.Farm || economicType == HoldingEconomicType.Pasture ? 2f :
                economicType == HoldingEconomicType.Fishery ? 2f : 0f;
            return food * multiplier.raw;
        }
        if (type == HoldingOutputType.AgriculturalValue)
            return (economicType == HoldingEconomicType.Farm ? 10f : economicType == HoldingEconomicType.Pasture ? 8f :
                economicType == HoldingEconomicType.Fishery ? 6f : 0f) * multiplier.value;
        if (type == HoldingOutputType.IndustrialValue)
            return (economicType == HoldingEconomicType.Workshop ? 10f * multiplier.skilled :
                economicType == HoldingEconomicType.Mine ? 10f * multiplier.raw : 0f);
        if (type == HoldingOutputType.CommercialValue)
            return economicType == HoldingEconomicType.Commerce ? 10f * multiplier.value : 0f;
        return result;
    }

    private static HoldingLabourCategory EffectiveCategory(HoldingOutputType type, HoldingLabourCategory category)
    {
        if (category != HoldingLabourCategory.Automatic) return category;
        if (type == HoldingOutputType.Food || type == HoldingOutputType.Manpower) return HoldingLabourCategory.Raw;
        return type == HoldingOutputType.AgriculturalValue || type == HoldingOutputType.IndustrialValue ||
            type == HoldingOutputType.CommercialValue || type == HoldingOutputType.Income
            ? HoldingLabourCategory.Value : HoldingLabourCategory.Skilled;
    }

    public static void AddTypePressure(Province province, HoldingEconomicType type, float amount, string source)
    {
        if (province == null) return;
        if (province.holdingTypePressures == null) province.holdingTypePressures = new List<HoldingTypePressure>();
        province.holdingTypePressures.Add(new HoldingTypePressure { type = type, amount = amount, source = source });
    }

    public static void AddClassPressure(Province province, SocioEconomicClass socialClass, float amount, string source)
    {
        if (province == null) return;
        if (province.holdingClassPressures == null) province.holdingClassPressures = new List<HoldingClassPressure>();
        province.holdingClassPressures.Add(new HoldingClassPressure { socialClass = socialClass, amount = amount, source = source });
    }

    public static void ProcessTick(Province province, int tick)
    {
        if (province == null || province.holdings == null || province.holdings.Count == 0) return;
        foreach (ProvinceHolding holding in province.holdings)
            if (holding != null && holding.adaptationCooldownTicks > 0) holding.adaptationCooldownTicks--;
        int phase = PositiveHash(province.name) % 24;
        if (PositiveHash(tick) % 24 != phase) return;
        bool typeTurn = ((tick / 24) & 1) == 0;
        if (typeTurn) ConvergeType(province); else ConvergeClass(province);
    }

    private static void ConvergeType(Province province)
    {
        Dictionary<HoldingEconomicType, float> target = TypeShares(province);
        Dictionary<HoldingEconomicType, int> current = new Dictionary<HoldingEconomicType, int>();
        foreach (ProvinceHolding holding in province.holdings) if (holding != null && holding.definition != null)
        { HoldingEconomicType key = ResolveType(holding.definition); current[key] = Count(current, key) + 1; }
        HoldingEconomicType wanted = LargestDeficit(Types, target, current, province.holdings.Count);
        ProvinceHolding source = null; float surplus = 0f;
        foreach (ProvinceHolding holding in province.holdings)
        {
            if (holding == null || holding.definition == null || holding.adaptationCooldownTicks > 0 ||
                ResolveType(holding.definition) == wanted) continue;
            HoldingEconomicType key = ResolveType(holding.definition);
            float value = Count(current, key) - target[key] * province.holdings.Count;
            if (value > surplus) { surplus = value; source = holding; }
        }
        HoldingDefinition replacement = FindDefinition(wanted, source != null ? source.socioEconomicClass : SocioEconomicClass.Freemen);
        if (source == null || replacement == null || target[wanted] * province.holdings.Count - Count(current, wanted) < .5f) return;
        source.definition = replacement; source.id = replacement.StableId; source.level = 1; source.adaptationCooldownTicks = 48;
    }

    private static void ConvergeClass(Province province)
    {
        Dictionary<SocioEconomicClass, float> target = ClassShares(province);
        Dictionary<SocioEconomicClass, int> current = new Dictionary<SocioEconomicClass, int>();
        foreach (ProvinceHolding holding in province.holdings) if (holding != null)
        { SocioEconomicClass key = SocioEconomicClassRules.Normalize(holding.socioEconomicClass); current[key] = Count(current, key) + 1; }
        SocioEconomicClass wanted = LargestDeficit(Classes, target, current, province.holdings.Count);
        ProvinceHolding source = null; float surplus = 0f;
        foreach (ProvinceHolding holding in province.holdings)
        {
            SocioEconomicClass key = holding != null ? SocioEconomicClassRules.Normalize(holding.socioEconomicClass) : wanted;
            if (holding == null || holding.adaptationCooldownTicks > 0 || key == wanted || !IsEligible(province, holding, wanted)) continue;
            float value = Count(current, key) - target[key] * province.holdings.Count;
            if (value > surplus) { surplus = value; source = holding; }
        }
        if (source == null || target[wanted] * province.holdings.Count - Count(current, wanted) < .5f) return;
        source.socioEconomicClass = wanted; source.adaptationCooldownTicks = 48;
    }

    public static Dictionary<HoldingEconomicType, float> TypeShares(Province province)
    {
        Dictionary<HoldingEconomicType, float> weights = new Dictionary<HoldingEconomicType, float>();
        foreach (HoldingEconomicType type in Types) weights[type] = type == HoldingEconomicType.Farm ? 40f :
            type == HoldingEconomicType.Pasture ? 20f : type == HoldingEconomicType.Workshop ? 15f :
            type == HoldingEconomicType.Commerce ? 15f : type == HoldingEconomicType.Mine ? 10f : 0f;
        foreach (BuildingEconomicEffect effect in LevyEconomySystem.EffectsAffecting(province))
            if (effect.type == BuildingEconomicEffectType.HoldingTypePressure && effect.holdingType != HoldingEconomicType.Unspecified)
                weights[effect.holdingType] = Mathf.Max(0f, weights[effect.holdingType] + effect.amount);
        if (province != null && province.holdingTypePressures != null) foreach (HoldingTypePressure pressure in province.holdingTypePressures)
            if (pressure != null && pressure.type != HoldingEconomicType.Unspecified) weights[pressure.type] = Mathf.Max(0f, weights[pressure.type] + pressure.amount);
        Normalize(weights); return weights;
    }

    public static Dictionary<SocioEconomicClass, float> ClassShares(Province province)
    {
        Dictionary<SocioEconomicClass, float> weights = new Dictionary<SocioEconomicClass, float>();
        Nation nation = province != null ? province.nation : null;
        CivilizationClassBaseline baseline = nation != null && nation.civilization != null
            ? nation.civilization.classBaseline : null;
        foreach (SocioEconomicClass item in Classes)
            weights[item] = baseline != null ? baseline.Weight(item) : item == SocioEconomicClass.Freemen ? 40f : 15f;
        if (province != null && province.holdingClassPressures != null) foreach (HoldingClassPressure pressure in province.holdingClassPressures)
        { SocioEconomicClass key = SocioEconomicClassRules.Normalize(pressure.socialClass); if (weights.ContainsKey(key)) weights[key] = Mathf.Max(0f, weights[key] + pressure.amount); }
        foreach (BuildingEconomicEffect effect in LevyEconomySystem.EffectsAffecting(province))
            if (effect.type == BuildingEconomicEffectType.ClassPressure)
            { SocioEconomicClass key = SocioEconomicClassRules.Normalize(effect.socialClass); if (weights.ContainsKey(key)) weights[key] = Mathf.Max(0f, weights[key] + effect.amount); }
        if (province != null && weights[SocioEconomicClass.Citizen] > 0f)
        {
            bool anyEligible = false;
            if (province.holdings != null) foreach (ProvinceHolding holding in province.holdings)
                if (IsEligible(province, holding, SocioEconomicClass.Citizen)) { anyEligible = true; break; }
            if (!anyEligible) weights[SocioEconomicClass.Citizen] = 0f;
        }
        Normalize(weights); return weights;
    }

    public static bool IsEligible(Province province, ProvinceHolding holding, SocioEconomicClass target)
    {
        target = SocioEconomicClassRules.Normalize(target);
        if (target != SocioEconomicClass.Citizen) return true;
        if (province == null || province.nation == null || !string.Equals(province.nation.name, "Rome", StringComparison.OrdinalIgnoreCase)) return true;
        return holding != null && string.Equals(holding.cultureName, "Latin", StringComparison.OrdinalIgnoreCase);
    }

    private static HoldingDefinition FindDefinition(HoldingEconomicType type, SocioEconomicClass socialClass)
    {
        HoldingDefinition canonical = HoldingArchetypeCatalog.Find(type);
        if (canonical != null) return canonical;
        if (definitions == null) definitions = Resources.LoadAll<HoldingDefinition>("Prefabs/NationData/HoldingData");
        HoldingDefinition fallback = null;
        foreach (HoldingDefinition item in definitions)
        {
            if (item == null || ResolveType(item) != type) continue;
            if (fallback == null || string.CompareOrdinal(item.StableId, fallback.StableId) < 0) fallback = item;
            if (SocioEconomicClassRules.Normalize(item.defaultClass) == SocioEconomicClassRules.Normalize(socialClass)) return item;
        }
        return fallback;
    }

    private static int Count<T>(Dictionary<T, int> values, T key) => values.TryGetValue(key, out int value) ? value : 0;
    private static T LargestDeficit<T>(T[] options, Dictionary<T, float> target, Dictionary<T, int> current, int total)
    {
        T result = options[0]; float best = float.MinValue;
        foreach (T option in options) { float deficit = target[option] * total - Count(current, option); if (deficit > best) { best = deficit; result = option; } }
        return result;
    }
    private static void Normalize<T>(Dictionary<T, float> values)
    {
        float total = 0f; foreach (float value in values.Values) total += Mathf.Max(0f, value);
        if (total <= 0f) { float equal = 1f / values.Count; List<T> keys = new List<T>(values.Keys); foreach (T key in keys) values[key] = equal; return; }
        List<T> normalizedKeys = new List<T>(values.Keys); foreach (T key in normalizedKeys) values[key] = Mathf.Max(0f, values[key]) / total;
    }
    private static int PositiveHash(string value) { unchecked { int hash = 17; if (value != null) foreach (char c in value) hash = hash * 31 + c; return hash & int.MaxValue; } }
    private static int PositiveHash(int value) => value & int.MaxValue;
}
