using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Canonical holdings used by the rewritten class economy. Old holding IDs are
/// accepted as save aliases, but never create an old tier/category holding at runtime.
/// </summary>
public static class HoldingArchetypeCatalog
{
    private static Dictionary<string, HoldingDefinition> definitions;
    private static readonly Dictionary<string, string> LegacyAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "TenantFarm", "Farm" }, { "CitizenFarm", "Farm" }, { "Freehold", "Farm" },
        { "Homestead", "Farm" }, { "Hamlet", "Farm" }, { "Village", "Farm" },
        { "Manor", "Farm" }, { "Estate", "Farm" }, { "GreatEstate", "Farm" },
        { "ChiefHousehold", "Pasture" }, { "ChieftainsHall", "Pasture" }, { "GreatHall", "Pasture" },
        { "NobleResidence", "Commerce" }, { "AristocraticPalace", "Commerce" }, { "GrandPalace", "Commerce" },
        { "MarketGarden", "Farm" }, { "CommercialFarm", "Farm" }, { "CashCropFarm", "Farm" },
        { "SlaveFarm", "Farm" }, { "Plantation", "Farm" }, { "GreatPlantation", "Farm" },
        { "CottageWorkshop", "Workshop" }, { "ArtisanQuarter", "Workshop" },
        { "TraderHousehold", "Commerce" }, { "MerchantHolding", "Commerce" }, { "MerchantHouse", "Commerce" },
        { "HerdingHomestead", "Pasture" }, { "PastoralHolding", "Pasture" }, { "PastoralEstate", "Pasture" },
        { "HunterCamp", "Pasture" }, { "HunterCommunity", "Pasture" }, { "HunterLodge", "Pasture" },
        { "SurfaceWorking", "Mine" }, { "MiningSettlement", "Mine" }
    };

    public static string CanonicalizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return id;
        string trimmed = id.Trim();
        if (trimmed.StartsWith("CitizenFarm:", StringComparison.OrdinalIgnoreCase)) return "Farm";
        return LegacyAliases.TryGetValue(trimmed, out string canonical) ? canonical : trimmed;
    }

    public static HoldingDefinition Find(string id)
    {
        EnsureBuilt();
        definitions.TryGetValue(CanonicalizeId(id) ?? string.Empty, out HoldingDefinition result);
        return result;
    }

    public static HoldingDefinition Find(HoldingEconomicType type)
    {
        EnsureBuilt();
        return definitions.TryGetValue(type.ToString(), out HoldingDefinition result) ? result : null;
    }

    public static List<HoldingDefinition> All()
    {
        EnsureBuilt();
        return new List<HoldingDefinition>(definitions.Values);
    }

    // Kept as a no-op compatibility entry point for old callers/assets.
    public static void ApplyMetadata(HoldingDefinition definition) { }

    private static void EnsureBuilt()
    {
        if (definitions != null) return;
        definitions = new Dictionary<string, HoldingDefinition>(StringComparer.OrdinalIgnoreCase);
        Add(HoldingEconomicType.Farm, "Farm", "Produces food and agricultural value.", HoldingOutputType.Food, 2f,
            HoldingOutputType.AgriculturalValue, 10f);
        Add(HoldingEconomicType.Pasture, "Pasture", "Produces food and agricultural value.", HoldingOutputType.Food, 2f,
            HoldingOutputType.AgriculturalValue, 8f);
        Add(HoldingEconomicType.Workshop, "Workshop", "Produces industrial value through skilled labour.",
            HoldingOutputType.IndustrialValue, 10f, HoldingOutputType.PoliticalInfluence, 0f, HoldingLabourCategory.Skilled);
        Add(HoldingEconomicType.Commerce, "Commerce", "Produces commercial value.", HoldingOutputType.CommercialValue, 10f);
        Add(HoldingEconomicType.Mine, "Mine", "Produces industrial value through raw labour.", HoldingOutputType.IndustrialValue, 10f,
            HoldingOutputType.PoliticalInfluence, 0f, HoldingLabourCategory.Raw);
        Add(HoldingEconomicType.Fishery, "Fishery", "Produces food and agricultural value.", HoldingOutputType.Food, 2f,
            HoldingOutputType.AgriculturalValue, 6f);
    }

    private static void Add(HoldingEconomicType type, string displayName, string description,
        HoldingOutputType firstType, float firstValue, HoldingOutputType secondType = HoldingOutputType.PoliticalInfluence,
        float secondValue = 0f, HoldingLabourCategory firstLabour = HoldingLabourCategory.Automatic)
    {
        HoldingDefinition item = ScriptableObject.CreateInstance<HoldingDefinition>();
        item.hideFlags = HideFlags.DontUnloadUnusedAsset;
        item.name = displayName; item.id = displayName; item.displayName = displayName; item.description = description;
        item.economicType = type; item.maximumLevel = 1; item.defaultClass = SocioEconomicClass.Freemen;
        item.foodConsumption = 1; item.defaultConstructionTicks = 10;
        item.economicOutputs.Add(new HoldingEconomicOutputDefinition { type = firstType, baseValue = firstValue,
            labourCategory = firstLabour });
        if (secondValue != 0f) item.economicOutputs.Add(new HoldingEconomicOutputDefinition { type = secondType,
            baseValue = secondValue, labourCategory = HoldingLabourCategory.Automatic });
        definitions.Add(item.StableId, item);
    }
}
