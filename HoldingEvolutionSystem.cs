using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum HoldingTag
{
    None = 0,
    Agricultural = 1 << 0,
    Commercial = 1 << 1,
    Urban = 1 << 2,
    Rural = 1 << 3,
    Pastoral = 1 << 4,
    Artisan = 1 << 5,
    Mining = 1 << 6,
    Elite = 1 << 7,
    Servile = 1 << 8,
    Subsistence = 1 << 9,
    Military = 1 << 10
}

public enum LevyArchetype : byte
{
    None,
    LightJavelinInfantry,
    LightSpearInfantry,
    HeavySpearInfantry,
    TribalInfantry,
    LightInfantry,
    HeavyInfantry,
    LightCavalry,
    HeavyCavalry,
    RangedCavalry
}

[Serializable]
public sealed class HoldingTagModifier
{
    public HoldingTag tag;
    [Tooltip("Optional nation flag required for this modifier. Empty means always active.")]
    public string requiredNationFlag;
    [Tooltip("Signed desired-composition weight. Ten is a modest influence; fifty is a major influence.")]
    public float desiredWeight;
    [Tooltip("Signed percentage applied to the output of holdings carrying this tag.")]
    public float outputEfficiencyPercent;
}

[Serializable]
public sealed class HoldingEvolutionSettings
{
    [Min(1)] public int evaluationIntervalTicks = 1;
    [Min(1)] public int pressureRequired = 100;
    [Min(0)] public int pressureGainPerEvaluation = 25;
    [Min(0)] public int pressureDecayPerEvaluation = 5;
    [Min(0)] public int transformationCooldownTicks = 24;
    [Range(0, 100)] public int minimumImprovementPercent = 8;
    [Range(0, 100)] public int diversityWeightPercent = 20;
    [Min(1)] public int urbanizationChangeIntervalTicks = 8;
}

public static class HoldingEvolutionSystem
{
    private static readonly Dictionary<string, UnitSaveData> LevyResolutionCache =
        new Dictionary<string, UnitSaveData>(StringComparer.Ordinal);
    private static NationCultureData[] cachedCultures;
    private static List<HoldingDefinition> cachedHoldingDefinitions;
    private static HoldingDemandSettings cachedDemandSettings;
    private static readonly HoldingTag[] IndividualTags =
    {
        HoldingTag.Agricultural, HoldingTag.Commercial, HoldingTag.Urban, HoldingTag.Rural,
        HoldingTag.Pastoral, HoldingTag.Artisan, HoldingTag.Mining, HoldingTag.Elite,
        HoldingTag.Servile, HoldingTag.Subsistence, HoldingTag.Military
    };

    public static HoldingTag[] Tags => IndividualTags;

    private static HoldingDemandSettings DemandSettings
    {
        get
        {
            if (cachedDemandSettings == null)
                cachedDemandSettings = Resources.Load<HoldingDemandSettings>("HoldingDemandSettings");
            return cachedDemandSettings;
        }
    }

    private static float NeutralDemand(HoldingTag tag)
    {
        return DemandSettings != null ? DemandSettings.Evaluate(tag, 0f) : 0f;
    }

    public static HoldingTag EffectiveTags(HoldingDefinition definition)
    {
        if (definition == null) return HoldingTag.None;
        if (definition.tags != HoldingTag.None) return definition.tags;
        switch (definition.category)
        {
            case HoldingCategory.FreeFarmers:
                // Free farmers are universal agriculture: both rural and urban pressure
                // supports them, while their zero output response remains neutral.
                return HoldingTag.Agricultural | HoldingTag.Rural | HoldingTag.Urban;
            case HoldingCategory.TribalSubsistence: return HoldingTag.Agricultural | HoldingTag.Rural | HoldingTag.Subsistence;
            case HoldingCategory.EliteAgriculture: return HoldingTag.Elite;
            case HoldingCategory.CommercialAgriculture: return HoldingTag.Agricultural | HoldingTag.Commercial;
            case HoldingCategory.ServileAgriculture: return HoldingTag.Agricultural | HoldingTag.Servile;
            case HoldingCategory.Artisans: return HoldingTag.Artisan | HoldingTag.Urban | HoldingTag.Commercial;
            case HoldingCategory.Commerce: return HoldingTag.Commercial | HoldingTag.Urban;
            case HoldingCategory.Pastoralists: return HoldingTag.Pastoral;
            case HoldingCategory.Hunters: return HoldingTag.Rural | HoldingTag.Subsistence;
            case HoldingCategory.Mining: return HoldingTag.Mining;
            default: return HoldingTag.None;
        }
    }

