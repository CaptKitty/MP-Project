using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class Owners : MonoBehaviour
{
    public static Owners Instance;
    public List<Nation> nationlist;
    public Dictionary<string, Nation> nationdict;
    public List<State> statelist;
    public List<CampaignRegion> regionlist = new List<CampaignRegion>();
    public Dictionary<string, CampaignRegion> regiondict;
    public List<Culture> culturelist;
    public Dictionary<string, Culture> culturedict;
    public List<Province> provincelist;
    public GameObject CityObject;
    public Dictionary<string, Province> provincedict;
    public Dictionary<Color32, Province> provincedictcolor;
    public List<FieldArmyHolder> armylist = new List<FieldArmyHolder>();
    public double timer;
    public int turncounter;
    [Range(0f, 10f)] public float CampaignSimulationSpeed = 0.25f;
    public bool CampaignPaused;
    private float campaignStepAccumulator;
    public int xxx = 25;

    // Start is called before the first frame update
    void Awake()
    {
        Instance = this;
    }
    void Start()
    {
        if (CampaignNetworkPlayer.Local != null && CampaignNetworkPlayer.Local.HasAssignment)
        {
            SessionManager.Instance.ApplyNetworkFaction(CampaignNetworkPlayer.Local.AssignedNation);
        }

        this.transform.GetComponent<LoadProvinces>().LoadinCultures();

        culturedict = new Dictionary<string, Culture>();
        foreach (Culture culture in culturelist)
        {
            culturedict.Add(culture.name, culture);
        }

        this.transform.GetComponent<LoadProvinces>().LoadStuff();
        nationdict = new Dictionary<string, Nation>();
        foreach (Nation nation in nationlist)
        {
            nationdict.Add(nation.name, nation);
            nation.IsPlayer = false;
            nation.faction = nation.faction.Init();
            //Debug.LogError(nation.name);
            nation.faction.Set();
            nation.nationalbrainy = ScriptableObject.CreateInstance<NationalBrain>();
            nation.nationalbrainy.nation = nation.name;
            nation.nationalbrainy.name = nation.name + "_brain";
            

            nation.faction.color = nation.ownerIdentity;

            if (SessionManager.Instance.HostFaction.name.Contains(nation.name))
            {
                nation.IsPlayer = true;
                nation.faction = SessionManager.Instance.HostFaction;
            }
        }

        provincedict = new Dictionary<string, Province>();
        provincedictcolor = new Dictionary<Color32, Province>();
        foreach (Province province in provincelist)
        {
            province.CreateGarrison();
            province.SetAdjacents();
            try
            {
                provincedict.Add(province.name, province);
                provincedictcolor.Add(new Color32(province.identity.r, province.identity.g, province.identity.b, 0), province);
            }
            catch
            {
                //Debug.LogError(province.name);
            }
            province.OriginalNation = province.nation;
            province.InitializeRecruitment();
        }
        Mapshower.Instance.Paint();

        if (RecruitmentMenu.Instance == null)
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null) RecruitmentMenu.Create(canvas);
        }

        PlantCities();

        foreach (Nation nation in nationlist)
        {
            nation.nationalbrainy.Startie();
        }

        StartCoroutine(EnsureFactionStarterArmies());

        if (GetComponent<CampaignPersistence>() == null)
        {
            gameObject.AddComponent<CampaignPersistence>();
        }
        if (GetComponent<DeterministicBattleManager>() == null)
        {
            gameObject.AddComponent<DeterministicBattleManager>();
        }
        if (GetComponent<ProjectX.TileBattle.TileBattleCampaignManager>() == null)
        {
            gameObject.AddComponent<ProjectX.TileBattle.TileBattleCampaignManager>();
        }
    }
    public void PlantCities()
    {
        foreach (var province in provincelist)
        {
            var a = Instantiate(CityObject, this.transform.GetChild(0).GetChild(1).GetChild(1));
            a.transform.localScale = new Vector3(25f, 25f, 25f);
            a.transform.position = new Vector3(province.position.x * 1f - 512f, province.position.y * 1f - 331f, 0);
        }
    }
    public Nation CallPlayer()
    {
        foreach (Nation Nation in Owners.Instance.nationlist)
        {
            if (Nation.IsPlayer == true)
            {
                Nation nation = Nation;
                return Nation;
            }
        }
        return new Nation();
    }
    public Nation CallNation(string nationname)
    {
        return nationdict[nationname];
    }
    public Province CallProvinceByString(string provincename)
    {
        return provincedict[provincename];
    }
    public CampaignRegion CallRegionByString(string regionname)
    {
        if (string.IsNullOrWhiteSpace(regionname) || regiondict == null) return null;
        regiondict.TryGetValue(regionname, out CampaignRegion region);
        return region;
    }
    public Province CallProvinceByColor(Color32 provincecolor)
    {
        if (provincecolor.r == 0 && provincecolor.g == 0 & provincecolor.b == 0)
        {
            return null;
        }
        return provincedictcolor[provincecolor];
    }
    public Culture CallCultureByName(string culturename)
    {
        return culturedict[culturename];
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (CampaignPaused) return;
        campaignStepAccumulator += Mathf.Max(0.01f, CampaignSimulationSpeed);
        int steps = 0;
        while (campaignStepAccumulator >= 1f && steps++ < 8)
        {
            campaignStepAccumulator -= 1f;
            RunCampaignStep();
        }
    }

    private IEnumerator EnsureFactionStarterArmies()
    {
        // Allow scene-placed armies to finish Start() and register first.
        yield return null;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
            yield break;

        foreach (Nation nation in nationlist)
        {
            if (nation == null) continue;
            nation.armies.RemoveAll(existingArmy => existingArmy == null);
            if (NationContentResolver.HasFlag(nation, "Braindead"))
            {
                // Braindead nations are passive expansion space and begin without
                // either generated or scene-placed campaign armies.
                foreach (FieldArmyHolder passiveArmy in new List<FieldArmyHolder>(nation.armies))
                    if (passiveArmy != null) Destroy(passiveArmy.gameObject);
                nation.armies.Clear();
                continue;
            }
            if (nation.armies.Count > 0) continue;
            Province start = provincelist.Find(province => province != null && province.nation == nation);
            if (start == null) continue;

            FieldArmyHolder starterArmy = Mapshower.Instance.SpawnArmy(start, "1st Army of " + nation.name);
            if (starterArmy == null) continue;
            starterArmy.PreserveConfiguredRoster = false;
            starterArmy.ConfigureNetworkIdentity("starter_" + nation.name, ulong.MaxValue, false, nation);
        }
    }

    private void RunCampaignStep()
    {
        bool battleRunning = DeterministicBattleManager.Instance != null && DeterministicBattleManager.Instance.ActiveBattles.Count > 0 ||
            ProjectX.TileBattle.TileBattleCampaignManager.Instance != null &&
            ProjectX.TileBattle.TileBattleCampaignManager.Instance.ActiveBattles.Count > 0;
        bool playerMoving = FieldArmyHolder.PlayerFieldArmy != null && !FieldArmyHolder.PlayerFieldArmy.IsTargetNull();
        if (1 == 1)//playerMoving || battleRunning || Input.GetKey("space"))
        {
            foreach (var item in armylist)
            {
                item.Act();
                if (timer % 50 == 0) //50
                {
                    //Recruitment etc
                    item.NextTurn();
                }
            }
            if (timer % 100 == 0)//1000 == 0) //if (timer % 250 == 0)
            {
                //TakeTurns();
            }
            if (timer % 10 == 0)
            {
                TakeTurns();
            }
            timer++;
            foreach(var nation in nationlist)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    nation.IsPlayer = CampaignNetworkPlayer.IsNationPlayerControlled(nation.name);
                if (!nation.IsPlayer) nation.nationalbrainy.Think();
            }
        }
    }
    public void TakeTurns()
    {
        turncounter++;
        foreach (FieldArmyHolder army in armylist)
            if (army != null && army.fieldArmy != null) army.fieldArmy.ProcessRecruitmentTick();
        foreach (var nation in nationlist)
        {
            nation.TakeTurn();
        }
        foreach (var province in provincelist)
        {
            province.RegenerateMercenaries();
            province.ProcessConstructionTick();
            province.ProcessLevyTick();
        }
        if (CampaignNetworkPlayer.Local != null)
            CampaignNetworkPlayer.Local.BroadcastQueueStateNow();
    }
}
public enum CampaignTerrainProfile : byte
{
    Auto, Plains, Forested, Hilly, Mountainous, Marshland, RiverValley, RoughCountry, Coastal
}

