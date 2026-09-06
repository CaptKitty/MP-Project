using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Resolves broad levy pressure into a culture/nation roster unit.</summary>
public static class LevyUnitResolver
{
    private static readonly Dictionary<string, UnitSaveData> Cache = new Dictionary<string, UnitSaveData>();
    private static NationCultureData[] cultures;

    public static UnitSaveData Resolve(Province province, ProvinceHolding holding, LevyPressureType role)
    {
        string cultureName = holding != null ? holding.cultureName ?? string.Empty : string.Empty;
        string nationName = province != null && province.nation != null ? province.nation.name : string.Empty;
        List<LevyPressureType> searchOrder = SearchOrder(role);
        string key = cultureName.ToLowerInvariant() + "|" + nationName.ToLowerInvariant() + "|" + (byte)role;
        if (Cache.TryGetValue(key, out UnitSaveData cached)) return cached;
        List<UnitSaveData> candidates = Candidates(province, cultureName);
        UnitSaveData best = null;
        foreach (LevyPressureType candidateRole in searchOrder)
        {
            int bestScore = int.MinValue;
            foreach (UnitSaveData unit in candidates)
            {
                if (!MatchesRole(unit, candidateRole)) continue;
                int score = Score(unit, candidateRole);
                if (score > bestScore || score == bestScore && StableId(unit).CompareTo(StableId(best)) < 0)
                { best = unit; bestScore = score; }
            }
            if (best != null) break;
        }
        // A non-empty roster must always absorb levy capacity, even if future unit
        // types cannot yet be classified into one of the three broad roles.
        if (best == null && candidates.Count > 0)
        {
            candidates.Sort((left, right) => string.CompareOrdinal(StableId(left), StableId(right)));
            best = candidates[0];
        }
        Cache[key] = best;
        return best;
    }

    public static void ClearCache() { Cache.Clear(); cultures = null; }

    private static List<UnitSaveData> Candidates(Province province, string cultureName)
    {
        List<UnitSaveData> result = new List<UnitSaveData>();
        if (cultures == null) cultures = Resources.LoadAll<NationCultureData>("Prefabs/NationData/Culture");
        foreach (NationCultureData culture in cultures)
            if (culture != null && culture.Matches(cultureName) && culture.content != null && culture.content.units != null)
                foreach (NationUnitEntry entry in culture.content.units) Add(result, entry != null ? entry.unit : null);
        if (result.Count == 0 && province != null && province.nation != null)
            foreach (NationUnitEntry entry in NationContentResolver.ResolveUnits(province.nation)) Add(result, entry != null ? entry.unit : null);
        return result;
    }

    private static void Add(List<UnitSaveData> values, UnitSaveData unit)
    { if (unit != null && !unit.Mercenary && !unit.notLevyable && !values.Contains(unit)) values.Add(unit); }

    private static List<LevyPressureType> SearchOrder(LevyPressureType requested)
    {
        List<LevyPressureType> result = new List<LevyPressureType> { requested };
        foreach (LevyPressureType role in LevyEconomySystem.Roles) if (role != requested) result.Add(role);
        return result;
    }

    private static bool MatchesRole(UnitSaveData unit, LevyPressureType role)
    {
        return unit != null && !unit.notLevyable && unit.levyRole == role;
    }

    private static string UnitText(UnitSaveData unit) => ((unit.name ?? string.Empty) + " " +
        (unit.unitname ?? string.Empty) + " " +
        (unit.flaglist != null ? string.Join(" ", unit.flaglist) : string.Empty)).ToLowerInvariant();

    private static int Score(UnitSaveData unit, LevyPressureType role)
    {
        if (unit == null) return int.MinValue;
        int score = unit.levyRole == role ? 100 : 0;
        score -= Mathf.Max(0, unit.cost / 100 - 3);
        return score;
    }

    private static string StableId(UnitSaveData unit) => unit != null
        ? (!string.IsNullOrWhiteSpace(unit.name) ? unit.name : unit.unitname ?? string.Empty) : "~";
}