    public static LevyArchetype EffectiveLevyArchetype(HoldingDefinition definition)
    {
        if (definition == null || !definition.canRaiseLevies) return LevyArchetype.None;
        if (definition.levyArchetype != LevyArchetype.None) return definition.levyArchetype;
        string value = definition.levyUnit != null
            ? (definition.levyUnit.name + " " + definition.levyUnit.unitname).ToLowerInvariant() : string.Empty;
        if (value.Contains("cavalry") || value.Contains("horse")) return value.Contains("light")
            ? LevyArchetype.LightCavalry : LevyArchetype.HeavyCavalry;
        if (value.Contains("trib")) return LevyArchetype.TribalInfantry;
        if (value.Contains("velite") || value.Contains("javelin")) return LevyArchetype.LightJavelinInfantry;
        if (value.Contains("heavy") || value.Contains("triarii")) return LevyArchetype.HeavyInfantry;
        return LevyArchetype.LightInfantry;
    }

    public static UnitSaveData ResolveLevyUnit(Province province, ProvinceHolding holding)
    {
        if (holding == null || holding.definition == null) return null;
        LevyArchetype archetype = EffectiveLevyArchetype(holding.definition);
        if (archetype == LevyArchetype.None) return holding.definition.levyUnit;
        string cultureName = holding.cultureName ?? string.Empty;
        string nationName = province != null && province.nation != null ? province.nation.name : string.Empty;
        string cacheKey = cultureName.ToLowerInvariant() + "|" + nationName.ToLowerInvariant() + "|" + (byte)archetype;
        if (LevyResolutionCache.TryGetValue(cacheKey, out UnitSaveData cached))
            return cached != null ? cached : holding.definition.levyUnit;
        List<UnitSaveData> candidates = CulturalCandidates(province, holding.cultureName);
        UnitSaveData best = null; int bestScore = int.MinValue;
        foreach (UnitSaveData unit in candidates)
        {
            int score = Score(unit, archetype);
            if (score > bestScore || score == bestScore && StableUnitId(unit).CompareTo(StableUnitId(best)) < 0)
            { best = unit; bestScore = score; }
        }
        UnitSaveData resolved = best != null && bestScore > -100 ? best : null;
        LevyResolutionCache[cacheKey] = resolved;
        return resolved != null ? resolved : holding.definition.levyUnit;
    }

    private static List<UnitSaveData> CulturalCandidates(Province province, string cultureName)
    {
        List<UnitSaveData> result = new List<UnitSaveData>();
        if (cachedCultures == null) cachedCultures = Resources.LoadAll<NationCultureData>("Prefabs/NationData/Culture");
        foreach (NationCultureData culture in cachedCultures)
            if (culture != null && culture.Matches(cultureName) && culture.content != null && culture.content.units != null)
                foreach (NationUnitEntry entry in culture.content.units) AddUnit(result, entry != null ? entry.unit : null);
        Nation nation = province != null ? province.nation : null;
        if (result.Count == 0 && nation != null)
            foreach (NationUnitEntry entry in NationContentResolver.ResolveUnits(nation)) AddUnit(result, entry != null ? entry.unit : null);
        return result;
    }

    public static void ClearLevyResolutionCache()
    {
        LevyResolutionCache.Clear();
        cachedCultures = null;
        cachedHoldingDefinitions = null;
    }

    private static void AddUnit(List<UnitSaveData> values, UnitSaveData unit)
    { if (unit != null && !values.Contains(unit) && !unit.Mercenary) values.Add(unit); }
    private static string StableUnitId(UnitSaveData unit) => unit != null
        ? (!string.IsNullOrWhiteSpace(unit.name) ? unit.name : unit.unitname ?? string.Empty) : "~";

