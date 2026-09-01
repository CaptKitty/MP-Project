using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;

public class CampaignPersistence : MonoBehaviour
{
    private const string SaveFileName = "campaign-network-save.json";
    private float nextSaveTime;
    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    private IEnumerator Start()
    {
        yield return null;
        if (IsAuthority())
        {
            LoadNow();
            nextSaveTime = Time.unscaledTime + 10f;
        }
    }

    private void Update()
    {
        if (IsAuthority() && Time.unscaledTime >= nextSaveTime)
        {
            nextSaveTime = Time.unscaledTime + 10f;
            SaveNow();
        }
    }

    private void OnApplicationQuit()
    {
        if (IsAuthority()) SaveNow();
    }

    private static bool IsAuthority()
    {
        return NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
    }

    public void SaveNow()
    {
        if (Owners.Instance == null) return;
        CampaignSaveData save = CampaignSaveData.Capture();
        File.WriteAllText(SavePath, JsonUtility.ToJson(save, true));
    }

    public void LoadNow()
    {
        if (!File.Exists(SavePath) || Owners.Instance == null || Mapshower.Instance == null) return;
        try
        {
            CampaignSaveData save = JsonUtility.FromJson<CampaignSaveData>(File.ReadAllText(SavePath));
            if (save != null) save.Apply();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}

[Serializable]
public class CampaignSaveData
{
    public List<SavedNation> nations = new List<SavedNation>();
    public List<SavedProvince> provinces = new List<SavedProvince>();
    public List<SavedRegion> regions = new List<SavedRegion>();
    public List<SavedArmy> armies = new List<SavedArmy>();
    public List<SavedActiveBattle> activeBattles = new List<SavedActiveBattle>();
    public List<ProjectX.TileBattle.SavedTileCampaignBattle> tileBattles = new List<ProjectX.TileBattle.SavedTileCampaignBattle>();

    public static CampaignSaveData Capture()
    {
        CampaignSaveData save = new CampaignSaveData();
        foreach (Nation nation in Owners.Instance.nationlist)
        {
            save.nations.Add(new SavedNation
            {
                name = nation.name,
                manpower = nation.Manpower,
                gold = nation.Gold,
                armyNumber = nation.ArmyNumber,
                barracksLevel = nation.faction.BarracksLevel,
                mercenaryLevel = nation.faction.MercLevel,
                farmLevel = nation.faction.FarmLevel,
                income = nation.faction.Income,
                upkeepDebt = nation.UpkeepDebt,
                levyLawPermille = nation.LevyLawPermille,
                laws = nation.laws != null ? new List<NationalLaw>(nation.laws) : new List<NationalLaw>(),
                politicalGroups = nation.politicalGroups != null ? new List<PoliticalGroup>(nation.politicalGroups) : new List<PoliticalGroup>(),
                allegiances = nation.allegiances != null ? nation.allegiances.ConvertAll(item => item != null ? item.Clone() : null) : new List<Allegiance>(),
                politicalProposals = nation.politicalProposals != null ? new List<PoliticalProposal>(nation.politicalProposals) : new List<PoliticalProposal>(),
                activeEdicts = nation.activeEdicts != null ? new List<ActiveNationalEdict>(nation.activeEdicts) : new List<ActiveNationalEdict>(),
                latestPassedEdict = nation.latestPassedEdict,
                levyRecoveryBoostTicks = nation.levyRecoveryBoostTicks,
                levyRecoveryBonusPerTick = nation.levyRecoveryBonusPerTick,
                flags = new List<string>(nation.faction.Flaglist)
            });
        }
        foreach (Province province in Owners.Instance.provincelist)
        {
            SavedProvince savedProvince = new SavedProvince
            {
                name = province.name,
                nation = province.nation.name,
                population = province.population,
                supply = province.supply,
                urbanization = Mathf.Clamp(province.urbanization, -100, province.MaximumDevelopment),
                unrest = province.unrest,
                terrainProfile = (int)province.terrainProfile
            };
            if (province.cultures != null)
                foreach (Culture culture in province.cultures)
                    if (culture != null) savedProvince.cultures.Add(new SavedCulture
                    {
                        name = culture.name, population = culture.population, color = culture.ownerIdentity
                    });
            foreach (ProvinceBuilding building in province.buildings)
            {
                if (building == null) continue;
                savedProvince.buildings.Add(new SavedBuilding
                {
                    id = building.BuildingId, level = building.level, maxLevel = building.EffectiveMaximumLevel, slotIndex = building.slotIndex
                });
            }
            if (province.constructionOrders != null)
                foreach (ProvinceConstructionOrder order in province.constructionOrders)
                    if (order != null) savedProvince.construction.Add(new SavedConstructionOrder
                    {
                        slotIndex = order.slotIndex, buildingId = order.buildingId,
                        targetLevel = order.targetLevel, remainingTicks = order.remainingTicks,
                        initiatedByAI = order.initiatedByAI
                    });
            foreach (ProvinceMercenaryPool pool in province.mercenaryPools)
            {
                if (pool == null || pool.unit == null) continue;
                savedProvince.mercenaries.Add(new SavedMercenaryPool
                {
                    unitName = pool.unit.name, available = pool.available, capacity = pool.capacity,
                    regenerationPerTurn = pool.regenerationPerTurn,
                    regenerationProgress = pool.regenerationProgress
                });
            }
            foreach (ProvinceLevyEntitlement levy in province.levyEntitlements)
                if (levy != null) savedProvince.levies.Add(new SavedLevyEntitlement { id = levy.id, ruleId = levy.ruleId,
                    unitName = levy.unitName, buildingSlot = levy.buildingSlot, ordinal = levy.ordinal,
                    holdingId = levy.holdingId, holdingInstanceId = levy.holdingInstanceId,
                    beneficiaryNation = levy.beneficiaryNation, state = (int)levy.state, eligible = levy.eligible,
                    remainingTicks = levy.remainingTicks, raisedArmyId = levy.raisedArmyId });
            if (province.holdings != null) foreach (ProvinceHolding holding in province.holdings)
                if (holding != null) savedProvince.holdings.Add(new SavedHolding { instanceId = holding.instanceId,
                    id = holding.HoldingId,
                    level = holding.level, slotIndex = holding.slotIndex, cultureName = holding.cultureName,
                    socioEconomicClass = (int)SocioEconomicClassRules.Normalize(holding.socioEconomicClass), allegiance = holding.allegiance,
                    levyEnabled = holding.levyEnabled, adaptationTargetId = holding.adaptationTargetId,
                    adaptationPressure = holding.adaptationPressure, adaptationCooldownTicks = holding.adaptationCooldownTicks });
            if (province.holdingConstructionOrders != null)
                foreach (HoldingConstructionOrder order in province.holdingConstructionOrders)
                    if (order != null) savedProvince.holdingConstruction.Add(new SavedHoldingConstructionOrder {
                        slotIndex = order.slotIndex, holdingInstanceId = order.holdingInstanceId,
                        holdingId = order.holdingId, targetLevel = order.targetLevel,
                        remainingTicks = order.remainingTicks });
            save.provinces.Add(savedProvince);
        }
        foreach (CampaignRegion region in Owners.Instance.regionlist)
            if (region != null)
            {
                SavedRegion savedRegion = new SavedRegion { name = region.name, loyalty = region.loyalty };
                if (region.loyaltyShares != null) foreach (RegionalLoyaltyShare share in region.loyaltyShares)
                    if (share != null) savedRegion.shares.Add(new SavedRegionalLoyaltyShare
                        { nationName = share.nationName, loyalty = share.loyalty, foodStorage = share.foodStorage,
                            foodStorageCapacity = share.foodStorageCapacity, lastFoodShortage = share.lastFoodShortage });
                save.regions.Add(savedRegion);
            }
        foreach (FieldArmyHolder army in Owners.Instance.armylist)
        {
            if (army == null || army.fieldArmy == null || army.fieldArmy.nation == null) continue;
            SavedArmy saved = new SavedArmy
            {
                id = army.NetworkArmyId,
                displayName = army.gameObject.name,
                nation = army.fieldArmy.nation.name,
                humanControlled = army.IsHumanControlled,
                position = army.transform.position,
                target = army.target,
                supply = army.fieldArmy.ArmySupply,
                maxSize = army.fieldArmy.MaxArmySize,
                flags = new List<string>(army.flaglist),
                deployment = army.fieldArmy.battleDeployment
            };
            foreach (ArmyReserves reserve in army.fieldArmy.USDReserves)
            {
                if (reserve != null && reserve.USD != null && reserve.amount > 0)
                {
                    saved.units.Add(new SavedUnit { name = reserve.USD.name, amount = reserve.amount });
                }
            }
            army.fieldArmy.ReconcileFormationRecords();
            foreach (ArmyFormationRecord record in army.fieldArmy.formationRecords)
                if (record != null && record.unit != null) saved.formations.Add(new SavedFormationRecord
                { unitName = record.unit.name, origin = (int)record.origin, entitlementId = record.entitlementId });
            if (army.fieldArmy.recruitmentOrders != null)
                foreach (ArmyRecruitmentOrder order in army.fieldArmy.recruitmentOrders)
                    if (order != null && order.unit != null) saved.recruitment.Add(new SavedRecruitmentOrder
                    {
                        unitName = order.unit.name, amount = order.amount, remainingTicks = order.remainingTicks,
                        origin = (int)order.origin
                    });
            save.armies.Add(saved);
        }
        if (DeterministicBattleManager.Instance != null)
            save.activeBattles = DeterministicBattleManager.Instance.CaptureActiveBattles();
        if (ProjectX.TileBattle.TileBattleCampaignManager.Instance != null)
            save.tileBattles = ProjectX.TileBattle.TileBattleCampaignManager.Instance.CaptureActiveBattles();
        return save;
    }

    public void Apply()
    {
        foreach (SavedNation state in nations)
        {
            Nation nation = Owners.Instance.nationlist.Find(item => item.name == state.name);
            if (nation == null) continue;
            nation.Manpower = state.manpower;
            nation.Gold = state.gold >= 0 ? state.gold : CampaignEconomy.StartingGold;
            nation.ArmyNumber = state.armyNumber;
            nation.faction.BarracksLevel = state.barracksLevel;
            nation.faction.MercLevel = state.mercenaryLevel;
            nation.faction.FarmLevel = state.farmLevel;
            nation.faction.Income = state.income;
            nation.UpkeepDebt = Mathf.Max(0, state.upkeepDebt);
            if (state.levyLawPermille >= 0) nation.LevyLawPermille = Mathf.Clamp(state.levyLawPermille, 0, 1000);
            nation.laws = state.laws != null ? new List<NationalLaw>(state.laws) : new List<NationalLaw>();
            nation.politicalGroups = state.politicalGroups != null ? new List<PoliticalGroup>(state.politicalGroups) : new List<PoliticalGroup>();
            nation.allegiances = state.allegiances != null
                ? state.allegiances.ConvertAll(item => item != null ? item.Clone() : null) : new List<Allegiance>();
            AllegianceSystem.EnsureNationAllegiances(nation);
            nation.politicalProposals = state.politicalProposals != null ? new List<PoliticalProposal>(state.politicalProposals) : new List<PoliticalProposal>();
            nation.activeEdicts = state.activeEdicts != null ? new List<ActiveNationalEdict>(state.activeEdicts) : new List<ActiveNationalEdict>();
            nation.latestPassedEdict = state.latestPassedEdict;
            nation.levyRecoveryBoostTicks = Mathf.Max(0, state.levyRecoveryBoostTicks);
            nation.levyRecoveryBonusPerTick = Mathf.Max(0, state.levyRecoveryBonusPerTick);
            nation.ResetLawResolution();
            nation.EnsureDefaultLaws();
            nation.faction.Flaglist = new List<string>(state.flags ?? new List<string>());
            nation.faction.Set();
        }
        foreach (SavedProvince state in provinces)
        {
            Province province = Owners.Instance.provincelist.Find(item => item.name == state.name);
            Nation nation = Owners.Instance.nationlist.Find(item => item.name == state.nation);
            if (province == null || nation == null) continue;
            province.nation = nation;
            province.supply = state.supply;
            province.urbanization = Mathf.Clamp(state.urbanization, -100, province.MaximumDevelopment);
            province.unrest = state.unrest;
            if (state.cultures != null && state.cultures.Count > 0)
            {
                province.cultures = new List<Culture>();
                foreach (SavedCulture culture in state.cultures)
                    province.cultures.Add(new Culture
                    {
                        name = culture.name, population = culture.population, ownerIdentity = culture.color
                    });
                // Legacy culture values seed old saves only; Holdings become authoritative below.
            }
            else province.EnsureCulture();
            province.terrainProfile = state.terrainProfile >= (int)CampaignTerrainProfile.Auto &&
                state.terrainProfile <= (int)CampaignTerrainProfile.Coastal
                ? (CampaignTerrainProfile)state.terrainProfile : CampaignTerrainProfile.Auto;
            if (state.buildings != null && state.buildings.Count > 0)
            {
                province.buildings.Clear();
                foreach (SavedBuilding building in state.buildings)
                {
                    province.buildings.Add(new ProvinceBuilding
                    {
                        definition = BuildingDefinition.Find(building.id),
                        id = building.id, level = building.level,
                        maxLevel = Mathf.Max(building.maxLevel, ProvinceBuilding.MaximumLevelFor(building.id)),
                        slotIndex = building.slotIndex >= 0 ? building.slotIndex : province.buildings.Count
                    });
                }
                province.RefreshGarrisonForFort();
            }
            if (province.constructionOrders == null) province.constructionOrders = new List<ProvinceConstructionOrder>();
            province.constructionOrders.Clear();
            if (state.construction != null)
                foreach (SavedConstructionOrder order in state.construction)
                    province.constructionOrders.Add(new ProvinceConstructionOrder
                    {
                        slotIndex = order.slotIndex, buildingId = order.buildingId,
                        targetLevel = order.targetLevel, remainingTicks = order.remainingTicks,
                        initiatedByAI = order.initiatedByAI
                    });
            if (state.mercenaries != null && state.mercenaries.Count > 0)
            {
                province.mercenaryPools.Clear();
                Nation localNation = province.OriginalNation != null ? province.OriginalNation : province.nation;
                foreach (SavedMercenaryPool pool in state.mercenaries)
                {
                    UnitSaveData unit = FindSavedUnit(localNation, pool.unitName);
                    if (unit == null) continue;
                    province.mercenaryPools.Add(new ProvinceMercenaryPool
                    {
                        unit = unit, available = pool.available, capacity = pool.capacity,
                        regenerationPerTurn = pool.regenerationPerTurn,
                        regenerationProgress = pool.regenerationProgress
                    });
                }
            }
            province.holdings = new List<ProvinceHolding>();
            if (state.holdings != null) foreach (SavedHolding holding in state.holdings)
            {
                HoldingDefinition definition = HoldingDefinition.Find(holding.id);
                province.holdings.Add(new ProvinceHolding { instanceId = holding.instanceId,
                    definition = definition, id = holding.id,
                    level = Mathf.Max(1, holding.level), slotIndex = holding.slotIndex,
                    cultureName = holding.cultureName,
                    socioEconomicClass = SocioEconomicClassRules.Normalize(
                        (SocioEconomicClass)Mathf.Clamp(holding.socioEconomicClass, 0, 8)),
                    allegiance = holding.allegiance,
                    levyEnabled = holding.levyEnabled, adaptationTargetId = holding.adaptationTargetId,
                    adaptationPressure = Mathf.Max(0, holding.adaptationPressure),
                    adaptationCooldownTicks = Mathf.Max(0, holding.adaptationCooldownTicks) });
            }
            province.holdingConstructionOrders = new List<HoldingConstructionOrder>();
            if (state.holdingConstruction != null)
                foreach (SavedHoldingConstructionOrder order in state.holdingConstruction)
                    province.holdingConstructionOrders.Add(new HoldingConstructionOrder { slotIndex = order.slotIndex,
                        holdingInstanceId = order.holdingInstanceId,
                        holdingId = order.holdingId, targetLevel = order.targetLevel, remainingTicks = order.remainingTicks });
            province.levyEntitlements.Clear();
            if (state.levies != null) foreach (SavedLevyEntitlement levy in state.levies)
            {
                UnitSaveData unit = FindSavedUnit(nation, levy.unitName);
                province.levyEntitlements.Add(new ProvinceLevyEntitlement { id = levy.id, ruleId = levy.ruleId,
                    unitName = levy.unitName, unit = unit, buildingSlot = levy.buildingSlot, ordinal = levy.ordinal,
                    holdingId = levy.holdingId, holdingInstanceId = levy.holdingInstanceId,
                    beneficiaryNation = levy.beneficiaryNation, state = (LevyEntitlementState)Mathf.Clamp(levy.state, 0, 3),
                    eligible = levy.eligible, remainingTicks = levy.remainingTicks, raisedArmyId = levy.raisedArmyId });
            }
            province.InitializeHoldings();
            province.ClampDevelopment();
        }
        if (regions != null)
            foreach (SavedRegion state in regions)
            {
                CampaignRegion region = Owners.Instance.CallRegionByString(state.name);
                if (region != null)
                {
                    region.loyalty = state.loyalty;
                    if (state.shares != null) foreach (SavedRegionalLoyaltyShare share in state.shares)
                    {
                        Nation nation = Owners.Instance.nationlist.Find(item => item != null && item.name == share.nationName);
                        if (nation != null)
                        {
                            region.SetLoyalty(nation, share.loyalty);
                            RegionalLoyaltyShare loadedShare = region.GetLoyaltyShare(nation, true);
                            loadedShare.foodStorageCapacity = share.foodStorageCapacity > 0 ? share.foodStorageCapacity : 1000;
                            loadedShare.foodStorage = share.foodStorage >= 0
                                ? Mathf.Clamp(share.foodStorage, 0, loadedShare.foodStorageCapacity) : loadedShare.foodStorageCapacity;
                            loadedShare.lastFoodShortage = Mathf.Max(0, share.lastFoodShortage);
                        }
                    }
                    if (state.shares == null || state.shares.Count == 0)
                        foreach (Province province in region.provincelist)
                            if (province != null && province.nation != null) region.SetLoyalty(province.nation, state.loyalty);
                }
            }
        foreach (SavedArmy state in armies)
        {
            Nation nation = Owners.Instance.nationlist.Find(item => item.name == state.nation);
            if (nation == null) continue;
            FieldArmyHolder army = Owners.Instance.armylist.Find(item => item != null && item.NetworkArmyId == state.id);
            if (army == null)
            {
                Province start = Owners.Instance.provincelist.Find(item => item.nation == nation);
                if (start == null) continue;
                army = Mapshower.Instance.SpawnArmy(start, state.displayName);
            }
            army.ConfigureNetworkIdentity(state.id, ulong.MaxValue, state.humanControlled, nation);
            army.PreserveConfiguredRoster = true;
            army.transform.position = state.position;
            army.target = state.target;
            army.fieldArmy.ArmySupply = state.supply;
            army.fieldArmy.MaxArmySize = state.maxSize;
            army.fieldArmy.battleDeployment = state.deployment ?? new SavedBattleDeployment();
            army.flaglist = new List<string>(state.flags ?? new List<string>());
            army.fieldArmy.USDReserves.Clear();
            army.fieldArmy.formationRecords.Clear();
            foreach (SavedUnit savedUnit in state.units)
            {
                UnitSaveData unit = FindSavedUnit(nation, savedUnit.name);
                if (unit != null) army.fieldArmy.AddTroop(unit, savedUnit.amount, true);
            }
            if (state.formations != null && state.formations.Count > 0)
            {
                army.fieldArmy.formationRecords.Clear();
                foreach (SavedFormationRecord formation in state.formations)
                {
                    UnitSaveData unit = FindSavedUnit(nation, formation.unitName);
                    if (unit != null) army.fieldArmy.formationRecords.Add(new ArmyFormationRecord { unit = unit,
                        origin = (CampaignUnitOrigin)Mathf.Clamp(formation.origin, 0, 3), entitlementId = formation.entitlementId });
                }
            }
            if (army.fieldArmy.recruitmentOrders == null) army.fieldArmy.recruitmentOrders = new List<ArmyRecruitmentOrder>();
            army.fieldArmy.recruitmentOrders.Clear();
            if (state.recruitment != null)
                foreach (SavedRecruitmentOrder order in state.recruitment)
                {
                    UnitSaveData unit = FindSavedUnit(nation, order.unitName);
                    if (unit != null) army.fieldArmy.recruitmentOrders.Add(new ArmyRecruitmentOrder
                    {
                        unit = unit, amount = order.amount, remainingTicks = order.remainingTicks,
                        origin = (CampaignUnitOrigin)Mathf.Clamp(order.origin, 0, 3)
                    });
                }
        }
        Mapshower.Instance.RePaint();
        if (DeterministicBattleManager.Instance != null)
            DeterministicBattleManager.Instance.RestoreActiveBattles(activeBattles);
        if (ProjectX.TileBattle.TileBattleCampaignManager.Instance != null)
            ProjectX.TileBattle.TileBattleCampaignManager.Instance.RestoreActiveBattles(tileBattles);
    }

    private static UnitSaveData FindSavedUnit(Nation nation, string unitName)
    {
        if (nation != null && nation.faction != null)
        {
            UnitSaveData unit = NationContentResolver.ResolveUnits(nation)
                .ConvertAll(entry => entry != null ? entry.unit : null)
                .Find(item => item != null && item.name == unitName);
            if (unit != null) return unit;
            unit = nation.faction.BarracksDataList.Find(item => item != null && item.name == unitName);
            if (unit != null) return unit;
            unit = nation.faction.MercenaryDataList.Find(item => item != null && item.name == unitName);
            if (unit != null) return unit;
        }
        return Array.Find(Resources.LoadAll<UnitSaveData>("Prefabs/Units"), item => item != null && item.name == unitName);
    }
}

[Serializable] public class SavedNation { public string name; public int manpower; public int gold = -1; public int armyNumber; public int barracksLevel; public int mercenaryLevel; public int farmLevel; public int income; public int upkeepDebt; public int levyLawPermille = -1; public List<NationalLaw> laws = new List<NationalLaw>(); public List<PoliticalGroup> politicalGroups = new List<PoliticalGroup>(); public List<Allegiance> allegiances = new List<Allegiance>(); public List<PoliticalProposal> politicalProposals = new List<PoliticalProposal>(); public List<ActiveNationalEdict> activeEdicts = new List<ActiveNationalEdict>(); public string latestPassedEdict; public int levyRecoveryBoostTicks; public int levyRecoveryBonusPerTick; public List<string> flags = new List<string>(); }
[Serializable] public class SavedProvince { public string name; public string nation; public int population; public int supply; public int urbanization; public int unrest; public int terrainProfile; public List<SavedCulture> cultures = new List<SavedCulture>(); public List<SavedBuilding> buildings = new List<SavedBuilding>(); public List<SavedConstructionOrder> construction = new List<SavedConstructionOrder>(); public List<SavedMercenaryPool> mercenaries = new List<SavedMercenaryPool>(); public List<SavedLevyEntitlement> levies = new List<SavedLevyEntitlement>(); public List<SavedHolding> holdings = new List<SavedHolding>(); public List<SavedHoldingConstructionOrder> holdingConstruction = new List<SavedHoldingConstructionOrder>(); }
[Serializable] public class SavedCulture { public string name; public int population; public Color32 color; }
[Serializable] public class SavedRegion { public string name; public float loyalty = 100f; public List<SavedRegionalLoyaltyShare> shares = new List<SavedRegionalLoyaltyShare>(); }
[Serializable] public class SavedRegionalLoyaltyShare { public string nationName; public float loyalty; public int foodStorage = -1; public int foodStorageCapacity = 1000; public int lastFoodShortage; }
[Serializable] public class SavedBuilding { public string id; public int level; public int maxLevel; public int slotIndex = -1; }
[Serializable] public class SavedConstructionOrder { public int slotIndex; public string buildingId; public int targetLevel; public int remainingTicks; public bool initiatedByAI; }
[Serializable] public class SavedMercenaryPool { public string unitName; public int available; public int capacity; public float regenerationPerTurn; public float regenerationProgress; }
[Serializable] public class SavedArmy { public string id; public string displayName; public string nation; public bool humanControlled; public Vector3 position; public Vector3 target; public int supply; public int maxSize; public List<string> flags = new List<string>(); public List<SavedUnit> units = new List<SavedUnit>(); public List<SavedFormationRecord> formations = new List<SavedFormationRecord>(); public List<SavedRecruitmentOrder> recruitment = new List<SavedRecruitmentOrder>(); public SavedBattleDeployment deployment = new SavedBattleDeployment(); }
[Serializable] public class SavedUnit { public string name; public int amount; }
[Serializable] public class SavedFormationRecord { public string unitName; public int origin; public string entitlementId; }
[Serializable] public class SavedLevyEntitlement { public string id; public string ruleId; public string unitName; public int buildingSlot; public string holdingId; public string holdingInstanceId; public int ordinal; public string beneficiaryNation; public int state; public bool eligible; public int remainingTicks; public string raisedArmyId; }
[Serializable] public class SavedHolding { public string instanceId; public string id; public int level; public int slotIndex; public string cultureName; public int socioEconomicClass; public string allegiance; public bool levyEnabled = true; public string adaptationTargetId; public int adaptationPressure; public int adaptationCooldownTicks; }
[Serializable] public class SavedHoldingConstructionOrder { public int slotIndex; public string holdingInstanceId; public string holdingId; public int targetLevel; public int remainingTicks; }
[Serializable] public class SavedRecruitmentOrder { public string unitName; public int amount; public int remainingTicks; public int origin; }
