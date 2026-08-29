using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Built-in economic holding lines. Resource assets with the same stable ID override these defaults.</summary>
public static class HoldingArchetypeCatalog
{
    private static Dictionary<string, HoldingDefinition> definitions;

    public static HoldingDefinition Find(string id)
    {
        EnsureBuilt();
        definitions.TryGetValue(id ?? string.Empty, out HoldingDefinition result);
        return result;
    }

    public static List<HoldingDefinition> All()
    {
        EnsureBuilt();
        return new List<HoldingDefinition>(definitions.Values);
    }

    public static void ApplyMetadata(HoldingDefinition definition)
    {
        if (definition == null) return;
        if (definition.StableId.Equals("CitizenFarm", StringComparison.OrdinalIgnoreCase) ||
            definition.StableId.StartsWith("CitizenFarm:", StringComparison.OrdinalIgnoreCase))
        {
            definition.category = HoldingCategory.FreeFarmers;
            definition.categoryTier = 2;
            definition.tags |= HoldingTag.Agricultural | HoldingTag.Urban | HoldingTag.Rural;
            if (definition.transformations == null) definition.transformations = new List<HoldingTransformationOption>();
            AddTransform(definition, "Freehold");
            AddTransform(definition, "CommercialFarm", 35);
        }
        else if (definition.StableId.Equals("Freehold", StringComparison.OrdinalIgnoreCase))
        {
            // Developed farms can lateral-transform under sustained commercial pressure.
            AddTransform(definition, "CommercialFarm", 35);
        }
    }

