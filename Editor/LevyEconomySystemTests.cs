#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class LevyEconomySystemTests
{
    private static ProvinceHolding Holding(SocioEconomicClass socialClass, string culture = "Latin")
    {
        HoldingDefinition definition = ScriptableObject.CreateInstance<HoldingDefinition>();
        definition.economicType = HoldingEconomicType.Farm;
        return new ProvinceHolding { definition = definition, socioEconomicClass = socialClass,
            cultureName = culture, instanceId = System.Guid.NewGuid().ToString(), allegiance = "Test" };
    }

    [TestCase(MobilizationSensitivity.Low, .75f)]
    [TestCase(MobilizationSensitivity.Normal, .5f)]
    [TestCase(MobilizationSensitivity.Severe, 0f)]
    public void SensitivityAtHalfMobilization(MobilizationSensitivity sensitivity, float expected)
    {
        Assert.That(Mathf.Max(0f, 1f - .5f * LevyEconomySystem.SensitivityMultiplier(sensitivity)), Is.EqualTo(expected));
        Assert.That(Mathf.Max(0f, 1f - LevyEconomySystem.SensitivityMultiplier(sensitivity)), Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void CitizenFarm_HasCapacityLightPressureAndSevereSensitivity()
    {
        Province province = new Province { holdings = new List<ProvinceHolding> { Holding(SocioEconomicClass.Citizen) } };
        Assert.That(LevyEconomySystem.ProvinceCapacity(province), Is.EqualTo(1f));
        Assert.That(LevyEconomySystem.Pressure(province)[LevyPressureType.LightInfantry], Is.EqualTo(10f));
        Assert.That(LevyEconomySystem.Sensitivity(SocioEconomicClass.Citizen), Is.EqualTo(MobilizationSensitivity.Severe));
    }

    [Test]
    public void RomeConvertsCitizenLightPressureToHeavy()
    {
        Province province = new Province { nation = new Nation { name = "Rome" },
            holdings = new List<ProvinceHolding> { Holding(SocioEconomicClass.Citizen) } };
        Dictionary<LevyPressureType, float> pressure = LevyEconomySystem.Pressure(province);
        Assert.That(pressure[LevyPressureType.LightInfantry], Is.Zero);
        Assert.That(pressure[LevyPressureType.HeavyInfantry], Is.EqualTo(10f));
    }

    [Test]
    public void PressureNormalizesToExpectedComposition()
    {
        BuildingDefinition definition = ScriptableObject.CreateInstance<BuildingDefinition>();
        definition.economicEffects = new List<BuildingEconomicEffect>
        {
            new BuildingEconomicEffect { type = BuildingEconomicEffectType.LevyTypePressure, levyType = LevyPressureType.LightInfantry, amount = 50 },
            new BuildingEconomicEffect { type = BuildingEconomicEffectType.LevyTypePressure, levyType = LevyPressureType.HeavyInfantry, amount = 30 },
            new BuildingEconomicEffect { type = BuildingEconomicEffectType.LevyTypePressure, levyType = LevyPressureType.Cavalry, amount = 20 }
        };
        Province province = new Province { buildings = new List<ProvinceBuilding>
            { new ProvinceBuilding { definition = definition, level = 1 } } };
        Dictionary<LevyPressureType, float> result = LevyEconomySystem.Composition(province);
        Assert.That(result[LevyPressureType.LightInfantry], Is.EqualTo(.5f).Within(.001f));
        Assert.That(result[LevyPressureType.HeavyInfantry], Is.EqualTo(.3f).Within(.001f));
        Assert.That(result[LevyPressureType.Cavalry], Is.EqualTo(.2f).Within(.001f));
    }

    [Test]
    public void UnlimitedStacksAndProvinceUniqueRejectsSecondCopy()
    {
        Province province = new Province();
        BuildingDefinition mill = ScriptableObject.CreateInstance<BuildingDefinition>(); mill.id = "Mill";
        Assert.IsTrue(BuildingPlacementSystem.CanPlace(province, mill, 0, out _));
        province.buildings.Add(new ProvinceBuilding { definition = mill, id = "Mill", slotIndex = 0 });
        Assert.IsTrue(BuildingPlacementSystem.CanPlace(province, mill, 1, out _));
        BuildingDefinition fort = ScriptableObject.CreateInstance<BuildingDefinition>(); fort.id = "Fort"; fort.placementLimit = BuildingPlacementLimit.ProvinceUnique;
        province.buildings.Add(new ProvinceBuilding { definition = fort, id = "Fort", slotIndex = 1 });
        Assert.IsFalse(BuildingPlacementSystem.CanPlace(province, fort, 2, out _));
    }
}
#endif