    private static int Score(UnitSaveData unit, LevyArchetype archetype)
    {
        if (unit == null) return int.MinValue;
        string text = ((unit.name ?? string.Empty) + " " + (unit.unitname ?? string.Empty) + " " +
            (unit.flaglist != null ? string.Join(" ", unit.flaglist) : string.Empty)).ToLowerInvariant();
        bool cavalry = unit.unittype == UnitTypes.LightCavalry || unit.unittype == UnitTypes.HeavyCavalry ||
            text.Contains("cavalry") || text.Contains("horse") || text.Contains("mounted");
        bool ranged = unit.unittype == UnitTypes.Ranged || unit.RangedWeapon != null && unit.RangedWeapon.Throwable != null;
        bool javelin = text.Contains("javelin") || text.Contains("velite") || text.Contains("throw") ||
            unit.RangedWeapon != null && (unit.RangedWeapon.attacktype ?? string.Empty).ToLowerInvariant().Contains("pierc");
        bool spear = text.Contains("spear") || text.Contains("hoplite") || text.Contains("phalanx") || text.Contains("triarii");
        bool heavy = unit.unittype == UnitTypes.HeavyInfantry || unit.unittype == UnitTypes.HeavyCavalry ||
            text.Contains("heavy") || text.Contains("armored") || text.Contains("armoured") || text.Contains("triarii");
        bool tribal = text.Contains("trib") || text.Contains("warrior");
        int score = 0;
        switch (archetype)
        {
            case LevyArchetype.LightJavelinInfantry: score += !cavalry ? 30 : -40; score += javelin ? 70 : ranged ? 25 : -25; score += heavy ? -20 : 15; break;
            case LevyArchetype.LightSpearInfantry: score += !cavalry ? 30 : -40; score += spear ? 55 : -20; score += heavy ? -15 : 15; break;
            case LevyArchetype.HeavySpearInfantry: score += !cavalry ? 25 : -40; score += spear ? 55 : -25; score += heavy ? 30 : 0; break;
            case LevyArchetype.TribalInfantry: score += !cavalry ? 25 : -35; score += tribal ? 70 : -15; break;
            case LevyArchetype.LightInfantry: score += !cavalry ? 35 : -40; score += heavy ? -25 : 25; break;
            case LevyArchetype.HeavyInfantry: score += !cavalry ? 30 : -40; score += heavy ? 55 : -15; break;
            case LevyArchetype.LightCavalry: score += cavalry ? 65 : -80; score += heavy ? -20 : 20; break;
            case LevyArchetype.HeavyCavalry: score += cavalry ? 65 : -80; score += heavy ? 35 : -10; break;
            case LevyArchetype.RangedCavalry: score += cavalry ? 55 : -80; score += ranged ? 45 : -25; break;
        }
        score -= Mathf.Max(0, unit.cost / 100 - 3); // Prefer a close, affordable cultural levy when roles tie.
        return score;
    }

    public static Dictionary<HoldingTag, float> DesiredWeights(Province province)
    {
        float development = province != null ? Mathf.Clamp(province.urbanization, -100f, 100f) : 0f;
        Dictionary<HoldingTag, float> result = new Dictionary<HoldingTag, float>();
        HoldingDemandSettings settings = DemandSettings;
        foreach (HoldingTag tag in IndividualTags)
            result[tag] = settings != null ? settings.Evaluate(tag, development) : 0f;
        if (province == null) return result;
        // Small stable local preferences stop otherwise identical provinces from converging on one monoculture.
        foreach (HoldingTag tag in IndividualTags)
            result[tag] = Mathf.Max(0f, result[tag] + (PositiveHash(province.name + "|" + tag) % 11 - 5));
        Apply(result, province.baseHoldingTagDesires, province.nation);
        if (province.buildings != null) foreach (ProvinceBuilding building in province.buildings)
            if (building != null && building.definition != null && building.definition.levels != null)
                foreach (BuildingLevelDefinition level in building.definition.levels)
                    if (level != null && level.level <= building.level) Apply(result, level.holdingEconomyModifiers, province.nation);
        ProvinceLocalModifiers local = province.GetLocalModifiers();
        if (local != null) Apply(result, local.holdingEconomyModifiers, province.nation);
        ApplyNation(result, province.nation);
        return result;
    }

