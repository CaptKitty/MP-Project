using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class NationUnitEntry
{
    public UnitSaveData unit;
    [Tooltip("Leave empty to use the Barracks, which is the default recruitment building.")]
    public BuildingDefinition requiredBuilding;
    [FormerlySerializedAs("minimumBarracksLevel")]
    [Min(1)] public int minimumBuildingLevel = 1;

    public string RequiredBuildingId => requiredBuilding != null ? requiredBuilding.StableId : "Barracks";
}

[Serializable]
public class NationContentLayer
{
    public List<NationUnitEntry> units = new List<NationUnitEntry>();
    public List<BuildingDefinition> buildings = new List<BuildingDefinition>();
    public List<string> generalNames = new List<string>();
    public List<string> flags = new List<string>();
    [Header("Allegiance data")]
    public List<string> AllegianceNames = new List<string>();
    [Tooltip("Singular political allegiance kind, for example Family, Tribe, or Party. Empty inherits from a broader identity layer.")]
    public string AllegianceType;
    [Header("Holding economy")]
    public List<HoldingTagModifier> holdingEconomyModifiers = new List<HoldingTagModifier>();
    [Header("Recoverable levies")]
    public List<LevyGrantRule> levies = new List<LevyGrantRule>();
    [Header("National laws")]
    public List<NationalLaw> laws = new List<NationalLaw>();
}

[Serializable]
public class FactionUnitReplacement
{
    [Tooltip("The inherited unit to replace.")]
    public UnitSaveData replace;
    [Tooltip("Leave null to remove the inherited unit without adding a replacement.")]
    public UnitSaveData with;
    [Tooltip("Leave empty to preserve the replaced unit's required building.")]
    public BuildingDefinition requiredBuilding;
    [Tooltip("Zero preserves the replaced unit's required building level.")]
    [FormerlySerializedAs("minimumBarracksLevel")]
    [Min(0)] public int minimumBuildingLevel;
}

[Serializable]
public class FactionBuildingReplacement
{
    public BuildingDefinition replace;
    [Tooltip("Leave null to remove the inherited building without adding a replacement.")]
    public BuildingDefinition with;
}

public static class NationContentResolver
{
    private static readonly string[] LegacyBuildings = { "Barracks", "Farm", "Fort" };

    public static string ResolveAssemblyName(Nation nation)
    {
        if (nation == null) return "Council of Aristocrats";
        if (nation.faction != null && !string.IsNullOrWhiteSpace(nation.faction.assemblyName))
            return nation.faction.assemblyName.Trim();
        if (nation.culture != null && !string.IsNullOrWhiteSpace(nation.culture.assemblyName))
            return nation.culture.assemblyName.Trim();
        if (nation.civilization != null && !string.IsNullOrWhiteSpace(nation.civilization.assemblyName))
            return nation.civilization.assemblyName.Trim();
        return "Council of Aristocrats";
    }

    public static string ResolveAllegianceType(Nation nation)
    {
        string result = "Party";
        if (nation == null) return result;
        ApplyAllegianceType(ref result, nation.civilization != null ? nation.civilization.content : null);
        ApplyAllegianceType(ref result, nation.culture != null ? nation.culture.content : null);
        ApplyAllegianceType(ref result, nation.religion != null ? nation.religion.content : null);
        ApplyAllegianceType(ref result, nation.faction != null ? nation.faction.content : null);
        return result;
    }

    public static List<string> ResolveAllegianceNames(Nation nation)
    {
        List<string> result = new List<string>();
        if (nation == null) return result;
        AddStrings(result, nation.civilization != null ? nation.civilization.content.AllegianceNames : null);
        AddStrings(result, nation.culture != null ? nation.culture.content.AllegianceNames : null);
        AddStrings(result, nation.religion != null ? nation.religion.content.AllegianceNames : null);
        AddStrings(result, nation.faction != null ? nation.faction.content.AllegianceNames : null);
        return result;
    }

    private static void ApplyAllegianceType(ref string target, NationContentLayer layer)
    {
        if (layer != null && !string.IsNullOrWhiteSpace(layer.AllegianceType))
            target = layer.AllegianceType.Trim();
    }

    public static List<NationalLaw> ResolveLaws(Nation nation)
    {
        List<NationalLaw> result = new List<NationalLaw>();
        if (nation == null) return result;
        AddLaws(result, nation.civilization != null ? nation.civilization.content : null);
        AddLaws(result, nation.culture != null ? nation.culture.content : null);
        AddLaws(result, nation.religion != null ? nation.religion.content : null);
        AddLaws(result, nation.faction != null ? nation.faction.content : null);
        return result;
    }

