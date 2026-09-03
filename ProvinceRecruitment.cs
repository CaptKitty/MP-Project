using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ProvinceBuilding
{
    public const int BarracksMaximumLevel = 10;
    public const int StandardMaximumLevel = 5;

    [Tooltip("Shared static building data. Legacy buildings may leave this empty and continue using id.")]
    public BuildingDefinition definition;
    public string id = "Barracks";
    public int level = 1;
    public int maxLevel = BarracksMaximumLevel;
    public int slotIndex = -1;
    public List<UnitSaveData> explicitUnitUnlocks = new List<UnitSaveData>();

    public string BuildingId => definition != null ? definition.StableId : id;
    public string DisplayName => definition != null ? definition.DisplayName : id;

    public int EffectiveMaximumLevel => definition != null
        ? Mathf.Max(1, definition.maximumLevel)
        : Mathf.Max(1, maxLevel);

    public BuildingLevelDefinition GetLevelDefinition(int targetLevel) =>
        definition != null ? definition.GetLevel(targetLevel) : null;

    public int DefinitionGoldIncome => SumDefinitionEffect(entry => entry.goldIncome);
    public int DefinitionGoldUpkeep => SumDefinitionEffect(entry => Mathf.Max(0, entry.goldUpkeep));
    public int DefinitionFoodOutput => SumDefinitionEffect(entry => entry.food);
    public int DefinitionFoodConsumption => SumDefinitionEffect(entry => Mathf.Max(0, entry.foodConsumption));
    public int DefinitionGarrisonCapacity => SumDefinitionEffect(entry => entry.garrisonCapacity);
    public float DefinitionManpowerRecovery => SumDefinitionFloatEffect(entry => entry.manpowerRecovery);

    private int SumDefinitionEffect(Func<BuildingLevelDefinition, int> selector)
    {
        if (definition == null || definition.levels == null) return 0;
        int total = 0;
        foreach (BuildingLevelDefinition entry in definition.levels)
            if (entry != null && entry.level <= level) total += selector(entry);
        return total;
    }

    private float SumDefinitionFloatEffect(Func<BuildingLevelDefinition, float> selector)
    {
        if (definition == null || definition.levels == null) return 0f;
        float total = 0f;
        foreach (BuildingLevelDefinition entry in definition.levels)
            if (entry != null && entry.level <= level) total += selector(entry);
        return total;
    }

    public int DefinitionGoldIncomeAt(int urbanization) => SumUrbanizedEffect(urbanization, entry => entry.goldIncome);
    public int DefinitionFoodOutputAt(int urbanization) => SumUrbanizedEffect(urbanization, entry => entry.food);
    public float DefinitionGoldIncomeUnrounded(float urbanization) => SumUrbanizedEffectUnrounded(urbanization, entry => entry.goldIncome);
    public float DefinitionFoodOutputUnrounded(float urbanization) => SumUrbanizedEffectUnrounded(urbanization, entry => entry.food);

    private int SumUrbanizedEffect(int urbanization, Func<BuildingLevelDefinition, int> selector)
    {
        if (definition == null || definition.levels == null) return 0;
        int total = 0;
        foreach (BuildingLevelDefinition entry in definition.levels)
            if (entry != null && entry.level <= level)
                total += UrbanizationOutputScaling.Apply(selector(entry), entry.urbanizationResponse, urbanization);
        return total;
    }

    private float SumUrbanizedEffectUnrounded(float urbanization, Func<BuildingLevelDefinition, int> selector)
    {
        if (definition == null || definition.levels == null) return 0f;
        float total = 0f;
        foreach (BuildingLevelDefinition entry in definition.levels)
            if (entry != null && entry.level <= level)
                total += UrbanizationOutputScaling.ApplyUnrounded(selector(entry), entry.urbanizationResponse, urbanization);
        return total;
    }

    private bool DefinitionUnlocks(UnitSaveData unit)
    {
        if (definition == null || definition.levels == null) return false;
        foreach (BuildingLevelDefinition entry in definition.levels)
            if (entry != null && entry.level <= level && entry.unitUnlocks != null && entry.unitUnlocks.Contains(unit)) return true;
        return false;
    }

    public bool Unlocks(UnitSaveData unit, Faction localFaction)
    {
        if (unit == null || level <= 0) return false;
        if (explicitUnitUnlocks.Contains(unit) || DefinitionUnlocks(unit)) return true;
        if (!BuildingId.Equals("Barracks", StringComparison.OrdinalIgnoreCase) || localFaction == null) return false;
        int tier = localFaction.GetBarracksTier(unit);
        return tier > 0 && tier <= level;
    }

    public bool Unlocks(UnitSaveData unit, Nation nation)
    {
        if (unit == null || level <= 0) return false;
        if (explicitUnitUnlocks.Contains(unit) || DefinitionUnlocks(unit)) return true;
        if (nation == null) return false;
        NationUnitEntry entry = NationContentResolver.GetUnitEntry(nation, unit);
        return entry != null && BuildingId.Equals(entry.RequiredBuildingId, StringComparison.OrdinalIgnoreCase) &&
            entry.minimumBuildingLevel <= level;
    }

    public static int MaximumLevelFor(string buildingId)
    {
        return !string.IsNullOrEmpty(buildingId) &&
            buildingId.Equals("Barracks", StringComparison.OrdinalIgnoreCase)
            ? BarracksMaximumLevel
            : StandardMaximumLevel;
    }
}

[Serializable]
public class ProvinceConstructionOrder
{
    public int slotIndex;
    public string buildingId;
    public int targetLevel = 1;
    public int remainingTicks;
    public bool initiatedByAI;
}

[Serializable]
public class ProvinceMercenaryPool
{
    // Data is retained for saves and future reactivation, but all recruitment paths respect this switch.
    public static readonly bool Enabled = false;
    public UnitSaveData unit;
    public int available;
    public int capacity = 3;
    public float regenerationPerTurn = 0.25f;
    public float regenerationProgress;

    public int EffectiveCapacity(Nation nation)
    {
        int result = Mathf.Max(0, capacity);
        return nation != null
            ? Mathf.Max(0, nation.ApplyLawModifiers(NationalLawEffectType.MercenaryPoolCapacity, result, null,
                CampaignUnitOrigin.Mercenary))
            : result;
    }

    public void Regenerate(Nation nation = null)
    {
        if (!Enabled) return;
        int currentCapacity = EffectiveCapacity(nation);
        if (unit == null || available >= currentCapacity) return;
        regenerationProgress += regenerationPerTurn;
        int gained = Mathf.FloorToInt(regenerationProgress);
        if (gained <= 0) return;
        available = Mathf.Min(currentCapacity, available + gained);
        regenerationProgress -= gained;
    }
}
 
