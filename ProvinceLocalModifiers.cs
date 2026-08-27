using System;
using UnityEngine;

[Serializable]
public sealed class ProvinceLocalModifiers
{
    [Tooltip("Signed change to this province's maximum development. Positive values are shown as Urbanization; negative values as Ruralization.")]
    public int maxDevelopment;
    public System.Collections.Generic.List<HoldingTagModifier> holdingEconomyModifiers =
        new System.Collections.Generic.List<HoldingTagModifier>();

    public bool IsEmpty => maxDevelopment == 0 && (holdingEconomyModifiers == null || holdingEconomyModifiers.Count == 0);

    public void Add(ProvinceLocalModifiers other)
    {
        if (other == null) return;
        maxDevelopment += other.maxDevelopment;
        if (other.holdingEconomyModifiers != null)
        {
            if (holdingEconomyModifiers == null) holdingEconomyModifiers = new System.Collections.Generic.List<HoldingTagModifier>();
            holdingEconomyModifiers.AddRange(other.holdingEconomyModifiers);
        }
    }

    public static string MaxDevelopmentDisplayName(int value) => value < 0 ? "Ruralization" : "Urbanization";

    public static string FormatMaxDevelopment(int value)
    {
        if (value == 0) return string.Empty;
        return MaxDevelopmentDisplayName(value) + ": " + (value > 0 ? "+" : string.Empty) + value + " max development";
    }
}