    private static void AddLaws(List<NationalLaw> target, NationContentLayer layer)
    {
        if (layer == null || layer.laws == null) return;
        foreach (NationalLaw law in layer.laws)
        {
            if (law == null || string.IsNullOrWhiteSpace(law.id)) continue;
            int existing = target.FindIndex(candidate => candidate != null &&
                string.Equals(candidate.id, law.id, StringComparison.OrdinalIgnoreCase));
            NationalLaw copy = law.Clone();
            if (existing >= 0) target[existing] = copy; else target.Add(copy);
        }
    }

    public static List<NationUnitEntry> ResolveUnits(Nation nation)
    {
        List<NationUnitEntry> result = new List<NationUnitEntry>();
        if (nation == null) return result;
        AddUnits(result, nation.civilization != null ? nation.civilization.content : null);
        AddUnits(result, nation.culture != null ? nation.culture.content : null);
        AddUnits(result, nation.religion != null ? nation.religion.content : null);
        AddUnits(result, nation.faction != null ? nation.faction.content : null);

        // Existing faction assets remain valid until they are migrated to identity layers.
        if (result.Count == 0 && nation.faction != null)
        {
            for (int i = 0; i < nation.faction.BarracksDataList.Count; i++)
                AddUnit(result, nation.faction.BarracksDataList[i], i + 1);
        }

        if (nation.faction != null && nation.faction.unitReplacements != null)
        {
            foreach (FactionUnitReplacement replacement in nation.faction.unitReplacements)
            {
                if (replacement == null || replacement.replace == null) continue;
                int index = FindUnit(result, replacement.replace);
                if (index < 0) continue;
                NationUnitEntry original = result[index];
                int tier = replacement.minimumBuildingLevel > 0
                    ? replacement.minimumBuildingLevel : original.minimumBuildingLevel;
                BuildingDefinition requiredBuilding = replacement.requiredBuilding != null
                    ? replacement.requiredBuilding : original.requiredBuilding;
                result.RemoveAt(index);
                if (replacement.with != null) AddUnit(result, replacement.with, tier, requiredBuilding);
            }
        }
        return result;
    }

    public static List<string> ResolveBuildings(Nation nation)
    {
        List<string> result = new List<string>();
        if (nation == null) return result;
        AddBuildings(result, nation.civilization != null ? nation.civilization.content.buildings : null);
        AddBuildings(result, nation.culture != null ? nation.culture.content.buildings : null);
        AddBuildings(result, nation.religion != null ? nation.religion.content.buildings : null);
        AddBuildings(result, nation.faction != null ? nation.faction.content.buildings : null);
        if (result.Count == 0) AddStrings(result, LegacyBuildings);

        if (nation.faction != null && nation.faction.buildingReplacements != null)
        {
            foreach (FactionBuildingReplacement replacement in nation.faction.buildingReplacements)
            {
                if (replacement == null || replacement.replace == null) continue;
                int index = result.FindIndex(value => SameId(value, replacement.replace.StableId));
                if (index < 0) continue;
                result.RemoveAt(index);
                if (replacement.with != null) AddString(result, replacement.with.StableId);
            }
        }
        return result;
    }

    public static List<string> ResolveGeneralNames(Nation nation)
    {
        List<string> result = new List<string>();
        if (nation == null) return result;
        AddStrings(result, nation.civilization != null ? nation.civilization.content.generalNames : null);
        AddStrings(result, nation.culture != null ? nation.culture.content.generalNames : null);
        AddStrings(result, nation.religion != null ? nation.religion.content.generalNames : null);
        AddStrings(result, nation.faction != null ? nation.faction.content.generalNames : null);
        return result;
    }

    public static string GenerateGeneralName(Nation nation, string stableIdentity)
    {
        List<string> names = ResolveGeneralNames(nation);
        if (names.Count == 0) return "Unnamed General";
        uint hash = 2166136261u;
        string source = (nation != null ? nation.name : string.Empty) + "|" + (stableIdentity ?? string.Empty);
        for (int i = 0; i < source.Length; i++) { hash ^= source[i]; hash *= 16777619u; }
        return names[(int)(hash % (uint)names.Count)];
    }

    public static int GetUnitTier(Nation nation, UnitSaveData unit)
    {
        NationUnitEntry entry = GetUnitEntry(nation, unit);
        return entry != null ? Mathf.Max(1, entry.minimumBuildingLevel) : 0;
    }

    public static NationUnitEntry GetUnitEntry(Nation nation, UnitSaveData unit)
    {
        List<NationUnitEntry> units = ResolveUnits(nation);
        int index = FindUnit(units, unit);
        return index >= 0 ? units[index] : null;
    }

    public static bool HasBuilding(Nation nation, string buildingId) =>
        ResolveBuildings(nation).Exists(value => SameId(value, buildingId));