    private static void EnsureBuilt()
    {
        if (definitions != null) return;
        definitions = new Dictionary<string, HoldingDefinition>(StringComparer.OrdinalIgnoreCase);

        Add("TenantFarm", "Tenant Farm", HoldingCategory.FreeFarmers, 1, SocioEconomicClass.Freemen,
            2, 0, 0, 1, true, 600, "Velite");
        Add("Freehold", "Freehold", HoldingCategory.FreeFarmers, 3, SocioEconomicClass.Citizen,
            3, 2, 0, 1, true, 1400, "LegionaryHeavy", "LegionaryLight");

        Add("Homestead", "Homestead", HoldingCategory.TribalSubsistence, 1, SocioEconomicClass.Freemen,
            2, 0, -40, 1, true, 800, "Velite");
        Add("Hamlet", "Hamlet", HoldingCategory.TribalSubsistence, 2, SocioEconomicClass.Freemen,
            2, 0, -40, 1, true, 1100, "Tribesman");
        Add("Village", "Village", HoldingCategory.TribalSubsistence, 3, SocioEconomicClass.Freemen,
            3, 0, -35, 1, true, 1500, "Tribesman_Chief", "Tribesman");

        Add("Manor", "Noble Household", HoldingCategory.EliteAgriculture, 1, SocioEconomicClass.Aristocracy,
            0, 0, 0, 1, false, 0, null, null, "Pro-Elite");
        Add("Estate", "House of Notables", HoldingCategory.EliteAgriculture, 2, SocioEconomicClass.Aristocracy,
            0, 0, 0, 2, false, 0, null, null, "Pro-Elite");
        Add("GreatEstate", "Provincial Court", HoldingCategory.EliteAgriculture, 3, SocioEconomicClass.Aristocracy,
            0, 0, 0, 3, false, 0, null, null, "Pro-Elite");
        ConfigureAristocratic(Get("Manor"), HoldingTag.Elite, 1);
        ConfigureAristocratic(Get("Estate"), HoldingTag.Elite, 2);
        ConfigureAristocratic(Get("GreatEstate"), HoldingTag.Elite, 3);

        Add("ChiefHousehold", "Chief's Household", HoldingCategory.EliteAgriculture, 1, SocioEconomicClass.Aristocracy,
            0, 0, -100, 1, true, 200, "Cavalry_Heavy", "Cavalry_Light", "Pro-Elite");
        Add("ChieftainsHall", "Chieftain's Hall", HoldingCategory.EliteAgriculture, 2, SocioEconomicClass.Aristocracy,
            0, 0, -100, 2, true, 400, "Cavalry_Heavy", "Cavalry_Light", "Pro-Elite");
        Add("GreatHall", "Great Hall", HoldingCategory.EliteAgriculture, 3, SocioEconomicClass.Aristocracy,
            0, 0, -100, 3, true, 600, "Cavalry_Heavy", "Cavalry_Light", "Pro-Elite");
        ConfigureAristocratic(Get("ChiefHousehold"), HoldingTag.Rural | HoldingTag.Elite | HoldingTag.Military, 0, LevyArchetype.HeavyCavalry);
        ConfigureAristocratic(Get("ChieftainsHall"), HoldingTag.Rural | HoldingTag.Elite | HoldingTag.Military, 0, LevyArchetype.HeavyCavalry);
        ConfigureAristocratic(Get("GreatHall"), HoldingTag.Rural | HoldingTag.Elite | HoldingTag.Military, 0, LevyArchetype.HeavyCavalry);

        Add("NobleResidence", "Noble Residence", HoldingCategory.EliteAgriculture, 1, SocioEconomicClass.Aristocracy,
            0, 0, 100, 2, false, 0, null, null, "Pro-Elite");
        Add("AristocraticPalace", "Aristocratic Palace", HoldingCategory.EliteAgriculture, 2, SocioEconomicClass.Aristocracy,
            0, 0, 100, 4, false, 0, null, null, "Pro-Elite");
        Add("GrandPalace", "Grand Palace", HoldingCategory.EliteAgriculture, 3, SocioEconomicClass.Aristocracy,
            0, 0, 100, 7, false, 0, null, null, "Pro-Elite");
        ConfigureAristocratic(Get("NobleResidence"), HoldingTag.Urban | HoldingTag.Elite, 0);
        ConfigureAristocratic(Get("AristocraticPalace"), HoldingTag.Urban | HoldingTag.Elite, 0);
        ConfigureAristocratic(Get("GrandPalace"), HoldingTag.Urban | HoldingTag.Elite, 0);

        Add("MarketGarden", "Market Garden", HoldingCategory.CommercialAgriculture, 1, SocioEconomicClass.Citizen,
            2, 2, 40, 1, true, 300, "Velite");
        Add("CommercialFarm", "Commercial Farm", HoldingCategory.CommercialAgriculture, 2, SocioEconomicClass.Citizen,
            2, 3, 50, 1, true, 500, "LegionaryLevy", "Velite");
        Add("CashCropFarm", "Cash-Crop Farm", HoldingCategory.CommercialAgriculture, 3, SocioEconomicClass.Citizen,
            3, 5, 60, 2, true, 700, "Cavalry_Light", "LegionaryLevy");

        Add("SlaveFarm", "Slave Farm", HoldingCategory.ServileAgriculture, 1, SocioEconomicClass.Enslaved,
            2, 2, 0, 2, false, 0, null);
        Add("Plantation", "Plantation", HoldingCategory.ServileAgriculture, 2, SocioEconomicClass.Enslaved,
            3, 3, 0, 3, false, 0, null);
        Add("GreatPlantation", "Great Plantation", HoldingCategory.ServileAgriculture, 3, SocioEconomicClass.Enslaved,
            5, 6, 0, 4, false, 0, null);

        Add("CottageWorkshop", "Cottage Workshop", HoldingCategory.Artisans, 1, SocioEconomicClass.Freemen,
            0, 1, 40, 1, true, 250, "Velite");
        Add("Workshop", "Workshop", HoldingCategory.Artisans, 2, SocioEconomicClass.Freemen,
            0, 2, 60, 2, true, 400, "LegionaryLevy", "Velite");
        Add("ArtisanQuarter", "Artisan Quarter", HoldingCategory.Artisans, 3, SocioEconomicClass.Freemen,
            0, 4, 80, 3, false, 0, null);

        Add("TraderHousehold", "Trader Household", HoldingCategory.Commerce, 1, SocioEconomicClass.Citizen,
            0, 2, 100, 1, false, 0, null);
        Add("MerchantHolding", "Merchant Holding", HoldingCategory.Commerce, 2, SocioEconomicClass.Citizen,
            0, 3, 100, 2, false, 0, null);
        Add("MerchantHouse", "Merchant House", HoldingCategory.Commerce, 3, SocioEconomicClass.Citizen,
            0, 7, 100, 3, false, 0, null);

        Add("HerdingHomestead", "Herding Homestead", HoldingCategory.Pastoralists, 1, SocioEconomicClass.Freemen,
            2, 1, 0, 1, true, 700, "Velite");
        Add("PastoralHolding", "Pastoral Holding", HoldingCategory.Pastoralists, 2, SocioEconomicClass.Freemen,
            3, 2, 0, 1, true, 1000, "Tribesman");
        Add("PastoralEstate", "Pastoral Estate", HoldingCategory.Pastoralists, 3, SocioEconomicClass.Freemen,
            5, 2, 0, 2, true, 1300, "Cavalry_Light", "Cavalry_Numidian");

        Add("HunterCamp", "Hunter Camp", HoldingCategory.Hunters, 1, SocioEconomicClass.Freemen,
            2, 0, -100, 1, true, 500, "Velite");
        Add("HunterCommunity", "Hunter Community", HoldingCategory.Hunters, 2, SocioEconomicClass.Freemen,
            3, 1, -100, 1, true, 700, "Gallic_Archer", "Velite");
        Add("HunterLodge", "Hunter Lodge/Settlement", HoldingCategory.Hunters, 3, SocioEconomicClass.Freemen,
            3, 2, -100, 1, true, 900, "Gallic_Archer", "Velite");

        Add("SurfaceWorking", "Surface Working", HoldingCategory.Mining, 1, SocioEconomicClass.Freemen,
            0, 2, 0, 1, false, 0, null);
        Add("Mine", "Mine", HoldingCategory.Mining, 2, SocioEconomicClass.Freemen,
            0, 3, 0, 2, false, 0, null);
        Add("MiningSettlement", "Mining Settlement", HoldingCategory.Mining, 3, SocioEconomicClass.Freemen,
            0, 6, 0, 3, false, 0, null);

        Linear("TenantFarm", "CitizenFarm", "Freehold");
        Linear("Homestead", "Hamlet", "Village");
        Linear("Manor", "Estate", "GreatEstate");
        Linear("ChiefHousehold", "ChieftainsHall", "GreatHall");
        Linear("NobleResidence", "AristocraticPalace", "GrandPalace");
        Linear("MarketGarden", "CommercialFarm", "CashCropFarm");
        Linear("SlaveFarm", "Plantation", "GreatPlantation");
        Linear("CottageWorkshop", "Workshop", "ArtisanQuarter");
        Linear("TraderHousehold", "MerchantHolding", "MerchantHouse");
        Linear("HerdingHomestead", "PastoralHolding", "PastoralEstate");
        Linear("HunterCamp", "HunterCommunity", "HunterLodge");
        Linear("SurfaceWorking", "Mine", "MiningSettlement");

        AddTransform(Get("TenantFarm"), "MarketGarden", 25);
        AddTransform(Get("Homestead"), "HerdingHomestead", 0, 45);
        AddTransform(Get("CottageWorkshop"), "TraderHousehold", 40);
        AddTransform(Get("Workshop"), "MerchantHolding", 50);
        AddTransform(Get("HerdingHomestead"), "HunterCamp", 0, 25);
    }

