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
    public bool representsUnalignedHoldings;
    public SocioEconomicClass representedClass;
    [Tooltip("ID of the real Family/Tribe represented by this voting adapter. Empty for synthetic unaligned blocs.")]
    public string allegianceId;
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
    public bool playerVoteCast;
    public bool playerSupports;
}

public static class PoliticalProposalSystem
{
    public static void EnsureGroups(Nation nation)
    {
        if (nation.politicalGroups == null) nation.politicalGroups = new List<PoliticalGroup>();
        nation.politicalGroups.RemoveAll(group => group == null);
        AllegianceSystem.EnsureNationAllegiances(nation);
        if (nation.allegiances != null && nation.allegiances.Count > 0)
        {
            List<PoliticalGroup> resolved = new List<PoliticalGroup>();
            foreach (Allegiance allegiance in nation.allegiances)
            {
                if (allegiance == null) continue;
                PoliticalGroup existing = nation.politicalGroups.Find(group => group != null &&
                    (string.Equals(group.id, allegiance.id, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(group.displayName, allegiance.displayName, StringComparison.OrdinalIgnoreCase)));
                if (existing == null) existing = new PoliticalGroup { votes = 1 };
                existing.id = allegiance.id; existing.allegianceId = allegiance.id;
                existing.displayName = allegiance.displayName; existing.representsUnalignedHoldings = false;
                resolved.Add(existing);
            }
            AddUnalignedGroups(resolved, nation.politicalGroups);
            nation.politicalGroups = resolved;
            AssignUnownedEnslavedHoldings(nation);
            return;
        }
        List<string> configuredNames = NationContentResolver.ResolveAllegianceNames(nation);
        if (configuredNames.Count > 0)
        {
            List<PoliticalGroup> resolved = new List<PoliticalGroup>();
            foreach (string configuredName in configuredNames)
            {
                string id = StableGroupId(configuredName);
                PoliticalGroup existing = nation.politicalGroups.Find(group => group != null &&
                    (string.Equals(group.id, id, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(group.displayName, configuredName, StringComparison.OrdinalIgnoreCase)));
                resolved.Add(existing ?? new PoliticalGroup { id = id, displayName = configuredName, votes = 1 });
            }
            AddUnalignedGroups(resolved, nation.politicalGroups);
            nation.politicalGroups = resolved;
            AssignUnownedEnslavedHoldings(nation);
            return;
        }
        if (nation.politicalGroups.Count > 0)
        {
            if (!nation.politicalGroups.Exists(group => group != null && !group.representsUnalignedHoldings))
            {
                string fallbackType = NationContentResolver.ResolveAllegianceType(nation);
                for (int i = 1; i <= 3; i++) nation.politicalGroups.Add(new PoliticalGroup
                    { id = fallbackType.ToLowerInvariant() + "_" + i,
                        displayName = fallbackType + " " + i, votes = 1 });
            }
            AddUnalignedGroups(nation.politicalGroups, nation.politicalGroups);
            AssignUnownedEnslavedHoldings(nation);
            return;
        }
        string assembly = NationContentResolver.ResolveAssemblyName(nation);
        string noun = assembly == "Senate" ? "Family" : assembly == "Adirim" ? "Faction" : "Tribe";
        for (int i = 1; i <= 3; i++) nation.politicalGroups.Add(new PoliticalGroup
            { id = noun.ToLowerInvariant() + "_" + i, displayName = noun + " " + i, votes = 1 });
        AddUnalignedGroups(nation.politicalGroups, nation.politicalGroups);
        AssignUnownedEnslavedHoldings(nation);
    }

    private static void AddUnalignedGroups(List<PoliticalGroup> target, List<PoliticalGroup> existing)
    {
        AddUnalignedGroup(target, existing, "unaligned_citizens", "Unaligned Citizens", SocioEconomicClass.Citizen);
        AddUnalignedGroup(target, existing, "unaligned_elites", "Unaligned Elites", SocioEconomicClass.Aristocracy);
        AddUnalignedGroup(target, existing, "unaligned_freemen", "Unaligned Freemen", SocioEconomicClass.Freemen);
    }

    private static void AddUnalignedGroup(List<PoliticalGroup> target, List<PoliticalGroup> existing,
        string id, string displayName, SocioEconomicClass representedClass)
    {
        if (target.Exists(group => group != null && group.id == id)) return;
        PoliticalGroup group = existing != null ? existing.Find(candidate => candidate != null && candidate.id == id) : null;
        if (group == null) group = new PoliticalGroup { id = id, displayName = displayName, votes = 1 };
        group.displayName = displayName;
        group.representsUnalignedHoldings = true;
        group.representedClass = representedClass;
        target.Add(group);
    }

    private static void AssignUnownedEnslavedHoldings(Nation nation)
    {
        if (nation == null || Owners.Instance == null) return;
        List<PoliticalGroup> owners = nation.politicalGroups.FindAll(group =>
            group != null && !group.representsUnalignedHoldings);
        if (owners.Count == 0) return;
        foreach (Province province in Owners.Instance.provincelist)
        {
            if (province == null || province.nation != nation || province.holdings == null) continue;
            foreach (ProvinceHolding holding in province.holdings)
            {
                if (holding == null || SocioEconomicClassRules.Normalize(holding.socioEconomicClass) !=
                    SocioEconomicClass.Enslaved) continue;
                if (!string.IsNullOrWhiteSpace(holding.allegiance) &&
                    !string.Equals(holding.allegiance, "Unaligned", StringComparison.OrdinalIgnoreCase)) continue;
                string identity = province.name + "|" + holding.instanceId + "|" + holding.HoldingId;
                holding.allegiance = owners[StableHash(identity) % owners.Count].id;
            }
        }
    }

    public static PoliticalProposal CurrentEdict(Nation nation) => nation != null && nation.politicalProposals != null
        ? nation.politicalProposals.Find(proposal => proposal != null && proposal.type == PoliticalProposalType.Edict)
        : null;

    public static bool CastPlayerVote(Nation nation, string proposalId, bool supports)
    {
        PoliticalProposal proposal = CurrentEdict(nation);
        if (proposal == null || proposal.playerVoteCast || proposal.id != proposalId) return false;
        proposal.playerVoteCast = true;
        proposal.playerSupports = supports;
        return true;
    }

    public static string DescribeEdict(NationalEdict edict)
    {
        if (edict == null) return "No edict details available.";
        if (edict.type == NationalEdictType.LevyRecovery)
            return "Spend " + edict.treasuryCost + " gold to immediately recover " +
                edict.immediateRecoveryTicks + " levy-recovery ticks and gain +" +
                Mathf.Max(1, edict.recoveryBonusPerTick) + " recovery per tick for " + edict.durationTicks + " ticks.";
        if (edict.type == NationalEdictType.ReassignHoldingAllegiance)
            return "Reassign " + (edict.allegianceScope == AllegianceEdictScope.SpecificHolding
                ? "the selected holding" : edict.allegianceScope.ToString()) + " to " +
                (!string.IsNullOrWhiteSpace(edict.allegiance) ? edict.allegiance : "an unassigned allegiance") + ".";
        return "Seize " + edict.treasuryGain + " gold from a holding and reduce that holding by one level.";
    }

    public static bool ProposeDefaultPlayerEdict(Nation nation)
    {
        if (nation == null || CurrentEdict(nation) != null) return false;
        return ProposeEdict(nation, "Emergency Levy Recovery", new NationalEdict
        {
            type = NationalEdictType.LevyRecovery,
            treasuryCost = 100,
            durationTicks = 12,
            immediateRecoveryTicks = 12,
            recoveryBonusPerTick = 1
        }, "player", 8);
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
            proposal.votes.Add(new PoliticalVote { groupId = group.id, votes = DerivedVotingPower(nation, group),
                supports = EvaluateSupportStub(nation, group, proposal) });
    }

    private static int DerivedVotingPower(Nation nation, PoliticalGroup group)
    {
        if (nation == null || group == null || Owners.Instance == null) return 1;
        int power = 0;
        foreach (Province province in Owners.Instance.provincelist)
        {
            if (province == null || province.nation != nation || province.holdings == null) continue;
            foreach (ProvinceHolding holding in province.holdings)
            {
                if (holding == null) continue;
                if (group.representsUnalignedHoldings)
                {
                    bool unaligned = string.IsNullOrWhiteSpace(holding.allegiance) ||
                        string.Equals(holding.allegiance, "Unaligned", StringComparison.OrdinalIgnoreCase);
                    if (unaligned && SocioEconomicClassRules.Normalize(holding.socioEconomicClass) ==
                        SocioEconomicClassRules.Normalize(group.representedClass)) power++;
                }
                else if (string.Equals(holding.allegiance, group.id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(holding.allegiance, group.displayName, StringComparison.OrdinalIgnoreCase)) power++;
            }
        }
        return Mathf.Max(1, power);
    }

    private static bool EvaluateSupportStub(Nation nation, PoliticalGroup group, PoliticalProposal proposal)
    {
        if (group == null || group.representsUnalignedHoldings) return true;
        Allegiance allegiance = AllegianceSystem.Find(nation, !string.IsNullOrWhiteSpace(group.allegianceId)
            ? group.allegianceId : group.id);
        return allegiance == null || PoliticalEvaluationSystem.EvaluateProposal(nation, allegiance, proposal).supports;
    }

    private static bool Passed(PoliticalProposal proposal)
    {
        int yes = 0, no = 0;
        foreach (PoliticalVote vote in proposal.votes) if (vote.supports) yes += vote.votes; else no += vote.votes;
        if (proposal.playerVoteCast) { if (proposal.playerSupports) yes++; else no++; }
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
        nation.latestPassedEdict = proposal.title + ": " + DescribeEdict(proposal.edict);
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
        List<PoliticalGroup> assignableGroups = nation.politicalGroups.FindAll(group => group != null && !group.representsUnalignedHoldings);
        if (provinces.Count == 0 || assignableGroups.Count == 0) return;
        Province province = provinces[StableHash(nation.name + tick) % provinces.Count];
        if (province.holdings == null || province.holdings.Count == 0) return;
        ProvinceHolding holding = province.holdings[StableHash(province.name + tick) % province.holdings.Count];
        PoliticalGroup group = assignableGroups[StableHash(holding.instanceId + tick) % assignableGroups.Count];
        ProposeEdict(nation, group.displayName + " requests " + holding.HoldingId,
            new NationalEdict { type = NationalEdictType.ReassignHoldingAllegiance,
                provinceName = province.name, holdingInstanceId = holding.instanceId,
                allegiance = group.id, allegianceScope = AllegianceEdictScope.SpecificHolding }, group.id);
    }

    private static List<Province> OwnedProvinces(Nation nation) => Owners.Instance != null
        ? Owners.Instance.provincelist.FindAll(province => province != null && province.nation == nation) : new List<Province>();
    private static Province FindProvince(Nation nation, string name) => OwnedProvinces(nation).Find(province => province.name == name);
    private static int StableHash(string value) { unchecked { int hash = 17; if (value != null) foreach (char c in value) hash = hash * 31 + c; return hash & int.MaxValue; } }
    private static string StableGroupId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "allegiance";
        System.Text.StringBuilder result = new System.Text.StringBuilder();
        foreach (char character in value.Trim().ToLowerInvariant())
            if (char.IsLetterOrDigit(character)) result.Append(character); else if (result.Length > 0 && result[result.Length - 1] != '_') result.Append('_');
        return result.ToString().Trim('_');
    }
}
