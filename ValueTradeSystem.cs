using System;
using System.Collections.Generic;
using UnityEngine;

public static class ValueTradeSystem
{
    public sealed class ConversionResult
    {
        public string key;
        public string buildingName;
        public string provinceName;
        public EconomicFlowResource input;
        public EconomicConversionOutput output;
        public float requestedInput;
        public float consumedInput;
        public float producedOutput;
        public float OperatingFraction => requestedInput > 0f ? Mathf.Clamp01(consumedInput / requestedInput) : 0f;
    }

    public sealed class ProvinceFlow
    {
        public readonly Dictionary<EconomicFlowResource, float> gross = NewResourceMap();
        public readonly Dictionary<EconomicFlowResource, float> consumed = NewResourceMap();
        public readonly List<ConversionResult> conversions = new List<ConversionResult>();
        public float foodProduced;
        public float populationGrowth;
    }

    public sealed class TradeFlow
    {
        public float generated, requested, allocated;
        public readonly List<ConversionResult> generators = new List<ConversionResult>();
        public readonly List<ConversionResult> consumers = new List<ConversionResult>();
        public float Unused => Mathf.Max(0f, generated - allocated);
    }

    private sealed class ProvinceCache { public int frame = -1; public ProvinceFlow flow; }
    private sealed class TradeCache { public int frame = -1; public TradeFlow flow; }
    private static readonly Dictionary<Province, ProvinceCache> ProvinceFlows = new Dictionary<Province, ProvinceCache>();
    private static readonly Dictionary<Nation, TradeCache> TradeFlows = new Dictionary<Nation, TradeCache>();
    public static void InvalidateAll() { ProvinceFlows.Clear(); TradeFlows.Clear(); }

    public static ProvinceFlow Province(Province province)
    {
        if (province == null) return new ProvinceFlow();
        if (!ProvinceFlows.TryGetValue(province, out ProvinceCache cache))
        { cache = new ProvinceCache(); ProvinceFlows.Add(province, cache); }
        if (cache.frame == Time.frameCount && cache.flow != null) return cache.flow;
        cache.frame = Time.frameCount; cache.flow = CalculateProvince(province);
        return cache.flow;
    }

    public static TradeFlow NationTrade(Nation nation)
    {
        if (nation == null) return new TradeFlow();
        if (!TradeFlows.TryGetValue(nation, out TradeCache cache))
        { cache = new TradeCache(); TradeFlows.Add(nation, cache); }
        if (cache.frame == Time.frameCount && cache.flow != null) return cache.flow;
        cache.frame = Time.frameCount; cache.flow = CalculateTrade(nation);
        return cache.flow;
    }

    public static float RemainingFraction(Province province, HoldingOutputType type)
    {
        EconomicFlowResource resource = Resource(type);
        ProvinceFlow flow = Province(province);
        float gross = flow.gross[resource];
        return gross > .0001f ? Mathf.Clamp01((gross - flow.consumed[resource]) / gross) : 1f;
    }

    public static float ConvertedFood(Province province)
    {
        float valueFood = Province(province).foodProduced;
        float tradeFood = 0f;
        TradeFlow trade = NationTrade(province != null ? province.nation : null);
        foreach (ConversionResult result in trade.consumers)
            if (result.provinceName == (province != null ? province.name : string.Empty) && result.output == EconomicConversionOutput.Food)
                tradeFood += result.producedOutput;
        return valueFood + tradeFood;
    }

    public static float LevyPressureOutput(Province province, LevyPressureType type)
    {
        float result = 0f;
        foreach (ConversionResult conversion in Province(province).conversions)
            if (conversion.output == EconomicConversionOutput.LevyPressure)
            {
                BuildingValueConversion source = FindConversion(province, conversion.key);
                if (source != null && source.levyType == type) result += conversion.producedOutput;
            }
        return result;
    }

    public static float OperatingFraction(Province province, ProvinceBuilding building, int conversionIndex)
    {
        if (province == null || building == null) return 0f;
        string key = province.name + "|" + building.slotIndex + "|" + conversionIndex;
        if (building.definition != null && building.definition.valueConversions != null && conversionIndex >= 0 &&
            conversionIndex < building.definition.valueConversions.Count &&
            building.definition.valueConversions[conversionIndex].input == EconomicFlowResource.TradeCapacity)
        {
            foreach (ConversionResult result in NationTrade(province.nation).consumers) if (result.key == key) return result.OperatingFraction;
        }
        else foreach (ConversionResult result in Province(province).conversions) if (result.key == key) return result.OperatingFraction;
        return 0f;
    }