[System.Serializable]
public class Province
{
    public string name;
    public Color32 identity;
    public Nation nation;
    public Nation OriginalNation;
    public string state;
    public string region;
    public Vector2 position;
    public CampaignTerrainProfile terrainProfile = CampaignTerrainProfile.Auto;
    public int population = 1000;
    public int supply = 1000;
    public FieldArmy garrison;

    public List<string> AdjacentProvinces = new List<string>();

    public List<Culture> cultures;
    public int taxincome;
    public int taxpercentage;
    public int levyincome;
    public int levypercentage;
    public int unrest;
    public List<ProvinceBuilding> buildings = new List<ProvinceBuilding>();
    public List<ProvinceConstructionOrder> constructionOrders = new List<ProvinceConstructionOrder>();
    public List<ProvinceMercenaryPool> mercenaryPools = new List<ProvinceMercenaryPool>();
    public List<ProvinceLevyEntitlement> levyEntitlements = new List<ProvinceLevyEntitlement>();

    public void InitializeRecruitment()
    {
        if (buildings == null) buildings = new List<ProvinceBuilding>();
        if (constructionOrders == null) constructionOrders = new List<ProvinceConstructionOrder>();
        if (mercenaryPools == null) mercenaryPools = new List<ProvinceMercenaryPool>();
        if (levyEntitlements == null) levyEntitlements = new List<ProvinceLevyEntitlement>();
        Nation recruitmentNation = nation != null ? nation : OriginalNation;
        if (buildings.Count == 0 && NationContentResolver.HasBuilding(recruitmentNation, "Farm"))
        {
            BuildingDefinition farm = BuildingDefinition.Find("Farm");
            buildings.Add(new ProvinceBuilding { definition = farm, id = "Farm", level = 1,
                maxLevel = ProvinceBuilding.StandardMaximumLevel, slotIndex = 0 });
        }
        for (int i = 0; i < buildings.Count; i++)
        {
            if (buildings[i] == null) continue;
            if (buildings[i].slotIndex < 0) buildings[i].slotIndex = i;
            buildings[i].maxLevel = buildings[i].definition != null
                ? buildings[i].definition.maximumLevel
                : Mathf.Max(buildings[i].maxLevel, ProvinceBuilding.MaximumLevelFor(buildings[i].id));
        }
        if (mercenaryPools.Count > 0 || OriginalNation == null || OriginalNation.faction == null) return;

        List<UnitSaveData> candidates = new List<UnitSaveData>();
        candidates.AddRange(OriginalNation.faction.MercenaryDataList);
        foreach (UnitSaveData unit in OriginalNation.faction.UnitDataList)
        {
            if (unit != null && unit.Mercenary && !candidates.Contains(unit)) candidates.Add(unit);
        }
        if (candidates.Count == 0 && OriginalNation.faction.UnitDataList.Count > 0)
        {
            candidates.Add(OriginalNation.faction.UnitDataList[0]);
        }
        foreach (UnitSaveData unit in candidates)
        {
            if (unit == null) continue;
            mercenaryPools.Add(new ProvinceMercenaryPool
            {
                unit = unit,
                available = 2,
                capacity = 3,
                regenerationPerTurn = 0.25f
            });
        }
    }
    public void ReconcileLevyEntitlements()
    {
        if (levyEntitlements == null) levyEntitlements = new List<ProvinceLevyEntitlement>();
        foreach (ProvinceLevyEntitlement entitlement in levyEntitlements) if (entitlement != null) entitlement.eligible = false;
        if (nation == null) return;
        foreach (LevyGrantRule rule in LevySystem.ResolveRules(nation))
        foreach (ProvinceBuilding building in buildings)
        {
            if (!rule.Applies(this, building)) continue;
            for (int ordinal = 0; ordinal < Mathf.Max(1, rule.formationsPerBuilding); ordinal++)
            {
                string entitlementId = name + "|" + building.slotIndex + "|" + rule.StableId + "|" + ordinal;
                ProvinceLevyEntitlement entitlement = levyEntitlements.Find(item => item != null && item.id == entitlementId);
                if (entitlement == null)
                {
                    entitlement = new ProvinceLevyEntitlement { id = entitlementId, ruleId = rule.StableId,
                        unitName = rule.unit.name, unit = rule.unit, buildingSlot = building.slotIndex, ordinal = ordinal,
                        beneficiaryNation = nation.name, state = LevyEntitlementState.Available };
                    levyEntitlements.Add(entitlement);
                }
                entitlement.unit = rule.unit; entitlement.unitName = rule.unit.name; entitlement.eligible = true;
                entitlement.beneficiaryNation = nation.name;
            }
        }
    }

