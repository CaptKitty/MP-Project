using UnityEngine;

public static class CampaignEconomy
{
    public const int StartingGold = 500;
    public const int ArmyCreationCost = 250;
    public const int BaseProvinceIncome = 10;
    public const int FarmIncomePerLevel = 5;
    public const float GoldIncomeRate = 0.5f;

    public static int ApplyGoldIncomeRate(int rawIncome) =>
        rawIncome <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(rawIncome * GoldIncomeRate));

    public static int UnitGoldCost(UnitSaveData unit, int amount = 1) =>
        unit == null ? 0 : Mathf.Max(1, unit.cost) * Mathf.Max(1, amount);

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