    private static void ApplyNation(Dictionary<HoldingTag, float> result, Nation nation)
    {
        if (nation == null) return;
        Apply(result, nation.holdingEconomyModifiers, nation);
        Apply(result, nation.civilization != null ? nation.civilization.content.holdingEconomyModifiers : null, nation);
        Apply(result, nation.culture != null ? nation.culture.content.holdingEconomyModifiers : null, nation);
        Apply(result, nation.religion != null ? nation.religion.content.holdingEconomyModifiers : null, nation);
        Apply(result, nation.faction != null ? nation.faction.content.holdingEconomyModifiers : null, nation);
    }

    private static void Apply(Dictionary<HoldingTag, float> result, List<HoldingTagModifier> modifiers, Nation nation)
    {
        if (modifiers == null) return;
        foreach (HoldingTagModifier modifier in modifiers) if (IsActive(modifier, nation))
            foreach (HoldingTag tag in IndividualTags) if ((modifier.tag & tag) != 0)
                result[tag] = Mathf.Max(0f, result[tag] + modifier.desiredWeight);
    }

    public static float OutputEfficiencyPercent(Province province, HoldingDefinition definition)
    {
        if (province == null || definition == null) return 0f;
        float result = 0f; HoldingTag tags = EffectiveTags(definition);
        result += HoldingDerivedEfficiencyPercent(province, definition);
        AccumulateEfficiency(ref result, tags, province.baseHoldingTagDesires, province.nation);
        if (province.buildings != null) foreach (ProvinceBuilding building in province.buildings)
            if (building != null && building.definition != null && building.definition.levels != null)
                foreach (BuildingLevelDefinition level in building.definition.levels)
                    if (level != null && level.level <= building.level)
                        AccumulateEfficiency(ref result, tags, level.holdingEconomyModifiers, province.nation);
        ProvinceLocalModifiers local = province.GetLocalModifiers();
        if (local != null) AccumulateEfficiency(ref result, tags, local.holdingEconomyModifiers, province.nation);
        if (province.nation != null)
        {
            AccumulateEfficiency(ref result, tags, province.nation.holdingEconomyModifiers, province.nation);
            AccumulateEfficiency(ref result, tags, province.nation.civilization != null ? province.nation.civilization.content.holdingEconomyModifiers : null, province.nation);
            AccumulateEfficiency(ref result, tags, province.nation.culture != null ? province.nation.culture.content.holdingEconomyModifiers : null, province.nation);
            AccumulateEfficiency(ref result, tags, province.nation.religion != null ? province.nation.religion.content.holdingEconomyModifiers : null, province.nation);
            AccumulateEfficiency(ref result, tags, province.nation.faction != null ? province.nation.faction.content.holdingEconomyModifiers : null, province.nation);
        }
        return Mathf.Clamp(result, -90f, 500f);
    }

    public static float HoldingDerivedEfficiencyPercent(Province province, HoldingDefinition receivingDefinition)
    {
        if (province == null || receivingDefinition == null || province.holdings == null ||
            (EffectiveTags(receivingDefinition) & HoldingTag.Servile) == 0) return 0f;
        float result = 0f;
        foreach (ProvinceHolding source in province.holdings)
        {
            if (source == null || source.definition == null ||
                source.definition.category != HoldingCategory.EliteAgriculture) continue;
            result += Mathf.Clamp(source.definition.categoryTier, 1, 3) * 5f;
        }
        return result;
    }

    private static void AccumulateEfficiency(ref float result, HoldingTag tags, List<HoldingTagModifier> modifiers, Nation nation)
    { if (modifiers != null) foreach (HoldingTagModifier modifier in modifiers)
        if (IsActive(modifier, nation) && (modifier.tag & tags) != 0) result += modifier.outputEfficiencyPercent; }

    private static bool IsActive(HoldingTagModifier modifier, Nation nation) => modifier != null &&
        (string.IsNullOrWhiteSpace(modifier.requiredNationFlag) || NationContentResolver.HasFlag(nation, modifier.requiredNationFlag));

