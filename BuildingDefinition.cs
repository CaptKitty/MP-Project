using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Nation Identity/Building Definition")]
public class BuildingDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable save/network identifier. Do not change after the building is in use.")]
    public string id;
    public string displayName;
    [TextArea(2, 6)] public string description;
    public Sprite icon;

    [Header("Progression")]
    [Min(1)] public int maximumLevel = 5;
    [Tooltip("Used when a level does not provide its own positive construction time.")]
    [Min(1)] public int defaultConstructionTicks = 10;
    public List<BuildingLevelDefinition> levels = new List<BuildingLevelDefinition>();

    public string StableId => !string.IsNullOrWhiteSpace(id) ? id.Trim() : name;
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName : name;

    public BuildingLevelDefinition GetLevel(int level)
    {
        if (level <= 0 || levels == null) return null;
        return levels.Find(entry => entry != null && entry.level == level);
    }

    public int GoldCostForLevel(int level)
    {
        BuildingLevelDefinition entry = GetLevel(level);
        return entry != null ? Mathf.Max(0, entry.goldCost) : 0;
    }

    public int ConstructionTicksForLevel(int level)
    {
        BuildingLevelDefinition entry = GetLevel(level);
        if (entry != null && entry.constructionTicks > 0) return entry.constructionTicks;
        int levelScaledTicks = 10 + (Mathf.Max(1, level) - 1) * 5;
        return Mathf.Clamp(Mathf.Max(defaultConstructionTicks, levelScaledTicks), 10, 30);
    }

    public static BuildingDefinition Find(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId)) return null;
        BuildingDefinition[] definitions = Resources.LoadAll<BuildingDefinition>(string.Empty);
        return Array.Find(definitions, candidate => candidate != null &&
            string.Equals(candidate.StableId, stableId, StringComparison.OrdinalIgnoreCase));
    }

    public static int ConstructionTicks(string stableId, int level)
    {
        BuildingDefinition definition = Find(stableId);
        BuildingLevelDefinition configured = definition != null ? definition.GetLevel(level) : null;
        return definition != null ? definition.ConstructionTicksForLevel(level) : Mathf.Clamp(10 + (Mathf.Max(1, level) - 1) * 5, 10, 30);
    }

    private void OnValidate()
    {
        maximumLevel = Mathf.Max(1, maximumLevel);
        defaultConstructionTicks = Mathf.Max(1, defaultConstructionTicks);
        if (string.IsNullOrWhiteSpace(id)) id = name;
        if (levels == null) levels = new List<BuildingLevelDefinition>();
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] == null) continue;
            levels[i].level = Mathf.Clamp(levels[i].level, 1, maximumLevel);
            levels[i].goldCost = Mathf.Max(0, levels[i].goldCost);
            levels[i].constructionTicks = Mathf.Max(0, levels[i].constructionTicks);
        }
    }
}

[Serializable]
public class BuildingLevelDefinition
{
    [Min(1)] public int level = 1;
    [Min(0)] public int goldCost;
    [Min(0)] public int constructionTicks;

    [Header("Effects")]
    public int goldIncome;
    public int garrisonCapacity;
    public List<UnitSaveData> unitUnlocks = new List<UnitSaveData>();
    public List<string> flags = new List<string>();
    [Tooltip("Additional player-facing effects supplied by this level. These are accumulated in building tooltips.")]
    public List<string> displayedEffects = new List<string>();
}