    private static ProvinceFlow CalculateProvince(Province province)
    {
        ProvinceFlow flow = new ProvinceFlow();
        flow.gross[EconomicFlowResource.AgriculturalValue] = Mathf.Max(0f, province.GetHoldingOutputUnrounded(HoldingOutputType.AgriculturalValue));
        flow.gross[EconomicFlowResource.IndustrialValue] = Mathf.Max(0f, province.GetHoldingOutputUnrounded(HoldingOutputType.IndustrialValue));
        flow.gross[EconomicFlowResource.CommercialValue] = Mathf.Max(0f, province.GetHoldingOutputUnrounded(HoldingOutputType.CommercialValue));
        foreach (ConversionSource source in Sources(province, false))
        {
            BuildingValueConversion conversion = source.conversion;
            EconomicFlowResource input = conversion.input;
            if (input == EconomicFlowResource.TradeCapacity) continue;
            float available = Mathf.Max(0f, flow.gross[input] - flow.consumed[input]);
            float consumed = Mathf.Min(available, Mathf.Max(0f, conversion.inputAmount));
            float fraction = conversion.inputAmount > 0f ? consumed / conversion.inputAmount : 0f;
            ConversionResult result = Result(source, consumed, conversion.outputAmount * fraction);
            flow.consumed[input] += consumed; flow.conversions.Add(result);
            if (conversion.output == EconomicConversionOutput.Food) flow.foodProduced += result.producedOutput;
            else if (conversion.output == EconomicConversionOutput.PopulationGrowth) flow.populationGrowth += result.producedOutput;
        }
        return flow;
    }

    private static TradeFlow CalculateTrade(Nation nation)
    {
        TradeFlow flow = new TradeFlow();
        List<Province> provinces = OwnedProvinces(nation);
        foreach (Province province in provinces)
            foreach (ConversionResult result in Province(province).conversions)
                if (result.output == EconomicConversionOutput.TradeCapacity)
                { flow.generated += result.producedOutput; flow.generators.Add(result); }
        float available = flow.generated;
        foreach (Province province in provinces)
            foreach (ConversionSource source in Sources(province, true))
            {
                BuildingValueConversion conversion = source.conversion;
                flow.requested += Mathf.Max(0f, conversion.inputAmount);
                float consumed = Mathf.Min(available, Mathf.Max(0f, conversion.inputAmount));
                available -= consumed; flow.allocated += consumed;
                float fraction = conversion.inputAmount > 0f ? consumed / conversion.inputAmount : 0f;
                flow.consumers.Add(Result(source, consumed, conversion.outputAmount * fraction));
            }
        return flow;
    }

    private sealed class ConversionSource
    {
        public Province province; public ProvinceBuilding building; public BuildingValueConversion conversion; public int index;
        public string Key => province.name + "|" + building.slotIndex + "|" + index;
    }

    private static List<ConversionSource> Sources(Province province, bool tradeConsumers)
    {
        List<ConversionSource> result = new List<ConversionSource>();
        if (province == null || province.buildings == null) return result;
        foreach (ProvinceBuilding building in province.buildings)
        {
            if (building == null || building.definition == null || building.definition.valueConversions == null) continue;
            for (int i = 0; i < building.definition.valueConversions.Count; i++)
            {
                BuildingValueConversion conversion = building.definition.valueConversions[i];
                if (conversion == null || building.level < Mathf.Max(1, conversion.minimumLevel) ||
                    (conversion.input == EconomicFlowResource.TradeCapacity) != tradeConsumers) continue;
                result.Add(new ConversionSource { province = province, building = building, conversion = conversion, index = i });
            }
        }
        result.Sort((a, b) => { int slot = a.building.slotIndex.CompareTo(b.building.slotIndex); return slot != 0 ? slot : a.index.CompareTo(b.index); });
        return result;
    }

    private static ConversionResult Result(ConversionSource source, float consumed, float produced) => new ConversionResult
    {
        key = source.Key, buildingName = source.building.DisplayName, provinceName = source.province.name,
        input = source.conversion.input, output = source.conversion.output,
        requestedInput = source.conversion.inputAmount, consumedInput = consumed, producedOutput = produced
    };

    private static List<Province> OwnedProvinces(Nation nation)
    {
        List<Province> result = new List<Province>();
        if (Owners.Instance != null && Owners.Instance.provincelist != null)
            foreach (Province province in Owners.Instance.provincelist)
                if (province != null && !province.IsOccupied && province.nation == nation) result.Add(province);
        result.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); return result;
    }

    private static BuildingValueConversion FindConversion(Province province, string key)
    {
        foreach (ConversionSource source in Sources(province, false)) if (source.Key == key) return source.conversion;
        return null;
    }
    private static EconomicFlowResource Resource(HoldingOutputType type) => type == HoldingOutputType.IndustrialValue
        ? EconomicFlowResource.IndustrialValue : type == HoldingOutputType.CommercialValue
            ? EconomicFlowResource.CommercialValue : EconomicFlowResource.AgriculturalValue;
    private static Dictionary<EconomicFlowResource, float> NewResourceMap() => new Dictionary<EconomicFlowResource, float>
    {
        { EconomicFlowResource.AgriculturalValue, 0f }, { EconomicFlowResource.IndustrialValue, 0f },
        { EconomicFlowResource.CommercialValue, 0f }, { EconomicFlowResource.TradeCapacity, 0f }
    };
}