    public static string TagList(HoldingDefinition definition)
    {
        List<string> values = new List<string>(); HoldingTag tags = EffectiveTags(definition);
        foreach (HoldingTag tag in IndividualTags) if ((tags & tag) != 0) values.Add(tag.ToString());
        return values.Count > 0 ? string.Join(", ", values) : "None";
    }

    public static void ProcessTick(Province province, int campaignTick)
    {
        if (province == null || province.holdings == null || province.holdings.Count == 0) return;
        HoldingEvolutionSettings settings = province.holdingEvolution ?? new HoldingEvolutionSettings();
        ProcessUrbanization(province, campaignTick, settings);
        foreach (ProvinceHolding holding in province.holdings)
            if (holding != null && holding.adaptationCooldownTicks > 0) holding.adaptationCooldownTicks--;
        int interval = Mathf.Max(1, settings.evaluationIntervalTicks);
        if (PositiveHash(province.name) % interval != PositiveHash(campaignTick) % interval) return;

        int validCount = 0;
        foreach (ProvinceHolding item in province.holdings)
            if (item != null && item.definition != null) validCount++;
        if (validCount == 0) return;
        int elapsedHoldingTicks = province.lastHoldingEvolutionTick < 0
            ? interval * validCount
            : Mathf.Max(interval, campaignTick - province.lastHoldingEvolutionTick);
        province.lastHoldingEvolutionTick = campaignTick;
        int pressureMultiplier = Mathf.Max(1,
            Mathf.CeilToInt(elapsedHoldingTicks / (float)(interval * validCount)));
        int selectedIndex = (PositiveHash(province.name) + province.holdingEvolutionCursor++) % validCount;
        ProvinceHolding selected = null;
        foreach (ProvinceHolding item in province.holdings)
        {
            if (item == null || item.definition == null) continue;
            if (selectedIndex-- == 0) { selected = item; break; }
        }
        if (selected == null) return;
        EvaluateHolding(province, selected, settings, pressureMultiplier);
    }

    public static int DesiredUrbanization(Province province)
    {
        if (province == null) return 0;
        // Population density drives development. Holding tags affect what holdings transform
        // into, but no longer feed back into development and thereby reinforce themselves.
        int populatedHoldings = province.holdings != null
            ? province.holdings.FindAll(holding => holding != null && holding.definition != null).Count : 0;
        float target = -50f + populatedHoldings * 2f;
        if (province.buildings != null) foreach (ProvinceBuilding building in province.buildings)
            if (building != null && building.definition != null && building.definition.levels != null)
                foreach (BuildingLevelDefinition level in building.definition.levels)
                    if (level != null && level.level <= building.level)
                        target += level.urbanizationTargetModifier;
        return Mathf.Clamp(Mathf.RoundToInt(target), -100, province.MaximumDevelopment);
    }

    private static void ProcessUrbanization(Province province, int campaignTick, HoldingEvolutionSettings settings)
    {
        int interval = Mathf.Max(1, settings.urbanizationChangeIntervalTicks);
        if (province.lastUrbanizationEvolutionTick < 0)
            province.lastUrbanizationEvolutionTick = campaignTick - interval;
        int elapsed = campaignTick - province.lastUrbanizationEvolutionTick;
        if (elapsed < interval) return;
        int elapsedSteps = Mathf.Max(1, elapsed / interval);
        province.lastUrbanizationEvolutionTick += elapsedSteps * interval;
        int target = DesiredUrbanization(province);
        Dictionary<HoldingTag, float> desired = DesiredWeights(province);
        int growthStep = 1 + Mathf.FloorToInt(
            Mathf.Max(0f, desired[HoldingTag.Commercial] - NeutralDemand(HoldingTag.Commercial)) / 25f);
        if (province.urbanization < target)
            province.urbanization = Mathf.Min(target, province.urbanization + growthStep * elapsedSteps);
        else if (province.urbanization > target)
            province.urbanization = Mathf.Max(target, province.urbanization - elapsedSteps);
        province.ClampDevelopment();
    }