    private static HoldingDefinition Add(string id, string displayName, HoldingCategory category, int tier,
        SocioEconomicClass socialClass, int food, int gold, int urbanResponse, int foodConsumption,
        bool levies, int levyContribution, string levyUnit, string fallbackUnit = null, string allegiance = null)
    {
        HoldingDefinition item = ScriptableObject.CreateInstance<HoldingDefinition>();
        item.hideFlags = HideFlags.DontUnloadUnusedAsset;
        item.name = id; item.id = id; item.displayName = displayName;
        item.description = displayName + " — tier " + tier + " " + category + " holding.";
        item.category = category; item.categoryTier = tier; item.maximumLevel = 1;
        item.defaultConstructionTicks = 8 + tier * 4; item.defaultClass = socialClass;
        item.foodConsumption = Mathf.Min(1, Mathf.Max(0, foodConsumption));
        item.foodUpkeep = Mathf.Max(0, foodConsumption - item.foodConsumption);
        item.suggestedAllegiance = allegiance;
        item.levels.Add(new HoldingLevelDefinition { level = 1, goldCost = 50 * tier * tier,
            constructionTicks = item.defaultConstructionTicks });
        if (food > 0) item.outputs.Add(Output(HoldingOutputType.Food, food, urbanResponse, levies));
        if (gold > 0) item.outputs.Add(Output(HoldingOutputType.Income, gold, urbanResponse, levies));
        item.levyUnit = Unit(levyUnit) ?? Unit(fallbackUnit);
        item.canRaiseLevies = levies && levyContribution > 0;
        item.levyArchetype = InferArchetype(levyUnit, fallbackUnit);
        item.levyContributionPermillePerLevel = item.canRaiseLevies ? levyContribution : 0;
        definitions.Add(id, item);
        return item;
    }