    public void ProcessLevyTick()
    {
        ReconcileLevyEntitlements();
        foreach (ProvinceLevyEntitlement entitlement in levyEntitlements)
        {
            if (entitlement == null || entitlement.remainingTicks <= 0) continue;
            if (entitlement.state == LevyEntitlementState.Mobilizing && !entitlement.eligible)
            { entitlement.state = LevyEntitlementState.Available; entitlement.remainingTicks = 0; entitlement.raisedArmyId = null; continue; }
            if (entitlement.state == LevyEntitlementState.Recovering && !entitlement.eligible) continue;
            entitlement.remainingTicks--;
            if (entitlement.remainingTicks != 0) continue;
            if (entitlement.state == LevyEntitlementState.Recovering)
            { entitlement.state = LevyEntitlementState.Available; entitlement.raisedArmyId = null; }
            else if (entitlement.state == LevyEntitlementState.Mobilizing)
            {
                FieldArmyHolder target = Owners.Instance != null ? Owners.Instance.armylist.Find(army => army != null &&
                    army.NetworkArmyId == entitlement.raisedArmyId && army.fieldArmy != null && army.fieldArmy.nation == nation) : null;
                if (target == null || target.fieldArmy.GrabArmySize() >= target.fieldArmy.MaxArmySize)
                { entitlement.state = LevyEntitlementState.Available; entitlement.raisedArmyId = null; }
                else
                { target.fieldArmy.AddTroop(entitlement.unit, 1, true, CampaignUnitOrigin.Levy, entitlement.id); entitlement.state = LevyEntitlementState.Raised; }
            }
        }
    }

    public List<ProvinceLevyEntitlement> GetAvailableRegionLevies(Nation owner)
    {
        List<ProvinceLevyEntitlement> result = new List<ProvinceLevyEntitlement>();
        foreach (Province province in GetOccupiedRegionProvinces(owner))
        {
            province.ReconcileLevyEntitlements();
            result.AddRange(province.levyEntitlements.FindAll(item => item != null && item.eligible &&
                item.state == LevyEntitlementState.Available && item.unit != null));
        }
        result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        return result;
    }

    public bool RaiseLevy(string entitlementId, FieldArmyHolder army)
    {
        ProvinceLevyEntitlement entitlement = levyEntitlements.Find(item => item != null && item.id == entitlementId);
        if (entitlement == null || army == null || army.fieldArmy == null || !entitlement.eligible ||
            entitlement.state != LevyEntitlementState.Available || army.fieldArmy.nation != nation ||
            army.fieldArmy.GrabArmySize() + army.fieldArmy.GrabQueuedArmySize() >= army.fieldArmy.MaxArmySize) return false;
        LevyGrantRule rule = LevySystem.FindRule(nation, entitlement.ruleId);
        entitlement.raisedArmyId = army.NetworkArmyId;
        entitlement.remainingTicks = rule != null ? Mathf.Max(0, rule.mobilizationTicks) : 0;
        if (entitlement.remainingTicks > 0) entitlement.state = LevyEntitlementState.Mobilizing;
        else
        {
            entitlement.state = LevyEntitlementState.Raised;
            army.fieldArmy.AddTroop(entitlement.unit, 1, true, CampaignUnitOrigin.Levy, entitlement.id);
        }
        return true;
    }

