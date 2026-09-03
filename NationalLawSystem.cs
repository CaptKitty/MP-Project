using System;
using System.Collections.Generic;
using UnityEngine;

public enum NationalLawEffectType : byte
{
    LevyConscription,
    MercenaryRecruitmentTime,
    MercenaryRecruitmentCost,
    MercenaryPoolCapacity,
    HoldingVictoryUpgradeChance,
    ConquestGold,
    LevyRecoveryTime,
    HoldingTaxation,
    ManpowerRecovery
}

public enum NationalLawOperation : byte { AddFlat, AddPercent, Multiply, Override }
public enum NationalLawCultureScope : byte { Any, PrimaryCulture, NonPrimaryCulture, SpecificCulture }
public enum NationalLawTarget : byte { Nation, Holdings, Units, MercenaryPools }
public enum NationalClassRuleType : byte { RequirePrimaryCultureForClass, ForceSpecificCultureToClass }

[Serializable]
public sealed class NationalClassRule
{
    public NationalClassRuleType type;
    public SocioEconomicClass affectedClass = SocioEconomicClass.Aristocracy;
    public SocioEconomicClass resultingClass = SocioEconomicClass.Freemen;
    public string cultureName;

    public bool Apply(Nation nation, ProvinceHolding holding)
    {
        if (nation == null || holding == null) return false;
        SocioEconomicClass current = SocioEconomicClassRules.Normalize(holding.socioEconomicClass);
        if (type == NationalClassRuleType.RequirePrimaryCultureForClass)
        {
            if (current != SocioEconomicClassRules.Normalize(affectedClass)) return false;
            string primary = nation.culture != null ? nation.culture.DisplayName : string.Empty;
            if (!string.IsNullOrEmpty(primary) && string.Equals(holding.cultureName, primary,
                StringComparison.OrdinalIgnoreCase)) return false;
        }
        else if (!string.Equals(holding.cultureName, cultureName, StringComparison.OrdinalIgnoreCase)) return false;
        SocioEconomicClass replacement = SocioEconomicClassRules.Normalize(resultingClass);
        if (current == replacement) return false;
        holding.socioEconomicClass = replacement;
        return true;
    }

    public string Describe()
    {
        if (type == NationalClassRuleType.RequirePrimaryCultureForClass)
            return "only primary-culture holdings may be " + SocioEconomicClassRules.DisplayName(affectedClass) +
                "; others become " + SocioEconomicClassRules.DisplayName(resultingClass);
        return (string.IsNullOrWhiteSpace(cultureName) ? "specified-culture" : cultureName) +
            " holdings are " + SocioEconomicClassRules.DisplayName(resultingClass);
    }
}

[Serializable]
public sealed class NationalLawEffect
{
    public NationalLawEffectType type;
    public NationalLawOperation operation = NationalLawOperation.AddFlat;
    [Tooltip("Permille value. 250 is 25%; negative values are penalties or reductions.")]
    [Range(-5000, 5000)] public int amountPermille;
    public NationalLawTarget target = NationalLawTarget.Nation;
    public bool anySocioEconomicClass = true;
    public SocioEconomicClass socioEconomicClass = SocioEconomicClass.Citizen;
    public NationalLawCultureScope cultureScope = NationalLawCultureScope.Any;
    public string cultureName;
    public bool anyUnitOrigin = true;
    public CampaignUnitOrigin unitOrigin = CampaignUnitOrigin.Professional;
    [Tooltip("When false, only holdings aligned with this Allegiance are affected.")]
    public bool anyAllegiance = true;
    public string allegianceId;
    [Tooltip("When enabled, the selected Allegiance's current-interest regions are targeted instead of its individual holdings.")]
    public bool useAllegianceFocusedRegions;

