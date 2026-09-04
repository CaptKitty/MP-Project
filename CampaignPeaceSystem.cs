using System;
using System.Collections.Generic;
using UnityEngine;

public enum BasicPeaceTerms : byte
{
    WhitePeace,
    AdjacentOccupiedProvinces,
    AllOccupiedProvinces,
    LiberateOccupiedRegionAsTributary
}

public static class CampaignPeaceSystem
{
    public static BasicPeaceTerms ChooseBasicAITerms(Nation victor, Nation defeated)
    {
        if (victor == null || defeated == null || Owners.Instance == null) return BasicPeaceTerms.WhitePeace;
        List<Province> occupied = Owners.Instance.provincelist.FindAll(province => province != null &&
            province.nation == defeated && province.OccupyingNation == victor);
        if (occupied.Count == 0) return BasicPeaceTerms.WhitePeace;
        bool canResurrect = Owners.Instance.regionlist.Exists(region => region != null &&
            region.provincelist.Exists(province => province != null && province.OccupyingNation == victor &&
                province.OriginalNation != null && !Owners.Instance.provincelist.Exists(other => other != null &&
                    other.nation == province.OriginalNation && !other.IsOccupied)));
        bool carthaginian = string.Equals(victor.name, "Carthage", StringComparison.OrdinalIgnoreCase);
        if (canResurrect && occupied.Count >= (carthaginian ? 1 : 3))
            return BasicPeaceTerms.LiberateOccupiedRegionAsTributary;
        int defeatedLand = Owners.Instance.provincelist.FindAll(province => province != null &&
            province.nation == defeated).Count;
        return occupied.Count * 2 >= Mathf.Max(1, defeatedLand)
            ? BasicPeaceTerms.AllOccupiedProvinces : BasicPeaceTerms.AdjacentOccupiedProvinces;
    }

    public static bool Resolve(Nation victor, Nation defeated, BasicPeaceTerms terms,
        string regionName = null)
    {
        if (victor == null || defeated == null || Owners.Instance == null) return false;
        if (DiplomacySystem.IsTotalWar(victor, defeated)) return false;
        bool changed;
        switch (terms)
        {
            case BasicPeaceTerms.WhitePeace:
                changed = ClearMutualOccupations(victor, defeated);
                break;
            case BasicPeaceTerms.AdjacentOccupiedProvinces:
                changed = TransferOccupied(victor, defeated, true);
                ClearRemainingOccupations(victor, defeated);
                break;
            case BasicPeaceTerms.AllOccupiedProvinces:
                changed = TransferOccupied(victor, defeated, false);
                ClearRemainingOccupations(victor, defeated);
                break;
            case BasicPeaceTerms.LiberateOccupiedRegionAsTributary:
                changed = LiberateRegion(victor, defeated, regionName);
                ClearRemainingOccupations(victor, defeated);
                break;
            default: return false;
        }
        DiplomacySystem.SetPeace(victor, defeated);
        Debug.Log(victor.name + " and " + defeated.name + " concluded peace: " + terms + ".");
        RefreshAfterSettlement(victor, defeated);
        return true;
    }

    private static bool ClearMutualOccupations(Nation first, Nation second)
    {
        bool changed = false;
        foreach (Province province in Owners.Instance.provincelist)
            if (province != null && province.IsOccupied &&
                (province.nation == first && province.OccupyingNation == second ||
                 province.nation == second && province.OccupyingNation == first))
            { RestoreControl(province); changed = true; }
        return changed;
    }

    private static bool TransferOccupied(Nation victor, Nation defeated, bool adjacentOnly)
    {
        List<Province> candidates = Owners.Instance.provincelist.FindAll(province => province != null &&
            province.nation == defeated && province.OccupyingNation == victor);
        HashSet<Province> transferable = new HashSet<Province>();
        if (!adjacentOnly) transferable.UnionWith(candidates);
        else
            foreach (Province province in candidates)
                if (province.GrabAdjacents().Exists(adjacent => adjacent != null && adjacent.nation == victor &&
                    !adjacent.IsOccupied)) transferable.Add(province);

        foreach (Province province in transferable)
        {
            Nation recipient = DiplomacySystem.RestorationRecipient(victor, province) ?? victor;
            province.nation = recipient;
            province.OccupyingNation = null;
            province.ReconcileLevyEntitlements();
            province.CreateGarrison();
            province.EnsureMinimumGarrison(1);
        }
        return transferable.Count > 0;
    }

    private static void ClearRemainingOccupations(Nation first, Nation second)
    {
        foreach (Province province in Owners.Instance.provincelist)
            if (province != null && province.IsOccupied &&
                (province.nation == first && province.OccupyingNation == second ||
                 province.nation == second && province.OccupyingNation == first)) RestoreControl(province);
    }

    private static bool LiberateRegion(Nation victor, Nation defeated, string requestedRegion)
    {
        CampaignRegion region = !string.IsNullOrWhiteSpace(requestedRegion)
            ? Owners.Instance.CallRegionByString(requestedRegion) : Owners.Instance.regionlist.Find(candidate =>
                candidate != null && candidate.provincelist.Exists(province => province != null &&
                    province.nation == defeated && province.OccupyingNation == victor));
        if (region == null) return false;

        Nation subject = FindResurrectableNation(region, victor);
        if (subject == null) subject = defeated;
        subject.TributaryMasterName = victor.name;
        subject.PeaceTreatyNationNames?.RemoveAll(name => string.Equals(name, victor.name,
            StringComparison.OrdinalIgnoreCase));
        bool changed = false;
        foreach (Province province in region.provincelist)
        {
            if (province == null || province.OccupyingNation != victor) continue;
            province.nation = subject;
            province.OccupyingNation = null;
            province.ReconcileLevyEntitlements();
            province.CreateGarrison();
            province.EnsureMinimumGarrison(1);
            changed = true;
        }
        return changed;
    }

    private static Nation FindResurrectableNation(CampaignRegion region, Nation victor)
    {
        foreach (Province province in region.provincelist)
        {
            Nation candidate = province != null ? province.OriginalNation : null;
            if (candidate == null || candidate == victor) continue;
            bool alive = Owners.Instance.provincelist.Exists(other => other != null && other.nation == candidate &&
                !other.IsOccupied);
            if (!alive) return candidate;
        }
        return null;
    }

    private static void RestoreControl(Province province)
    {
        province.OccupyingNation = null;
        province.ReconcileLevyEntitlements();
        province.CreateGarrison();
        province.EnsureMinimumGarrison(1);
    }

    private static void RefreshAfterSettlement(Nation first, Nation second)
    {
        first.RefreshManpowerTotal(); second.RefreshManpowerTotal();
        first.nationalbrainy?.ReSetPriorities(); second.nationalbrainy?.ReSetPriorities();
        if (Mapshower.Instance != null) Mapshower.Instance.RePaint();
    }
}