    public static int UsefulBuildingMaximumLevel(Nation nation, string buildingId)
    {
        if (nation == null || string.IsNullOrWhiteSpace(buildingId)) return 0;
        BuildingDefinition definition = BuildingDefinition.Find(buildingId);
        int configuredMaximum = definition != null
            ? Mathf.Max(1, definition.maximumLevel)
            : ProvinceBuilding.MaximumLevelFor(buildingId);
        int highestUnlockLevel = 0;
        List<NationUnitEntry> roster = ResolveUnits(nation);
        foreach (NationUnitEntry entry in roster)
        {
            if (entry != null && entry.unit != null && SameId(entry.RequiredBuildingId, buildingId))
                highestUnlockLevel = Mathf.Max(highestUnlockLevel, entry.minimumBuildingLevel);
        }

        if (definition != null && definition.levels != null)
        {
            foreach (BuildingLevelDefinition level in definition.levels)
            {
                if (level == null || level.unitUnlocks == null) continue;
                foreach (UnitSaveData unit in level.unitUnlocks)
                    if (unit != null && roster.Exists(entry => entry != null && entry.unit == unit))
                        highestUnlockLevel = Mathf.Max(highestUnlockLevel, level.level);
            }
        }

        if (!IsRecruitmentBuilding(buildingId, roster, definition)) return configuredMaximum;
        return Mathf.Clamp(highestUnlockLevel, 0, configuredMaximum);
    }

    public static bool CanConstructBuildingLevel(Nation nation, string buildingId, int targetLevel)
    {
        return targetLevel >= 1 && targetLevel <= UsefulBuildingMaximumLevel(nation, buildingId);
    }

    public static bool IsRecruitmentBuilding(Nation nation, string buildingId)
    {
        BuildingDefinition definition = BuildingDefinition.Find(buildingId);
        return IsRecruitmentBuilding(buildingId, ResolveUnits(nation), definition);
    }

    private static bool IsRecruitmentBuilding(string buildingId, List<NationUnitEntry> roster,
        BuildingDefinition definition)
    {
        if (SameId(buildingId, "Barracks") ||
            buildingId.IndexOf("Mercenary", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (roster.Exists(entry => entry != null && SameId(entry.RequiredBuildingId, buildingId))) return true;
        return definition != null && definition.levels != null && definition.levels.Exists(level =>
            level != null && level.unitUnlocks != null && level.unitUnlocks.Count > 0);
    }

    public static bool HasFlag(Nation nation, string flag)
    {
        if (nation == null || string.IsNullOrEmpty(flag)) return false;
        if (nation.faction != null && nation.faction.HasFlag(flag)) return true;
        return LayerHasFlag(nation.civilization != null ? nation.civilization.content : null, flag) ||
            LayerHasFlag(nation.culture != null ? nation.culture.content : null, flag) ||
            LayerHasFlag(nation.religion != null ? nation.religion.content : null, flag) ||
            LayerHasFlag(nation.faction != null ? nation.faction.content : null, flag);
    }

    private static bool LayerHasFlag(NationContentLayer layer, string flag) =>
        layer != null && layer.flags != null && layer.flags.Exists(value => SameId(value, flag));

    private static void AddUnits(List<NationUnitEntry> target, NationContentLayer layer)
    {
        if (layer == null || layer.units == null) return;
        foreach (NationUnitEntry entry in layer.units)
            if (entry != null) AddUnit(target, entry.unit, entry.minimumBuildingLevel, entry.requiredBuilding);
    }

    private static void AddUnit(List<NationUnitEntry> target, UnitSaveData unit, int tier,
        BuildingDefinition requiredBuilding = null)
    {
        if (unit == null || FindUnit(target, unit) >= 0) return;
        target.Add(new NationUnitEntry
        {
            unit = unit,
            requiredBuilding = requiredBuilding,
            minimumBuildingLevel = Mathf.Max(1, tier)
        });
    }

    private static int FindUnit(List<NationUnitEntry> entries, UnitSaveData unit) =>
        entries.FindIndex(entry => entry != null && entry.unit != null &&
            (entry.unit == unit || SameId(entry.unit.name, unit != null ? unit.name : string.Empty)));

    private static void AddStrings(List<string> target, IEnumerable<string> values)
    {
        if (values == null) return;
        foreach (string value in values) AddString(target, value);
    }

    private static void AddBuildings(List<string> target, IEnumerable<BuildingDefinition> values)
    {
        if (values == null) return;
        foreach (BuildingDefinition value in values)
            if (value != null) AddString(target, value.StableId);
    }

    private static void AddString(List<string> target, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || target.Exists(existing => SameId(existing, value))) return;
        target.Add(value.Trim());
    }

    private static bool SameId(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
