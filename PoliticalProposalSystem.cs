using System;
using System.Collections.Generic;
using UnityEngine;

public enum PoliticalProposalType : byte { Law, Edict }
public enum NationalEdictType : byte { LevyRecovery, ReassignHoldingAllegiance, SeizeHoldingWealth }
public enum AllegianceEdictScope : byte { SpecificHolding, Citizens, Aristocracy, PoliticalGroup }

[Serializable]
public sealed class PoliticalGroup
{
    public string id;
    public string displayName;
    [Min(1)] public int votes = 1;
}

[Serializable]
public sealed class PoliticalVote
{
    public string groupId;
    public int votes;
    public bool supports;
}

[Serializable]
public sealed class NationalEdict
{
    public NationalEdictType type;
    public string provinceName;
    public string holdingInstanceId;
    public string allegiance;
    public AllegianceEdictScope allegianceScope;
    [Min(0)] public int treasuryCost;
    [Min(0)] public int durationTicks;
    [Min(0)] public int immediateRecoveryTicks;
    [Min(0)] public int recoveryBonusPerTick = 1;
    [Min(0)] public int treasuryGain;
}

[Serializable]
public sealed class PoliticalProposal
{
    public string id;
    public string title;
    public string proposerGroupId;
    public PoliticalProposalType type;
    public NationalLaw law;
    public NationalEdict edict;
    public int remainingDebateTicks = 8;
    public List<PoliticalVote> votes = new List<PoliticalVote>();
}

public static class PoliticalProposalSystem
{
    public static void EnsureGroups(Nation nation)
    {
        if (nation.politicalGroups == null) nation.politicalGroups = new List<PoliticalGroup>();
        nation.politicalGroups.RemoveAll(group => group == null);
        if (nation.politicalGroups.Count > 0) return;
        string assembly = NationContentResolver.ResolveAssemblyName(nation);
        string noun = assembly == "Senate" ? "Family" : assembly == "Adirim" ? "Faction" : "Tribe";
        for (int i = 1; i <= 3; i++) nation.politicalGroups.Add(new PoliticalGroup
            { id = noun.ToLowerInvariant() + "_" + i, displayName = noun + " " + i, votes = 1 });
    }

    public static bool ProposeLaw(Nation nation, NationalLaw law, string proposer = "player", int debateTicks = 8)
    {
        if (nation == null || law == null || string.IsNullOrWhiteSpace(law.id)) return false;
        return Add(nation, new PoliticalProposal { id = Guid.NewGuid().ToString("N"), title = law.displayName,
            proposerGroupId = proposer, type = PoliticalProposalType.Law, law = law.Clone(),
            remainingDebateTicks = Mathf.Max(1, debateTicks) });
    }

    public static bool ProposeEdict(Nation nation, string title, NationalEdict edict, string proposer = "player", int debateTicks = 8)
    {
        if (nation == null || edict == null) return false;
        return Add(nation, new PoliticalProposal { id = Guid.NewGuid().ToString("N"), title = title,
            proposerGroupId = proposer, type = PoliticalProposalType.Edict, edict = edict,
            remainingDebateTicks = Mathf.Max(1, debateTicks) });
    }

    private static bool Add(Nation nation, PoliticalProposal proposal)
    {
        if (nation.politicalProposals == null) nation.politicalProposals = new List<PoliticalProposal>();
        if (nation.politicalProposals.Exists(item => item != null && item.title == proposal.title)) return false;
        nation.politicalProposals.Add(proposal); return true;
    }