    public bool AppliesTo(Nation nation, ProvinceHolding holding, CampaignUnitOrigin origin,
        string sourceRegionId = null)
    {
        if (!anyUnitOrigin && origin != unitOrigin) return false;
        if (holding == null) return target != NationalLawTarget.Holdings;
        if (!anyAllegiance)
        {
            if (useAllegianceFocusedRegions)
            {
                Allegiance allegiance = nation != null && nation.allegiances != null
                    ? nation.allegiances.Find(item => item != null &&
                        (string.Equals(item.id, allegianceId, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(item.displayName, allegianceId, StringComparison.OrdinalIgnoreCase))) : null;
                if (allegiance == null || allegiance.currentInterestRegionIds == null ||
                    !allegiance.currentInterestRegionIds.Exists(region =>
                        string.Equals(region, sourceRegionId, StringComparison.OrdinalIgnoreCase))) return false;
            }
            else if (!string.Equals(holding.allegiance, allegianceId,
                StringComparison.OrdinalIgnoreCase)) return false;
        }
        if (!anySocioEconomicClass && SocioEconomicClassRules.Normalize(holding.socioEconomicClass) !=
            SocioEconomicClassRules.Normalize(socioEconomicClass)) return false;
        string primaryCulture = nation != null && nation.culture != null ? nation.culture.DisplayName : string.Empty;
        bool isPrimary = !string.IsNullOrEmpty(primaryCulture) && string.Equals(holding.cultureName, primaryCulture,
            StringComparison.OrdinalIgnoreCase);
        switch (cultureScope)
        {
            case NationalLawCultureScope.PrimaryCulture: return isPrimary;
            case NationalLawCultureScope.NonPrimaryCulture: return !isPrimary;
            case NationalLawCultureScope.SpecificCulture:
                return string.Equals(holding.cultureName, cultureName, StringComparison.OrdinalIgnoreCase);
            default: return true;
        }
    }

    public string Describe()
    {
        string amount = type == NationalLawEffectType.ConquestGold
            ? Mathf.Abs(amountPermille) + " gold"
            : (Mathf.Abs(amountPermille) / 10f).ToString("0.#") + "%";
        string effectName = type == NationalLawEffectType.LevyConscription ? "levy conscription" :
            type == NationalLawEffectType.MercenaryRecruitmentTime ? "mercenary recruitment time" :
            type == NationalLawEffectType.MercenaryRecruitmentCost ? "mercenary recruitment cost" :
            type == NationalLawEffectType.MercenaryPoolCapacity ? "mercenary pool capacity" :
            type == NationalLawEffectType.ConquestGold ? "conquest loot" :
            type == NationalLawEffectType.LevyRecoveryTime ? "levy recovery time" :
            type == NationalLawEffectType.HoldingTaxation ? "holding taxation" :
            type == NationalLawEffectType.ManpowerRecovery ? "manpower recovery" :
            "holding upgrade chance after victories";
        string scope = target.ToString().ToLowerInvariant();
        if (target == NationalLawTarget.Holdings)
        {
            string classText = anySocioEconomicClass ? "all classes" : SocioEconomicClassRules.DisplayName(socioEconomicClass);
            string cultureText = cultureScope == NationalLawCultureScope.Any ? "any culture" :
                cultureScope == NationalLawCultureScope.PrimaryCulture ? "primary culture" :
                cultureScope == NationalLawCultureScope.NonPrimaryCulture ? "non-primary cultures" : cultureName;
            scope = classText + " holdings of " + cultureText;
            if (!anyAllegiance) scope += useAllegianceFocusedRegions
                ? " in the focused regions of " +
                    (string.IsNullOrWhiteSpace(allegianceId) ? "the selected Allegiance" : allegianceId)
                : " aligned with " +
                    (string.IsNullOrWhiteSpace(allegianceId) ? "the selected Allegiance" : allegianceId);
        }
        if (type == NationalLawEffectType.LevyConscription && operation == NationalLawOperation.AddFlat)
            return amount + " of " + effectName + " applies to " + scope;
        string verb = operation == NationalLawOperation.Override ? "sets" : amountPermille < 0 ? "reduces" : "increases";
        return verb + " " + effectName + " by " + amount + (target == NationalLawTarget.Nation ? string.Empty : " for " + scope);
    }
}

[Serializable]
public sealed class NationalLaw
{
    public string id;
    public string displayName;
    public List<NationalLawEffect> effects = new List<NationalLawEffect>();
    public List<NationalClassRule> classRules = new List<NationalClassRule>();
    [Tooltip("Temporary political authorizations made available by this law.")]
    public List<NationalEdict> availableExtensions = new List<NationalEdict>();