    public int RaiseAllAvailableRegionLevies(FieldArmyHolder army)
    {
        if (army == null || army.fieldArmy == null || army.fieldArmy.nation == null || nation != army.fieldArmy.nation) return 0;
        int capacity = army.fieldArmy.MaxArmySize - army.fieldArmy.GrabArmySize() - army.fieldArmy.GrabQueuedArmySize();
        if (capacity <= 0) return 0;
        List<Province> regionProvinces = GetOccupiedRegionProvinces(army.fieldArmy.nation);
        List<ProvinceLevyEntitlement> available = GetAvailableRegionLevies(army.fieldArmy.nation);
        int raised = 0;
        foreach (ProvinceLevyEntitlement entitlement in available)
        {
            if (raised >= capacity) break;
            Province source = regionProvinces.Find(candidate => candidate.levyEntitlements != null &&
                candidate.levyEntitlements.Contains(entitlement));
            if (source != null && source.RaiseLevy(entitlement.id, army)) raised++;
        }
        return raised;
    }

    public void BeginLevyRecovery(string entitlementId)
    {
        ProvinceLevyEntitlement entitlement = levyEntitlements.Find(item => item != null && item.id == entitlementId);
        if (entitlement == null) return;
        LevyGrantRule rule = LevySystem.FindRule(nation, entitlement.ruleId);
        entitlement.state = LevyEntitlementState.Recovering; entitlement.raisedArmyId = null;
        entitlement.remainingTicks = rule != null ? Mathf.Max(0, rule.recoveryTicks) : 20;
        if (entitlement.remainingTicks == 0 && entitlement.eligible) entitlement.state = LevyEntitlementState.Available;
    }

    public bool CanRecruitLocal(UnitSaveData unit)
    {
        if (nation == null || NationContentResolver.GetUnitTier(nation, unit) <= 0) return false;
        return buildings.Exists(building => building != null && building.Unlocks(unit, nation));
    }

    public List<UnitSaveData> GetRecruitableLocalUnits()
    {
        List<UnitSaveData> result = new List<UnitSaveData>();
        if (nation == null) return result;
        foreach (NationUnitEntry entry in NationContentResolver.ResolveUnits(nation))
        {
            UnitSaveData unit = entry != null ? entry.unit : null;
            if (unit != null && CanRecruitLocal(unit) && !result.Contains(unit)) result.Add(unit);
        }
        return result;
    }

    public bool SharesRegionWith(Province other)
    {
        return other != null && !string.IsNullOrWhiteSpace(region) &&
            string.Equals(region, other.region, System.StringComparison.OrdinalIgnoreCase);
    }

    public List<Province> GetOccupiedRegionProvinces(Nation occupyingNation = null)
    {
        List<Province> result = new List<Province>();
        Nation owner = occupyingNation != null ? occupyingNation : nation;
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        if (campaignRegion != null && campaignRegion.provincelist != null)
        {
            foreach (Province province in campaignRegion.provincelist)
                if (province != null && province.nation == owner) result.Add(province);
        }
        else if (nation == owner) result.Add(this);
        return result;
    }

    public List<UnitSaveData> GetRecruitableRegionUnits(Nation recruitingNation = null)
    {
        List<UnitSaveData> result = new List<UnitSaveData>();
        Nation owner = recruitingNation != null ? recruitingNation : nation;
        foreach (Province province in GetOccupiedRegionProvinces(owner))
            foreach (UnitSaveData unit in province.GetRecruitableLocalUnits())
                if (unit != null && !result.Contains(unit)) result.Add(unit);
        return result;
    }

    public Province FindRegionalRecruitmentSource(UnitSaveData unit, Nation recruitingNation = null)
    {
        Nation owner = recruitingNation != null ? recruitingNation : nation;
        return GetOccupiedRegionProvinces(owner).Find(province => province.CanRecruitLocal(unit));
    }

    public List<ProvinceMercenaryPool> GetAvailableMercenaries()
    {
        if (!ProvinceMercenaryPool.Enabled) return new List<ProvinceMercenaryPool>();
        return mercenaryPools.FindAll(pool => pool != null && pool.unit != null && pool.available > 0);
    }

    public ProvinceMercenaryPool FindMercenary(string unitName)
    {
        if (!ProvinceMercenaryPool.Enabled) return null;
        return mercenaryPools.Find(pool => pool != null && pool.unit != null && pool.unit.name == unitName);
    }

    public void RegenerateMercenaries()
    {
        foreach (ProvinceMercenaryPool pool in mercenaryPools) pool?.Regenerate();
    }

    public ProvinceBuilding GetBuilding(string buildingId)
    {
        return buildings.Find(building => building != null && building.BuildingId.Equals(buildingId, System.StringComparison.OrdinalIgnoreCase));
    }

    public bool UpgradeBuilding(string buildingId)
    {
        ProvinceBuilding building = GetBuilding(buildingId);
        if (building == null)
        {
            buildings.Add(new ProvinceBuilding
            {
                id = buildingId, level = 1, maxLevel = ProvinceBuilding.MaximumLevelFor(buildingId)
            });
            return true;
        }
        if (building.level >= building.maxLevel) return false;
        building.level++;
        return true;
    }

    public int GetGoldIncome()
    {
        ProvinceBuilding farm = GetBuilding("Farm");
        int income = 0;
        foreach (ProvinceBuilding building in buildings)
            if (building != null && building.definition != null) income += building.DefinitionGoldIncome;
        if (farm != null && farm.definition == null)
            income += Mathf.Max(0, farm.level) * CampaignEconomy.FarmIncomePerLevel;
        return CampaignEconomy.ApplyGoldIncomeRate(income);
    }

