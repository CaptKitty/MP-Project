using UnityEngine;

public static class CampaignEconomy
{
    public const int StartingGold = 500;
    public const int ArmyCreationCost = 250;
    public const int FarmIncomePerLevel = 5;
    public const float GoldIncomeRate = 0.5f;

    public static int ApplyGoldIncomeRate(int rawIncome) =>
        rawIncome <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(rawIncome * GoldIncomeRate));

    public static int UnitGoldCost(UnitSaveData unit, int amount = 1, Nation nation = null,
        CampaignUnitOrigin origin = CampaignUnitOrigin.Professional)
    {
        if (unit == null) return 0;
        int cost = Mathf.Max(1, unit.cost) * Mathf.Max(1, amount);
        if (nation != null && origin == CampaignUnitOrigin.Mercenary)
            cost = nation.ApplyLawModifiers(NationalLawEffectType.MercenaryRecruitmentCost, cost, null, origin);
        return Mathf.Max(0, cost);
    }

    // Professional formations always cost at least two gold. Many UnitSaveData assets
    // predate the serialized upkeep field and Unity loads that absent value as zero.
    // Levy exemption is handled by FieldArmy.GetUpkeep through CampaignUnitOrigin,
    // rather than by allowing ordinary unit assets to silently become upkeep-free.
    public static int UnitUpkeep(UnitSaveData unit, int amount = 1) =>
        unit == null ? 0 : Mathf.Max(2, unit.upkeep) * Mathf.Max(0, amount);

    public static int BuildingGoldCost(string buildingId, int targetLevel)
    {
        BuildingDefinition definition = BuildingDefinition.Find(buildingId);
        BuildingLevelDefinition configuredLevel = definition != null ? definition.GetLevel(targetLevel) : null;
        if (configuredLevel != null) return Mathf.Max(0, configuredLevel.goldCost);
        int baseCost = buildingId == "Farm" ? 150 : buildingId == "Fort" ? 250 : 200;
        return baseCost * Mathf.Max(1, targetLevel);
    }

    public static int BuildingGoldCost(BuildingDefinition definition, int targetLevel)
    {
        if (definition == null) return 0;
        return BuildingGoldCost(definition.StableId, targetLevel);
    }
}
