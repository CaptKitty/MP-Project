#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ValueTradeSystemTests
{
    private GameObject ownerObject;
    private Owners owners;

    [SetUp]
    public void SetUp()
    {
        ownerObject = new GameObject("Value Trade Test Owners");
        owners = ownerObject.AddComponent<Owners>();
        Owners.Instance = owners;
        owners.provincelist = new List<Province>(); owners.regionlist = new List<CampaignRegion>();
        ValueTradeSystem.InvalidateAll(); LevyEconomySystem.InvalidateAll();
    }

    [TearDown]
    public void TearDown() { Object.DestroyImmediate(ownerObject); Owners.Instance = null; ValueTradeSystem.InvalidateAll(); }

    private static Province ProvinceWithValue(Nation nation, string name, HoldingOutputType type, float amount)
    {
        HoldingDefinition definition = ScriptableObject.CreateInstance<HoldingDefinition>();
        definition.foodConsumption = 0;
        definition.economicOutputs.Add(new HoldingEconomicOutputDefinition
            { type = type, baseValue = amount, labourCategory = HoldingLabourCategory.Value });
        Province province = new Province { name = name, nation = nation, region = name + " Region" };
        province.holdings.Add(new ProvinceHolding { definition = definition, socioEconomicClass = SocioEconomicClass.Freemen,
            instanceId = name + " holding" });
        return province;
    }

    private static ProvinceBuilding Converter(string id, int slot, EconomicFlowResource input, float inputAmount,
        EconomicConversionOutput output, float outputAmount)
    {
        BuildingDefinition definition = ScriptableObject.CreateInstance<BuildingDefinition>(); definition.id = id; definition.displayName = id;
        definition.valueConversions.Add(new BuildingValueConversion
            { input = input, inputAmount = inputAmount, output = output, outputAmount = outputAmount });
        return new ProvinceBuilding { definition = definition, id = id, level = 1, slotIndex = slot };
    }

    [Test]
    public void ValueConversionConsumesBeforeGoldAndSupportsPartialOperation()
    {
        Nation nation = new Nation { name = "Test" };
        Province full = ProvinceWithValue(nation, "Full", HoldingOutputType.AgriculturalValue, 100);
        full.buildings.Add(Converter("Provisioning", 0, EconomicFlowResource.AgriculturalValue, 50, EconomicConversionOutput.Food, 10));
        owners.provincelist.Add(full);
        Assert.That(ValueTradeSystem.Province(full).consumed[EconomicFlowResource.AgriculturalValue], Is.EqualTo(50));
        Assert.That(ValueTradeSystem.ConvertedFood(full), Is.EqualTo(10));
        Assert.That(full.GetHoldingOutputUnrounded(HoldingOutputType.Income), Is.EqualTo(5).Within(.001));

        Province partial = ProvinceWithValue(nation, "Partial", HoldingOutputType.AgriculturalValue, 25);
        partial.buildings.Add(Converter("Provisioning", 0, EconomicFlowResource.AgriculturalValue, 50, EconomicConversionOutput.Food, 10));
        owners.provincelist.Add(partial); ValueTradeSystem.InvalidateAll();
        Assert.That(ValueTradeSystem.Province(partial).conversions[0].OperatingFraction, Is.EqualTo(.5f));
        Assert.That(ValueTradeSystem.ConvertedFood(partial), Is.EqualTo(5));
        Assert.That(partial.GetHoldingOutputUnrounded(HoldingOutputType.Income), Is.EqualTo(0).Within(.001));
    }

    [Test]
    public void MultipleConvertersUseStableSlotOrderAndNeverOverConsume()
    {
        Nation nation = new Nation { name = "Test" };
        Province province = ProvinceWithValue(nation, "Allocation", HoldingOutputType.AgriculturalValue, 75);
        province.buildings.Add(Converter("Second", 1, EconomicFlowResource.AgriculturalValue, 50, EconomicConversionOutput.Food, 10));
        province.buildings.Add(Converter("First", 0, EconomicFlowResource.AgriculturalValue, 50, EconomicConversionOutput.Food, 10));
        owners.provincelist.Add(province);
        ValueTradeSystem.ProvinceFlow flow = ValueTradeSystem.Province(province);
        Assert.That(flow.conversions[0].buildingName, Is.EqualTo("First"));
        Assert.That(flow.conversions[0].OperatingFraction, Is.EqualTo(1));
        Assert.That(flow.conversions[1].OperatingFraction, Is.EqualTo(.5f));
        Assert.That(flow.consumed[EconomicFlowResource.AgriculturalValue], Is.EqualTo(75));
    }

    [Test]
    public void NationalTradeGenerationFeedsRemoteFoodMarketAndDisruptsOnRemoval()
    {
        Nation nation = new Nation { name = "Test" };
        Province port = ProvinceWithValue(nation, "A Port", HoldingOutputType.CommercialValue, 40);
        port.buildings.Add(Converter("Port", 0, EconomicFlowResource.CommercialValue, 40, EconomicConversionOutput.TradeCapacity, 20));
        Province city = ProvinceWithValue(nation, "B City", HoldingOutputType.AgriculturalValue, 0);
        city.buildings.Add(Converter("Food Market", 0, EconomicFlowResource.TradeCapacity, 10, EconomicConversionOutput.Food, 10));
        owners.provincelist.Add(port); owners.provincelist.Add(city);
        Assert.That(Owners.Instance, Is.SameAs(owners));
        Assert.That(ValueTradeSystem.Province(port).gross[EconomicFlowResource.CommercialValue], Is.EqualTo(40));
        Assert.That(ValueTradeSystem.Province(port).conversions.Count, Is.EqualTo(1));
        Assert.That(ValueTradeSystem.NationTrade(nation).generated, Is.EqualTo(20));
        Assert.That(ValueTradeSystem.ConvertedFood(city), Is.EqualTo(10));
        port.buildings.Clear(); ValueTradeSystem.InvalidateAll();
        Assert.That(ValueTradeSystem.NationTrade(nation).generated, Is.Zero);
        Assert.That(ValueTradeSystem.ConvertedFood(city), Is.Zero);
    }

    [Test]
    public void DivergentUpgradeOptionsAreExclusiveDefinitions()
    {
        BuildingDefinition mill = ScriptableObject.CreateInstance<BuildingDefinition>();
        BuildingDefinition provisioning = ScriptableObject.CreateInstance<BuildingDefinition>(); provisioning.id = "Provisioning";
        BuildingDefinition commercial = ScriptableObject.CreateInstance<BuildingDefinition>(); commercial.id = "Commercial";
        mill.upgradeOptions.Add(provisioning); mill.upgradeOptions.Add(commercial);
        Assert.IsTrue(mill.CanUpgradeTo(provisioning)); Assert.IsTrue(mill.CanUpgradeTo(commercial));
        Assert.IsFalse(provisioning.CanUpgradeTo(commercial));
    }
}
#endif
