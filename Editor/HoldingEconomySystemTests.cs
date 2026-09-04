#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class HoldingEconomySystemTests
{
    private static ProvinceHolding Holding(HoldingEconomicType type, SocioEconomicClass socialClass)
    {
        HoldingDefinition definition = ScriptableObject.CreateInstance<HoldingDefinition>();
        definition.economicType = type;
        definition.foodConsumption = 0;
        return new ProvinceHolding { definition = definition, socioEconomicClass = socialClass,
            cultureName = "Latin", allegiance = "Scipio" };
    }

    [Test]
    public void DesiredComposition_IsNormalized()
    {
        float sum = 0f;
        foreach (float value in HoldingEconomySystem.TypeShares(new Province()).Values) sum += value;
        Assert.That(sum, Is.EqualTo(1f).Within(.0001f));
    }

    [Test]
    public void CivilizedBaseline_IsCitizenHeavy()
    {
        CivilizationData civilization = ScriptableObject.CreateInstance<CivilizationData>();
        civilization.classBaseline = new CivilizationClassBaseline
            { citizen = 50, tribesman = 0, freemen = 20, elite = 20, enslaved = 10 };
        Province province = new Province { nation = new Nation { name = "Rome", civilization = civilization } };
        province.holdings.Add(Holding(HoldingEconomicType.Farm, SocioEconomicClass.Freemen));
        var shares = HoldingEconomySystem.ClassShares(province);
        Assert.That(shares[SocioEconomicClass.Citizen], Is.EqualTo(.5f).Within(.0001f));
        Object.DestroyImmediate(civilization);
    }

    [Test]
    public void BarbarianBaseline_IsPredominantlyTribesmen()
    {
        CivilizationData civilization = ScriptableObject.CreateInstance<CivilizationData>();
        civilization.name = "Barbarian";
        civilization.classBaseline = new CivilizationClassBaseline
            { citizen = 0, tribesman = 50, freemen = 25, elite = 25, enslaved = 0 };
        Province province = new Province { nation = new Nation { name = "Gaul", civilization = civilization } };
        var shares = HoldingEconomySystem.ClassShares(province);
        Assert.That(shares[SocioEconomicClass.Tribesman], Is.EqualTo(.5f).Within(.0001f));
        Object.DestroyImmediate(civilization);
    }

    [TestCase(SocioEconomicClass.Freemen, 2f, 1f)]
    [TestCase(SocioEconomicClass.Citizen, 2f, 1f)]
    [TestCase(SocioEconomicClass.Enslaved, 4f, 1f)]
    [TestCase(SocioEconomicClass.Tribesman, 2f, 1f)]
    [TestCase(SocioEconomicClass.Elite, 1f, 2f)]
    public void Farm_UsesClassMultipliers(SocioEconomicClass socialClass, float food, float gold)
    {
        Province province = new Province();
        ProvinceHolding holding = Holding(HoldingEconomicType.Farm, socialClass);
        Assert.That(HoldingEconomySystem.GetOutput(province, holding, HoldingOutputType.Food, false, 1f), Is.EqualTo(food));
        Assert.That(HoldingEconomySystem.GetOutput(province, holding, HoldingOutputType.Income, false, 1f), Is.EqualTo(gold));
    }

    [Test]
    public void RomanCitizens_RequireLatinCulture()
    {
        Province province = new Province { nation = new Nation { name = "Rome" } };
        ProvinceHolding holding = Holding(HoldingEconomicType.Farm, SocioEconomicClass.Freemen);
        holding.cultureName = "Punic";
        Assert.IsFalse(HoldingEconomySystem.IsEligible(province, holding, SocioEconomicClass.Citizen));
        holding.cultureName = "Latin";
        Assert.IsTrue(HoldingEconomySystem.IsEligible(province, holding, SocioEconomicClass.Citizen));
    }

    [Test]
    public void TypedValues_AreNotStockpiles_AndConvertToGold()
    {
        Province province = new Province();
        ProvinceHolding holding = Holding(HoldingEconomicType.Workshop, SocioEconomicClass.Citizen);
        Assert.That(HoldingEconomySystem.GetOutput(province, holding, HoldingOutputType.IndustrialValue, false, 1f), Is.EqualTo(20f));
        Assert.That(HoldingEconomySystem.GetOutput(province, holding, HoldingOutputType.Income, false, 1f), Is.EqualTo(2f));
    }
}
#endif