    // Legacy single-effect fields retained so saves created before multi-effect laws migrate automatically.
    [HideInInspector] public int amountPermille;
    [HideInInspector] public NationalLawEffectType effect;
    [HideInInspector] public bool anySocioEconomicClass = true;
    [HideInInspector] public SocioEconomicClass socioEconomicClass = SocioEconomicClass.Citizen;
    [HideInInspector] public bool anyCulture = true;
    [HideInInspector] public string cultureName;

    public void EnsureEffectsMigrated()
    {
        if (effects == null) effects = new List<NationalLawEffect>();
        if (effects.Count > 0 || amountPermille == 0) return;
        effects.Add(new NationalLawEffect { type = effect, operation = NationalLawOperation.AddFlat,
            amountPermille = amountPermille, target = NationalLawTarget.Holdings,
            anySocioEconomicClass = anySocioEconomicClass, socioEconomicClass = socioEconomicClass,
            cultureScope = anyCulture ? NationalLawCultureScope.Any : NationalLawCultureScope.SpecificCulture,
            cultureName = cultureName ?? string.Empty });
    }

    public NationalLaw Clone()
    {
        EnsureEffectsMigrated();
        NationalLaw copy = new NationalLaw { id = id, displayName = displayName };
        foreach (NationalLawEffect source in effects)
            if (source != null) copy.effects.Add(new NationalLawEffect { type = source.type, operation = source.operation,
                amountPermille = source.amountPermille, target = source.target,
                anySocioEconomicClass = source.anySocioEconomicClass, socioEconomicClass = source.socioEconomicClass,
                cultureScope = source.cultureScope, cultureName = source.cultureName,
                anyUnitOrigin = source.anyUnitOrigin, unitOrigin = source.unitOrigin,
                anyAllegiance = source.anyAllegiance, allegianceId = source.allegianceId,
                useAllegianceFocusedRegions = source.useAllegianceFocusedRegions });
        if (classRules != null) foreach (NationalClassRule source in classRules)
            if (source != null) copy.classRules.Add(new NationalClassRule { type = source.type,
                affectedClass = source.affectedClass, resultingClass = source.resultingClass,
                cultureName = source.cultureName });
        if (availableExtensions != null) foreach (NationalEdict extension in availableExtensions)
            if (extension != null) copy.availableExtensions.Add(extension.Clone());
        return copy;
    }

    public string Describe()
    {
        EnsureEffectsMigrated();
        List<string> lines = new List<string>();
        foreach (NationalLawEffect entry in effects) if (entry != null) lines.Add(entry.Describe());
        if (classRules != null) foreach (NationalClassRule rule in classRules) if (rule != null) lines.Add(rule.Describe());
        return lines.Count > 0 ? string.Join("; ", lines) : "No effects";
    }

    public string DescribeExtensions()
    {
        if (availableExtensions == null || availableExtensions.Count == 0) return "None";
        List<string> descriptions = new List<string>();
        foreach (NationalEdict extension in availableExtensions)
            if (extension != null) descriptions.Add(extension.DisplayName + ": " + extension.DescribeCore());
        return descriptions.Count > 0 ? string.Join("\n", descriptions) : "None";
    }

    public string DescribeWithName() => (!string.IsNullOrWhiteSpace(displayName) ? displayName :
        !string.IsNullOrWhiteSpace(id) ? id : "Unnamed law") + ": " + Describe();
}

public static class NationalLawDefaults
{
    public static NationalLaw Levy(string id, string name, int amountPermille, bool anyClass,
        SocioEconomicClass socialClass, NationalLawCultureScope cultureScope = NationalLawCultureScope.Any)
    {
        NationalLaw law = new NationalLaw { id = id, displayName = name };
        law.effects.Add(new NationalLawEffect { type = NationalLawEffectType.LevyConscription,
            operation = NationalLawOperation.AddFlat, amountPermille = amountPermille,
            target = NationalLawTarget.Holdings, anySocioEconomicClass = anyClass,
            socioEconomicClass = socialClass, cultureScope = cultureScope });
        return law;
    }