    public static void ProcessTurn(Nation nation, int campaignTick)
    {
        if (nation == null) return;
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening &&
            !Unity.Netcode.NetworkManager.Singleton.IsServer) return;
        EnsureGroups(nation);
        if (nation.politicalProposals == null) nation.politicalProposals = new List<PoliticalProposal>();
        ProcessRecoveryBoost(nation);
        for (int i = nation.politicalProposals.Count - 1; i >= 0; i--)
        {
            PoliticalProposal proposal = nation.politicalProposals[i];
            if (proposal == null) { nation.politicalProposals.RemoveAt(i); continue; }
            if (--proposal.remainingDebateTicks > 0) continue;
            CastVotes(nation, proposal);
            if (Passed(proposal)) Execute(nation, proposal);
            nation.politicalProposals.RemoveAt(i);
        }
        TryGenerateGroupProposal(nation, campaignTick);
    }

    private static void CastVotes(Nation nation, PoliticalProposal proposal)
    {
        proposal.votes.Clear();
        foreach (PoliticalGroup group in nation.politicalGroups)
            proposal.votes.Add(new PoliticalVote { groupId = group.id, votes = Mathf.Max(1, group.votes),
                supports = EvaluateSupportStub(nation, group, proposal) });
    }

    // Deliberate extension point: every group approves until ideology, interests and relationships are implemented.
    private static bool EvaluateSupportStub(Nation nation, PoliticalGroup group, PoliticalProposal proposal) => true;

    private static bool Passed(PoliticalProposal proposal)
    {
        int yes = 0, no = 0;
        foreach (PoliticalVote vote in proposal.votes) if (vote.supports) yes += vote.votes; else no += vote.votes;
        return yes > no;
    }

    private static void Execute(Nation nation, PoliticalProposal proposal)
    {
        if (proposal.type == PoliticalProposalType.Law)
        {
            if (nation.laws == null) nation.laws = new List<NationalLaw>();
            nation.laws.RemoveAll(law => law != null && string.Equals(law.id, proposal.law.id, StringComparison.OrdinalIgnoreCase));
            nation.laws.Add(proposal.law.Clone()); nation.ResetLawResolution(); return;
        }
        ExecuteEdict(nation, proposal.edict);
    }

    private static void ExecuteEdict(Nation nation, NationalEdict edict)
    {
        if (edict == null) return;
        if (edict.type == NationalEdictType.LevyRecovery)
        {
            if (nation.Gold < edict.treasuryCost) return;
            nation.Gold -= edict.treasuryCost;
            nation.levyRecoveryBoostTicks = Mathf.Max(nation.levyRecoveryBoostTicks, edict.durationTicks);
            nation.levyRecoveryBonusPerTick = Mathf.Max(nation.levyRecoveryBonusPerTick, edict.recoveryBonusPerTick);
            ReduceRecovery(nation, edict.immediateRecoveryTicks);
        }
        else if (edict.type == NationalEdictType.ReassignHoldingAllegiance)
        {
            foreach (Province province in OwnedProvinces(nation)) foreach (ProvinceHolding holding in province.holdings)
            {
                if (!Matches(edict, province, holding)) continue;
                holding.allegiance = edict.allegiance ?? string.Empty;
            }
        }
        else
        {
            Province province = FindProvince(nation, edict.provinceName);
            ProvinceHolding holding = province != null ? province.GetHolding(edict.holdingInstanceId) : null;
            if (holding == null || holding.level <= 1) return;
            holding.level--; nation.Gold += Mathf.Max(0, edict.treasuryGain);
            province.ReconcileLevyEntitlements();
        }
    }

    private static bool Matches(NationalEdict edict, Province province, ProvinceHolding holding)
    {
        if (holding == null || !string.IsNullOrEmpty(edict.provinceName) && province.name != edict.provinceName) return false;
        if (edict.allegianceScope == AllegianceEdictScope.SpecificHolding) return holding.instanceId == edict.holdingInstanceId;
        if (edict.allegianceScope == AllegianceEdictScope.Citizens) return SocioEconomicClassRules.Normalize(holding.socioEconomicClass) == SocioEconomicClass.Citizen;
        if (edict.allegianceScope == AllegianceEdictScope.Aristocracy) return SocioEconomicClassRules.Normalize(holding.socioEconomicClass) == SocioEconomicClass.Aristocracy;
        return true;
    }

    private static void ProcessRecoveryBoost(Nation nation)
    {
        if (nation.levyRecoveryBoostTicks <= 0) return;
        ReduceRecovery(nation, Mathf.Max(1, nation.levyRecoveryBonusPerTick));
        if (--nation.levyRecoveryBoostTicks == 0) nation.levyRecoveryBonusPerTick = 0;
    }

    private static void ReduceRecovery(Nation nation, int amount)
    {
        if (amount <= 0) return;
        foreach (Province province in OwnedProvinces(nation)) foreach (ProvinceLevyEntitlement levy in province.levyEntitlements)
            if (levy != null && levy.state == LevyEntitlementState.Recovering)
            {
                levy.remainingTicks -= amount;
                if (levy.remainingTicks <= 0)
                { levy.remainingTicks = 0; levy.state = LevyEntitlementState.Available; levy.raisedArmyId = null; }
            }
    }

    private static void TryGenerateGroupProposal(Nation nation, int tick)
    {
        if (nation.politicalProposals.Count > 0 || tick <= 0 || tick % 40 != StableHash(nation.name) % 40) return;
        List<Province> provinces = OwnedProvinces(nation);
        if (provinces.Count == 0 || nation.politicalGroups.Count == 0) return;
        Province province = provinces[StableHash(nation.name + tick) % provinces.Count];
        if (province.holdings == null || province.holdings.Count == 0) return;
        ProvinceHolding holding = province.holdings[StableHash(province.name + tick) % province.holdings.Count];
        PoliticalGroup group = nation.politicalGroups[StableHash(holding.instanceId + tick) % nation.politicalGroups.Count];
        ProposeEdict(nation, group.displayName + " requests " + holding.HoldingId,
            new NationalEdict { type = NationalEdictType.ReassignHoldingAllegiance,
                provinceName = province.name, holdingInstanceId = holding.instanceId,
                allegiance = group.id, allegianceScope = AllegianceEdictScope.SpecificHolding }, group.id);
    }

    private static List<Province> OwnedProvinces(Nation nation) => Owners.Instance != null
        ? Owners.Instance.provincelist.FindAll(province => province != null && province.nation == nation) : new List<Province>();
    private static Province FindProvince(Nation nation, string name) => OwnedProvinces(nation).Find(province => province.name == name);
    private static int StableHash(string value) { unchecked { int hash = 17; if (value != null) foreach (char c in value) hash = hash * 31 + c; return hash & int.MaxValue; } }
}
