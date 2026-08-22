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
    public int DefinitionGarrisonCapacity => SumDefinitionEffect(entry => entry.garrisonCapacity);

    private int SumDefinitionEffect(Func<BuildingLevelDefinition, int> selector)
    {
        if (definition == null || definition.levels == null) return 0;
        int total = 0;
        foreach (BuildingLevelDefinition entry in definition.levels)
            if (entry != null && entry.level <= level) total += selector(entry);
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
    public const bool Enabled = false;
    public UnitSaveData unit;
    public int available;
    public int capacity = 3;
    public float regenerationPerTurn = 0.25f;
    public float regenerationProgress;

    public void Regenerate()
    {
        if (!Enabled) return;
        if (unit == null || available >= capacity) return;
        regenerationProgress += regenerationPerTurn;
        int gained = Mathf.FloorToInt(regenerationProgress);
        if (gained <= 0) return;
        available = Mathf.Min(capacity, available + gained);
        regenerationProgress -= gained;
    }
}
 