    public static NationalLaw WarriorRites()
    {
        NationalLaw law = new NationalLaw { id = "warrior_rites", displayName = "Warrior Rites" };
        law.effects.Add(new NationalLawEffect { type = NationalLawEffectType.HoldingVictoryUpgradeChance,
            operation = NationalLawOperation.AddFlat, amountPermille = 250, target = NationalLawTarget.Nation });
        return law;
    }

    public static NationalLaw KingsShare()
    {
        NationalLaw law = new NationalLaw { id = "kings_share", displayName = "King's Share" };
        law.effects.Add(new NationalLawEffect { type = NationalLawEffectType.ConquestGold,
            operation = NationalLawOperation.AddFlat, amountPermille = 200, target = NationalLawTarget.Nation });
        return law;
    }

    public static NationalLaw MercenaryContracts()
    {
        NationalLaw law = new NationalLaw { id = "mercenary_contracts", displayName = "Mercenary Contracts" };
        law.effects.Add(new NationalLawEffect { type = NationalLawEffectType.MercenaryRecruitmentTime,
            operation = NationalLawOperation.AddPercent, amountPermille = -250, target = NationalLawTarget.Units,
            anyUnitOrigin = false, unitOrigin = CampaignUnitOrigin.Mercenary });
        law.effects.Add(new NationalLawEffect { type = NationalLawEffectType.MercenaryRecruitmentCost,
            operation = NationalLawOperation.AddPercent, amountPermille = -100, target = NationalLawTarget.Units,
            anyUnitOrigin = false, unitOrigin = CampaignUnitOrigin.Mercenary });
        law.effects.Add(new NationalLawEffect { type = NationalLawEffectType.MercenaryPoolCapacity,
            operation = NationalLawOperation.AddPercent, amountPermille = 300, target = NationalLawTarget.MercenaryPools });
        return law;
    }

    public static NationalLaw RomanLevyRecovery()
    {
        NationalLaw law = new NationalLaw { id = "roman_muster_rolls", displayName = "Citizen Muster Rolls" };
        law.effects.Add(new NationalLawEffect { type = NationalLawEffectType.LevyRecoveryTime,
            operation = NationalLawOperation.AddPercent, amountPermille = -500, target = NationalLawTarget.Nation });
        return law;
    }

    public static NationalLaw CarthaginianManpowerRecovery()
    {
        NationalLaw law = new NationalLaw
        {
            id = "carthaginian_subject_mustering",
            displayName = "Reliance on Subject Musters"
        };
        law.effects.Add(new NationalLawEffect
        {
            type = NationalLawEffectType.ManpowerRecovery,
            operation = NationalLawOperation.AddPercent,
            amountPermille = -500,
            target = NationalLawTarget.Nation
        });
        return law;
    }

    public static NationalLaw RepublicanLevy()
    {
        NationalLaw law = Levy("roman_citizen_levy", "Republican Levy", 200, false,
            SocioEconomicClass.Citizen);
        law.availableExtensions.Add(NationalEdict.CreateLevyExtension("raise_citizen_levy",
            "Raise Citizen Levy", 200, SocioEconomicClass.Citizen, 24));
        law.availableExtensions.Add(NationalEdict.CreateEmergencyFreemenMuster());
        law.availableExtensions.Add(NationalEdict.CreateExtraordinaryWarTax());
        return law;
    }

    public static NationalLaw TribalMuster()
    {
        NationalLaw law = Levy("tribal_muster", "Tribal Muster", 200, true, SocioEconomicClass.Freemen);
        law.availableExtensions.Add(NationalEdict.CreateAllegianceLevyExtension());
        return law;
    }
}