    private static LevyArchetype InferArchetype(string primary, string fallback)
    {
        string value = ((primary ?? string.Empty) + " " + (fallback ?? string.Empty)).ToLowerInvariant();
        if (value.Contains("cavalry")) return LevyArchetype.LightCavalry;
        if (value.Contains("trib")) return LevyArchetype.TribalInfantry;
        if (value.Contains("velite") || value.Contains("javelin")) return LevyArchetype.LightJavelinInfantry;
        if (value.Contains("heavy") || value.Contains("triarii")) return LevyArchetype.HeavyInfantry;
        return string.IsNullOrWhiteSpace(value) ? LevyArchetype.None : LevyArchetype.LightInfantry;
    }

    private static void ConfigureAristocratic(HoldingDefinition definition, HoldingTag tags,
        int garrisonCapacity, LevyArchetype levyArchetype = LevyArchetype.None)
    {
        if (definition == null) return;
        definition.tags = tags;
        definition.garrisonCapacity = Mathf.Max(0, garrisonCapacity);
        if (levyArchetype != LevyArchetype.None) definition.levyArchetype = levyArchetype;
    }

    private static HoldingOutputDefinition Output(HoldingOutputType type, int value, int response, bool mobilized)
    {
        return new HoldingOutputDefinition { type = type, baseValue = value, urbanizationResponse = response,
            disabledWhileMobilized = mobilized };
    }

    private static UnitSaveData Unit(string unitName)
    {
        if (string.IsNullOrWhiteSpace(unitName)) return null;
        return Array.Find(Resources.LoadAll<UnitSaveData>("Prefabs/Units"), item => item != null &&
            (item.name.Equals(unitName, StringComparison.OrdinalIgnoreCase) ||
             !string.IsNullOrEmpty(item.unitname) && item.unitname.Equals(unitName, StringComparison.OrdinalIgnoreCase)));
    }

    private static HoldingDefinition Get(string id) => definitions[id];
    private static void Linear(string first, string second, string third)
    {
        AddTransform(Get(first), second);
        HoldingDefinition middle = HoldingDefinition.Find(second);
        if (middle != null) AddTransform(middle, third);
    }
    private static void AddTransform(HoldingDefinition source, string target, int minimumUrbanization = -100,
        int maximumUrbanization = 100)
    {
        if (source == null) return;
        if (source.transformations == null) source.transformations = new List<HoldingTransformationOption>();
        if (!source.transformations.Exists(option => option != null &&
            option.targetHoldingId.Equals(target, StringComparison.OrdinalIgnoreCase)))
            source.transformations.Add(new HoldingTransformationOption { targetHoldingId = target,
                minimumUrbanization = minimumUrbanization, maximumUrbanization = maximumUrbanization });
    }
}