    /// <summary>Hook for future edicts, levy casualties, loot returns, raids, and similar campaign events.</summary>
    public static void ApplyExternalPressure(Province province, string holdingInstanceId, string targetHoldingId, int pressure)
    {
        ProvinceHolding holding = province != null ? province.GetHolding(holdingInstanceId) : null;
        if (holding == null || string.IsNullOrWhiteSpace(targetHoldingId) || pressure == 0) return;
        if (!string.Equals(holding.adaptationTargetId, targetHoldingId, StringComparison.OrdinalIgnoreCase))
        { holding.adaptationTargetId = targetHoldingId; holding.adaptationPressure = 0; }
        holding.adaptationPressure = Mathf.Max(0, holding.adaptationPressure + pressure);
    }

    private static void EvaluateHolding(Province province, ProvinceHolding holding, HoldingEvolutionSettings settings,
        int pressureMultiplier)
    {
        if (holding.adaptationCooldownTicks > 0 || province.holdingConstructionOrders != null &&
            province.holdingConstructionOrders.Exists(order => order != null && order.slotIndex == holding.slotIndex)) return;
        Dictionary<HoldingTag, float> desired = DesiredWeights(province);
        List<HoldingDefinition> candidates = NaturalCandidates(province, holding);
        HoldingDefinition best = null; float bestImprovement = 0f;
        foreach (HoldingDefinition candidate in candidates)
        {
            float improvement = CompositionImprovement(province, holding, candidate, desired, settings.diversityWeightPercent);
            if (improvement > bestImprovement || Mathf.Approximately(improvement, bestImprovement) && best != null &&
                string.CompareOrdinal(candidate.StableId, best.StableId) < 0)
            { best = candidate; bestImprovement = improvement; }
        }
        if (best == null || bestImprovement < Mathf.Max(0, settings.minimumImprovementPercent))
        {
            holding.adaptationPressure = Mathf.Max(0, holding.adaptationPressure -
                Mathf.Max(0, settings.pressureDecayPerEvaluation) * Mathf.Max(1, pressureMultiplier));
            if (holding.adaptationPressure == 0) holding.adaptationTargetId = string.Empty;
            return;
        }
        if (!string.Equals(holding.adaptationTargetId, best.StableId, StringComparison.OrdinalIgnoreCase))
        {
            holding.adaptationTargetId = best.StableId;
            holding.adaptationPressure = Mathf.Max(0, holding.adaptationPressure -
                Mathf.Max(0, settings.pressureDecayPerEvaluation) * Mathf.Max(1, pressureMultiplier));
        }
        holding.adaptationPressure += Mathf.Max(1, settings.pressureGainPerEvaluation) * Mathf.Max(1, pressureMultiplier);
        if (holding.adaptationPressure < Mathf.Max(1, settings.pressureRequired)) return;

        // Natural evolution preserves the people attached to the holding. Only its economic form changes.
        holding.definition = best; holding.id = best.StableId; holding.level = 1;
        holding.adaptationTargetId = string.Empty; holding.adaptationPressure = 0;
        holding.adaptationCooldownTicks = Mathf.Max(0, settings.transformationCooldownTicks);
        province.ClampDevelopment(); province.ReconcileLevyEntitlements();
        province.RefreshGarrisonForFort();
    }

