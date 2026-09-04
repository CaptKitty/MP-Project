using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "FieldArmy/Basic")]
public class FieldArmy : ScriptableObject
{
    public Faction faction;
    public Nation nation;
    public List<ArmyReserves> USDReserves = new List<ArmyReserves>();
    [Tooltip("Per-formation campaign provenance. Kept alongside USDReserves for compatibility with existing combat/UI code.")]
    public List<ArmyFormationRecord> formationRecords = new List<ArmyFormationRecord>();
    public int ArmySupply;
    public int MaxArmySize = 20;
    public List<ArmyRecruitmentOrder> recruitmentOrders = new List<ArmyRecruitmentOrder>();
    [Header("Deterministic Battle Deployment")]
    public SavedBattleDeployment battleDeployment = new SavedBattleDeployment();
    public void RemoveRandomUnit()
    {
        List<ArmyReserves> templist = new List<ArmyReserves>();
        foreach (ArmyReserves item in USDReserves)
        {
            if (item.amount > 0)
            {
                templist.Add(item);
            }
        }
        if (templist.Count > 0)
        {
            templist[Random.Range(0, templist.Count)].amount -= 1;
        }
    }
    public void AddTroop(UnitSaveData unittoAdd = null, string name = "", int amount = 1)
    {
        //Debug.LogError("Trying to add " + amount + " of " + name + unittoAdd);
        if (name != "")
        {
            try
            {
                var a = USDReserves.Find(x => x.name == name).USD;
                AddTroop(a, amount);
            }
            catch
            {
                try
                {
                    var b = Resources.Load<UnitSaveData>("Prefabs/Units/NormieData/" + name);
                    var c = Instantiate(b);
                    c.name = b.name;
                    AddTroop(c, amount);
                }
                catch
                {
                    Debug.LogError("Could not find " + name + " Unit in database");
                }

            }

        }
        else
        {
            if (unittoAdd == null)
            {
                List<NationUnitEntry> roster = NationContentResolver.ResolveUnits(nation);
                if (roster.Count > 0) AddTroop(roster[Random.Range(0, roster.Count)].unit, amount);
            }
            else
            {

                AddTroop(unittoAdd, amount);
            }
        }
    }
    public void AddTroop(UnitSaveData UnitToAdd, int amount = 1, bool ForceRecruit = false)
    {
        AddTroop(UnitToAdd, amount, ForceRecruit, UnitToAdd != null && UnitToAdd.Mercenary ? CampaignUnitOrigin.Mercenary : CampaignUnitOrigin.Professional, null);
    }
    public void AddTroop(UnitSaveData UnitToAdd, int amount, bool ForceRecruit, CampaignUnitOrigin origin, string entitlementId,
        string sourceNationName = null)
    {
        if (UnitToAdd == null || amount == 0) return;
        if (amount > 0 && GrabArmySize() > MaxArmySize && ForceRecruit == false)
        {
            return;
        }
        foreach (ArmyReserves item in USDReserves)
        {
            try
            {
                if (item.USD.name == UnitToAdd.name)
                {
                    item.amount += amount;
                    if (item.amount < 0)
                    {
                        item.amount = 0;
                    }
                    SyncFormationRecords(UnitToAdd, amount, origin, entitlementId, sourceNationName);
                    return;
                }
            }
            catch
            {
                Debug.LogError(UnitToAdd.name);
                Debug.LogError(item.USD.name);
            }

        }
        //Debug.LogError("We don't have " + UnitToAdd.name + " yet.");
        ArmyReserves UR = new ArmyReserves();
        UR.name = UnitToAdd.name;
        UR.USD = UnitToAdd;
        UR.amount = amount;
        USDReserves.Add(UR);
        SyncFormationRecords(UnitToAdd, amount, origin, entitlementId, sourceNationName);
    }
    private void SyncFormationRecords(UnitSaveData unit, int delta, CampaignUnitOrigin origin, string entitlementId,
        string sourceNationName = null)
    {
        if (formationRecords == null) formationRecords = new List<ArmyFormationRecord>();
        if (delta > 0)
            for (int i = 0; i < delta; i++) formationRecords.Add(new ArmyFormationRecord { unit = unit, origin = origin,
                entitlementId = entitlementId, sourceNationName = sourceNationName });
        else
            for (int i = 0; i < -delta; i++)
            {
                int index = formationRecords.FindLastIndex(record => record != null && record.unit != null && record.unit.name == unit.name);
                if (index >= 0) formationRecords.RemoveAt(index);
            }
    }
    public void ReconcileFormationRecords()
    {
        if (formationRecords == null) formationRecords = new List<ArmyFormationRecord>();
        formationRecords.RemoveAll(record => record == null || record.unit == null);
        formationRecords.RemoveAll(record => !USDReserves.Exists(reserve => reserve != null && reserve.USD != null &&
            reserve.amount > 0 && reserve.USD.name == record.unit.name));
        foreach (ArmyReserves reserve in USDReserves)
        {
            if (reserve == null || reserve.USD == null) continue;
            int count = formationRecords.FindAll(record => record.unit != null && record.unit.name == reserve.USD.name).Count;
            for (int i = count; i < reserve.amount; i++) formationRecords.Add(new ArmyFormationRecord { unit = reserve.USD,
                origin = reserve.USD.Mercenary ? CampaignUnitOrigin.Mercenary : CampaignUnitOrigin.Professional });
            while (count-- > reserve.amount)
            {
                int index = formationRecords.FindLastIndex(record => record.unit != null && record.unit.name == reserve.USD.name);
                if (index >= 0) formationRecords.RemoveAt(index);
            }
        }
    }
    public int GetUpkeep()
    {
        ReconcileFormationRecords(); int total = 0;
        foreach (ArmyFormationRecord record in formationRecords)
            if (record != null && record.unit != null)
            {
                if (record.origin == CampaignUnitOrigin.Professional)
                    total += CampaignEconomy.UnitUpkeep(record.unit);
                else if (record.origin == CampaignUnitOrigin.Mercenary)
                    total += CampaignEconomy.MercenaryUnitUpkeep(record.unit);
            }
        return total;
    }
    public bool DemobilizeLevy(string entitlementId)
    {
        if (string.IsNullOrEmpty(entitlementId)) return false;
        ReconcileFormationRecords();
        int recordIndex = formationRecords.FindIndex(record => record != null && record.origin == CampaignUnitOrigin.Levy &&
            record.entitlementId == entitlementId && record.unit != null);
        if (recordIndex < 0) return false;
        UnitSaveData unit = formationRecords[recordIndex].unit;
        formationRecords.RemoveAt(recordIndex);
        ArmyReserves reserve = USDReserves.Find(item => item != null && item.USD != null && item.USD.name == unit.name);
        if (reserve != null) reserve.amount = Mathf.Max(0, reserve.amount - 1);
        if (Owners.Instance != null)
        foreach (Province province in Owners.Instance.provincelist)
        {
            ProvinceLevyEntitlement entitlement = province != null && province.levyEntitlements != null
                ? province.levyEntitlements.Find(item => item != null && item.id == entitlementId) : null;
            if (entitlement == null) continue;
            entitlement.state = LevyEntitlementState.Recovering; entitlement.raisedArmyId = null;
            entitlement.remainingTicks = LevyEconomySystem.DefaultDemobilizationTicks;
            if (entitlement.remainingTicks == 0 && entitlement.eligible) entitlement.state = LevyEntitlementState.Available;
            break;
        }
        return true;
    }
    public int CountRaisedLevies()
    {
        ReconcileFormationRecords();
        int count = 0;
        foreach (ArmyFormationRecord record in formationRecords)
            if (record != null && record.origin == CampaignUnitOrigin.Levy && !string.IsNullOrEmpty(record.entitlementId)) count++;
        return count;
    }
    public int CountFormations(CampaignUnitOrigin origin)
    {
        ReconcileFormationRecords();
        int count = 0;
        foreach (ArmyFormationRecord record in formationRecords)
            if (record != null && record.origin == origin && record.unit != null) count++;
        return count;
    }
    public int DemobilizeAllLevies()
    {
        ReconcileFormationRecords();
        List<string> entitlementIds = new List<string>();
        foreach (ArmyFormationRecord record in formationRecords)
            if (record != null && record.origin == CampaignUnitOrigin.Levy && !string.IsNullOrEmpty(record.entitlementId))
                entitlementIds.Add(record.entitlementId);
        int removed = 0;
        foreach (string entitlementId in entitlementIds) if (DemobilizeLevy(entitlementId)) removed++;
        return removed;
    }
    public void UpdateUI()
    {
        if (UIElement.ArmyHost == null) return;
        // The army panel is selection-based. UpdateUI can also be invoked by
        // hovering an unrelated army, so the panel resolves the selected holder
        // rather than blindly presenting this ScriptableObject.
        UIElement.ArmyHost.RefreshArmyPanel(true);
    }
    public int GrabArmySize()
    {
        int a = 0;
        foreach (ArmyReserves item in USDReserves)
        {
            a += item.amount;
        }
        return a;
    }
    public int GrabQueuedArmySize()
    {
        int amount = 0;
        if (recruitmentOrders == null) recruitmentOrders = new List<ArmyRecruitmentOrder>();
        foreach (ArmyRecruitmentOrder order in recruitmentOrders)
            if (order != null) amount += Mathf.Max(0, order.amount);
        return amount + GrabQueuedLevySize();
    }
    public int GrabQueuedLevySize()
    {
        if (Owners.Instance == null) return 0;
        FieldArmyHolder holder = Owners.Instance.armylist.Find(candidate => candidate != null && candidate.fieldArmy == this);
        if (holder == null || string.IsNullOrEmpty(holder.NetworkArmyId)) return 0;
        int amount = 0;
        foreach (Province province in Owners.Instance.provincelist)
        {
            if (province == null || province.levyEntitlements == null) continue;
            foreach (ProvinceLevyEntitlement entitlement in province.levyEntitlements)
                if (entitlement != null && entitlement.state == LevyEntitlementState.Mobilizing &&
                    entitlement.raisedArmyId == holder.NetworkArmyId) amount++;
        }
        return amount;
    }
    public bool QueueRecruitment(UnitSaveData unit, int amount, CampaignUnitOrigin origin = CampaignUnitOrigin.Professional,
        string sourceNationName = null)
    {
        if (unit == null || amount <= 0 || GrabArmySize() + GrabQueuedArmySize() + amount > MaxArmySize) return false;
        if (recruitmentOrders == null) recruitmentOrders = new List<ArmyRecruitmentOrder>();
        recruitmentOrders.Add(new ArmyRecruitmentOrder
        {
            unit = unit,
            amount = amount,
            origin = origin,
            sourceNationName = sourceNationName,
            remainingTicks = RecruitmentTicks(unit, origin)
        });
        return true;
    }
    public void ProcessRecruitmentTick()
    {
        if (recruitmentOrders == null || recruitmentOrders.Count == 0) return;

        // Recruitment is a FIFO queue. Only its first valid order consumes a tick,
        // and batched orders produce their units one at a time.
        while (recruitmentOrders.Count > 0)
        {
            ArmyRecruitmentOrder order = recruitmentOrders[0];
            if (order == null || order.unit == null || order.amount <= 0)
            {
                recruitmentOrders.RemoveAt(0);
                continue;
            }

            order.remainingTicks--;
            if (order.remainingTicks > 0)
            {
                RecruitmentMenu.RefreshQueueFor(this);
                return;
            }

            AddTroop(order.unit, 1, true, order.origin, string.Empty, order.sourceNationName);
            FieldArmyHolder holder = Owners.Instance != null
                ? Owners.Instance.armylist.Find(candidate => candidate != null && candidate.fieldArmy == this)
                : null;
            if (holder != null) CampaignRecruitmentVisual.Present(order.unit, holder, order.sourceNationName);
            order.amount--;
            if (order.amount <= 0)
                recruitmentOrders.RemoveAt(0);
            else
                order.remainingTicks = RecruitmentTicks(order.unit, order.origin);
            RecruitmentMenu.RefreshQueueFor(this);
            return;
        }

        RecruitmentMenu.RefreshQueueFor(this);
    }

    private int RecruitmentTicks(UnitSaveData unit, CampaignUnitOrigin origin)
    {
        int ticks = unit != null ? unit.EffectiveRecruitmentTicks : 1;
        if (nation != null && origin == CampaignUnitOrigin.Mercenary)
            ticks = nation.ApplyLawModifiers(NationalLawEffectType.MercenaryRecruitmentTime, ticks, null, origin);
        return Mathf.Max(1, ticks);
    }
    public void AddSupply(int suppliesToAdd)
    {
        ArmySupply += suppliesToAdd;
        if (ArmySupply < 0)
        {
            ArmySupply = 0;
        }
        if (ArmySupply > nation.faction.FarmLevel * 100)
        {
            ArmySupply = nation.faction.FarmLevel * 100;
        }
        UpdateUI();
    }
}
[System.Serializable]
public class ArmyReserves
{
    public string name;
    public UnitSaveData USD;
    public int amount;
}

[System.Serializable]
public class ArmyRecruitmentOrder
{
    public UnitSaveData unit;
    public int amount = 1;
    public int remainingTicks = 1;
    public CampaignUnitOrigin origin = CampaignUnitOrigin.Professional;
    public string sourceNationName;
}
