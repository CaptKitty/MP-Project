using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Normalized consequences of an existing state action. Traits only score this data; they never execute it.</summary>
public sealed class PoliticalActionConsequences
{
    public int citizenHoldings;
    public int eliteHoldings;
    public int freemenHoldings;
    public int preferredCulture;
    public int commerce;
    public int militaryStrength;
    public int territorialExpansion;
    public int regionalInterests;
    public int foreignInfluence;
    public int defense;
    public int statusQuo;
    public int federationBenefit;
    public int tribalAutonomy;
    public int ownPoliticalPower;
    public string affectedRegionId;
    public string beneficiaryAllegianceId;
}

public struct PoliticalEvaluationResult
{
    public int score;
    public bool supports;
    public string summary;
}

public static class PoliticalEvaluationSystem
{
    private const int PrimaryWeight = 100;
    private const int DynamicWeight = 75;

    public static PoliticalEvaluationResult EvaluateProposal(Nation nation, Allegiance allegiance,
        PoliticalProposal proposal)
    {
        PoliticalActionConsequences consequences = DescribeProposal(nation, allegiance, proposal);
        return Evaluate(nation, allegiance, consequences);
    }

    public static PoliticalEvaluationResult Evaluate(Nation nation, Allegiance allegiance,
        PoliticalActionConsequences consequences)
    {
        if (allegiance == null || consequences == null)
            return new PoliticalEvaluationResult { supports = true, summary = "No allegiance preference data." };

        int score = 0;
        List<string> reasons = new List<string>();
        ScoreTrait(allegiance.PrimaryIdentity, allegiance, consequences, PrimaryWeight, ref score, reasons);
        ScoreTrait(allegiance.DynamicIdentity, allegiance, consequences, DynamicWeight, ref score, reasons);

        int ownedHoldingCount = AllegianceSystem.Holdings(nation, allegiance).Count;
        if (consequences.ownPoliticalPower != 0)
            score += consequences.ownPoliticalPower * Mathf.Clamp(ownedHoldingCount, 1, 10);

        bool currentInterest = Contains(allegiance.currentInterestRegionIds, consequences.affectedRegionId);
        bool futureInterest = Contains(allegiance.futureInterestRegionIds, consequences.affectedRegionId);
        if (currentInterest) { score += consequences.defense * 8 + consequences.regionalInterests * 10; reasons.Add("current-interest region"); }
        if (futureInterest) { score += consequences.territorialExpansion * 12 + consequences.regionalInterests * 6; reasons.Add("future-interest region"); }

        if (!string.IsNullOrWhiteSpace(consequences.beneficiaryAllegianceId))
        {
            bool self = Same(consequences.beneficiaryAllegianceId, allegiance.id) ||
                Same(consequences.beneficiaryAllegianceId, allegiance.displayName);
            score += consequences.ownPoliticalPower * (self ? 10 : -6);
        }

        return new PoliticalEvaluationResult { score = score, supports = score >= 0,
            summary = reasons.Count > 0 ? string.Join(", ", reasons) : "general political preferences" };
    }

    private static void ScoreTrait(PoliticalTrait trait, Allegiance allegiance, PoliticalActionConsequences c,
        int identityWeight, ref int score, List<string> reasons)
    {
        if (trait == null || !trait.Allows(allegiance.type) || trait.preferences == null) return;
        PoliticalPreferenceWeights w = trait.preferences;
        int raw = w.citizenHoldings * c.citizenHoldings + w.eliteHoldings * c.eliteHoldings +
            w.freemenHoldings * c.freemenHoldings + w.preferredCulture * c.preferredCulture +
            w.commerce * c.commerce + w.militaryStrength * c.militaryStrength +
            w.territorialExpansion * c.territorialExpansion + w.regionalInterests * c.regionalInterests +
            w.foreignInfluence * c.foreignInfluence + w.defense * c.defense + w.statusQuo * c.statusQuo +
            w.federationBenefit * c.federationBenefit + w.tribalAutonomy * c.tribalAutonomy +
            w.ownPoliticalPower * c.ownPoliticalPower;
        score += raw * identityWeight / 100;
        if (raw != 0) reasons.Add(trait.DisplayName + " " + (raw > 0 ? "+" : string.Empty) + raw);
    }

    private static PoliticalActionConsequences DescribeProposal(Nation nation, Allegiance allegiance,
        PoliticalProposal proposal)
    {
        PoliticalActionConsequences c = new PoliticalActionConsequences();
        if (proposal == null) return c;
        if (proposal.type == PoliticalProposalType.Law) DescribeLaw(nation, proposal.law, c);
        else DescribeEdict(nation, allegiance, proposal.edict, c);
        return c;
    }

