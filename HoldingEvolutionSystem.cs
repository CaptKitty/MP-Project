using UnityEngine;

public static class HoldingEvolutionSystem
{
    public static int DesiredUrbanization(Province province)
    {
        if (province == null) return 0;
        int populatedHoldings = province.holdings != null
            ? province.holdings.FindAll(holding => holding != null && holding.definition != null).Count : 0;
        float target = -50f + populatedHoldings * 2f;
        if (province.buildings != null) foreach (ProvinceBuilding building in province.buildings)
            if (building != null && building.definition != null && building.definition.levels != null)
                foreach (BuildingLevelDefinition level in building.definition.levels)
                    if (level != null && level.level <= building.level)
                        target += level.urbanizationTargetModifier;
        return Mathf.Clamp(Mathf.RoundToInt(target), -100, province.MaximumDevelopment);
    }
}