    public ProvinceBuilding GetBuildingInSlot(int slotIndex)
    {
        return buildings.Find(building => building != null && building.slotIndex == slotIndex);
    }

    public bool BeginBuildingConstruction(int slotIndex, string buildingId, int targetLevel, int constructionTicks,
        bool initiatedByAI = false)
    {
        if (slotIndex < 0 || string.IsNullOrEmpty(buildingId) || constructionOrders.Exists(order => order.slotIndex == slotIndex)) return false;
        if (nation == null || !NationContentResolver.CanConstructBuildingLevel(nation, buildingId, targetLevel)) return false;
        ProvinceBuilding existing = GetBuildingInSlot(slotIndex);
        if (existing != null && !existing.BuildingId.Equals(buildingId, System.StringComparison.OrdinalIgnoreCase)) return false;
        ProvinceConstructionOrder order = new ProvinceConstructionOrder
        {
            slotIndex = slotIndex, buildingId = buildingId,
            targetLevel = Mathf.Max(1, targetLevel), remainingTicks = Mathf.Max(0, constructionTicks),
            initiatedByAI = initiatedByAI
        };
        constructionOrders.Add(order);
        if (order.remainingTicks == 0) CompleteConstruction(order);
        return true;
    }

    public void ProcessConstructionTick()
    {
        if (constructionOrders == null) return;
        for (int i = constructionOrders.Count - 1; i >= 0; i--)
        {
            ProvinceConstructionOrder order = constructionOrders[i];
            order.remainingTicks--;
            if (order.remainingTicks <= 0) CompleteConstruction(order);
        }
    }

    private void CompleteConstruction(ProvinceConstructionOrder order)
    {
        ProvinceBuilding building = GetBuildingInSlot(order.slotIndex);
        if (building == null)
        {
            int maximum = ProvinceBuilding.MaximumLevelFor(order.buildingId);
            buildings.Add(new ProvinceBuilding
            {
                definition = BuildingDefinition.Find(order.buildingId),
                id = order.buildingId, level = order.targetLevel, maxLevel = maximum, slotIndex = order.slotIndex
            });
        }
        else building.level = Mathf.Min(building.maxLevel, Mathf.Max(building.level, order.targetLevel));
        constructionOrders.Remove(order);
        if (building != null && building.DefinitionGarrisonCapacity > 0 ||
            order.buildingId.Equals("Fort", System.StringComparison.OrdinalIgnoreCase) ||
            order.buildingId.Equals("Hillfort", System.StringComparison.OrdinalIgnoreCase))
            RefreshGarrisonForFort();
    }

    public List<Province> GrabAdjacents()
    {
        var a = new List<Province>();
        foreach(var b in AdjacentProvinces)
        {
            a.Add(Owners.Instance.provincedict[b]);
        }
        return a;
    }
    public void SetAdjacents()
    {
        //Debug.LogError("setting adjacents");
        AdjacentProvinces.Clear();
        
        foreach(var b in Owners.Instance.provincelist)
        {
            if(b == this)
            {
                continue;
            }
            if(Vector3.Distance(b.position, position) < 50)
            {
                AdjacentProvinces.Add(b.name);
            }
        }
        // if(AdjacentProvinces.Count < 5)
        // {
        //     foreach(var b in Owners.Instance.provincelist)
        //     {
        //         if(b == this)
        //         {
        //             continue;
        //         }
        //         if(Vector3.Distance(b.position, position) < 70)
        //         {
        //             AdjacentProvinces.Add(b.name);
        //         }
        //     }
        // }
        // if(AdjacentProvinces.Count < 5)
        // {
        //     foreach(var b in Owners.Instance.provincelist)
        //     {
        //         if(b == this)
        //         {
        //             continue;
        //         }
        //         if(Vector3.Distance(b.position, position) < 90)
        //         {
        //             AdjacentProvinces.Add(b.name);
        //         }
        //     }
        // }
    }

    public void CreateGarrison()
    {
        garrison = ScriptableObject.CreateInstance<FieldArmy>();
        garrison.nation = nation;
        garrison.MaxArmySize = GetGarrisonCapacity();
        ReinforceGarrisonToCapacity();
    }

    public int GetGarrisonCapacity()
    {
        const int baseGarrisonSize = 6;
        const int troopsPerFortLevel = 3;
        ProvinceBuilding fort = GetBuilding("Fort");
        int capacity = baseGarrisonSize;
        foreach (ProvinceBuilding building in buildings)
            if (building != null && building.definition != null) capacity += building.DefinitionGarrisonCapacity;
        if (fort != null && fort.definition == null)
            capacity += Mathf.Max(0, fort.level) * troopsPerFortLevel;
        return capacity;
    }

    public void RefreshGarrisonForFort()
    {
        if (garrison == null)
        {
            CreateGarrison();
            return;
        }
        garrison.nation = nation;
        garrison.MaxArmySize = GetGarrisonCapacity();
        ReinforceGarrisonToCapacity();
    }

