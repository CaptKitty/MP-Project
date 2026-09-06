using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Nation Identity/Holding Definition")]
public sealed class HoldingDefinition : ScriptableObject
{
    private const string ResourcePath = "Prefabs/NationData/HoldingData";
    private static HoldingDefinition[] cachedDefinitions;
    [Header("Identity")]
    [Tooltip("Stable save identifier. Do not change after using this holding in a campaign.")]
    public string id;
    public string displayName;
    [TextArea(2, 6)] public string description;
    public Sprite icon;
    public HoldingEconomicType economicType;

    [Header("Economy")]
    [Tooltip("Class-scaled output pipeline. Empty uses the migration profile for Economic Type.")]
    public List<HoldingEconomicOutputDefinition> economicOutputs = new List<HoldingEconomicOutputDefinition>();
    [Tooltip("Food consumed by each holding instance per tick. This remains active while its levy is mobilized.")]
    [Min(0)] public int foodConsumption = 1;
    [Tooltip("Additional food upkeep beyond the universal one-food holding consumption.")]
    [Min(0)] public int foodUpkeep;
    [Tooltip("Additional formations supported by this holding in the provincial garrison.")]
    [Min(0)] public int garrisonCapacity;

    [Header("People")]
    public SocioEconomicClass defaultClass = SocioEconomicClass.Freemen;

    [Header("Levy composition pressure")]
    [Min(0f)] public float lightInfantryPressure;
    [Min(0f)] public float lineInfantryPressure;
    [Min(0f)] public float heavyInfantryPressure;
    [Min(0f)] public float rangedInfantryPressure;
    [Min(0f)] public float lightCavalryPressure;
    [Min(0f)] public float shockCavalryPressure;
    [Min(0f)] public float chariotPressure;

    public void AddLevyPressure(Dictionary<LevyPressureType, float> target)
    {
        target[LevyPressureType.LightInfantry] += lightInfantryPressure;
        target[LevyPressureType.LineInfantry] += lineInfantryPressure;
        target[LevyPressureType.HeavyInfantry] += heavyInfantryPressure;
        target[LevyPressureType.RangedInfantry] += rangedInfantryPressure;
        target[LevyPressureType.LightCavalry] += lightCavalryPressure;
        target[LevyPressureType.ShockCavalry] += shockCavalryPressure;
        target[LevyPressureType.Chariot] += chariotPressure;
    }

    public string StableId => !string.IsNullOrWhiteSpace(id) ? id.Trim() : name;
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName : name;
    public HoldingEconomicType EffectiveEconomicType => HoldingEconomySystem.ResolveType(this);

    public static HoldingDefinition Find(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId)) return null;
        string canonicalId = HoldingArchetypeCatalog.CanonicalizeId(stableId);
        HoldingDefinition found = Array.Find(cachedDefinitions ?? (cachedDefinitions = Resources.LoadAll<HoldingDefinition>(ResourcePath)), candidate => candidate != null &&
            string.Equals(candidate.StableId, canonicalId, StringComparison.OrdinalIgnoreCase));
        if (found != null) { HoldingArchetypeCatalog.ApplyMetadata(found); return found; }
        found = HoldingArchetypeCatalog.Find(canonicalId);
        if (found != null) return found;
        return null;
    }

    private static HoldingDefinition defaultCitizenFarm;

    public static HoldingDefinition DefaultCitizenFarm()
    {
        return defaultCitizenFarm != null ? defaultCitizenFarm : (defaultCitizenFarm = HoldingArchetypeCatalog.Find(HoldingEconomicType.Farm));
    }

}
