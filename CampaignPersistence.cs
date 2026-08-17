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
                unrest = province.unrest,
                terrainProfile = (int)province.terrainProfile
            };
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
                        targetLevel = order.targetLevel, remainingTicks = order.remainingTicks
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
            save.provinces.Add(savedProvince);
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
            if (army.fieldArmy.recruitmentOrders != null)
                foreach (ArmyRecruitmentOrder order in army.fieldArmy.recruitmentOrders)
                    if (order != null && order.unit != null) saved.recruitment.Add(new SavedRecruitmentOrder
                    {
                        unitName = order.unit.name, amount = order.amount, remainingTicks = order.remainingTicks
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
            nation.faction.Flaglist = new List<string>(state.flags ?? new List<string>());
            nation.faction.Set();
        }
        foreach (SavedProvince state in provinces)
        {
            Province province = Owners.Instance.provincelist.Find(item => item.name == state.name);
            Nation nation = Owners.Instance.nationlist.Find(item => item.name == state.nation);
            if (province == null || nation == null) continue;
            province.nation = nation;
            province.population = state.population;
            province.supply = state.supply;
            province.unrest = state.unrest;
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
                        targetLevel = order.targetLevel, remainingTicks = order.remainingTicks
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
            foreach (SavedUnit savedUnit in state.units)
            {
                UnitSaveData unit = FindSavedUnit(nation, savedUnit.name);
                if (unit != null) army.fieldArmy.AddTroop(unit, savedUnit.amount, true);
            }
            if (army.fieldArmy.recruitmentOrders == null) army.fieldArmy.recruitmentOrders = new List<ArmyRecruitmentOrder>();
            army.fieldArmy.recruitmentOrders.Clear();
            if (state.recruitment != null)
                foreach (SavedRecruitmentOrder order in state.recruitment)
                {
                    UnitSaveData unit = FindSavedUnit(nation, order.unitName);
                    if (unit != null) army.fieldArmy.recruitmentOrders.Add(new ArmyRecruitmentOrder
                    {
                        unit = unit, amount = order.amount, remainingTicks = order.remainingTicks
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

[Serializable] public class SavedNation { public string name; public int manpower; public int gold = -1; public int armyNumber; public int barracksLevel; public int mercenaryLevel; public int farmLevel; public int income; public List<string> flags = new List<string>(); }
[Serializable] public class SavedProvince { public string name; public string nation; public int population; public int supply; public int unrest; public int terrainProfile; public List<SavedBuilding> buildings = new List<SavedBuilding>(); public List<SavedConstructionOrder> construction = new List<SavedConstructionOrder>(); public List<SavedMercenaryPool> mercenaries = new List<SavedMercenaryPool>(); }
[Serializable] public class SavedBuilding { public string id; public int level; public int maxLevel; public int slotIndex = -1; }
[Serializable] public class SavedConstructionOrder { public int slotIndex; public string buildingId; public int targetLevel; public int remainingTicks; }
[Serializable] public class SavedMercenaryPool { public string unitName; public int available; public int capacity; public float regenerationPerTurn; public float regenerationProgress; }
[Serializable] public class SavedArmy { public string id; public string displayName; public string nation; public bool humanControlled; public Vector3 position; public Vector3 target; public int supply; public int maxSize; public List<string> flags = new List<string>(); public List<SavedUnit> units = new List<SavedUnit>(); public List<SavedRecruitmentOrder> recruitment = new List<SavedRecruitmentOrder>(); public SavedBattleDeployment deployment = new SavedBattleDeployment(); }
[Serializable] public class SavedUnit { public string name; public int amount; }
[Serializable] public class SavedRecruitmentOrder { public string unitName; public int amount; public int remainingTicks; }