    private void ReinforceGarrisonToCapacity()
    {
        if (garrison == null || nation == null || nation.faction == null) return;
        List<UnitSaveData> roster = NationContentResolver.ResolveUnits(nation)
            .ConvertAll(entry => entry != null ? entry.unit : null);
        roster.RemoveAll(unit => unit == null);
        if (roster == null || roster.Count == 0) return;

        int missing = Mathf.Max(0, garrison.MaxArmySize - garrison.GrabArmySize());
        int unitTypes = Mathf.Min(2, roster.Count);
        for (int i = 0; i < missing; i++)
        {
            UnitSaveData unit = roster[i % unitTypes];
            if (unit != null) garrison.AddTroop(unit, 1, true);
        }
    }
    public FieldArmyHolder SallyOut(FieldArmyHolder sally)
    {
        return Mapshower.Instance.SpawnArmy(this);
    }
    
    public void UpdatePopulation()
    {
        population = 0;
        foreach (Culture culture in cultures)
        {
            population += culture.population;
        }
    }
    public void LosePopulation(int percentage)
    {
        foreach (Culture culture in cultures)
        {
            culture.population -= (int)(culture.population*percentage/100);
        }
        UpdatePopulation();
    }
}
[System.Serializable]
public class Nation
{
    public string name;
    public Color32 ownerIdentity;
    public bool IsPlayer;
    public List<FieldArmyHolder> armies = new List<FieldArmyHolder>();
    // public List<Nation> Enemies;
    public Faction faction;
    [Header("Nation identity")]
    public CivilizationData civilization;
    public NationCultureData culture;
    public ReligionData religion;
    public NationalBrain nationalbrainy;
    public int Manpower = 0;
    public int Gold = CampaignEconomy.StartingGold;
    public int ArmyNumber = 0;
    public int NextAIInfrastructureTurn;
    public int AIInfrastructureIntervalTurns = 12;
    public int NextAIEconomyTurn;
    public int LastGrossIncome;
    public int LastUnitUpkeep;
    public int UpkeepDebt;

    public void TakeTurn()
    {
        int ownedProvinces = Owners.Instance.provincelist.FindAll(province => province.nation == this).Count;
        Manpower += Mathf.Max(1, 1 + ownedProvinces / 4);
        LastGrossIncome = 0;
        foreach (Province province in Owners.Instance.provincelist)
            if (province != null && province.nation == this) LastGrossIncome += province.GetGoldIncome();
        LastUnitUpkeep = 0;
        foreach (FieldArmyHolder army in armies)
            if (army != null && army.fieldArmy != null) LastUnitUpkeep += army.fieldArmy.GetUpkeep();
        int netIncome = LastGrossIncome - LastUnitUpkeep;
        if (netIncome >= 0) Gold += netIncome;
        else
        {
            int paid = Mathf.Min(Gold, -netIncome);
            Gold -= paid;
            UpkeepDebt += -netIncome - paid;
        }
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            IsPlayer = CampaignNetworkPlayer.IsNationPlayerControlled(name);
        if (IsPlayer) return;
        // Braindead reserves a nation as expansion space: it accumulates normal
        // campaign resources but takes no autonomous strategic actions.
        if (NationContentResolver.HasFlag(this, "Braindead")) return;
        DevelopEconomicInfrastructure();
        DevelopMilitaryInfrastructure();
        ReinforceMostUnderstrengthArmy();
        OrderIdleFullArmiesToFrontier();
    }

    private void DevelopEconomicInfrastructure()
    {
        if (!NationContentResolver.HasBuilding(this, "Farm")) return;
        if (Owners.Instance.turncounter < NextAIEconomyTurn) return;
        Province selected = null; int lowestFarm = int.MaxValue;
        foreach (Province province in Owners.Instance.provincelist)
        {
            if (province == null || province.nation != this) continue;
            ProvinceBuilding farm = province.GetBuilding("Farm"); int level = farm != null ? farm.level : 0;
            if (level < ProvinceBuilding.StandardMaximumLevel && level < lowestFarm) { lowestFarm = level; selected = province; }
        }
        if (selected == null) return;
        ProvinceBuilding existing = selected.GetBuilding("Farm"); int targetLevel = existing != null ? existing.level + 1 : 1;
        int cost = CampaignEconomy.BuildingGoldCost("Farm", targetLevel);
        // Retain enough money to recruit at least one ordinary formation.
        if (Gold < cost + 100) return;
        int slot = existing != null ? existing.slotIndex : FirstFreeBuildingSlot(selected);
        if (slot < 0 || !selected.BeginBuildingConstruction(slot, "Farm", targetLevel,
            BuildingDefinition.ConstructionTicks("Farm", targetLevel), true)) return;
        Gold -= cost; NextAIEconomyTurn = Owners.Instance.turncounter + 15;
    }

    private static int FirstFreeBuildingSlot(Province province)
    {
        for (int slot = 0; slot < 4; slot++)
            if (province.GetBuildingInSlot(slot) == null &&
                (province.constructionOrders == null || !province.constructionOrders.Exists(order => order != null && order.slotIndex == slot)))
                return slot;
        return -1;
    }

