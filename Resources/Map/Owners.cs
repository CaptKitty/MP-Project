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
    [Tooltip("Maximum provinces whose slow holding economy is evaluated in one campaign tick. Keeping this small prevents periodic frame spikes.")]
    [Range(1, 16)] public int HoldingEvolutionProvinceBudget = 3;
    private int holdingEvolutionCursor;
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

        LoadCulturesFromNationData();

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
        foreach (Province province in provincelist) province.InitializeHoldings();
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
        if (string.IsNullOrWhiteSpace(culturename) || culturedict == null) return null;
        culturedict.TryGetValue(culturename, out Culture culture);
        return culture;
    }

    private void LoadCulturesFromNationData()
    {
        culturelist = new List<Culture>();
        culturedict = new Dictionary<string, Culture>(System.StringComparer.OrdinalIgnoreCase);
        NationCultureData[] definitions = Resources.LoadAll<NationCultureData>("Prefabs/NationData/Culture");
        System.Array.Sort(definitions, (left, right) => string.CompareOrdinal(left != null ? left.DisplayName : string.Empty,
            right != null ? right.DisplayName : string.Empty));
        foreach (NationCultureData definition in definitions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.DisplayName)) continue;
            Color32 color = definition.color; color.a = 255;
            Culture culture = new Culture { name = definition.DisplayName, ownerIdentity = color };
            culturelist.Add(culture);
            culturedict[definition.DisplayName] = culture;
            // The asset name remains a compatibility alias for older saves.
            culturedict[definition.name] = culture;
        }
    }

    public NationCultureData CultureDefinition(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName)) return null;
        return System.Array.Find(Resources.LoadAll<NationCultureData>("Prefabs/NationData/Culture"),
            definition => definition != null && definition.Matches(cultureName));
    }

    public Color32 CultureColor(string cultureName, Color32 fallback)
    {
        Culture culture = CallCultureByName(cultureName);
        if (culture != null) { Color32 configured = culture.ownerIdentity; configured.a = 255; return configured; }
        fallback.a = 255;
        return fallback;
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
            if (army != null && army.fieldArmy != null && army.IsTargetNull()) army.fieldArmy.ProcessRecruitmentTick();
        foreach (CampaignRegion region in regionlist)
            if (region != null) region.ProcessLoyaltyTurn();
        foreach (var nation in nationlist)
        {
            nation.TakeTurn();
        }
        foreach (var province in provincelist)
        {
            province.RegenerateMercenaries();
            province.ProcessConstructionTick();
            province.ProcessHoldingConstructionTick();
            province.ProcessLevyTick();
        }
        ProcessHoldingEvolutionBudget();
        if (CampaignNetworkPlayer.Local != null)
            CampaignNetworkPlayer.Local.BroadcastQueueStateNow();
    }

    private void ProcessHoldingEvolutionBudget()
    {
        if (provincelist == null || provincelist.Count == 0) return;
        int budget = Mathf.Min(Mathf.Max(1, HoldingEvolutionProvinceBudget), provincelist.Count);
        for (int processed = 0; processed < budget; processed++)
        {
            if (holdingEvolutionCursor >= provincelist.Count) holdingEvolutionCursor = 0;
            Province province = provincelist[holdingEvolutionCursor++];
            if (province != null) province.ProcessHoldingEvolutionTick(turncounter);
        }
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
    [System.NonSerialized] public bool regionConfiguredFromData;
    public Vector2 position;
    public CampaignTerrainProfile terrainProfile = CampaignTerrainProfile.Auto;
    public int population = 1000;
    public int supply = 1000;
    [Min(0)] public int urbanization;
    [Min(0)] public int baseMaximumDevelopment = 100;
    public List<ProvinceNamedModifier> uniqueModifiers = new List<ProvinceNamedModifier>();
    public int MaxDevelopmentModifier
    {
        get
        {
            ProvinceLocalModifiers total = GetLocalModifiers();
            return total != null ? total.maxDevelopment : 0;
        }
    }
    public int MaximumDevelopment => Mathf.Max(0, baseMaximumDevelopment + MaxDevelopmentModifier);
    public FieldArmy garrison;

    public List<string> AdjacentProvinces = new List<string>();

    public List<Culture> cultures;
    public Culture PrimaryCulture => cultures != null && cultures.Count > 0 ? cultures[0] : null;
    public float GetCulturePercentage(string cultureName)
    {
        if (string.IsNullOrEmpty(cultureName) || cultures == null || population <= 0) return 0f;
        int culturalPopulation = 0;
        foreach (Culture culture in cultures)
            if (culture != null && string.Equals(culture.name, cultureName, System.StringComparison.OrdinalIgnoreCase))
                culturalPopulation += Mathf.Max(0, culture.population);
        return culturalPopulation * 100f / population;
    }
    public int taxincome;
    public int taxpercentage;
    public int levyincome;
    public int levypercentage;
    public int unrest;
    public List<ProvinceBuilding> buildings = new List<ProvinceBuilding>();
    public List<ProvinceConstructionOrder> constructionOrders = new List<ProvinceConstructionOrder>();
    public List<ProvinceMercenaryPool> mercenaryPools = new List<ProvinceMercenaryPool>();
    public List<ProvinceLevyEntitlement> levyEntitlements = new List<ProvinceLevyEntitlement>();
    [System.NonSerialized] private int nextLevyReconcileTick = -1;
    [System.NonSerialized] private string levyAllInArmedArmyId;
    public List<ProvinceHolding> holdings = new List<ProvinceHolding>();
    public List<HoldingConstructionOrder> holdingConstructionOrders = new List<HoldingConstructionOrder>();
    [Header("Holding composition")]
    public List<HoldingTagModifier> baseHoldingTagDesires = new List<HoldingTagModifier>();
    public HoldingEvolutionSettings holdingEvolution = new HoldingEvolutionSettings();

    public void InitializeRecruitment()
    {
        urbanization = Mathf.Clamp(urbanization, 0, MaximumDevelopment);
        if (buildings == null) buildings = new List<ProvinceBuilding>();
        if (constructionOrders == null) constructionOrders = new List<ProvinceConstructionOrder>();
        if (mercenaryPools == null) mercenaryPools = new List<ProvinceMercenaryPool>();
        if (levyEntitlements == null) levyEntitlements = new List<ProvinceLevyEntitlement>();
        if (holdings == null) holdings = new List<ProvinceHolding>();
        if (holdingConstructionOrders == null) holdingConstructionOrders = new List<HoldingConstructionOrder>();
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
    public void EnsureCulture()
    {
        if (cultures == null) cultures = new List<Culture>();
        cultures.RemoveAll(culture => culture == null);
        if (cultures.Count > 0) return;
        string cultureName = nation != null && nation.culture != null
            ? nation.culture.DisplayName
            : nation != null ? nation.name : "Unassigned";
        cultures.Add(new Culture
        {
            name = cultureName,
            ownerIdentity = nation != null ? nation.ownerIdentity : new Color32(128, 128, 128, 255),
            population = Mathf.Max(1, population)
        });
    }
    public void InitializeHoldings()
    {
        if (holdings == null) holdings = new List<ProvinceHolding>();
        holdings.RemoveAll(holding => holding == null);
        if (holdings.Count == 0)
        {
            List<string> cultureNames = new List<string>();
            EnsureCulture();
            if (PrimaryCulture != null && !string.IsNullOrWhiteSpace(PrimaryCulture.name)) cultureNames.Add(PrimaryCulture.name);
            if (Owners.Instance != null)
            {
                foreach (string adjacentName in AdjacentProvinces)
                {
                    Province adjacent = Owners.Instance.provincelist.Find(candidate => candidate != null && candidate.name == adjacentName);
                    string adjacentCulture = adjacent != null && adjacent.PrimaryCulture != null ? adjacent.PrimaryCulture.name : null;
                    if (!string.IsNullOrWhiteSpace(adjacentCulture) && !cultureNames.Exists(value =>
                        string.Equals(value, adjacentCulture, System.StringComparison.OrdinalIgnoreCase))) cultureNames.Add(adjacentCulture);
                    if (cultureNames.Count >= 3) break;
                }
                if (cultureNames.Count < 3)
                    foreach (Province candidate in Owners.Instance.provincelist)
                    {
                        string candidateCulture = candidate != null && candidate.PrimaryCulture != null ? candidate.PrimaryCulture.name : null;
                        if (!string.IsNullOrWhiteSpace(candidateCulture) && !cultureNames.Exists(value =>
                            string.Equals(value, candidateCulture, System.StringComparison.OrdinalIgnoreCase))) cultureNames.Add(candidateCulture);
                        if (cultureNames.Count >= 3) break;
                    }
            }
            while (cultureNames.Count < 3) cultureNames.Add(cultureNames.Count > 0 ? cultureNames[0] : "Unassigned");
            LevyGrantRule migrationRule = LevySystem.ResolveRules(nation).Find(rule => rule != null && rule.unit != null &&
                buildings.Exists(building => rule.Applies(this, building)));
            HoldingDefinition definition = migrationRule != null
                ? HoldingDefinition.DefaultCitizenFarm(migrationRule.unit) : HoldingDefinition.DefaultCitizenFarm();
            for (int i = 0; i < 10; i++) holdings.Add(new ProvinceHolding { instanceId = name + "-holding-" + i,
                definition = definition, id = definition.StableId, level = 1, slotIndex = i,
                cultureName = i < 7 ? cultureNames[0] : i < 9 ? cultureNames[1] : cultureNames[2],
                socioEconomicClass = definition.defaultClass });
        }
        for (int i = 0; i < holdings.Count; i++)
        {
            ProvinceHolding holding = holdings[i];
            if (holding.slotIndex < 0) holding.slotIndex = i;
            if (string.IsNullOrWhiteSpace(holding.instanceId)) holding.instanceId = name + "-holding-" + holding.slotIndex;
            if (holding.definition == null) holding.definition = HoldingDefinition.Find(holding.id);
        }
        RebuildPopulationFromHoldings();
        ReconcileLevyEntitlements();
    }

    public void RebuildPopulationFromHoldings()
    {
        population = holdings != null ? holdings.FindAll(holding => holding != null).Count : 0;
        Dictionary<string, int> totals = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
        {
            if (holding == null) continue;
            string holdingCulture = !string.IsNullOrWhiteSpace(holding.cultureName) ? holding.cultureName : "Unassigned";
            totals[holdingCulture] = totals.TryGetValue(holdingCulture, out int count) ? count + 1 : 1;
        }
        cultures = new List<Culture>();
        foreach (KeyValuePair<string, int> pair in totals)
        {
            Culture source = Owners.Instance != null ? Owners.Instance.CallCultureByName(pair.Key) : null;
            cultures.Add(new Culture { name = pair.Key, population = pair.Value,
                ownerIdentity = source != null ? source.ownerIdentity : nation != null ? nation.ownerIdentity : identity });
        }
        cultures.Sort((left, right) => right.population.CompareTo(left.population));
    }

    public Dictionary<SocioEconomicClass, int> GetSocioEconomicComposition()
    {
        Dictionary<SocioEconomicClass, int> result = new Dictionary<SocioEconomicClass, int>();
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
        {
            if (holding == null) continue;
            result[holding.socioEconomicClass] = result.TryGetValue(holding.socioEconomicClass, out int count) ? count + 1 : 1;
        }
        return result;
    }

    public ProvinceLocalModifiers GetLocalModifiers()
    {
        ProvinceLocalModifiers result = new ProvinceLocalModifiers();
        if (buildings != null) foreach (ProvinceBuilding building in buildings)
        {
            if (building == null || building.definition == null || building.definition.levels == null) continue;
            foreach (BuildingLevelDefinition level in building.definition.levels)
                if (level != null && level.level <= building.level) result.Add(level.localModifiers);
        }
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
        {
            if (holding == null || holding.definition == null || holding.definition.levels == null) continue;
            foreach (HoldingLevelDefinition level in holding.definition.levels)
                if (level != null && level.level <= holding.level) result.Add(level.localModifiers);
        }
        if (uniqueModifiers != null) foreach (ProvinceNamedModifier modifier in uniqueModifiers)
            if (modifier != null) result.Add(modifier.localModifiers);
        return result;
    }

    public void ClampDevelopment()
    {
        urbanization = Mathf.Clamp(urbanization, 0, MaximumDevelopment);
    }

    public ProvinceHolding AddHoldingPopulation(HoldingDefinition definition, string cultureName)
    {
        if (definition == null) return null;
        if (holdings == null) holdings = new List<ProvinceHolding>();
        int slot = 0; while (GetHoldingInSlot(slot) != null) slot++;
        ProvinceHolding holding = new ProvinceHolding { instanceId = name + "-holding-" + System.Guid.NewGuid().ToString("N"),
            definition = definition, id = definition.StableId, level = 1, slotIndex = slot,
            cultureName = !string.IsNullOrWhiteSpace(cultureName) ? cultureName : PrimaryCulture != null ? PrimaryCulture.name : "Unassigned",
            socioEconomicClass = definition.defaultClass };
        holdings.Add(holding); RebuildPopulationFromHoldings(); ReconcileLevyEntitlements(); return holding;
    }

    public bool RemoveHoldingPopulation(string instanceId)
    {
        ProvinceHolding holding = GetHolding(instanceId);
        if (holding == null || IsHoldingMobilized(instanceId)) return false;
        holdings.Remove(holding); RebuildPopulationFromHoldings(); ReconcileLevyEntitlements(); return true;
    }

    private sealed class LevyUnitPool
    {
        public UnitSaveData unit;
        public long accumulated;
        public long remainder;
        public Province remainderProvince;
        public ProvinceHolding remainderHolding;
        public readonly List<string> pendingContributors = new List<string>();
    }

    public void ReconcileLevyEntitlements()
    {
        if (levyEntitlements == null) levyEntitlements = new List<ProvinceLevyEntitlement>();
        foreach (ProvinceLevyEntitlement entitlement in levyEntitlements) if (entitlement != null) entitlement.eligible = false;
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        if (nation == null) return;
        bool callupsAllowed = campaignRegion == null || campaignRegion.AllowsLevyCallups(nation);

        // Fractional holding contributions are pooled across the occupied part of the region.
        // Integer arithmetic keeps entitlement allocation deterministic in multiplayer.
        List<Province> sourceProvinces = GetOccupiedRegionProvinces(nation);
        sourceProvinces.Sort((left, right) => string.CompareOrdinal(left != null ? left.name : string.Empty,
            right != null ? right.name : string.Empty));
        const long completeFormation = 1000000L;
        Dictionary<string, LevyUnitPool> unitPools = new Dictionary<string, LevyUnitPool>(System.StringComparer.OrdinalIgnoreCase);
        foreach (Province sourceProvince in sourceProvinces)
        {
            if (sourceProvince == null || sourceProvince.holdings == null) continue;
            List<ProvinceHolding> sources = sourceProvince.holdings.FindAll(item => item != null && item.CanRaiseLevies);
            sources.Sort((left, right) =>
            {
                int bySlot = left.slotIndex.CompareTo(right.slotIndex);
                return bySlot != 0 ? bySlot : string.CompareOrdinal(left.instanceId, right.instanceId);
            });
            foreach (ProvinceHolding holding in sources)
            {
                UnitSaveData resolvedLevy = HoldingEvolutionSystem.ResolveLevyUnit(sourceProvince, holding);
                if (resolvedLevy == null) continue;
                string poolId = !string.IsNullOrWhiteSpace(resolvedLevy.name) ? resolvedLevy.name : resolvedLevy.unitname;
                if (!unitPools.TryGetValue(poolId, out LevyUnitPool pool))
                { pool = new LevyUnitPool { unit = resolvedLevy }; unitPools.Add(poolId, pool); }
                long before = pool.accumulated / completeFormation;
                if (!pool.pendingContributors.Contains(holding.instanceId)) pool.pendingContributors.Add(holding.instanceId);
                pool.accumulated += (long)holding.LevyContributionPermille * Mathf.Max(0, nation.LevyLawPermille);
                pool.remainderProvince = sourceProvince;
                pool.remainderHolding = holding;
                int formations = (int)(pool.accumulated / completeFormation - before);
                for (int ordinal = 0; ordinal < formations; ordinal++)
                {
                    List<string> contributors = new List<string>(pool.pendingContributors);
                    pool.pendingContributors.Clear();
                    long completedBoundary = (before + ordinal + 1L) * completeFormation;
                    if (completedBoundary < pool.accumulated) pool.pendingContributors.Add(holding.instanceId);
                    if (sourceProvince != this) continue;
                    long formationIndex = before + ordinal;
                    string entitlementId = name + "|" + nameof(ProvinceHolding) + "|" + poolId + "|" + formationIndex;
                    ProvinceLevyEntitlement entitlement = levyEntitlements.Find(item => item != null && item.id == entitlementId);
                    if (entitlement == null)
                    {
                        entitlement = new ProvinceLevyEntitlement { id = entitlementId, ruleId = string.Empty,
                            holdingId = holding.HoldingId, holdingInstanceId = holding.instanceId,
                            unitName = pool.unit.name,
                            unit = pool.unit, buildingSlot = holding.slotIndex, ordinal = (int)formationIndex,
                            beneficiaryNation = nation.name, state = LevyEntitlementState.Available };
                        levyEntitlements.Add(entitlement);
                    }
                    entitlement.holdingId = holding.HoldingId; entitlement.holdingInstanceId = holding.instanceId;
                    entitlement.unit = pool.unit;
                    entitlement.unitName = pool.unit.name; entitlement.eligible = callupsAllowed;
                    entitlement.beneficiaryNation = nation.name;
                    entitlement.contributorHoldingInstanceIds = contributors;
                }
            }
        }

        // Whole formations remain type-pure. Only the fractional leftovers are pooled:
        // the largest remainder claims the rounded formation and absorbs smaller pools.
        List<LevyUnitPool> remainderPools = new List<LevyUnitPool>(unitPools.Values);
        foreach (LevyUnitPool pool in remainderPools) pool.remainder = pool.accumulated % completeFormation;
        int roundedOrdinal = 0;
        while (true)
        {
            remainderPools.Sort((left, right) =>
            {
                int byCapacity = right.remainder.CompareTo(left.remainder);
                return byCapacity != 0 ? byCapacity : string.CompareOrdinal(left.unit.name, right.unit.name);
            });
            long totalRemainder = 0; foreach (LevyUnitPool pool in remainderPools) totalRemainder += pool.remainder;
            if (remainderPools.Count == 0 || remainderPools[0].remainder <= 0 || totalRemainder < completeFormation) break;
            LevyUnitPool recipient = remainderPools[0];
            long needed = completeFormation - recipient.remainder;
            List<string> contributors = new List<string>(recipient.pendingContributors);
            recipient.remainder = 0;
            recipient.pendingContributors.Clear();
            for (int i = 1; i < remainderPools.Count && needed > 0; i++)
            {
                LevyUnitPool donor = remainderPools[i];
                if (donor.remainder <= 0) continue;
                long absorbed = System.Math.Min(needed, donor.remainder);
                donor.remainder -= absorbed; needed -= absorbed;
                foreach (string contributor in donor.pendingContributors)
                    if (!contributors.Contains(contributor)) contributors.Add(contributor);
                if (donor.remainder == 0) donor.pendingContributors.Clear();
            }
            if (needed > 0 || recipient.remainderProvince != this || recipient.remainderHolding == null)
            { roundedOrdinal++; continue; }
            ProvinceHolding holding = recipient.remainderHolding;
            string poolId = !string.IsNullOrWhiteSpace(recipient.unit.name) ? recipient.unit.name : recipient.unit.unitname;
            string entitlementId = name + "|RoundedLevy|" + poolId + "|" + roundedOrdinal;
            ProvinceLevyEntitlement entitlement = levyEntitlements.Find(item => item != null && item.id == entitlementId);
            if (entitlement == null)
            {
                entitlement = new ProvinceLevyEntitlement { id = entitlementId, ruleId = string.Empty,
                    holdingId = holding.HoldingId, holdingInstanceId = holding.instanceId,
                    unitName = recipient.unit.name, unit = recipient.unit, buildingSlot = holding.slotIndex,
                    ordinal = roundedOrdinal, beneficiaryNation = nation.name, state = LevyEntitlementState.Available };
                levyEntitlements.Add(entitlement);
            }
            entitlement.holdingId = holding.HoldingId; entitlement.holdingInstanceId = holding.instanceId;
            entitlement.unit = recipient.unit; entitlement.unitName = recipient.unit.name;
            entitlement.eligible = callupsAllowed; entitlement.beneficiaryNation = nation.name;
            entitlement.contributorHoldingInstanceIds = contributors;
            roundedOrdinal++;
        }
    }

    public void ProcessLevyTick()
    {
        int currentTick = Owners.Instance != null ? Owners.Instance.turncounter : 0;
        if (nextLevyReconcileTick < 0)
            nextLevyReconcileTick = currentTick + StableLevyReconcilePhase(name, 8);
        if (currentTick >= nextLevyReconcileTick)
        {
            ReconcileLevyEntitlements();
            nextLevyReconcileTick = currentTick + 8;
        }
        foreach (ProvinceLevyEntitlement entitlement in levyEntitlements)
        {
            if (entitlement == null || entitlement.remainingTicks <= 0) continue;
            if (entitlement.state == LevyEntitlementState.Mobilizing && !entitlement.eligible)
            { entitlement.state = LevyEntitlementState.Available; entitlement.remainingTicks = 0; entitlement.raisedArmyId = null; continue; }
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

    private static int StableLevyReconcilePhase(string value, int interval)
    {
        unchecked
        {
            int hash = 17;
            if (value != null) for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
            return (hash & int.MaxValue) % Mathf.Max(1, interval);
        }
    }

    public List<ProvinceLevyEntitlement> GetAvailableRegionLevies(Nation owner)
    {
        List<ProvinceLevyEntitlement> result = new List<ProvinceLevyEntitlement>();
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        if (campaignRegion != null && !campaignRegion.AllowsLevyCallups(owner)) return result;
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
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        ProvinceLevyEntitlement entitlement = levyEntitlements.Find(item => item != null && item.id == entitlementId);
        if (campaignRegion != null && !campaignRegion.AllowsLevyCallups(nation) || entitlement == null || army == null ||
            army.fieldArmy == null || !army.IsTargetNull() || !entitlement.eligible ||
            entitlement.state != LevyEntitlementState.Available || army.fieldArmy.nation != nation ||
            army.fieldArmy.GrabArmySize() + army.fieldArmy.GrabQueuedArmySize() >= army.fieldArmy.MaxArmySize) return false;
        LevyGrantRule rule = LevySystem.FindRule(nation, entitlement.ruleId);
        entitlement.raisedArmyId = army.NetworkArmyId;
        HoldingDefinition holding = HoldingDefinition.Find(entitlement.holdingId);
        entitlement.remainingTicks = holding != null ? Mathf.Max(0, holding.levyMobilizationTicks) :
            rule != null ? Mathf.Max(0, rule.mobilizationTicks) : 0;
        if (entitlement.remainingTicks > 0) entitlement.state = LevyEntitlementState.Mobilizing;
        else
        {
            entitlement.state = LevyEntitlementState.Raised;
            army.fieldArmy.AddTroop(entitlement.unit, 1, true, CampaignUnitOrigin.Levy, entitlement.id);
        }
        return true;
    }

    public int RaiseAllAvailableRegionLevies(FieldArmyHolder army, bool allowAllInSecondClick = false)
    {
        if (army == null || army.fieldArmy == null || army.fieldArmy.nation == null || nation != army.fieldArmy.nation) return 0;
        int capacity = army.fieldArmy.MaxArmySize - army.fieldArmy.GrabArmySize() - army.fieldArmy.GrabQueuedArmySize();
        if (capacity <= 0) return 0;
        List<Province> regionProvinces = GetOccupiedRegionProvinces(army.fieldArmy.nation);
        List<ProvinceLevyEntitlement> available = GetAvailableRegionLevies(army.fieldArmy.nation);
        string armyKey = !string.IsNullOrEmpty(army.NetworkArmyId) ? army.NetworkArmyId : army.GetInstanceID().ToString();
        bool allIn = allowAllInSecondClick && levyAllInArmedArmyId == armyKey;
        bool skippedForFood = false;
        if (allIn) levyAllInArmedArmyId = null;
        int raised = 0;
        foreach (ProvinceLevyEntitlement entitlement in available)
        {
            if (raised >= capacity) break;
            if (!allIn && !CanMobilizeWithoutFoodDeficit(entitlement, army.fieldArmy.nation, regionProvinces))
            { skippedForFood = true; continue; }
            Province source = regionProvinces.Find(candidate => candidate.levyEntitlements != null &&
                candidate.levyEntitlements.Contains(entitlement));
            if (source != null && source.RaiseLevy(entitlement.id, army)) raised++;
        }
        if (allowAllInSecondClick)
            levyAllInArmedArmyId = !allIn && skippedForFood ? armyKey : null;
        return raised;
    }

    private bool CanMobilizeWithoutFoodDeficit(ProvinceLevyEntitlement entitlement, Nation owner,
        List<Province> regionProvinces)
    {
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        if (campaignRegion == null || entitlement == null) return true;
        int projectedFood = campaignRegion.RegionalFood(owner);
        HashSet<string> contributors = new HashSet<string>();
        if (!string.IsNullOrEmpty(entitlement.holdingInstanceId)) contributors.Add(entitlement.holdingInstanceId);
        if (entitlement.contributorHoldingInstanceIds != null)
            foreach (string id in entitlement.contributorHoldingInstanceIds) if (!string.IsNullOrEmpty(id)) contributors.Add(id);
        foreach (Province source in regionProvinces)
        foreach (string id in contributors)
        {
            ProvinceHolding holding = source != null ? source.GetHolding(id) : null;
            if (holding == null || source.IsHoldingMobilized(id)) continue;
            projectedFood -= Mathf.Max(0, source.GetHoldingOutput(holding, HoldingOutputType.Food, false) -
                source.GetHoldingOutput(holding, HoldingOutputType.Food, true));
        }
        return projectedFood >= 0;
    }

    public void BeginLevyRecovery(string entitlementId)
    {
        ProvinceLevyEntitlement entitlement = levyEntitlements.Find(item => item != null && item.id == entitlementId);
        if (entitlement == null) return;
        LevyGrantRule rule = LevySystem.FindRule(nation, entitlement.ruleId);
        entitlement.state = LevyEntitlementState.Recovering; entitlement.raisedArmyId = null;
        HoldingDefinition holding = HoldingDefinition.Find(entitlement.holdingId);
        entitlement.remainingTicks = holding != null ? Mathf.Max(0, holding.levyRecoveryTicks) :
            rule != null ? Mathf.Max(0, rule.recoveryTicks) : 20;
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
            if (building != null && building.definition != null) income += building.DefinitionGoldIncomeAt(urbanization);
        income += GetHoldingOutput(HoldingOutputType.Income);
        if (farm != null && farm.definition == null)
            income += Mathf.Max(0, farm.level) * CampaignEconomy.FarmIncomePerLevel;
        CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(region) : null;
        float loyalty = campaignRegion != null ? campaignRegion.GetLoyalty(nation) : 100f;
        int loyaltyAdjustedIncome = Mathf.RoundToInt(income * Mathf.Clamp(loyalty, 0f, 100f) / 100f);
        return CampaignEconomy.ApplyGoldIncomeRate(loyaltyAdjustedIncome);
    }

    public int GetHoldingOutput(HoldingOutputType type)
    {
        int total = 0;
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
            if (holding != null) total += GetHoldingOutput(holding, type);
        return total;
    }

    public int GetHoldingOutput(ProvinceHolding holding, HoldingOutputType type)
    {
        return GetHoldingOutput(holding, type, holding != null && IsHoldingMobilized(holding.instanceId));
    }

    public int GetHoldingOutput(ProvinceHolding holding, HoldingOutputType type, bool mobilized)
    {
        if (holding == null) return 0;
        int value = holding.GetOutput(type, urbanization, mobilized);
        float efficiency = HoldingEvolutionSystem.OutputEfficiencyPercent(this, holding.definition);
        if (type == HoldingOutputType.Food)
        {
            int consumption = holding.FoodConsumption;
            int grossProduction = value + consumption;
            return Mathf.RoundToInt(grossProduction * (1f + efficiency / 100f)) - consumption;
        }
        return Mathf.RoundToInt(value * (1f + efficiency / 100f));
    }

    public void ProcessHoldingEvolutionTick(int campaignTick)
    {
        HoldingEvolutionSystem.ProcessTick(this, campaignTick);
    }

    public int GetFoodOutput()
    {
        return GetFoodProduction() - GetFoodConsumption();
    }

    public int GetFoodProduction()
    {
        int total = 0;
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
            if (holding != null) total += holding.GetOutput(HoldingOutputType.Food, urbanization,
                IsHoldingMobilized(holding.instanceId)) + holding.FoodConsumption;
        if (buildings != null) foreach (ProvinceBuilding building in buildings)
            if (building != null && building.definition != null) total += building.DefinitionFoodOutputAt(urbanization);
        return total;
    }

    public int GetFoodConsumption()
    {
        int total = 0;
        if (holdings != null) foreach (ProvinceHolding holding in holdings)
            if (holding != null) total += holding.FoodConsumption;
        if (buildings != null) foreach (ProvinceBuilding building in buildings)
            if (building != null && building.definition != null) total += building.DefinitionFoodConsumption;
        return total;
    }

    public bool IsHoldingMobilized(string holdingInstanceId)
    {
        if (string.IsNullOrWhiteSpace(holdingInstanceId)) return false;
        foreach (Province province in GetOccupiedRegionProvinces(nation))
        {
            if (province == null || province.levyEntitlements == null) continue;
            if (province.levyEntitlements.Exists(entitlement => entitlement != null &&
                (entitlement.state == LevyEntitlementState.Mobilizing || entitlement.state == LevyEntitlementState.Raised) &&
                (entitlement.holdingInstanceId == holdingInstanceId || entitlement.contributorHoldingInstanceIds != null &&
                    entitlement.contributorHoldingInstanceIds.Contains(holdingInstanceId)))) return true;
        }
        return false;
    }

    public List<ProvinceLevyEntitlement> GetRegionalLevyEntitlementsForHolding(string holdingInstanceId)
    {
        List<ProvinceLevyEntitlement> result = new List<ProvinceLevyEntitlement>();
        if (string.IsNullOrWhiteSpace(holdingInstanceId)) return result;
        foreach (Province province in GetOccupiedRegionProvinces(nation))
        {
            if (province == null || province.levyEntitlements == null) continue;
            foreach (ProvinceLevyEntitlement entitlement in province.levyEntitlements)
                if (entitlement != null && entitlement.eligible &&
                    (entitlement.holdingInstanceId == holdingInstanceId || entitlement.contributorHoldingInstanceIds != null &&
                        entitlement.contributorHoldingInstanceIds.Contains(holdingInstanceId)) &&
                    !result.Contains(entitlement)) result.Add(entitlement);
        }
        return result;
    }

    public int GetTempleUpkeep()
    {
        return buildings != null && buildings.Exists(building => building != null &&
            building.BuildingId.Equals("Temple", System.StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
    }

    public void ApplyTempleCultureInfluence()
    {
        ProvinceBuilding temple = GetBuilding("Temple");
        string nationalCulture = nation != null && nation.culture != null ? nation.culture.DisplayName : string.Empty;
        if (temple == null || string.IsNullOrWhiteSpace(nationalCulture) || holdings == null) return;
        ProvinceHolding converted = holdings.FindLast(holding => holding != null &&
            !string.Equals(holding.cultureName, nationalCulture, System.StringComparison.OrdinalIgnoreCase));
        if (converted != null) { converted.cultureName = nationalCulture; RebuildPopulationFromHoldings(); }
    }

    public ProvinceBuilding GetBuildingInSlot(int slotIndex)
    {
        return buildings.Find(building => building != null && building.slotIndex == slotIndex);
    }

    public ProvinceHolding GetHoldingInSlot(int slotIndex) => holdings != null
        ? holdings.Find(holding => holding != null && holding.slotIndex == slotIndex) : null;
    public ProvinceHolding GetHolding(string instanceId) => holdings != null
        ? holdings.Find(holding => holding != null && holding.instanceId == instanceId) : null;

    public bool BeginHoldingTransformation(string instanceId, string targetHoldingId, int transformationTicks)
    {
        ProvinceHolding holding = GetHolding(instanceId);
        return holding != null && BeginHoldingConstruction(holding.slotIndex, targetHoldingId, 1, transformationTicks);
    }

    public bool BeginHoldingConstruction(int slotIndex, string holdingId, int targetLevel, int constructionTicks)
    {
        if (slotIndex < 0 || string.IsNullOrWhiteSpace(holdingId)) return false;
        if (holdings == null) holdings = new List<ProvinceHolding>();
        if (holdingConstructionOrders == null) holdingConstructionOrders = new List<HoldingConstructionOrder>();
        if (holdingConstructionOrders.Exists(order => order != null && order.slotIndex == slotIndex)) return false;
        HoldingDefinition definition = HoldingDefinition.Find(holdingId);
        if (definition == null || targetLevel < 1 || targetLevel > Mathf.Max(1, definition.maximumLevel)) return false;
        ProvinceHolding existing = GetHoldingInSlot(slotIndex);
        if (existing == null || existing.HoldingId.Equals(definition.StableId, System.StringComparison.OrdinalIgnoreCase)) return false;
        if (existing.definition != null && !existing.definition.CanTransformTo(definition.StableId, this)) return false;
        HoldingConstructionOrder order = new HoldingConstructionOrder { slotIndex = slotIndex,
            holdingInstanceId = existing.instanceId, holdingId = definition.StableId, targetLevel = 1,
            remainingTicks = Mathf.Max(0, constructionTicks) };
        holdingConstructionOrders.Add(order);
        if (order.remainingTicks == 0) CompleteHoldingConstruction(order);
        return true;
    }

    public void ProcessHoldingConstructionTick()
    {
        if (holdingConstructionOrders == null) return;
        for (int i = holdingConstructionOrders.Count - 1; i >= 0; i--)
        {
            HoldingConstructionOrder order = holdingConstructionOrders[i];
            if (order == null) { holdingConstructionOrders.RemoveAt(i); continue; }
            order.remainingTicks--;
            if (order.remainingTicks <= 0) CompleteHoldingConstruction(order);
        }
    }

    private void CompleteHoldingConstruction(HoldingConstructionOrder order)
    {
        HoldingDefinition definition = HoldingDefinition.Find(order.holdingId);
        if (definition == null) { holdingConstructionOrders.Remove(order); return; }
        ProvinceHolding holding = GetHolding(order.holdingInstanceId) ?? GetHoldingInSlot(order.slotIndex);
        if (holding == null) { holdingConstructionOrders.Remove(order); return; }
        holding.definition = definition; holding.id = definition.StableId; holding.level = 1;
        holdingConstructionOrders.Remove(order);
        ClampDevelopment();
        ReconcileLevyEntitlements();
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
        ClampDevelopment();
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
        RebuildPopulationFromHoldings();
    }
    public void LosePopulation(int percentage)
    {
        if (holdings == null || holdings.Count == 0) return;
        int remove = Mathf.Clamp(Mathf.RoundToInt(holdings.Count * Mathf.Clamp(percentage, 0, 100) / 100f), 0, holdings.Count);
        for (int i = 0; i < remove; i++) holdings.RemoveAt(holdings.Count - 1);
        RebuildPopulationFromHoldings(); ReconcileLevyEntitlements();
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
    [Header("National laws")]
    [Tooltip("National levy rate in permille. 200 means 20% of eligible holding levy capacity becomes formations.")]
    [Range(0, 1000)] public int LevyLawPermille = 200;
    [Tooltip("National laws and temporary effects can influence desired holding tags and their efficiency here.")]
    public List<HoldingTagModifier> holdingEconomyModifiers = new List<HoldingTagModifier>();
    [Min(1)] public int AIMinimumCampaignArmySize = 10;

    public bool IsArmyCombatReady(FieldArmyHolder army, bool includeQueuedRecruitment = false)
    {
        if (army == null || army.fieldArmy == null) return false;
        int maximum = Mathf.Max(1, army.fieldArmy.MaxArmySize);
        int size = army.fieldArmy.GrabArmySize();
        if (includeQueuedRecruitment) size += army.fieldArmy.GrabQueuedArmySize();
        return size >= Mathf.Min(maximum, Mathf.Max(1, AIMinimumCampaignArmySize));
    }

    public int GetHostileFieldArmyStrengthNear(Province province, float radius = 75f)
    {
        if (province == null || Owners.Instance == null) return 0;
        int strongest = 0;
        foreach (FieldArmyHolder other in Owners.Instance.armylist)
        {
            if (other == null || other.fieldArmy == null || other.fieldArmy.nation == null ||
                other.fieldArmy.nation == this || other.flaglist.Contains("Battle")) continue;
            if (other.GrabDistanceToProvince(province) <= radius)
                strongest = Mathf.Max(strongest, other.fieldArmy.GrabArmySize());
        }
        return strongest;
    }

    public void TakeTurn()
    {
        int ownedProvinces = Owners.Instance.provincelist.FindAll(province => province.nation == this).Count;
        int holdingManpower = 0;
        foreach (Province province in Owners.Instance.provincelist)
            if (province != null && province.nation == this) holdingManpower += province.GetHoldingOutput(HoldingOutputType.Manpower);
        Manpower += Mathf.Max(1, holdingManpower);
        LastGrossIncome = 0;
        foreach (Province province in Owners.Instance.provincelist)
            if (province != null && province.nation == this) LastGrossIncome += province.GetGoldIncome();
        LastUnitUpkeep = 0;
        foreach (FieldArmyHolder army in armies)
            if (army != null && army.fieldArmy != null) LastUnitUpkeep += army.fieldArmy.GetUpkeep();
        foreach (Province province in Owners.Instance.provincelist)
            if (province != null && province.nation == this) LastUnitUpkeep += province.GetTempleUpkeep();
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
        float bestValue = 0f;
        foreach (string buildingId in NationContentResolver.ResolveBuildings(this))
        {
            if (string.Equals(buildingId, "Farm", System.StringComparison.OrdinalIgnoreCase)) continue;
            bool alreadyConstructing = Owners.Instance.provincelist.Exists(province =>
                province != null && province.nation == this && province.constructionOrders != null &&
                province.constructionOrders.Exists(order => order != null &&
                    string.Equals(order.buildingId, buildingId, System.StringComparison.OrdinalIgnoreCase)));
            if (alreadyConstructing) continue;

            foreach (Province province in Owners.Instance.provincelist)
            {
                if (province == null || province.nation != this) continue;
                ProvinceBuilding existing = province.GetBuilding(buildingId);
                int candidateLevel = existing != null ? existing.level + 1 : 1;
                if ((existing == null && FirstFreeBuildingSlot(province) < 0) ||
                    !NationContentResolver.CanConstructBuildingLevel(this, buildingId, candidateLevel)) continue;
                float value = EvaluateBuildingLevel(province, buildingId, candidateLevel, existing);
                if (value <= bestValue) continue;
                bestValue = value;
                selectedBuildingId = buildingId;
                selectedProvince = province;
                selectedExisting = existing;
            }
        }

        if (selectedProvince == null || string.IsNullOrEmpty(selectedBuildingId)) return;
        int selectedTargetLevel = selectedExisting != null ? selectedExisting.level + 1 : 1;
        int slot = selectedExisting != null ? selectedExisting.slotIndex : FirstFreeBuildingSlot(selectedProvince);
        int goldCost = CampaignEconomy.BuildingGoldCost(selectedBuildingId, selectedTargetLevel);
        if (Gold < goldCost || !selectedProvince.BeginBuildingConstruction(slot, selectedBuildingId, selectedTargetLevel,
            BuildingDefinition.ConstructionTicks(selectedBuildingId, selectedTargetLevel), true)) return;

        Gold -= goldCost;
        if (selectedBuildingId.Equals("Barracks", System.StringComparison.OrdinalIgnoreCase))
            faction.BarracksLevel = Mathf.Max(faction.BarracksLevel, selectedTargetLevel);
        else if (selectedBuildingId.IndexOf("Mercenary", System.StringComparison.OrdinalIgnoreCase) >= 0)
            faction.MercLevel = Mathf.Max(faction.MercLevel, selectedTargetLevel);
        NextAIInfrastructureTurn = Owners.Instance.turncounter + Mathf.Max(1, AIInfrastructureIntervalTurns);
    }

    private float EvaluateBuildingLevel(Province province, string buildingId, int targetLevel, ProvinceBuilding existing)
    {
        BuildingDefinition definition = BuildingDefinition.Find(buildingId);
        BuildingLevelDefinition level = definition != null ? definition.GetLevel(targetLevel) : null;
        float benefit = 0f;
        if (level != null)
        {
            CampaignRegion campaignRegion = Owners.Instance != null ? Owners.Instance.CallRegionByString(province.region) : null;
            float incomeEfficiency = (campaignRegion != null ? Mathf.Clamp(campaignRegion.GetLoyalty(this), 0f, 100f) : 100f) / 100f;
            benefit += Mathf.Max(0, level.goldIncome) * incomeEfficiency * (LastGrossIncome < LastUnitUpkeep ? 16f : 8f);
            benefit += Mathf.Max(0, level.garrisonCapacity) * 8f;
            benefit += level.unitUnlocks != null ? level.unitUnlocks.Count * 70f : 0f;
            benefit += level.flags != null ? level.flags.Count * 45f : 0f;
            benefit += level.displayedEffects != null ? level.displayedEffects.Count * 30f : 0f;
        }

        foreach (NationUnitEntry entry in NationContentResolver.ResolveUnits(this))
            if (entry != null && entry.unit != null && entry.minimumBuildingLevel == targetLevel &&
                string.Equals(entry.RequiredBuildingId, buildingId, System.StringComparison.OrdinalIgnoreCase))
                benefit += 55f + NationContentResolver.GetUnitTier(this, entry.unit) * 20f;

        ProvinceBuilding projected = new ProvinceBuilding
        {
            definition = definition, id = buildingId, level = targetLevel,
            maxLevel = definition != null ? definition.maximumLevel : ProvinceBuilding.MaximumLevelFor(buildingId),
            slotIndex = existing != null ? existing.slotIndex : FirstFreeBuildingSlot(province)
        };
        if (benefit <= 0f) return 0f;
        int cost = CampaignEconomy.BuildingGoldCost(buildingId, targetLevel);
        int ticks = BuildingDefinition.ConstructionTicks(buildingId, targetLevel);
        float affordability = Gold >= cost + 100 ? 1f : Gold >= cost ? .55f : 0f;
        return benefit * affordability / (1f + cost / 250f + ticks / 30f);
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
            // Ten formations are enough to campaign against ordinary provinces.
            // Requiring 100% strength deadlocked low-income factions below their cap.
            if (!IsArmyCombatReady(army) || army.fieldArmy.GrabQueuedArmySize() > 0) continue;
            Province targetProvince = null;
            float bestDistance = float.MaxValue;
            foreach (Province candidate in candidates)
            {
                float distance = army.GrabDistanceToProvince(candidate);
                int enemyStrength = GetHostileFieldArmyStrengthNear(candidate);
                int danger = Mathf.Max(0, enemyStrength - army.fieldArmy.GrabArmySize());
                float riskAdjustedDistance = distance + danger * 50f;
                if (riskAdjustedDistance < bestDistance || Mathf.Approximately(riskAdjustedDistance, bestDistance) &&
                    (targetProvince == null || string.CompareOrdinal(candidate.name, targetProvince.name) < 0))
                { targetProvince = candidate; bestDistance = riskAdjustedDistance; }
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
        if (army == null || army.fieldArmy == null || !army.IsTargetNull() ||
            army.fieldArmy.GrabArmySize() + army.fieldArmy.GrabQueuedArmySize() >= army.fieldArmy.MaxArmySize) return false;
        if (Owners.Instance.turncounter < army.NextAIReinforcementTurn) return false;
        Province province = army.GrabNearestProvince();
        if (province != null && province.nation == this)
        {
            List<ProvinceLevyEntitlement> availableLevies = province.GetAvailableRegionLevies(this);
            if (availableLevies.Count > 0)
            {
                int raised = province.RaiseAllAvailableRegionLevies(army);
                if (raised > 0)
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
public sealed class ProvinceNamedModifier
{
    public string name;
    public ProvinceLocalModifiers localModifiers = new ProvinceLocalModifiers();
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
public class RegionalLoyaltyShare
{
    public string nationName;
    [Range(0f, 100f)] public float loyalty;
    [Min(0)] public int foodStorage = 1000;
    [Min(1)] public int foodStorageCapacity = 1000;
    [Min(0)] public int lastFoodShortage;
}

[System.Serializable]
public class CampaignRegion
{
    public string name;
    public bool configuredFromProvinceData;
    public Color32 identity;
    [Range(0f, 100f)] public float loyalty = 0f;
    public List<RegionalLoyaltyShare> loyaltyShares = new List<RegionalLoyaltyShare>();
    public List<Province> provincelist = new List<Province>();

    public float GetLoyalty(Nation nation)
    {
        if (nation == null) return loyalty;
        if (loyaltyShares == null) loyaltyShares = new List<RegionalLoyaltyShare>();
        RegionalLoyaltyShare share = loyaltyShares.Find(item => item != null && item.nationName == nation.name);
        return share != null ? share.loyalty : 0f;
    }

    public bool AllowsLevyCallups(Nation nation) => GetLoyalty(nation) > 50f;

    public void SetLoyalty(Nation nation, float value)
    {
        if (nation == null) { loyalty = Mathf.Clamp(value, 0f, 100f); return; }
        if (loyaltyShares == null) loyaltyShares = new List<RegionalLoyaltyShare>();
        RegionalLoyaltyShare share = loyaltyShares.Find(item => item != null && item.nationName == nation.name);
        if (share == null) { share = new RegionalLoyaltyShare { nationName = nation.name }; loyaltyShares.Add(share); }
        share.loyalty = Mathf.Clamp(value, 0f, 100f);
    }

    public void ChangeLoyalty(Nation nation, float amount) { SetLoyalty(nation, GetLoyalty(nation) + amount); }

    public Nation ControllingNation()
    {
        Nation result = null;
        int best = 0;
        if (provincelist == null) return null;
        foreach (Province province in provincelist)
        {
            if (province == null || province.nation == null) continue;
            int count = provincelist.FindAll(candidate => candidate != null && candidate.nation == province.nation).Count;
            if (count > best) { best = count; result = province.nation; }
        }
        return result;
    }

    public float PrimaryCultureShare(Nation controllingNation)
    {
        if (controllingNation == null || controllingNation.culture == null || string.IsNullOrEmpty(controllingNation.culture.DisplayName)) return 0f;
        int total = 0;
        int matching = 0;
        foreach (Province province in GetProvincesOwnedBy(controllingNation))
        {
            total += Mathf.Max(0, province.population);
            if (province.cultures == null) continue;
            foreach (Culture culture in province.cultures)
                if (culture != null && string.Equals(culture.name, controllingNation.culture.DisplayName,
                    System.StringComparison.OrdinalIgnoreCase)) matching += Mathf.Max(0, culture.population);
        }
        return total > 0 ? Mathf.Clamp01((float)matching / total) : 0f;
    }

    public float LoyaltyDelta(Nation controllingNation)
    {
        if (controllingNation == null) return 0f;
        float delta = -.2f;
        delta += .5f * PrimaryCultureShare(controllingNation);
        foreach (Province province in GetProvincesOwnedBy(controllingNation))
        {
            ProvinceBuilding fort = province.GetBuilding("Fort");
            ProvinceBuilding temple = province.GetBuilding("Temple");
            if (fort != null) delta += .1f * Mathf.Max(1, fort.level);
            if (temple != null) delta += .1f * Mathf.Max(1, temple.level);
        }
        delta += FoodLoyaltyModifier(controllingNation);
        return delta;
    }

    public int RegionalFood(Nation nation)
    {
        int total = 0;
        foreach (Province province in GetProvincesOwnedBy(nation))
            if (province != null) total += province.GetFoodOutput();
        return total;
    }

    public float FoodLoyaltyModifier(Nation nation)
    {
        RegionalLoyaltyShare share = GetLoyaltyShare(nation, true);
        int shortage = share != null ? Mathf.Max(0, share.lastFoodShortage) : 0;
        if (shortage == 0) return 0f;
        int initialShortage = Mathf.Min(10, shortage);
        int severeShortage = Mathf.Max(0, shortage - 10);
        return -(initialShortage * .01f + severeShortage * .02f);
    }

    public RegionalLoyaltyShare GetLoyaltyShare(Nation nation, bool create)
    {
        if (nation == null) return null;
        if (loyaltyShares == null) loyaltyShares = new List<RegionalLoyaltyShare>();
        RegionalLoyaltyShare share = loyaltyShares.Find(item => item != null && item.nationName == nation.name);
        if (share == null && create)
        {
            share = new RegionalLoyaltyShare { nationName = nation.name, foodStorage = 1000, foodStorageCapacity = 1000 };
            loyaltyShares.Add(share);
        }
        return share;
    }

    public void ProcessFoodTurn(Nation nation)
    {
        RegionalLoyaltyShare share = GetLoyaltyShare(nation, true);
        if (share == null) return;
        share.foodStorageCapacity = Mathf.Max(1, share.foodStorageCapacity);
        share.foodStorage = Mathf.Clamp(share.foodStorage, 0, share.foodStorageCapacity);
        int balance = RegionalFood(nation);
        share.lastFoodShortage = 0;
        if (balance >= 0)
        {
            share.foodStorage = Mathf.Min(share.foodStorageCapacity, share.foodStorage + balance);
            return;
        }
        int deficit = -balance;
        int withdrawn = Mathf.Min(share.foodStorage, deficit);
        share.foodStorage -= withdrawn;
        share.lastFoodShortage = deficit - withdrawn;
    }

    public List<string> LoyaltyInfluenceLines(Nation controllingNation)
    {
        List<string> result = new List<string> { "Taxes: -0.2" };
        float cultureShare = PrimaryCultureShare(controllingNation);
        result.Add("Primary culture (" + (cultureShare * 100f).ToString("0.#") + "%): +" +
            (.5f * cultureShare).ToString("0.##"));
        int fortLevels = 0, templeLevels = 0;
        foreach (Province province in GetProvincesOwnedBy(controllingNation))
        {
            ProvinceBuilding fort = province.GetBuilding("Fort");
            ProvinceBuilding temple = province.GetBuilding("Temple");
            if (fort != null) fortLevels += Mathf.Max(1, fort.level);
            if (temple != null) templeLevels += Mathf.Max(1, temple.level);
        }
        result.Add("Forts (" + fortLevels + " levels): +" + (fortLevels * .1f).ToString("0.#"));
        result.Add("Temples (" + templeLevels + " levels): +" + (templeLevels * .1f).ToString("0.#"));
        int regionalFood = RegionalFood(controllingNation);
        result.Add("Regional food: " + (regionalFood >= 0 ? "+" : string.Empty) + regionalFood);
        RegionalLoyaltyShare foodShare = GetLoyaltyShare(controllingNation, true);
        result.Add("Food storage: " + foodShare.foodStorage + " / " + foodShare.foodStorageCapacity);
        if (foodShare.lastFoodShortage > 0)
            result.Add("Food shortage: " + FoodLoyaltyModifier(controllingNation).ToString("0.##"));
        result.Add("Net per turn: " + (LoyaltyDelta(controllingNation) >= 0f ? "+" : string.Empty) +
            LoyaltyDelta(controllingNation).ToString("0.##"));
        return result;
    }

    public void ProcessLoyaltyTurn()
    {
        List<Nation> owners = new List<Nation>();
        foreach (Province province in provincelist)
            if (province != null && province.nation != null && !owners.Contains(province.nation)) owners.Add(province.nation);
        foreach (Nation owner in owners)
        {
            ProcessFoodTurn(owner);
            ChangeLoyalty(owner, LoyaltyDelta(owner));
            foreach (Province province in GetProvincesOwnedBy(owner)) province.ApplyTempleCultureInfluence();
            if (GetLoyalty(owner) < 25f) RepatriateLevies(owner);
        }
    }

    private void RepatriateLevies(Nation owner)
    {
        if (Owners.Instance == null || provincelist == null) return;
        foreach (Province province in GetProvincesOwnedBy(owner))
        foreach (ProvinceLevyEntitlement entitlement in province.levyEntitlements)
        {
            if (entitlement == null) continue;
            if (entitlement.state == LevyEntitlementState.Mobilizing)
            {
                entitlement.state = LevyEntitlementState.Available;
                entitlement.remainingTicks = 0;
                entitlement.raisedArmyId = null;
            }
            else if (entitlement.state == LevyEntitlementState.Raised)
            {
                FieldArmyHolder army = Owners.Instance.armylist.Find(candidate => candidate != null &&
                    candidate.fieldArmy != null && candidate.NetworkArmyId == entitlement.raisedArmyId);
                if (army != null && army.fieldArmy.DemobilizeLevy(entitlement.id))
                    province.BeginLevyRecovery(entitlement.id);
            }
        }
    }

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