    private static List<HoldingDefinition> NaturalCandidates(Province province, ProvinceHolding holding)
    {
        HoldingDefinition current = holding != null ? holding.definition : null;
        if (current == null) return new List<HoldingDefinition>();
        HoldingArchetypeCatalog.ApplyMetadata(current);
        List<HoldingDefinition> all = AllDefinitions();
        List<HoldingDefinition> result = new List<HoldingDefinition>();
        if (current.transformations != null) foreach (HoldingTransformationOption option in current.transformations)
        {
            if (option == null || !option.IsAvailable(province)) continue;
            AddDefinition(result, all.Find(item => item != null && item.StableId.Equals(option.targetHoldingId,
                StringComparison.OrdinalIgnoreCase)));
        }
        // Reverse edges allow holdings to ruralize or otherwise move back only after the
        // forward path's environmental requirements cease to be valid.
        foreach (HoldingDefinition possibleSource in all)
            if (possibleSource != null && possibleSource.transformations != null && possibleSource.transformations.Exists(option =>
                option != null && option.targetHoldingId.Equals(current.StableId, StringComparison.OrdinalIgnoreCase) &&
                !option.IsAvailable(province)))
                AddDefinition(result, possibleSource);

        // Lateral economic adaptation is broad within a population class and tier. Urbanization,
        // buildings, laws and local tag pressure decide the preferred form; class is preserved.
        SocioEconomicClass holdingClass = SocioEconomicClassRules.Normalize(holding.socioEconomicClass);
        foreach (HoldingDefinition candidate in all)
            if (candidate != null && candidate.categoryTier == current.categoryTier &&
                SocioEconomicClassRules.Normalize(candidate.defaultClass) == holdingClass)
                AddDefinition(result, candidate);
        return result;
    }

    private static List<HoldingDefinition> AllDefinitions()
    {
        if (cachedHoldingDefinitions != null) return cachedHoldingDefinitions;
        List<HoldingDefinition> result = new List<HoldingDefinition>();
        foreach (HoldingDefinition item in Resources.LoadAll<HoldingDefinition>("Prefabs/NationData/HoldingData")) AddDefinition(result, item);
        foreach (HoldingDefinition item in HoldingArchetypeCatalog.All()) AddDefinition(result, item);
        cachedHoldingDefinitions = result;
        return cachedHoldingDefinitions;
    }

    public static HoldingDefinition FindCategoryTier(HoldingCategory category, int tier)
    {
        HoldingDefinition best = null;
        foreach (HoldingDefinition definition in AllDefinitions())
        {
            if (definition == null || definition.category != category || definition.categoryTier != tier) continue;
            if (best == null || string.CompareOrdinal(definition.StableId, best.StableId) < 0) best = definition;
        }
        return best;
    }

    private static void AddDefinition(List<HoldingDefinition> result, HoldingDefinition item)
    { if (item != null && !result.Exists(existing => existing.StableId.Equals(item.StableId, StringComparison.OrdinalIgnoreCase))) result.Add(item); }

    private static float CompositionImprovement(Province province, ProvinceHolding current, HoldingDefinition candidate,
        Dictionary<HoldingTag, float> desired, int diversityPercent)
    {
        float before = CompositionError(province, desired, null, null);
        float after = CompositionError(province, desired, current, candidate);
        float improvement = before - after;
        bool candidateAbsent = !province.holdings.Exists(item => item != null && item != current && item.definition != null &&
            item.definition.StableId.Equals(candidate.StableId, StringComparison.OrdinalIgnoreCase));
        // Diversity should create economic variety, not force a cosmetic/tier change between
        // holdings with the exact same economic tags and block a desired lateral path.
        if (candidateAbsent && EffectiveTags(current.definition) != EffectiveTags(candidate))
            improvement += Mathf.Max(0, diversityPercent);
        return improvement;
    }

    private static float CompositionError(Province province, Dictionary<HoldingTag, float> desired,
        ProvinceHolding replaced, HoldingDefinition replacement)
    {
        int populated = 0;
        foreach (ProvinceHolding item in province.holdings)
            if (item != null && item.definition != null) populated++;
        int count = Mathf.Max(1, populated);
        float error = 0f;
        foreach (HoldingTag tag in IndividualTags)
        {
            int present = 0;
            foreach (ProvinceHolding holding in province.holdings) if (holding != null && holding.definition != null)
            {
                HoldingDefinition definition = holding == replaced ? replacement : holding.definition;
                if ((EffectiveTags(definition) & tag) != 0) present++;
            }
            float currentPercent = present * 100f / count;
            float desiredPercent = Mathf.Clamp(desired[tag], 0f, 100f);
            error += Mathf.Abs(currentPercent - desiredPercent);
        }
        return error;
    }

    private static int PositiveHash(string value)
    {
        unchecked
        {
            int hash = 17;
            if (value != null) for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
            return hash & int.MaxValue;
        }
    }

    private static int PositiveHash(int value) => value & int.MaxValue;
}