    private void DevelopMilitaryInfrastructure()
    {
        if (IsPlayer || faction == null || Owners.Instance.turncounter < NextAIInfrastructureTurn) return;

        string selectedBuildingId = null;
        Province selectedProvince = null;
        ProvinceBuilding selectedExisting = null;
        float lowestCompletion = float.MaxValue;
        foreach (string buildingId in NationContentResolver.ResolveBuildings(this))
        {
            if (!NationContentResolver.IsRecruitmentBuilding(this, buildingId)) continue;
            int usefulMaximum = NationContentResolver.UsefulBuildingMaximumLevel(this, buildingId);
            if (usefulMaximum <= 0) continue;
            bool alreadyConstructing = Owners.Instance.provincelist.Exists(province =>
                province != null && province.nation == this && province.constructionOrders != null &&
                province.constructionOrders.Exists(order => order != null &&
                    string.Equals(order.buildingId, buildingId, System.StringComparison.OrdinalIgnoreCase)));
            if (alreadyConstructing) continue;

            Province selected = null;
            ProvinceBuilding existing = null;
            foreach (Province province in Owners.Instance.provincelist)
            {
                if (province == null || province.nation != this) continue;
                ProvinceBuilding candidate = province.GetBuilding(buildingId);
                if (candidate == null || candidate.level >= usefulMaximum) continue;
                if (existing == null || candidate.level > existing.level)
                {
                    selected = province;
                    existing = candidate;
                }
            }

            if (existing == null)
            {
                selected = Owners.Instance.provincelist.Find(province =>
                    province != null && province.nation == this && FirstFreeBuildingSlot(province) >= 0);
                if (selected == null) continue;
            }

            float completion = existing != null ? (float)existing.level / usefulMaximum : 0f;
            if (completion >= lowestCompletion) continue;
            lowestCompletion = completion;
            selectedBuildingId = buildingId;
            selectedProvince = selected;
            selectedExisting = existing;
        }

        if (selectedProvince == null || string.IsNullOrEmpty(selectedBuildingId)) return;
        int targetLevel = selectedExisting != null ? selectedExisting.level + 1 : 1;
        int slot = selectedExisting != null ? selectedExisting.slotIndex : FirstFreeBuildingSlot(selectedProvince);
        int goldCost = CampaignEconomy.BuildingGoldCost(selectedBuildingId, targetLevel);
        if (Gold < goldCost || !selectedProvince.BeginBuildingConstruction(slot, selectedBuildingId, targetLevel,
            BuildingDefinition.ConstructionTicks(selectedBuildingId, targetLevel), true)) return;

        Gold -= goldCost;
        if (selectedBuildingId.Equals("Barracks", System.StringComparison.OrdinalIgnoreCase))
            faction.BarracksLevel = Mathf.Max(faction.BarracksLevel, targetLevel);
        else if (selectedBuildingId.IndexOf("Mercenary", System.StringComparison.OrdinalIgnoreCase) >= 0)
            faction.MercLevel = Mathf.Max(faction.MercLevel, targetLevel);
        NextAIInfrastructureTurn = Owners.Instance.turncounter + Mathf.Max(1, AIInfrastructureIntervalTurns);
    }

