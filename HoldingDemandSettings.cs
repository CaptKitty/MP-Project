using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class HoldingTagDemandValue
{
    public HoldingTag tag;
    [Range(0f, 100f)] public float demand;
}

[CreateAssetMenu(fileName = "HoldingDemandSettings", menuName = "Campaign/Holdings/Demand Settings")]
public sealed class HoldingDemandSettings : ScriptableObject
{
    [Tooltip("Baseline holding-tag demand at -100 urbanization (maximum ruralization).")]
    public List<HoldingTagDemandValue> ruralBaseline = new List<HoldingTagDemandValue>();
    [Tooltip("Baseline holding-tag demand at 0 urbanization.")]
    public List<HoldingTagDemandValue> neutralBaseline = new List<HoldingTagDemandValue>();
    [Tooltip("Baseline holding-tag demand at +100 urbanization.")]
    public List<HoldingTagDemandValue> urbanBaseline = new List<HoldingTagDemandValue>();

    public float Evaluate(HoldingTag tag, float urbanization)
    {
        float clamped = Mathf.Clamp(urbanization, -100f, 100f);
        if (clamped < 0f)
            return Mathf.Lerp(Value(ruralBaseline, tag), Value(neutralBaseline, tag), (clamped + 100f) / 100f);
        return Mathf.Lerp(Value(neutralBaseline, tag), Value(urbanBaseline, tag), clamped / 100f);
    }

    private static float Value(List<HoldingTagDemandValue> values, HoldingTag tag)
    {
        if (values != null)
            foreach (HoldingTagDemandValue value in values)
                if (value != null && value.tag == tag)
                    return Mathf.Max(0f, value.demand);
        return 0f;
    }
}
