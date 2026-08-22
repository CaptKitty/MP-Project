using System;
using System.Collections.Generic;
using UnityEngine;

public enum CampaignUnitOrigin : byte { Professional, Levy, Mercenary, Garrison }
public enum LevyEntitlementState : byte { Available, Mobilizing, Raised, Recovering }

[Serializable]
public class LevyGrantRule
{
    [Tooltip("Stable identifier used by saves and multiplayer.")]
    public string id;
    public BuildingDefinition building;
    [Min(1)] public int minimumBuildingLevel = 1;
    [Min(1)] public int maximumBuildingLevel = 99;
    public UnitSaveData unit;
    [Min(1)] public int formationsPerBuilding = 1;
    [Min(0)] public int mobilizationTicks;
    [Min(0)] public int recoveryTicks = 20;
    [Min(0)] public int demobilizationTicks;
    public List<string> requiredNationFlags = new List<string>();
    public List<string> excludedNationFlags = new List<string>();

    public string StableId => !string.IsNullOrWhiteSpace(id) ? id.Trim() :
        ((building != null ? building.StableId : "Building") + ":" + (unit != null ? unit.name : "Unit"));
    public bool Applies(Province province, ProvinceBuilding instance)
    {
        if (province == null || province.nation == null || instance == null || unit == null || building == null ||
            !string.Equals(instance.BuildingId, building.StableId, StringComparison.OrdinalIgnoreCase) ||
            instance.level < minimumBuildingLevel || instance.level > maximumBuildingLevel) return false;
        foreach (string flag in requiredNationFlags) if (!NationContentResolver.HasFlag(province.nation, flag)) return false;
        foreach (string flag in excludedNationFlags) if (NationContentResolver.HasFlag(province.nation, flag)) return false;
        return true;
    }
}

[Serializable]
public class ProvinceLevyEntitlement
{
    public string id;
    public string ruleId;
    public string unitName;
    public UnitSaveData unit;
    public int buildingSlot;
    public int ordinal;
    public string beneficiaryNation;
    public LevyEntitlementState state;
    public bool eligible = true;
    public int remainingTicks;
    public string raisedArmyId;
}

[Serializable]
public class ArmyFormationRecord
{
    public UnitSaveData unit;
    public CampaignUnitOrigin origin;
    public string entitlementId;
}

public static class LevySystem
{
    public static List<LevyGrantRule> ResolveRules(Nation nation)
    {
        List<LevyGrantRule> result = new List<LevyGrantRule>();
        if (nation == null) return result;
        Add(result, nation.civilization != null ? nation.civilization.content : null);
        Add(result, nation.culture != null ? nation.culture.content : null);
        Add(result, nation.religion != null ? nation.religion.content : null);
        Add(result, nation.faction != null ? nation.faction.content : null);
        return result;
    }

    private static void Add(List<LevyGrantRule> target, NationContentLayer layer)
    {
        if (layer == null || layer.levies == null) return;
        foreach (LevyGrantRule rule in layer.levies)
            if (rule != null && rule.unit != null && !target.Exists(item => item.StableId == rule.StableId)) target.Add(rule);
    }

    public static LevyGrantRule FindRule(Nation nation, string id) =>
        ResolveRules(nation).Find(rule => string.Equals(rule.StableId, id, StringComparison.OrdinalIgnoreCase));
}
