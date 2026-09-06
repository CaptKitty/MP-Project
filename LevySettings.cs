using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class LevyClassSettings
{
    public SocioEconomicClass socialClass;
    [Min(0f)] public float baseCapacity;
    [Min(0f)] public float lightInfantryPressure;
    [Min(0f)] public float lineInfantryPressure;
    [Min(0f)] public float heavyInfantryPressure;
    [Min(0f)] public float rangedInfantryPressure;
    [Min(0f)] public float lightCavalryPressure;
    [Min(0f)] public float shockCavalryPressure;
    [Min(0f)] public float chariotPressure;

    public void AddPressure(Dictionary<LevyPressureType, float> target, bool includeLightInfantry = true)
    {
        if (includeLightInfantry) target[LevyPressureType.LightInfantry] += lightInfantryPressure;
        target[LevyPressureType.LineInfantry] += lineInfantryPressure;
        target[LevyPressureType.HeavyInfantry] += heavyInfantryPressure;
        target[LevyPressureType.RangedInfantry] += rangedInfantryPressure;
        target[LevyPressureType.LightCavalry] += lightCavalryPressure;
        target[LevyPressureType.ShockCavalry] += shockCavalryPressure;
        target[LevyPressureType.Chariot] += chariotPressure;
    }
}

[CreateAssetMenu(menuName = "Campaign/Levy Settings")]
public sealed class LevySettings : ScriptableObject
{
    private const string ResourceName = "LevySettings";
    private static LevySettings cached;

    [Header("Timing")]
    [Min(0)] public int recoveryTicks = 120;
    [Min(0)] public int demobilizationTicks;

    [Header("Class baselines")]
    public List<LevyClassSettings> classes = new List<LevyClassSettings>();

    public static LevySettings Current => cached != null ? cached : (cached = Resources.Load<LevySettings>(ResourceName));

    public static LevyClassSettings ForClass(SocioEconomicClass socialClass)
    {
        LevySettings settings = Current;
        if (settings == null || settings.classes == null) return null;
        socialClass = SocioEconomicClassRules.Normalize(socialClass);
        return settings.classes.Find(item => item != null &&
            SocioEconomicClassRules.Normalize(item.socialClass) == socialClass);
    }

    public static void ClearCache() => cached = null;
}