    private void OrderIdleFullArmiesToFrontier()
    {
        if (IsPlayer) return;
        List<Province> hostile = Owners.Instance.provincelist.FindAll(province => province != null && province.nation != this);
        if (hostile.Count == 0) return;
        List<Province> frontier = hostile.FindAll(province =>
        {
            List<Province> adjacent = province.GrabAdjacents();
            return adjacent != null && adjacent.Exists(neighbor => neighbor != null && neighbor.nation == this);
        });
        List<Province> candidates = frontier.Count > 0 ? frontier : hostile;
        foreach (FieldArmyHolder army in armies)
        {
            if (army == null || army.IsHumanControlled || army.fieldArmy == null ||
                army.flaglist.Contains("Battle") || !army.IsTargetNull()) continue;
            if (army.fieldArmy.GrabArmySize() < Mathf.Max(1, army.fieldArmy.MaxArmySize)) continue;
            Province targetProvince = null;
            float bestDistance = float.MaxValue;
            foreach (Province candidate in candidates)
            {
                float distance = army.GrabDistanceToProvince(candidate);
                if (distance < bestDistance || Mathf.Approximately(distance, bestDistance) &&
                    (targetProvince == null || string.CompareOrdinal(candidate.name, targetProvince.name) < 0))
                { targetProvince = candidate; bestDistance = distance; }
            }
            if (targetProvince == null) continue;
            army.SetTarget(targetProvince);
            army.TargetProvince = targetProvince;
            army.generalbrain.NewGoal("MoveArmy");
        }
    }
    public bool ReinforceMostUnderstrengthArmy()
    {
        FieldArmyHolder army = null;
        float lowestStrength = 2f;
        foreach (FieldArmyHolder candidate in armies)
        {
            if (candidate == null || candidate.IsHumanControlled || candidate.fieldArmy == null) continue;
            float strength = candidate.fieldArmy.MaxArmySize <= 0 ? 1f :
                (float)candidate.fieldArmy.GrabArmySize() / candidate.fieldArmy.MaxArmySize;
            if (strength < lowestStrength)
            {
                lowestStrength = strength;
                army = candidate;
            }
        }
        return army != null && ReinforceArmy(army);
    }
    public bool ReinforceArmy(FieldArmyHolder army)
    {
        if (army == null || army.fieldArmy == null ||
            army.fieldArmy.GrabArmySize() + army.fieldArmy.GrabQueuedArmySize() >= army.fieldArmy.MaxArmySize) return false;
        if (Owners.Instance.turncounter < army.NextAIReinforcementTurn) return false;
        Province province = army.GrabNearestProvince();
        if (province != null && province.nation == this)
        {
            List<ProvinceLevyEntitlement> availableLevies = province.GetAvailableRegionLevies(this);
            if (availableLevies.Count > 0)
            {
                ProvinceLevyEntitlement levy = availableLevies[0];
                Province levyProvince = province.GetOccupiedRegionProvinces(this).Find(candidate =>
                    candidate.levyEntitlements != null && candidate.levyEntitlements.Contains(levy));
                if (levyProvince != null && levyProvince.RaiseLevy(levy.id, army))
                {
                    army.NextAIReinforcementTurn = Owners.Instance.turncounter + Mathf.Max(1, army.AIReinforcementIntervalTurns);
                    return true;
                }
            }
            List<UnitSaveData> units = province.GetRecruitableRegionUnits(this);
            if (units.Count > 0)
            {
                // Nations favour the best locally available tier while retaining
                // some lower-tier recruitment for cost and army variety.
                units.Sort((left, right) => NationContentResolver.GetUnitTier(this, left)
                    .CompareTo(NationContentResolver.GetUnitTier(this, right)));
                int upperHalfStart = Mathf.Max(0, units.Count / 2);
                UnitSaveData unit = Random.value < 0.7f
                    ? units[Random.Range(upperHalfStart, units.Count)]
                    : units[Random.Range(0, units.Count)];
                int manpowerCost = Mathf.Max(1, unit.cost / 100);
                int goldCost = CampaignEconomy.UnitGoldCost(unit);
                if (Manpower >= manpowerCost && Gold >= goldCost)
                {
                    Manpower -= manpowerCost;
                    Gold -= goldCost;
                    if (!army.fieldArmy.QueueRecruitment(unit, 1)) return false;
                    army.NextAIReinforcementTurn = Owners.Instance.turncounter + Mathf.Max(1, army.AIReinforcementIntervalTurns);
                    return true;
                }
            }
        }

        if (ProvinceMercenaryPool.Enabled && province != null)
        {
            ProvinceMercenaryPool pool = province.mercenaryPools.Find(item => item != null && item.unit != null && item.available > 0);
            if (pool != null)
            {
                int supplyCost = Mathf.Max(1, pool.unit.cost / 50);
                int goldCost = CampaignEconomy.UnitGoldCost(pool.unit);
                if (army.fieldArmy.ArmySupply >= supplyCost && Gold >= goldCost)
                {
                    army.fieldArmy.ArmySupply -= supplyCost;
                    Gold -= goldCost;
                    pool.available--;
                    if (!army.fieldArmy.QueueRecruitment(pool.unit, 1)) return false;
                    army.NextAIReinforcementTurn = Owners.Instance.turncounter + Mathf.Max(1, army.AIReinforcementIntervalTurns);
                    return true;
                }
            }
        }

        if (army.IsTargetNull())
        {
            Province recruitmentProvince = Owners.Instance.provincelist
                .Find(candidate => candidate.nation == this && candidate.GetRecruitableRegionUnits(this).Count > 0);
            if (recruitmentProvince != null && recruitmentProvince != province)
            {
                army.SetTarget(recruitmentProvince);
                army.TargetProvince = recruitmentProvince;
            }
        }
        return false;
    }
    public float AverageArmyStrength()
    {
        int count = 0;
        float total = 0f;
        foreach (FieldArmyHolder army in armies)
        {
            if (army == null || army.IsHumanControlled || army.fieldArmy == null || army.fieldArmy.MaxArmySize <= 0) continue;
            total += Mathf.Clamp01((float)army.fieldArmy.GrabArmySize() / army.fieldArmy.MaxArmySize);
            count++;
        }
        return count == 0 ? 1f : total / count;
    }
    public void SpawnArmy()
    {
        int ownedProvinces = Owners.Instance.provincelist.FindAll(province => province.nation == this).Count;
        int desiredArmyLimit = Mathf.Clamp(1 + ownedProvinces / 4, 1, 5);
        if (armies.Count < desiredArmyLimit && AverageArmyStrength() >= 0.65f && Gold >= CampaignEconomy.ArmyCreationCost)
        {
            var a = new List<Province>();
            foreach (var province in Owners.Instance.provincelist)
            {
                if (province.nation == this)
                {
                    a.Add(province);
                }
            }
            if (a.Count > 0)
            {
                ArmyNumber++;
                FieldArmyHolder spawned = Mapshower.Instance.SpawnArmy(
                    a[Random.Range(0, a.Count)], ArmyNumber.ToString() + "st Army of " + name);
                // Awake runs during Instantiate, but Start (which historically registered the
                // army) can be delayed until after several high-timescale FixedUpdates. Reserve
                // the slot now so those ticks cannot spawn duplicates.
                if (spawned != null && spawned.fieldArmy != null)
                {
                    Gold -= CampaignEconomy.ArmyCreationCost;
                    spawned.PreserveConfiguredRoster = true;
                    spawned.fieldArmy.nation = this;
                    if (!armies.Contains(spawned)) armies.Add(spawned);
                }
            }
        }
    }
}
[System.Serializable]
public class Culture
{
    public string name;
    public Color32 ownerIdentity;
    public int population;
}
[System.Serializable]
public class State
{
    public string name;
    public List<Province> provincelist;
    public Color32 stateIdentity; 
    public Nation nation;
    public int taxpercentage;
    public int levypercentage;
}

[System.Serializable]
public class CampaignRegion
{
    public string name;
    public Color32 identity;
    public List<Province> provincelist = new List<Province>();

    public int Population
    {
        get
        {
            int total = 0;
            foreach (Province province in provincelist)
                if (province != null) total += province.population;
            return total;
        }
    }

    public int Supply
    {
        get
        {
            int total = 0;
            foreach (Province province in provincelist)
                if (province != null) total += province.supply;
            return total;
        }
    }

    public List<Province> GetProvincesOwnedBy(Nation nation)
    {
        return provincelist.FindAll(province => province != null && province.nation == nation);
    }
}