    private static void DescribeLaw(Nation nation, NationalLaw law, PoliticalActionConsequences c)
    {
        if (law == null) return;
        bool currentlyEstablished = nation != null && nation.laws != null && nation.laws.Exists(existing =>
            existing != null && Same(existing.id, law.id));
        c.statusQuo = currentlyEstablished ? 2 : -2;
        law.EnsureEffectsMigrated();
        if (law.effects != null) foreach (NationalLawEffect effect in law.effects)
        {
            if (effect == null) continue;
            int direction = effect.amountPermille == 0 ? 0 : effect.amountPermille > 0 ? 1 : -1;
            if (effect.type == NationalLawEffectType.LevyConscription ||
                effect.type == NationalLawEffectType.LevyRecoveryTime ||
                effect.type == NationalLawEffectType.HoldingVictoryUpgradeChance) c.militaryStrength +=
                    effect.type == NationalLawEffectType.LevyRecoveryTime ? -direction : direction;
            if (effect.type == NationalLawEffectType.MercenaryPoolCapacity) c.militaryStrength += direction;
            if (effect.type == NationalLawEffectType.MercenaryRecruitmentCost ||
                effect.type == NationalLawEffectType.MercenaryRecruitmentTime) { c.militaryStrength -= direction; c.commerce -= direction; }
            if (effect.type == NationalLawEffectType.ConquestGold) { c.territorialExpansion += direction; c.commerce += direction; }
            ApplyScope(effect, direction, c);
        }
        if (law.classRules != null) foreach (NationalClassRule rule in law.classRules)
        {
            if (rule == null) continue;
            AddClass(c, rule.affectedClass, -2);
            AddClass(c, rule.resultingClass, 2);
            if (rule.type == NationalClassRuleType.RequirePrimaryCultureForClass) c.preferredCulture += 2;
        }
    }

    private static void ApplyScope(NationalLawEffect effect, int direction, PoliticalActionConsequences c)
    {
        if (effect.target == NationalLawTarget.Holdings)
        {
            if (effect.anySocioEconomicClass) { c.citizenHoldings += direction; c.eliteHoldings += direction; c.freemenHoldings += direction; }
            else AddClass(c, effect.socioEconomicClass, direction);
            if (effect.cultureScope == NationalLawCultureScope.PrimaryCulture) c.preferredCulture += direction;
            else if (effect.cultureScope == NationalLawCultureScope.NonPrimaryCulture) c.preferredCulture -= direction;
        }
    }

    private static void DescribeEdict(Nation nation, Allegiance allegiance, NationalEdict edict,
        PoliticalActionConsequences c)
    {
        if (edict == null) return;
        c.statusQuo = -1;
        c.affectedRegionId = RegionForProvince(edict.provinceName);
        if (edict.coreEffects != null && edict.coreEffects.Count > 0)
        {
            foreach (NationalLawEffect effect in edict.coreEffects)
            {
                if (effect == null) continue;
                int direction = effect.amountPermille == 0 ? 0 : effect.amountPermille > 0 ? 1 : -1;
                if (effect.type == NationalLawEffectType.LevyConscription) c.militaryStrength += direction * 3;
                if (effect.type == NationalLawEffectType.HoldingTaxation) c.commerce += direction;
                if (!effect.anyAllegiance)
                {
                    c.federationBenefit += direction * 2;
                    if (allegiance != null && (Same(effect.allegianceId, allegiance.id) ||
                        Same(effect.allegianceId, allegiance.displayName))) c.tribalAutonomy -= direction * 2;
                }
                ApplyScope(effect, direction, c);
            }
            if (edict.aftermathType == EdictAftermathType.ConvertHoldingClass)
            {
                AddClass(c, edict.aftermathFromClass, -2);
                AddClass(c, edict.aftermathToClass, 2);
            }
            else if (edict.aftermathType == EdictAftermathType.TimedEffect && edict.aftermathEffects != null)
                foreach (NationalLawEffect effect in edict.aftermathEffects)
                    if (effect != null)
                    {
                        int direction = effect.amountPermille == 0 ? 0 : effect.amountPermille > 0 ? 1 : -1;
                        if (effect.type == NationalLawEffectType.HoldingTaxation) c.commerce += direction;
                        ApplyScope(effect, direction, c);
                    }
            return;
        }
        if (edict.type == NationalEdictType.LevyRecovery)
        {
            c.militaryStrength = 3;
            c.defense = 1;
            c.commerce = edict.treasuryCost > 0 ? -1 : 0;
            c.federationBenefit = 1;
            c.tribalAutonomy = -1;
        }
        else if (edict.type == NationalEdictType.ReassignHoldingAllegiance)
        {
            c.beneficiaryAllegianceId = edict.allegiance;
            c.ownPoliticalPower = 3;
            ProvinceHolding holding = FindHolding(nation, edict);
            if (holding != null) AddClass(c, holding.socioEconomicClass, 2);
        }
        else
        {
            c.commerce = 2;
            ProvinceHolding holding = FindHolding(nation, edict);
            if (holding != null)
            {
                AddClass(c, holding.socioEconomicClass, -2);
                c.beneficiaryAllegianceId = holding.allegiance;
                c.ownPoliticalPower = -2;
            }
        }
    }

    private static ProvinceHolding FindHolding(Nation nation, NationalEdict edict)
    {
        if (Owners.Instance == null || edict == null) return null;
        Province province = Owners.Instance.provincelist.Find(p => p != null && p.nation == nation && p.name == edict.provinceName);
        return province != null ? province.GetHolding(edict.holdingInstanceId) : null;
    }

    private static string RegionForProvince(string provinceName)
    {
        Province province = Owners.Instance != null ? Owners.Instance.provincelist.Find(p => p != null && p.name == provinceName) : null;
        return province != null ? province.region : string.Empty;
    }

    private static void AddClass(PoliticalActionConsequences c, SocioEconomicClass socialClass, int amount)
    {
        switch (SocioEconomicClassRules.Normalize(socialClass))
        {
            case SocioEconomicClass.Citizen: c.citizenHoldings += amount; break;
            case SocioEconomicClass.Aristocracy: c.eliteHoldings += amount; break;
            case SocioEconomicClass.Freemen: c.freemenHoldings += amount; break;
        }
    }

    private static bool Contains(List<string> values, string value) => !string.IsNullOrWhiteSpace(value) &&
        values != null && values.Exists(candidate => Same(candidate, value));
    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
